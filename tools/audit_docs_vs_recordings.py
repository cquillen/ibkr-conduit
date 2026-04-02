#!/usr/bin/env python3
"""
Compare ibkr_api.md documented fields against actual recorded API responses.

For each endpoint where we have both documentation and a recording,
compare field names, types, and presence.
"""

import json
import os
import re
import sys
from collections import defaultdict
from pathlib import Path

REPO_ROOT = Path(__file__).parent.parent
RECORDINGS_DIR = REPO_ROOT / "tests" / "IbkrConduit.Tests.Integration" / "bin" / "Release" / "net10.0" / "Recordings"
API_DOCS = REPO_ROOT / "docs" / "ibkr_api.md"


# --- Phase 1: Parse recorded responses ---

def parse_recordings():
    """Extract field names and inferred types from all recorded responses."""
    endpoints = defaultdict(lambda: {
        "fields": defaultdict(lambda: {"types": set(), "samples": [], "count": 0}),
        "recordings": [],
    })

    for json_file in sorted(RECORDINGS_DIR.rglob("*.json")):
        try:
            recording = json.loads(json_file.read_text())
        except json.JSONDecodeError:
            continue

        path = recording.get("Request", {}).get("Path", "")
        method = (recording.get("Request", {}).get("Methods", [""])[0] or "").upper()
        status = recording.get("Response", {}).get("StatusCode", 0)

        norm_path = normalize_path(path)
        key = f"{method} {norm_path}"

        # Only look at successful responses
        if status != 200:
            continue

        body_str = recording.get("Response", {}).get("Body", "")
        if not body_str:
            continue

        try:
            body = json.loads(body_str)
        except json.JSONDecodeError:
            continue

        endpoints[key]["recordings"].append(str(json_file.relative_to(REPO_ROOT)))
        extract_fields(body, endpoints[key]["fields"], "")

    # Convert sets to lists
    for ep in endpoints:
        for field in endpoints[ep]["fields"]:
            endpoints[ep]["fields"][field]["types"] = sorted(endpoints[ep]["fields"][field]["types"])
            endpoints[ep]["fields"][field]["samples"] = endpoints[ep]["fields"][field]["samples"][:3]

    return dict(endpoints)


def normalize_path(path):
    """Normalize API path by replacing dynamic segments."""
    path = re.sub(r'/DU\w+/', '/{accountId}/', path)
    path = re.sub(r'/U\d+/', '/{accountId}/', path)
    path = re.sub(r'/\d+$', '/{id}', path)
    path = re.sub(r'/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', '/{id}', path)
    # Remove /v1/api prefix for matching with docs
    path = re.sub(r'^/v1/api', '', path)
    return path


def json_type_name(value):
    """Return descriptive type for a JSON value."""
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "boolean"
    if isinstance(value, int):
        return "integer"
    if isinstance(value, float):
        return "float"
    if isinstance(value, str):
        if value == "":
            return "string(empty)"
        try:
            float(value)
            return "string(numeric)"
        except (ValueError, TypeError):
            pass
        return "string"
    if isinstance(value, list):
        return "array"
    if isinstance(value, dict):
        return "object"
    return "unknown"


def extract_fields(data, field_map, prefix):
    """Recursively extract fields with types."""
    if isinstance(data, dict):
        for key, value in data.items():
            full_key = f"{prefix}.{key}" if prefix else key
            jtype = json_type_name(value)
            field_map[full_key]["types"].add(jtype)
            if len(field_map[full_key]["samples"]) < 3:
                sample = repr(value)[:80]
                if sample not in field_map[full_key]["samples"]:
                    field_map[full_key]["samples"].append(sample)
            field_map[full_key]["count"] += 1

            if isinstance(value, dict):
                extract_fields(value, field_map, full_key)
            elif isinstance(value, list) and value:
                for i, item in enumerate(value[:2]):
                    if isinstance(item, dict):
                        extract_fields(item, field_map, f"{full_key}[*]")

    elif isinstance(data, list) and data:
        for i, item in enumerate(data[:2]):
            if isinstance(item, dict):
                extract_fields(item, field_map, f"{prefix}[*]" if prefix else "[*]")


# --- Phase 2: Parse ibkr_api.md for documented endpoints ---

def parse_api_docs():
    """Extract endpoint documentation: path, method, documented fields with types."""
    content = API_DOCS.read_text()
    endpoints = {}

    # Find endpoint blocks: `METHOD /path` — allow spaces inside {{ }} placeholders
    backtick = '\x60'
    endpoint_pattern = re.compile(
        backtick + r'(GET|POST|PUT|DELETE)\s+(/[^' + backtick + r']+?)\s*' + backtick,
    )

    lines = content.split('\n')

    for i, line in enumerate(lines):
        match = endpoint_pattern.search(line)
        if not match:
            continue

        method = match.group(1)
        path = match.group(2)

        # Normalize path: replace {{ x }}, { x }, and {x} placeholders
        norm_path = re.sub(r'\{\{?\s*\w+\s*\}?\}', '{id}', path)

        key = f"{method} {norm_path}"

        # Extract fields from the surrounding text block (next ~100 lines)
        block = '\n'.join(lines[i:i+150])

        fields = extract_doc_fields(block)
        endpoints[key] = {
            "path": path,
            "method": method,
            "fields": fields,
            "line": i + 1,
        }

    return endpoints


def extract_doc_fields(block):
    """Extract field names and documented types from a doc block."""
    fields = {}

    # Pattern: **fieldName:** type. or **field_name:** Type.
    # Also: **fieldName** type. (no colon)
    field_pattern = re.compile(
        r'\*\*(\w[\w_]*?)(?:\*\*:?\s*\*?|\*\*:)\s*'
        r'(\w[\w\s,/]*?)\.?\s*(?:\n|$)',
    )

    for match in field_pattern.finditer(block):
        name = match.group(1).strip()
        type_desc = match.group(2).strip().lower()

        # Skip common non-field headings
        if name in ('NOTE', 'Request', 'Response', 'Query', 'Path', 'Body',
                     'Usage', 'Notes', 'Important', 'Warning', 'Example',
                     'Params', 'Parameters', 'Details', 'Object', 'Headers'):
            continue

        # Infer type
        doc_type = "unknown"
        if any(t in type_desc for t in ('string', 'str')):
            doc_type = "string"
        elif any(t in type_desc for t in ('int', 'integer')):
            doc_type = "integer"
        elif any(t in type_desc for t in ('float', 'decimal', 'double', 'number')):
            doc_type = "float"
        elif any(t in type_desc for t in ('bool', 'boolean')):
            doc_type = "boolean"
        elif any(t in type_desc for t in ('array', 'list')):
            doc_type = "array"
        elif any(t in type_desc for t in ('object', 'json')):
            doc_type = "object"

        fields[name] = {
            "doc_type": doc_type,
            "raw_type": type_desc,
        }

    return fields


# --- Phase 3: Compare ---

def compare(doc_endpoints, rec_endpoints):
    """Compare documented fields against recorded fields."""
    findings = []

    # Try to match doc endpoints to recording endpoints
    matched = 0
    unmatched_docs = []
    unmatched_recs = []

    for doc_key, doc_info in sorted(doc_endpoints.items()):
        rec_key = find_matching_recording(doc_key, rec_endpoints)
        if not rec_key:
            unmatched_docs.append(doc_key)
            continue

        matched += 1
        rec_info = rec_endpoints[rec_key]
        doc_fields = doc_info["fields"]

        # Get top-level recorded fields (no dots except [*].field)
        rec_top = {}
        for field_path, field_info in rec_info["fields"].items():
            parts = field_path.split(".")
            # Top level: no dot, or [*].field (one level into array)
            if len(parts) == 1 and "[*]" not in parts[0]:
                rec_top[parts[0]] = field_info
            elif len(parts) == 2 and parts[0] == "[*]":
                rec_top[parts[1]] = field_info

        # Also get fields inside known wrapper objects (e.g., orders[*].field)
        for field_path, field_info in rec_info["fields"].items():
            parts = field_path.replace("[*]", "").split(".")
            parts = [p for p in parts if p]
            if len(parts) == 2:
                # e.g., orders.field or conditions[*].field
                rec_top[f"{parts[0]}.{parts[1]}"] = field_info

        doc_field_names = set(doc_fields.keys())
        rec_field_names = set(rec_top.keys())

        # Fields documented but not in recording
        doc_only = doc_field_names - rec_field_names
        for field in sorted(doc_only):
            # Also check if it appears as a nested field
            appears_nested = any(field in fp for fp in rec_info["fields"].keys())
            findings.append({
                "type": "DOC_ONLY",
                "endpoint": doc_key,
                "field": field,
                "doc_type": doc_fields[field]["doc_type"],
                "appears_nested": appears_nested,
                "message": f"'{field}' documented but not in recorded response",
            })

        # Fields in recording but not documented
        # Only check simple field names (no dots)
        simple_rec = {f for f in rec_field_names if "." not in f}
        rec_only = simple_rec - doc_field_names
        for field in sorted(rec_only):
            info = rec_top.get(field, {})
            findings.append({
                "type": "REC_ONLY",
                "endpoint": doc_key,
                "field": field,
                "observed_types": info.get("types", []),
                "samples": info.get("samples", []),
                "message": f"'{field}' in recorded response but not documented",
            })

        # Type mismatches for common fields
        common = doc_field_names & simple_rec
        for field in sorted(common):
            doc_type = doc_fields[field]["doc_type"]
            rec_types = rec_top.get(field, {}).get("types", [])

            if doc_type == "unknown" or not rec_types:
                continue

            mismatch = check_doc_type_mismatch(doc_type, rec_types)
            if mismatch:
                findings.append({
                    "type": "TYPE_DIFF",
                    "endpoint": doc_key,
                    "field": field,
                    "doc_type": doc_type,
                    "observed_types": rec_types,
                    "samples": rec_top.get(field, {}).get("samples", []),
                    "message": mismatch,
                })

    for rec_key in sorted(rec_endpoints.keys()):
        if not find_matching_doc(rec_key, doc_endpoints):
            unmatched_recs.append(rec_key)

    return findings, matched, unmatched_docs, unmatched_recs


def find_matching_recording(doc_key, rec_endpoints):
    """Find a recording key matching a doc endpoint key."""
    method, path = doc_key.split(" ", 1)

    for rec_key in rec_endpoints:
        rec_method, rec_path = rec_key.split(" ", 1)
        if rec_method != method:
            continue
        # Normalize both to compare
        norm_doc = re.sub(r'\{[^}]+\}', '{id}', path)
        norm_rec = re.sub(r'\{[^}]+\}', '{id}', rec_path)
        if norm_doc == norm_rec:
            return rec_key

    return None


def find_matching_doc(rec_key, doc_endpoints):
    """Find a doc endpoint matching a recording key."""
    method, path = rec_key.split(" ", 1)

    for doc_key in doc_endpoints:
        doc_method, doc_path = doc_key.split(" ", 1)
        if doc_method != method:
            continue
        norm_doc = re.sub(r'\{[^}]+\}', '{id}', doc_path)
        norm_rec = re.sub(r'\{[^}]+\}', '{id}', path)
        if norm_doc == norm_rec:
            return doc_key

    return None


def check_doc_type_mismatch(doc_type, observed_types):
    """Check if documented type matches observed types."""
    non_null = [t for t in observed_types if t != "null"]
    if not non_null:
        return None

    if doc_type == "string":
        if any(t in ("integer", "float") for t in non_null):
            return f"Docs say string, API sends {non_null}"
    elif doc_type == "integer":
        if "string" in non_null or "string(numeric)" in non_null:
            return f"Docs say integer, API sends {non_null}"
        if "float" in non_null:
            return f"Docs say integer, API sends float"
    elif doc_type == "float":
        if "string" in non_null or "string(numeric)" in non_null:
            return f"Docs say float, API sends {non_null}"
    elif doc_type == "boolean":
        if any(t not in ("boolean",) for t in non_null):
            return f"Docs say boolean, API sends {non_null}"
    elif doc_type == "array":
        if "array" not in non_null:
            return f"Docs say array, API sends {non_null}"

    return None


# --- Main ---

def main():
    print("Phase 1: Parsing recorded responses...", file=sys.stderr)
    rec_endpoints = parse_recordings()
    print(f"  {len(rec_endpoints)} endpoints with recordings", file=sys.stderr)

    print("Phase 2: Parsing ibkr_api.md...", file=sys.stderr)
    doc_endpoints = parse_api_docs()
    print(f"  {len(doc_endpoints)} documented endpoints", file=sys.stderr)

    print("Phase 3: Comparing...", file=sys.stderr)
    findings, matched, unmatched_docs, unmatched_recs = compare(doc_endpoints, rec_endpoints)
    print(f"  {matched} matched endpoints", file=sys.stderr)
    print(f"  {len(findings)} findings", file=sys.stderr)

    # Summarize
    by_type = defaultdict(int)
    for f in findings:
        by_type[f["type"]] += 1

    report = {
        "summary": {
            "documented_endpoints": len(doc_endpoints),
            "recorded_endpoints": len(rec_endpoints),
            "matched": matched,
            "unmatched_docs": len(unmatched_docs),
            "unmatched_recordings": len(unmatched_recs),
            "total_findings": len(findings),
            "by_type": dict(by_type),
        },
        "findings": findings,
        "unmatched_docs": unmatched_docs,
        "unmatched_recordings": unmatched_recs,
    }

    json.dump(report, sys.stdout, indent=2, default=str)


if __name__ == "__main__":
    main()
