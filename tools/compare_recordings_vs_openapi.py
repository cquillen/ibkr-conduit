#!/usr/bin/env python3
"""Compare recorded API responses against OpenAPI spec schemas.

For each 200-status recording, collects all field names from the actual response
and compares them against the fully-resolved OpenAPI schema for that endpoint.
"""

import json
import re
import sys
from pathlib import Path


def load_openapi(path: str) -> dict:
    with open(path) as f:
        return json.load(f)


def resolve_ref(ref: str, schemas: dict) -> dict | None:
    """Resolve a $ref like '#/components/schemas/Foo'."""
    if not ref.startswith('#/components/schemas/'):
        return None
    name = ref.split('/')[-1]
    return schemas.get(name)


def collect_schema_fields(schema: dict, schemas: dict, visited: set | None = None, depth: int = 0) -> set[str]:
    """Recursively collect ALL field names from an OpenAPI schema."""
    if depth > 15:
        return set()
    if visited is None:
        visited = set()

    if '$ref' in schema:
        ref = schema['$ref']
        if ref in visited:
            return set()
        visited = visited | {ref}
        resolved = resolve_ref(ref, schemas)
        if resolved:
            return collect_schema_fields(resolved, schemas, visited, depth + 1)
        return set()

    fields = set()

    for combiner in ('allOf', 'oneOf', 'anyOf'):
        if combiner in schema:
            for sub in schema[combiner]:
                fields.update(collect_schema_fields(sub, schemas, visited, depth + 1))

    if 'properties' in schema:
        for name, prop in schema['properties'].items():
            fields.add(name)
            fields.update(collect_schema_fields(prop, schemas, visited, depth + 1))

    if schema.get('type') == 'array' and 'items' in schema:
        fields.update(collect_schema_fields(schema['items'], schemas, visited, depth + 1))

    if 'additionalProperties' in schema and isinstance(schema['additionalProperties'], dict):
        fields.update(collect_schema_fields(schema['additionalProperties'], schemas, visited, depth + 1))

    return fields


def collect_response_fields(obj, prefix: str = '') -> set[str]:
    """Recursively collect all field names from a JSON response object."""
    fields = set()
    if isinstance(obj, dict):
        for k, v in obj.items():
            full = f'{prefix}.{k}' if prefix else k
            fields.add(full)
            fields.update(collect_response_fields(v, full))
    elif isinstance(obj, list):
        for item in obj:
            fields.update(collect_response_fields(item, prefix))
    return fields


def recording_path_to_api_path(request_path: str) -> str:
    """Convert a recording request path to an OpenAPI-style path template.

    e.g. /v1/api/iserver/account/DU1234567/orders -> /iserver/account/{accountId}/orders
    """
    # Strip /v1/api prefix
    path = re.sub(r'^/v1/api', '', request_path)

    # Known account ID patterns
    path = re.sub(r'/DU\w+/', '/{accountId}/', path)
    path = re.sub(r'/U\w+/', '/{accountId}/', path)

    return path


def find_oa_endpoint(api_path: str, method: str, oa_paths: dict) -> tuple[str, dict] | None:
    """Find matching OpenAPI path entry, handling path parameter substitution."""
    method_lower = method.lower()

    # Direct match
    if api_path in oa_paths:
        op = oa_paths[api_path].get(method_lower)
        if op:
            return api_path, op

    # Try parameterized matching
    # Replace numeric segments and known IDs with {param}
    for oa_path, methods in oa_paths.items():
        if method_lower not in methods:
            continue
        # Build regex from OA path: /iserver/account/{accountId}/orders
        pattern = re.sub(r'\{[^}]+\}', r'[^/]+', oa_path)
        pattern = f'^{pattern}$'
        if re.match(pattern, api_path):
            return oa_path, methods[method_lower]

    return None


def main():
    recordings_dir = Path('/workspace/ibkr-conduit/recordings/2026-04-04T070845')
    oa_path = Path('/workspace/ibkr-conduit/docs/ibkr-web-api-openapi.json')

    spec = load_openapi(str(oa_path))
    schemas = spec.get('components', {}).get('schemas', {})
    oa_paths = spec.get('paths', {})

    # Collect all recording files
    recording_files = sorted(recordings_dir.rglob('*.json'))

    results = []
    no_match = []
    non_200 = []

    for rec_file in recording_files:
        with open(rec_file) as f:
            rec = json.load(f)

        status = rec.get('Response', {}).get('StatusCode', 0)
        if status != 200:
            non_200.append((rec_file.relative_to(recordings_dir), status))
            continue

        request = rec.get('Request', {})
        req_path = request.get('Path', '')
        req_methods = request.get('Methods', [])
        req_method = req_methods[0] if req_methods else 'GET'

        body = rec.get('Response', {}).get('Body')
        if body is None or body == '':
            continue

        # If body is a string, parse it
        if isinstance(body, str):
            try:
                body = json.loads(body)
            except json.JSONDecodeError:
                continue

        # Collect all field names from recording
        rec_fields_full = collect_response_fields(body)
        # Get just leaf field names for comparison
        rec_leaf_fields = {f.split('.')[-1] for f in rec_fields_full}
        # Also get top-level fields
        if isinstance(body, dict):
            rec_top_fields = set(body.keys())
        elif isinstance(body, list) and body and isinstance(body[0], dict):
            rec_top_fields = set(body[0].keys())
        else:
            rec_top_fields = set()

        # Map to OpenAPI path
        api_path = recording_path_to_api_path(req_path)
        match = find_oa_endpoint(api_path, req_method, oa_paths)

        if not match:
            no_match.append((rec_file.relative_to(recordings_dir), req_method, req_path, api_path))
            continue

        oa_matched_path, operation = match

        # Get 200 response schema
        resp_200 = operation.get('responses', {}).get('200', {})
        content = resp_200.get('content', {}).get('application/json', {})
        schema = content.get('schema', {})

        schema_ref = schema.get('$ref', '')
        schema_name = schema_ref.split('/')[-1] if schema_ref else ''

        oa_fields = collect_schema_fields(schema, schemas)

        # Compare: use leaf fields from recording vs all fields from OA
        only_rec = sorted(rec_leaf_fields - oa_fields, key=str.lower)
        only_oa = sorted(oa_fields - rec_leaf_fields, key=str.lower)
        common = rec_leaf_fields & oa_fields

        results.append({
            'file': str(rec_file.relative_to(recordings_dir)),
            'method': req_method,
            'req_path': req_path,
            'oa_path': oa_matched_path,
            'schema_name': schema_name,
            'rec_field_count': len(rec_leaf_fields),
            'oa_field_count': len(oa_fields),
            'common': len(common),
            'only_rec': only_rec,
            'only_oa': only_oa,
            'identical': not only_rec and not only_oa,
        })

    # Print report
    print("=" * 80)
    print("Recording vs OpenAPI Schema Comparison Report")
    print(f"Recordings: {recordings_dir}")
    print("=" * 80)

    identical = [r for r in results if r['identical']]
    with_diffs = [r for r in results if not r['identical']]
    no_oa_schema = [r for r in results if r['oa_field_count'] == 0]

    print(f"\n200-status recordings:    {len(results)}")
    print(f"Non-200 recordings:       {len(non_200)}")
    print(f"No OA match found:        {len(no_match)}")
    print(f"Identical schemas:        {len(identical)}")
    print(f"With differences:         {len(with_diffs)}")

    # Identical
    print(f"\n{'=' * 80}")
    print(f"IDENTICAL ({len(identical)})")
    print(f"{'=' * 80}")
    for r in identical:
        ref = f" [{r['schema_name']}]" if r['schema_name'] else ""
        print(f"  {r['method']} {r['oa_path']}{ref} ({r['common']} fields)")

    # Differences
    with_diffs.sort(key=lambda r: len(r['only_rec']) + len(r['only_oa']), reverse=True)
    print(f"\n{'=' * 80}")
    print(f"DIFFERENCES ({len(with_diffs)})")
    print(f"{'=' * 80}")
    for r in with_diffs:
        ref = f" [{r['schema_name']}]" if r['schema_name'] else ""
        print(f"\n  {r['file']}")
        print(f"  {r['method']} {r['oa_path']}{ref}")
        print(f"  Common: {r['common']}  |  Rec only: {len(r['only_rec'])}  |  OA only: {len(r['only_oa'])}  |  Rec total: {r['rec_field_count']}  |  OA total: {r['oa_field_count']}")
        if r['only_rec']:
            print(f"    In recording but NOT in OA:  {', '.join(r['only_rec'])}")
        if r['only_oa']:
            print(f"    In OA but NOT in recording:  {', '.join(r['only_oa'])}")

    # No match
    if no_match:
        print(f"\n{'=' * 80}")
        print(f"NO OPENAPI MATCH FOUND ({len(no_match)})")
        print(f"{'=' * 80}")
        for file, method, req_path, api_path in no_match:
            print(f"  {file}  ({method} {req_path} -> {api_path})")

    # Non-200
    if non_200:
        print(f"\n{'=' * 80}")
        print(f"NON-200 RECORDINGS SKIPPED ({len(non_200)})")
        print(f"{'=' * 80}")
        for file, status in non_200:
            print(f"  {file}  (status {status})")


if __name__ == "__main__":
    main()
