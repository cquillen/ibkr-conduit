#!/usr/bin/env python3
"""Generate C# record DTOs from captured IBKR API recordings.

Reads WireMock-style JSON recordings from a recordings/ directory,
infers C# types from JSON values, and generates positional C# records
matching the wire format exactly.

Usage:
    python3 tools/generate_dtos.py [recordings_dir] [output_dir]

Defaults:
    recordings_dir = recordings/
    output_dir     = generated/
"""

from __future__ import annotations

import json
import re
import sys
from collections import defaultdict
from pathlib import Path
from typing import Any

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

INT_MAX = 2_147_483_647

# Endpoint prefix -> C# namespace segment
NAMESPACE_MAP: dict[str, str] = {
    "portfolio": "Portfolio",
    "iserver/secdef": "Contracts",
    "iserver/marketdata": "MarketData",
    "iserver/contract": "Contracts",
    "iserver/account": "Account",
    "iserver": "Trading",
    "ccp": "Ccp",
    "fyi": "Notifications",
    "tickle": "Session",
    "logout": "Session",
    "sso": "Session",
    "hmds": "HistoricalData",
    "pa": "PerformanceAnalytics",
    "trsrv": "TradingServices",
}

# Patterns to normalise in endpoint paths
ACCOUNT_ID_RE = re.compile(r"[A-Z]{1,3}\d{5,}")
NUMERIC_ID_RE = re.compile(r"(?<=/)\d{3,}(?=/|$)")

# ---------------------------------------------------------------------------
# Type inference
# ---------------------------------------------------------------------------


class CSharpType:
    """Represents an inferred C# type for a JSON field."""

    def __init__(self, base: str, nullable: bool = False):
        self.base = base
        self.nullable = nullable

    def __repr__(self) -> str:
        suffix = "?" if self.nullable else ""
        return f"{self.base}{suffix}"

    def merge(self, other: CSharpType) -> CSharpType:
        """Widen two types into their common supertype."""
        nullable = self.nullable or other.nullable
        a, b = self.base, other.base

        if a == b:
            return CSharpType(a, nullable)

        pair = frozenset({a, b})

        # long + decimal -> decimal
        if pair == frozenset({"long", "decimal"}):
            return CSharpType("decimal", nullable)
        # anything + string -> string
        if "string" in pair:
            return CSharpType("string", nullable)

        # Fallback: object
        return CSharpType("object", nullable)


def infer_type(value: Any) -> CSharpType:
    """Map a JSON value to a CSharpType."""
    if value is None:
        return CSharpType("string", nullable=True)

    if isinstance(value, bool):
        return CSharpType("bool")

    if isinstance(value, int):
        return CSharpType("long")

    if isinstance(value, float):
        return CSharpType("decimal")

    if isinstance(value, str):
        return CSharpType("string")

    if isinstance(value, list):
        if value and isinstance(value[0], dict):
            return CSharpType("List<object>")
        return CSharpType("List<object>")

    if isinstance(value, dict):
        return CSharpType("object")

    return CSharpType("object")


# ---------------------------------------------------------------------------
# Recording parser
# ---------------------------------------------------------------------------


def normalise_path(path: str) -> str:
    """Replace account IDs and numeric IDs with placeholders."""
    path = path.rstrip("/")
    # Strip leading /v1/api or /api prefix
    path = re.sub(r"^/v\d+/api", "", path)
    path = re.sub(r"^/api", "", path)
    # Replace account IDs
    path = ACCOUNT_ID_RE.sub("{accountId}", path)
    # Replace numeric IDs
    path = NUMERIC_ID_RE.sub("{id}", path)
    return path


def parse_recording(file_path: Path) -> dict[str, Any] | None:
    """Parse a single recording file and return structured info, or None if not usable."""
    try:
        data = json.loads(file_path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, OSError) as exc:
        print(f"  WARN: skipping {file_path}: {exc}")
        return None

    request = data.get("Request", {})
    response = data.get("Response", {})

    status = response.get("StatusCode", 0)
    if status != 200:
        return None

    methods = request.get("Methods", [])
    method = methods[0] if methods else "GET"
    raw_path = request.get("Path", "")
    norm_path = normalise_path(raw_path)

    body = response.get("Body")
    if body is None:
        return None

    return {
        "method": method,
        "path": norm_path,
        "body": body,
    }


FieldMap = dict[str, CSharpType]


def merge_fields(existing: FieldMap, new_fields: FieldMap) -> FieldMap:
    """Merge new field observations into existing, widening types."""
    merged = dict(existing)
    for name, typ in new_fields.items():
        if name in merged:
            merged[name] = merged[name].merge(typ)
        else:
            # Field absent in previous samples -> nullable
            typ.nullable = True
            merged[name] = typ

    # Fields in existing but not in new -> nullable
    for name in existing:
        if name not in new_fields:
            merged[name].nullable = True

    return merged


def extract_fields(obj: dict[str, Any]) -> FieldMap:
    """Extract field names and inferred types from a JSON object."""
    fields: FieldMap = {}
    for key, value in obj.items():
        fields[key] = infer_type(value)
    return fields


# ---------------------------------------------------------------------------
# Endpoint grouping
# ---------------------------------------------------------------------------


class EndpointInfo:
    """Aggregated info for a single endpoint."""

    def __init__(self, method: str, path: str):
        self.method = method
        self.path = path
        self.fields: FieldMap = {}
        self.is_array = False
        self.sample_count = 0

    def add_sample(self, body: Any) -> None:
        items: list[dict[str, Any]] = []

        if isinstance(body, list):
            self.is_array = True
            items = [item for item in body if isinstance(item, dict)]
        elif isinstance(body, dict):
            items = [body]

        for item in items:
            new_fields = extract_fields(item)
            if self.sample_count == 0 and not self.fields:
                self.fields = new_fields
            else:
                self.fields = merge_fields(self.fields, new_fields)

        self.sample_count += 1


def group_recordings(recordings_dir: Path) -> dict[str, EndpointInfo]:
    """Read all recordings and group by normalised endpoint."""
    endpoints: dict[str, EndpointInfo] = {}
    file_count = 0

    for json_file in sorted(recordings_dir.rglob("*.json")):
        file_count += 1
        parsed = parse_recording(json_file)
        if parsed is None:
            continue

        key = f"{parsed['method']} {parsed['path']}"
        if key not in endpoints:
            endpoints[key] = EndpointInfo(parsed["method"], parsed["path"])
        endpoints[key].add_sample(parsed["body"])

    print(f"Scanned {file_count} recording file(s), found {len(endpoints)} endpoint(s).\n")
    return endpoints


# ---------------------------------------------------------------------------
# Naming
# ---------------------------------------------------------------------------


def to_pascal_case(s: str) -> str:
    """Convert a string to PascalCase, handling camelCase, snake_case, hyphens."""
    import inflection
    # Replace hyphens/dots with underscores so inflection can handle them
    clean = s.replace("-", "_").replace(".", "_")
    return inflection.camelize(clean)


def endpoint_to_record_name(method: str, path: str) -> str:
    """Derive a C# record name from the endpoint."""
    # Strip leading slash and placeholders
    clean = path.lstrip("/")
    # Remove placeholders
    clean = re.sub(r"\{[^}]+\}", "", clean)
    # Split segments and filter empties
    segments = [s for s in clean.split("/") if s]
    if not segments:
        return "UnknownResponse"
    name = "".join(to_pascal_case(seg) for seg in segments)
    return f"{name}Response"


def endpoint_to_namespace(path: str) -> str:
    """Derive a C# namespace from the endpoint path prefix."""
    clean = path.lstrip("/")
    # Try two-segment prefixes first, then one-segment
    for prefix, ns in sorted(NAMESPACE_MAP.items(), key=lambda x: -len(x[0])):
        if clean.startswith(prefix):
            return f"IbkrConduit.{ns}"
    # Fallback: first segment
    first = clean.split("/")[0] if clean else "Unknown"
    return f"IbkrConduit.{to_pascal_case(first)}"


def field_to_property_name(field: str) -> str:
    """Convert a JSON field name to a PascalCase C# property name."""
    return to_pascal_case(field)


# ---------------------------------------------------------------------------
# API doc field descriptions
# ---------------------------------------------------------------------------

API_DOCS_PATH = Path(__file__).parent.parent / "docs" / "ibkr_api.md"


def parse_api_doc_fields() -> dict[str, dict[str, str]]:
    """Parse ibkr_api.md to extract field descriptions per endpoint.

    Returns {normalized_endpoint: {field_name_lower: description}}.
    Field names are lowercased for case-insensitive matching since the docs
    sometimes use different casing than the actual API (e.g., faClient vs faclient).
    """
    if not API_DOCS_PATH.exists():
        return {}

    content = API_DOCS_PATH.read_text(encoding="utf-8")
    lines = content.split("\n")

    result: dict[str, dict[str, str]] = {}

    # Find endpoint definitions: `METHOD /path`
    backtick = "\x60"
    endpoint_re = re.compile(
        backtick + r"(GET|POST|PUT|DELETE)\s+(/[^" + backtick + r"]+?)\s*" + backtick,
    )

    # Field definition: **fieldName:** Type.\n  Description text
    # Note: markdown bold wraps the colon: **id:** renders as bold "id:"
    # So the pattern is **fieldName:** (colon inside the bold) or **fieldName**:
    field_re = re.compile(
        r"\*\*(\w+)(?::\*\*|\*\*:?)\s*"
    )

    i = 0
    while i < len(lines):
        match = endpoint_re.search(lines[i])
        if not match:
            i += 1
            continue

        method = match.group(1)
        path = match.group(2)
        norm_path = re.sub(r"\{\{?\s*\w+\s*\}?\}", "{id}", path)
        endpoint_key = f"{method} {norm_path}"

        # Scan forward to find "#### Response Object" section
        j = i + 1
        found_response = False
        while j < min(i + 30, len(lines)):
            if "Response Object" in lines[j] or "Response object" in lines[j]:
                found_response = True
                break
            j += 1

        if not found_response:
            i += 1
            continue

        # Parse fields from the response section until we hit another section
        fields: dict[str, str] = {}
        k = j + 1
        while k < min(j + 200, len(lines)):
            line = lines[k].strip()

            # Stop at next section or endpoint
            if line.startswith("### ") or line.startswith("## "):
                break
            if endpoint_re.search(line):
                break

            # Match field definition
            fm = field_re.match(line)
            if fm:
                field_name = fm.group(1)
                # Skip non-field headings
                if field_name.upper() in ("NOTE", "NOTES", "REQUEST", "RESPONSE",
                                           "QUERY", "PATH", "BODY", "HEADERS"):
                    k += 1
                    continue

                # Get description from remainder of this line and/or next line
                remainder = line[fm.end():].strip()
                # Strip type annotation (e.g., "String." or "int." or "bool.")
                remainder = re.sub(
                    r"^:?\s*(?:String|int|float|bool|boolean|Array|object|decimal|number)"
                    r"[\s.]*",
                    "", remainder, flags=re.IGNORECASE,
                ).strip()

                # If remainder is empty, check next line for description
                if not remainder and k + 1 < len(lines):
                    next_line = lines[k + 1].strip()
                    if next_line and not next_line.startswith("**") and not next_line.startswith("```"):
                        remainder = next_line

                if remainder:
                    # Clean up description
                    remainder = remainder.strip().rstrip(".")
                    # Strip stray braces/brackets from object/array type hints
                    remainder = remainder.strip("{}[]")
                    remainder = remainder.strip()
                    # Skip if nothing meaningful left
                    if not remainder or len(remainder) < 3:
                        k += 1
                        continue
                    # Escape XML special chars for doc comments
                    remainder = (remainder
                                 .replace("&", "&amp;")
                                 .replace("<", "&lt;")
                                 .replace(">", "&gt;"))
                    fields[field_name.lower()] = remainder

            k += 1

        if fields:
            result[endpoint_key] = fields

        i += 1

    return result


def lookup_field_description(
    doc_fields: dict[str, dict[str, str]],
    endpoint_key: str,
    field_name: str,
) -> str | None:
    """Look up a field description from the parsed API docs.

    Tries exact endpoint match first, then fuzzy matching by normalizing
    placeholders. Field names are matched case-insensitively.
    """
    # Normalize the endpoint key the same way
    method, path = endpoint_key.split(" ", 1)
    norm = re.sub(r"/\{[^}]+\}", "/{id}", path)
    norm_key = f"{method} {norm}"

    # Try exact match
    fields = doc_fields.get(norm_key)
    if not fields:
        # Try fuzzy: strip /v1/api prefix from our key
        stripped = re.sub(r"^/v1/api", "", norm)
        stripped_key = f"{method} {stripped}"
        fields = doc_fields.get(stripped_key)

    if not fields:
        # Try matching just the path tail
        for dk, dv in doc_fields.items():
            dm, dp = dk.split(" ", 1)
            if dm == method:
                dp_norm = re.sub(r"/\{[^}]+\}", "/{id}", dp)
                path_norm = re.sub(r"/\{[^}]+\}", "/{id}", path)
                # Strip /v1/api from our path for comparison
                path_clean = re.sub(r"^/v1/api", "", path_norm)
                if dp_norm == path_clean:
                    fields = dv
                    break

    if not fields:
        return None

    return fields.get(field_name.lower())


# ---------------------------------------------------------------------------
# C# code generation
# ---------------------------------------------------------------------------


def generate_record(endpoint: EndpointInfo, doc_fields: dict[str, dict[str, str]] | None = None) -> tuple[str, str, str]:
    """Generate a C# record source file.

    Returns (record_name, namespace, source_code).
    """
    record_name = endpoint_to_record_name(endpoint.method, endpoint.path)
    namespace = endpoint_to_namespace(endpoint.path)

    lines: list[str] = []
    lines.append("// <auto-generated/>")
    lines.append("// Generated by tools/generate_dtos.py — do not edit by hand.")
    lines.append("")
    lines.append("using System.Collections.Generic;")
    lines.append("using System.Diagnostics.CodeAnalysis;")
    lines.append("using System.Text.Json;")
    lines.append("using System.Text.Json.Serialization;")
    lines.append("")
    lines.append(f"namespace {namespace};")
    lines.append("")
    lines.append("/// <summary>")
    lines.append(f"/// Response DTO for {endpoint.method} {endpoint.path}.")
    if endpoint.is_array:
        lines.append("/// The wire response is a JSON array of these objects.")
    lines.append("/// </summary>")
    lines.append("[ExcludeFromCodeCoverage]")
    lines.append(f"public sealed record {record_name}")
    lines.append("{")

    endpoint_key = f"{endpoint.method} {endpoint.path}"
    for field_name, field_type in endpoint.fields.items():
        prop_name = field_to_property_name(field_name)
        type_str = str(field_type)

        # Reference types are inherently nullable via ?
        # Value types need ? suffix (already handled by CSharpType)
        is_value_type = field_type.base in ("bool", "long", "decimal")
        if field_type.nullable and not is_value_type:
            # Reference types: ensure trailing ?
            if not type_str.endswith("?"):
                type_str += "?"

        # Add XML doc comment from API docs if available
        desc = None
        if doc_fields:
            desc = lookup_field_description(doc_fields, endpoint_key, field_name)
        if desc:
            lines.append(f"    /// <summary>{desc}.</summary>")

        lines.append(f"    [JsonPropertyName(\"{field_name}\")]")
        lines.append(f"    public {type_str} {prop_name} {{ get; init; }}")
        lines.append("")

    lines.append("    [JsonExtensionData]")
    lines.append("    public Dictionary<string, JsonElement>? ExtensionData { get; init; }")
    lines.append("}")
    lines.append("")

    source = "\n".join(lines)
    return record_name, namespace, source


# ---------------------------------------------------------------------------
# File writer
# ---------------------------------------------------------------------------


def write_generated(output_dir: Path, endpoints: dict[str, EndpointInfo], doc_fields: dict[str, dict[str, str]] | None = None) -> list[tuple[str, str, int]]:
    """Generate and write C# files. Returns list of (endpoint_key, record_name, field_count)."""
    output_dir.mkdir(parents=True, exist_ok=True)
    results: list[tuple[str, str, int]] = []

    for key, endpoint in sorted(endpoints.items()):
        record_name, namespace, source = generate_record(endpoint, doc_fields)
        filename = f"{record_name}.generated.cs"
        out_path = output_dir / filename
        out_path.write_text(source, encoding="utf-8")
        field_count = len(endpoint.fields)
        results.append((key, record_name, field_count))
        print(f"  {key}")
        print(f"    -> {namespace}.{record_name} ({field_count} fields)")
        print(f"    -> {out_path}")
        print()

    return results


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------


def main() -> int:
    # Resolve paths relative to repo root
    script_dir = Path(__file__).resolve().parent
    repo_root = script_dir.parent

    args = sys.argv[1:]
    recordings_dir = Path(args[0]) if len(args) > 0 else repo_root / "recordings"
    output_dir = Path(args[1]) if len(args) > 1 else repo_root / "generated"

    if not recordings_dir.exists():
        print(f"ERROR: recordings directory not found: {recordings_dir}")
        return 1

    print(f"Recordings: {recordings_dir}")
    print(f"Output:     {output_dir}")
    print()

    endpoints = group_recordings(recordings_dir)

    if not endpoints:
        print("No usable recordings found (200 OK with JSON body).")
        return 0

    # Parse API docs for field descriptions
    doc_fields = parse_api_doc_fields()
    if doc_fields:
        print(f"Loaded field descriptions for {len(doc_fields)} endpoint(s) from ibkr_api.md")
    else:
        print("No API docs found — generating without XML doc comments")
    print()

    print("Generated DTOs:")
    print("-" * 60)
    results = write_generated(output_dir, endpoints, doc_fields)
    print("-" * 60)
    print(f"Summary: {len(results)} record(s) generated from {sum(1 for _ in recordings_dir.rglob('*.json'))} recording(s).")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
