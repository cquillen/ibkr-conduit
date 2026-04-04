#!/usr/bin/env python3
"""Compare response schemas between ibkr-web-api-spec.md and ibkr-web-api-openapi.json.

Uses a robust $ref resolver with cycle detection to fully traverse nested OpenAPI schemas.
"""

import json
import re
import sys
from pathlib import Path


# ---------------------------------------------------------------------------
# Markdown extraction (unchanged from v1)
# ---------------------------------------------------------------------------

def extract_md_endpoints(path: str) -> dict[str, dict]:
    """Extract endpoint method+url and their response field names from markdown."""
    endpoints = {}
    current_method = None
    current_url = None
    current_heading = None
    in_response_body = False
    in_response_table = False
    fields = set()

    with open(path) as f:
        lines = list(f)

    def save():
        nonlocal fields
        if current_method and current_url and fields:
            key = f"{current_method} {norm(current_url)}"
            endpoints[key] = {
                'raw_url': f"{current_method} {current_url}",
                'fields': fields.copy(),
                'heading': current_heading or ""
            }

    for line_raw in lines:
        line = line_raw.strip()

        if line.startswith("### "):
            save()
            current_heading = line[4:]
            current_method = None
            current_url = None
            in_response_body = False
            in_response_table = False
            fields = set()
            continue

        m = re.match(r'^- \*\*Method:\*\* `(\w+)`', line)
        if m:
            current_method = m.group(1).upper()
            continue

        m = re.match(r'^- \*\*URL:\*\* `([^`]+)`', line)
        if m:
            current_url = '/' + m.group(1).lstrip('/')
            continue

        if line == "#### Response Body":
            in_response_body = True
            in_response_table = False
            continue

        if line.startswith("#### ") and line != "#### Response Body":
            in_response_body = False
            in_response_table = False
            continue

        if line.startswith("---"):
            save()
            current_method = None
            current_url = None
            in_response_body = False
            in_response_table = False
            fields = set()
            continue

        if in_response_body:
            if re.match(r'^\| Field\s*\| Type', line) or re.match(r'^\| Name\s*\| Type', line):
                in_response_table = True
                continue
            if in_response_table and re.match(r'^\|[-\s|]+\|$', line):
                continue
            if in_response_table and line.startswith('|') and not line.startswith('|--'):
                cols = [c.strip() for c in line.split('|')]
                if len(cols) >= 3 and cols[1]:
                    field_name = cols[1].strip()
                    if field_name and not field_name.startswith('**') and field_name not in ('Field', 'Name'):
                        fields.add(field_name)
            elif in_response_table and not line.startswith('|'):
                in_response_table = False

    save()
    return endpoints


# ---------------------------------------------------------------------------
# OpenAPI extraction with proper recursive $ref resolution + cycle detection
# ---------------------------------------------------------------------------

def load_openapi(path: str) -> dict:
    with open(path) as f:
        return json.load(f)


def collect_all_fields(schema: dict, components: dict, visited: set | None = None, depth: int = 0) -> set[str]:
    """Recursively collect ALL field names from a schema, resolving $ref with cycle detection.

    Traverses into nested objects, arrays, allOf/oneOf/anyOf, and additionalProperties
    to collect every property name at every level.
    """
    if depth > 15:
        return set()
    if visited is None:
        visited = set()

    # Handle $ref
    if '$ref' in schema:
        ref = schema['$ref']
        if ref in visited:
            return set()  # cycle detected
        visited = visited | {ref}
        resolved = resolve_ref(ref, components)
        if resolved:
            return collect_all_fields(resolved, components, visited, depth + 1)
        return set()

    fields = set()

    # Combiners: allOf, oneOf, anyOf
    for combiner in ('allOf', 'oneOf', 'anyOf'):
        if combiner in schema:
            for sub_schema in schema[combiner]:
                fields.update(collect_all_fields(sub_schema, components, visited, depth + 1))

    # Direct properties
    if 'properties' in schema:
        for prop_name, prop_schema in schema['properties'].items():
            fields.add(prop_name)
            # Recurse into each property to get nested fields
            fields.update(collect_all_fields(prop_schema, components, visited, depth + 1))

    # Array items
    if schema.get('type') == 'array' and 'items' in schema:
        fields.update(collect_all_fields(schema['items'], components, visited, depth + 1))

    # additionalProperties (for dynamic keys)
    if 'additionalProperties' in schema and isinstance(schema['additionalProperties'], dict):
        fields.update(collect_all_fields(schema['additionalProperties'], components, visited, depth + 1))

    return fields


def resolve_ref(ref: str, components: dict) -> dict | None:
    """Resolve a $ref string like '#/components/schemas/Foo' to the actual schema dict."""
    if not ref.startswith('#/'):
        return None
    parts = ref[2:].split('/')
    obj = {'components': components}
    # Navigate: components -> schemas -> name
    # But ref is like #/components/schemas/Foo, so we need the full spec root
    # We store just components, so adjust
    if len(parts) >= 3 and parts[0] == 'components' and parts[1] == 'schemas':
        return components.get('schemas', {}).get(parts[2])
    return None


def extract_oa_endpoints(path: str) -> dict[str, dict]:
    """Extract endpoint method+url and their fully-resolved response field names."""
    spec = load_openapi(path)
    components = spec.get('components', {})
    endpoints = {}

    for url_path, methods in spec.get('paths', {}).items():
        for method_lower, operation in methods.items():
            if method_lower not in ('get', 'post', 'put', 'delete', 'patch'):
                continue
            if not isinstance(operation, dict):
                continue

            method = method_lower.upper()
            key = f"{method} {norm(url_path)}"

            responses = operation.get('responses', {})
            resp_200 = responses.get('200', {})
            content = resp_200.get('content', {}).get('application/json', {})
            schema = content.get('schema', {})

            if not schema:
                continue

            schema_ref = schema.get('$ref', '')
            if schema_ref:
                schema_ref = schema_ref.split('/')[-1]

            fields = collect_all_fields(schema, components)

            endpoints[key] = {
                'raw_url': f"{method} {url_path}",
                'fields': fields,
                'schema_ref': schema_ref
            }

    return endpoints


def norm(url: str) -> str:
    return re.sub(r'\{[^}]+\}', '{param}', url)


# ---------------------------------------------------------------------------
# Report generation
# ---------------------------------------------------------------------------

def main():
    docs_dir = Path(__file__).parent.parent / "docs"
    md_path = docs_dir / "ibkr-web-api-spec.md"
    oa_path = docs_dir / "ibkr-web-api-openapi.json"

    if not md_path.exists() or not oa_path.exists():
        print("ERROR: Required files not found")
        sys.exit(1)

    md_eps = extract_md_endpoints(str(md_path))
    oa_eps = extract_oa_endpoints(str(oa_path))

    matched_keys = sorted(set(md_eps.keys()) & set(oa_eps.keys()))

    total_matched = 0
    total_identical = 0
    diff_entries = []

    print("=" * 80)
    print("IBKR API Response Schema Comparison Report (Deep Resolution)")
    print("=" * 80)

    for key in matched_keys:
        md = md_eps.get(key)
        oa = oa_eps.get(key)
        if not md or not oa:
            continue

        md_fields = md['fields']
        oa_fields = oa['fields']
        if not md_fields and not oa_fields:
            continue

        total_matched += 1
        only_md = sorted(md_fields - oa_fields, key=str.lower)
        only_oa = sorted(oa_fields - md_fields, key=str.lower)
        common = md_fields & oa_fields

        if not only_md and not only_oa:
            total_identical += 1
        else:
            diff_entries.append({
                'key': key,
                'md_display': md['raw_url'],
                'heading': md.get('heading', ''),
                'schema_ref': oa.get('schema_ref', ''),
                'common': len(common),
                'only_md': only_md,
                'only_oa': only_oa,
                'md_total': len(md_fields),
                'oa_total': len(oa_fields),
            })

    total_with_diffs = len(diff_entries)

    print(f"\nEndpoints compared:    {total_matched}")
    print(f"Identical schemas:     {total_identical}")
    print(f"Schemas with diffs:    {total_with_diffs}")

    # Identical
    print(f"\n{'=' * 80}")
    print(f"IDENTICAL SCHEMAS ({total_identical})")
    print(f"{'=' * 80}")
    for key in matched_keys:
        md = md_eps.get(key)
        oa = oa_eps.get(key)
        if not md or not oa:
            continue
        if not md['fields'] and not oa['fields']:
            continue
        if md['fields'] == oa['fields']:
            ref = f" [{oa.get('schema_ref', '')}]" if oa.get('schema_ref') else ""
            print(f"  {md['raw_url']} ({len(md['fields'])} fields){ref}")

    # Diffs sorted by total difference count
    diff_entries.sort(key=lambda d: len(d['only_md']) + len(d['only_oa']), reverse=True)

    print(f"\n{'=' * 80}")
    print(f"ENDPOINTS WITH SCHEMA DIFFERENCES ({total_with_diffs})")
    print(f"{'=' * 80}")

    for d in diff_entries:
        ref = f" [{d['schema_ref']}]" if d['schema_ref'] else ""
        print(f"\n  {d['md_display']}{ref}")
        print(f"  Heading: {d['heading']}")
        print(f"  Common: {d['common']}  |  MD only: {len(d['only_md'])}  |  OA only: {len(d['only_oa'])}  |  MD total: {d['md_total']}  |  OA total: {d['oa_total']}")
        if d['only_md']:
            print(f"    Only in Markdown:  {', '.join(d['only_md'])}")
        if d['only_oa']:
            print(f"    Only in OpenAPI:   {', '.join(d['only_oa'])}")

    # No-schema cases
    print(f"\n{'=' * 80}")
    print("MATCHED ENDPOINTS — SCHEMA IN ONE SOURCE ONLY")
    print(f"{'=' * 80}")
    for key in matched_keys:
        md = md_eps.get(key)
        oa = oa_eps.get(key)
        has_md = md and md['fields']
        has_oa = oa and oa['fields']
        if has_md == has_oa:
            continue
        src = "MD has fields, OA missing" if has_md else "OA has fields, MD missing"
        display = md['raw_url'] if md else oa['raw_url']
        count = len(md['fields']) if has_md else len(oa['fields'])
        print(f"  {display} — {src} ({count} fields)")


if __name__ == "__main__":
    main()
