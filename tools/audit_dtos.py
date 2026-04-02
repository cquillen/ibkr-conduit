#!/usr/bin/env python3
"""
DTO Audit Script for IbkrConduit.

Cross-references three sources to find field name/type mismatches:
1. C# DTO models (JsonPropertyName attributes)
2. Recorded API responses (actual JSON from IBKR)
3. ibkr_api.md documentation (documented field names/types)

Outputs a JSON report to stdout.
"""

import json
import os
import re
import sys
from collections import defaultdict
from pathlib import Path

# Paths
REPO_ROOT = Path(__file__).parent.parent
SRC_DIR = REPO_ROOT / "src" / "IbkrConduit"
RECORDINGS_DIR = REPO_ROOT / "tests" / "IbkrConduit.Tests.Integration" / "bin" / "Release" / "net10.0" / "Recordings"
API_DOCS = REPO_ROOT / "docs" / "ibkr_api.md"

# --- Phase 1: Parse C# DTOs ---

def parse_csharp_models():
    """Extract all records/classes with JsonPropertyName mappings from model files."""
    models = {}
    model_files = list(SRC_DIR.rglob("*Models.cs"))

    for filepath in model_files:
        content = filepath.read_text()
        module = filepath.parent.name  # e.g., "Orders", "Portfolio"

        # Find all record/class definitions with their properties
        # Pattern: public record Name(\n  [attributes] type PropName, ...
        record_pattern = re.compile(
            r'public\s+(?:sealed\s+)?record\s+(\w+)\s*\((.*?)\)',
            re.DOTALL
        )

        # Also find class-style records with { get; init; }
        class_pattern = re.compile(
            r'public\s+(?:sealed\s+)?record\s+(\w+)\s*\{(.*?)\}',
            re.DOTALL
        )

        for match in record_pattern.finditer(content):
            record_name = match.group(1)
            params_block = match.group(2)
            props = parse_record_params(params_block)
            models[record_name] = {
                "module": module,
                "file": str(filepath.relative_to(REPO_ROOT)),
                "style": "positional",
                "properties": props,
            }

        for match in class_pattern.finditer(content):
            record_name = match.group(1)
            body = match.group(2)
            # Skip if already found as positional record
            if record_name in models:
                continue
            props = parse_class_properties(body)
            if props:
                models[record_name] = {
                    "module": module,
                    "file": str(filepath.relative_to(REPO_ROOT)),
                    "style": "class",
                    "properties": props,
                }

    return models


def parse_record_params(params_block):
    """Parse positional record parameters to extract JsonPropertyName and type info."""
    props = {}

    # Split on parameter boundaries (tricky with attributes)
    # Strategy: find all [property: JsonPropertyName("xxx")] type Name patterns
    jpn_pattern = re.compile(
        r'\[property:\s*JsonPropertyName\("([^"]+)"\)\]'
        r'(?:\s*\[property:[^\]]*\])*'  # additional attributes
        r'\s*(\S+(?:\?)?)\s+(\w+)',
        re.DOTALL
    )

    for match in jpn_pattern.finditer(params_block):
        json_name = match.group(1)
        csharp_type = match.group(2)
        csharp_name = match.group(3)

        # Check for JsonNumberHandling
        has_number_handling = bool(re.search(
            rf'JsonNumberHandling.*?\]\s*\[property:\s*JsonPropertyName\("{re.escape(json_name)}"\)'
            rf'|JsonPropertyName\("{re.escape(json_name)}"\)\].*?JsonNumberHandling',
            params_block, re.DOTALL
        ))

        # Check for JsonConverter
        converter_match = re.search(
            rf'JsonConverter\(typeof\((\w+)\)\).*?JsonPropertyName\("{re.escape(json_name)}"\)'
            rf'|JsonPropertyName\("{re.escape(json_name)}"\).*?JsonConverter\(typeof\((\w+)\)\)',
            params_block, re.DOTALL
        )
        converter = (converter_match.group(1) or converter_match.group(2)) if converter_match else None

        props[json_name] = {
            "csharp_name": csharp_name,
            "csharp_type": csharp_type,
            "has_number_handling": has_number_handling,
            "converter": converter,
        }

    return props


def parse_class_properties(body):
    """Parse class-style record properties."""
    props = {}

    jpn_pattern = re.compile(
        r'\[JsonPropertyName\("([^"]+)"\)\]\s*'
        r'public\s+(\S+(?:\?)?)\s+(\w+)',
        re.DOTALL
    )

    for match in jpn_pattern.finditer(body):
        json_name = match.group(1)
        csharp_type = match.group(2)
        csharp_name = match.group(3)
        props[json_name] = {
            "csharp_name": csharp_name,
            "csharp_type": csharp_type,
            "has_number_handling": False,
            "converter": None,
        }

    return props


# --- Phase 2: Parse Recorded Responses ---

def parse_recordings():
    """Extract field names and types from all recorded JSON responses."""
    endpoint_fields = defaultdict(lambda: defaultdict(lambda: {
        "observed_types": set(),
        "sample_values": [],
        "seen_count": 0,
    }))

    request_fields = defaultdict(lambda: defaultdict(lambda: {
        "observed_types": set(),
        "sample_values": [],
        "seen_count": 0,
    }))

    if not RECORDINGS_DIR.exists():
        print(f"WARNING: Recordings dir not found: {RECORDINGS_DIR}", file=sys.stderr)
        return {}, {}

    for json_file in sorted(RECORDINGS_DIR.rglob("*.json")):
        try:
            recording = json.loads(json_file.read_text())
        except json.JSONDecodeError:
            continue

        path = recording.get("Request", {}).get("Path", "")
        method = (recording.get("Request", {}).get("Methods", [""])[0] or "").upper()
        status = recording.get("Response", {}).get("StatusCode", 0)

        # Normalize path: remove dynamic segments (account IDs, order IDs, etc.)
        norm_path = normalize_path(path)
        endpoint_key = f"{method} {norm_path}"

        # Parse response body
        if status == 200:
            body_str = recording.get("Response", {}).get("Body", "")
            if body_str:
                try:
                    body = json.loads(body_str)
                    extract_fields(body, endpoint_fields[endpoint_key])
                except json.JSONDecodeError:
                    pass

        # Parse request body
        req_body_str = recording.get("Request", {}).get("Body")
        if req_body_str:
            try:
                req_body = json.loads(req_body_str)
                extract_fields(req_body, request_fields[endpoint_key])
            except json.JSONDecodeError:
                pass

    # Convert sets to lists for JSON serialization
    for endpoint in endpoint_fields:
        for field in endpoint_fields[endpoint]:
            endpoint_fields[endpoint][field]["observed_types"] = sorted(
                endpoint_fields[endpoint][field]["observed_types"])
            endpoint_fields[endpoint][field]["sample_values"] = (
                endpoint_fields[endpoint][field]["sample_values"][:3])

    for endpoint in request_fields:
        for field in request_fields[endpoint]:
            request_fields[endpoint][field]["observed_types"] = sorted(
                request_fields[endpoint][field]["observed_types"])
            request_fields[endpoint][field]["sample_values"] = (
                request_fields[endpoint][field]["sample_values"][:3])

    return dict(endpoint_fields), dict(request_fields)


def normalize_path(path):
    """Normalize API path by replacing dynamic segments with placeholders."""
    # Replace account IDs (DU*, U*)
    path = re.sub(r'/DU\w+/', '/{accountId}/', path)
    path = re.sub(r'/U\d+/', '/{accountId}/', path)
    # Replace numeric IDs at end of path
    path = re.sub(r'/\d+$', '/{id}', path)
    # Replace UUIDs
    path = re.sub(r'/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', '/{id}', path)
    return path


def json_type(value):
    """Return the JSON type name for a Python value."""
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "boolean"
    if isinstance(value, int):
        return "integer"
    if isinstance(value, float):
        return "number"
    if isinstance(value, str):
        # Check if it looks like a number encoded as string
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


def extract_fields(data, field_map, prefix=""):
    """Recursively extract field names and types from a JSON structure."""
    if isinstance(data, dict):
        for key, value in data.items():
            full_key = f"{prefix}.{key}" if prefix else key
            jtype = json_type(value)
            field_map[full_key]["observed_types"].add(jtype)
            if len(field_map[full_key]["sample_values"]) < 3:
                sample = str(value)[:100] if value is not None else "null"
                if sample not in [str(s) for s in field_map[full_key]["sample_values"]]:
                    field_map[full_key]["sample_values"].append(sample)
            field_map[full_key]["seen_count"] += 1

            # Recurse into objects
            if isinstance(value, dict):
                extract_fields(value, field_map, full_key)
            elif isinstance(value, list) and value:
                # Extract fields from first array element
                if isinstance(value[0], dict):
                    for item in value[:2]:  # Check first 2 items
                        extract_fields(item, field_map, f"{full_key}[]")

    elif isinstance(data, list) and data:
        # Top-level array
        if isinstance(data[0], dict):
            for item in data[:2]:
                extract_fields(item, field_map, prefix + "[]" if prefix else "[]")


# --- Phase 3: Parse Refit Interfaces for Endpoint-to-Model Mapping ---

def parse_refit_interfaces():
    """Map API endpoints to their DTO model types."""
    endpoint_to_model = {}

    api_files = list(SRC_DIR.rglob("IIbkr*Api.cs"))
    for filepath in api_files:
        content = filepath.read_text()

        # Find [Get/Post/Delete/Put("path")] followed by Task<ReturnType> MethodName
        pattern = re.compile(
            r'\[(Get|Post|Delete|Put)\("([^"]+)"\)\]\s*'
            r'Task<([^>]+)>\s+(\w+)',
            re.DOTALL
        )

        for match in pattern.finditer(content):
            http_method = match.group(1).upper()
            path = match.group(2)
            return_type = match.group(3).strip()
            method_name = match.group(4)

            # Normalize path placeholders
            norm_path = re.sub(r'\{[^}]+\}', '{id}', path)
            endpoint_key = f"{http_method} {norm_path}"

            # Extract inner type from List<T>, IApiResponse<T>, etc.
            inner_match = re.match(r'(?:List|IApiResponse)<(\w+)>', return_type)
            model_type = inner_match.group(1) if inner_match else return_type

            endpoint_to_model[endpoint_key] = {
                "method": http_method,
                "path": path,
                "return_type": return_type,
                "model_type": model_type,
                "method_name": method_name,
            }

    return endpoint_to_model


# --- Phase 4: Compare and Report ---

def compare(models, endpoint_to_model, response_fields, request_fields):
    """Compare DTOs against recorded responses and produce findings."""
    findings = []

    # Build reverse map: model_type -> endpoint_key
    model_to_endpoints = defaultdict(list)
    for ek, info in endpoint_to_model.items():
        model_to_endpoints[info["model_type"]].append(ek)

    for model_name, model_info in sorted(models.items()):
        endpoints = model_to_endpoints.get(model_name, [])
        dto_fields = set(model_info["properties"].keys())

        for endpoint_key in endpoints:
            # Find matching recorded response fields
            # Try exact match first, then fuzzy
            rec_fields = find_matching_recordings(endpoint_key, response_fields)

            if not rec_fields:
                findings.append({
                    "type": "NO_RECORDING",
                    "model": model_name,
                    "endpoint": endpoint_key,
                    "message": f"No recorded response found for {endpoint_key}",
                })
                continue

            # Get top-level field names from recordings (strip prefixes)
            rec_top_fields = set()
            rec_field_info = {}
            for field_path, info in rec_fields.items():
                # Get immediate field name (no dots, no array brackets)
                parts = field_path.replace("[].", ".").split(".")
                if len(parts) <= 2:  # top-level or one level into array
                    top_field = parts[-1] if parts[-1] != "[]" else parts[0]
                    if top_field and not top_field.startswith("["):
                        rec_top_fields.add(top_field)
                        rec_field_info[top_field] = info

            # Fields in recording but not in DTO (we're dropping data)
            missing = rec_top_fields - dto_fields
            # Filter out nested object fields and known wrapper fields
            missing = {f for f in missing if "." not in f}

            for field in sorted(missing):
                info = rec_field_info.get(field, {})
                findings.append({
                    "type": "MISSING_IN_DTO",
                    "model": model_name,
                    "endpoint": endpoint_key,
                    "field": field,
                    "observed_types": info.get("observed_types", []),
                    "sample_values": info.get("sample_values", []),
                    "message": f"Field '{field}' in API response but not in {model_name}",
                })

            # Fields in DTO but not in recording (wrong name or unused)
            extra = dto_fields - rec_top_fields
            for field in sorted(extra):
                prop_info = model_info["properties"][field]
                findings.append({
                    "type": "EXTRA_IN_DTO",
                    "model": model_name,
                    "endpoint": endpoint_key,
                    "field": field,
                    "csharp_name": prop_info["csharp_name"],
                    "csharp_type": prop_info["csharp_type"],
                    "message": f"Field '{field}' in {model_name} but not in API response (wrong name?)",
                })

            # Type mismatches for fields that exist in both
            common = dto_fields & rec_top_fields
            for field in sorted(common):
                prop_info = model_info["properties"][field]
                rec_info = rec_field_info.get(field, {})
                observed = rec_info.get("observed_types", [])

                mismatch = check_type_mismatch(prop_info, observed)
                if mismatch:
                    findings.append({
                        "type": "TYPE_MISMATCH",
                        "model": model_name,
                        "endpoint": endpoint_key,
                        "field": field,
                        "csharp_type": prop_info["csharp_type"],
                        "observed_types": observed,
                        "sample_values": rec_info.get("sample_values", []),
                        "has_number_handling": prop_info.get("has_number_handling", False),
                        "converter": prop_info.get("converter"),
                        "message": mismatch,
                    })

    # Models with no endpoint mapping
    for model_name, model_info in sorted(models.items()):
        if model_name not in model_to_endpoints or not model_to_endpoints[model_name]:
            # Check if it's a request model (used as [Body])
            if not any(model_name.endswith(s) for s in ("Request", "Payload", "WireModel")):
                findings.append({
                    "type": "UNMAPPED_MODEL",
                    "model": model_name,
                    "module": model_info["module"],
                    "message": f"{model_name} has no Refit endpoint mapping",
                })

    return findings


def find_matching_recordings(endpoint_key, response_fields):
    """Find recorded fields matching an endpoint, with fuzzy path matching."""
    if endpoint_key in response_fields:
        return response_fields[endpoint_key]

    # Try fuzzy: normalize both sides
    method, path = endpoint_key.split(" ", 1)
    for rec_key, rec_data in response_fields.items():
        rec_method, rec_path = rec_key.split(" ", 1)
        if rec_method == method:
            # Normalize both paths the same way
            norm_rec = re.sub(r'/\{[^}]+\}', '/{id}', rec_path)
            norm_ep = re.sub(r'/\{[^}]+\}', '/{id}', path)
            if norm_rec == norm_ep:
                return rec_data

    return None


def check_type_mismatch(prop_info, observed_types):
    """Check if the C# type is compatible with observed JSON types."""
    csharp_type = prop_info["csharp_type"]
    has_nh = prop_info.get("has_number_handling", False)
    converter = prop_info.get("converter")

    # Filter out null from observed types for comparison
    non_null = [t for t in observed_types if t != "null"]
    if not non_null:
        return None

    # String type expecting number
    if csharp_type in ("string", "string?"):
        if any(t in ("integer", "number") for t in non_null):
            if converter == "FlexibleStringConverter":
                return None  # Already handled
            return f"C# type is {csharp_type} but API sends {non_null} — needs FlexibleStringConverter"

    # Numeric types
    if csharp_type in ("int", "int?", "long", "long?", "decimal", "decimal?", "double", "double?"):
        if "string(numeric)" in non_null or "string" in non_null:
            if has_nh or converter:
                return None  # Already handled
            return f"C# type is {csharp_type} but API sends string-encoded number — needs JsonNumberHandling"
        if csharp_type in ("int", "int?") and "number" in non_null:
            # JSON number could be float, int expects integer
            pass  # Usually fine

    # Decimal expecting empty string
    if csharp_type in ("decimal?", "decimal"):
        if "string" in non_null:  # Could be empty string
            if converter == "FlexibleDecimalConverter":
                return None
            if has_nh:
                return f"C# type is {csharp_type} with NumberHandling but API may send empty string — needs FlexibleDecimalConverter"

    return None


# --- Main ---

def main():
    print("Phase 1: Parsing C# DTOs...", file=sys.stderr)
    models = parse_csharp_models()
    print(f"  Found {len(models)} models", file=sys.stderr)

    print("Phase 2: Parsing recorded responses...", file=sys.stderr)
    response_fields, request_fields = parse_recordings()
    print(f"  Found {len(response_fields)} response endpoints, {len(request_fields)} request endpoints", file=sys.stderr)

    print("Phase 3: Parsing Refit interfaces...", file=sys.stderr)
    endpoint_to_model = parse_refit_interfaces()
    print(f"  Found {len(endpoint_to_model)} endpoint-to-model mappings", file=sys.stderr)

    print("Phase 4: Comparing...", file=sys.stderr)
    findings = compare(models, endpoint_to_model, response_fields, request_fields)
    print(f"  Found {len(findings)} findings", file=sys.stderr)

    # Output
    report = {
        "summary": {
            "total_models": len(models),
            "total_endpoints_recorded": len(response_fields),
            "total_findings": len(findings),
            "by_type": defaultdict(int),
        },
        "findings": findings,
        "models": {k: {**v, "property_count": len(v["properties"])} for k, v in models.items()},
        "recorded_endpoints": sorted(response_fields.keys()),
    }

    for f in findings:
        report["summary"]["by_type"][f["type"]] += 1
    report["summary"]["by_type"] = dict(report["summary"]["by_type"])

    json.dump(report, sys.stdout, indent=2, default=str)


if __name__ == "__main__":
    main()
