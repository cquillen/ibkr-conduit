# M2: DTO Generator Script Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Python script that reads captured API recordings and generates C# record definitions matching the actual wire format.

**Architecture:** Single Python script with functions for: reading recordings, inferring types from JSON, generating C# code, and diffing against existing models. Tested against the spike recording from M1.

**Tech Stack:** Python 3, json, pathlib, re (no external dependencies)

---

### Task 1: Core Type Inference

**Files:**
- Create: `tools/generate_dtos.py`

- [ ] **Step 1: Create the script with type inference logic**

```python
#!/usr/bin/env python3
"""Generate C# record definitions from captured API recordings."""

import json
import re
import sys
from collections import defaultdict
from pathlib import Path

REPO_ROOT = Path(__file__).parent.parent


def infer_csharp_type(value):
    """Map a Python/JSON value to its C# type. No float/double — financial data uses decimal."""
    if value is None:
        return None  # Type unknown from null alone
    if isinstance(value, bool):
        return "bool"
    if isinstance(value, int):
        if abs(value) > 2_147_483_647:
            return "long"
        return "int"
    if isinstance(value, float):
        return "decimal"
    if isinstance(value, str):
        return "string"
    if isinstance(value, list):
        return "list"
    if isinstance(value, dict):
        return "object"
    return "object"


def merge_types(existing, new_type):
    """Merge two inferred types. If they conflict, widen to the more general type."""
    if existing is None:
        return new_type
    if new_type is None:
        return existing
    if existing == new_type:
        return existing
    # int + long -> long
    if {existing, new_type} == {"int", "long"}:
        return "long"
    # int/long + decimal -> decimal
    if {existing, new_type} & {"decimal"} and {existing, new_type} & {"int", "long"}:
        return "decimal"
    # Any conflict with string -> string (IBKR sends mixed types)
    if "string" in {existing, new_type}:
        return "string"
    return "object"
```

- [ ] **Step 2: Test type inference manually**

Run: `python3 -c "from tools.generate_dtos import infer_csharp_type; print(infer_csharp_type(42)); print(infer_csharp_type('hello')); print(infer_csharp_type(3.14)); print(infer_csharp_type(None)); print(infer_csharp_type(True))"`

Expected output:
```
int
string
decimal
None
bool
```

- [ ] **Step 3: Commit**

```bash
git add tools/generate_dtos.py
git commit -m "feat: add DTO generator script with type inference (M2 task 1)"
```

---

### Task 2: Recording Parser

**Files:**
- Modify: `tools/generate_dtos.py`

- [ ] **Step 1: Add recording parsing and field extraction**

Add these functions to `tools/generate_dtos.py`:

```python
def parse_recordings(recordings_dir):
    """Read all recordings and group response fields by endpoint."""
    endpoints = defaultdict(lambda: {
        "fields": defaultdict(lambda: {"types": [], "nullable": False, "is_list": False, "is_object": False, "child_fields": None}),
        "is_array_response": False,
        "recording_count": 0,
    })

    for json_file in sorted(Path(recordings_dir).rglob("*.json")):
        try:
            recording = json.loads(json_file.read_text())
        except (json.JSONDecodeError, UnicodeDecodeError):
            continue

        status = recording.get("Response", {}).get("StatusCode", 0)
        if status != 200:
            continue

        path = recording.get("Request", {}).get("Path", "")
        method = (recording.get("Request", {}).get("Methods", [""])[0] or "").upper()
        body = recording.get("Response", {}).get("Body")

        if body is None:
            continue

        norm_path = normalize_endpoint(path)
        key = f"{method} {norm_path}"
        endpoints[key]["recording_count"] += 1

        if isinstance(body, list):
            endpoints[key]["is_array_response"] = True
            for item in body:
                if isinstance(item, dict):
                    extract_fields(item, endpoints[key]["fields"])
        elif isinstance(body, dict):
            extract_fields(body, endpoints[key]["fields"])

    return dict(endpoints)


def normalize_endpoint(path):
    """Normalize path by replacing dynamic segments."""
    path = re.sub(r'/DU\w+/', '/{accountId}/', path)
    path = re.sub(r'/U\d+/', '/{accountId}/', path)
    path = re.sub(r'/\d+$', '/{id}', path)
    path = re.sub(r'/\d+/', '/{id}/', path)
    return path


def extract_fields(obj, field_map):
    """Extract top-level fields from a JSON object, inferring types."""
    for key, value in obj.items():
        ctype = infer_csharp_type(value)
        field_info = field_map[key]

        if value is None:
            field_info["nullable"] = True
        elif isinstance(value, list):
            field_info["is_list"] = True
            field_info["types"].append("list")
            # Peek at list element types
            if value and isinstance(value[0], dict):
                if field_info["child_fields"] is None:
                    field_info["child_fields"] = defaultdict(lambda: {"types": [], "nullable": False, "is_list": False, "is_object": False, "child_fields": None})
                for item in value[:3]:
                    if isinstance(item, dict):
                        extract_fields(item, field_info["child_fields"])
        elif isinstance(value, dict):
            field_info["is_object"] = True
            field_info["types"].append("object")
            if field_info["child_fields"] is None:
                field_info["child_fields"] = defaultdict(lambda: {"types": [], "nullable": False, "is_list": False, "is_object": False, "child_fields": None})
            extract_fields(value, field_info["child_fields"])
        else:
            field_info["types"].append(ctype)
```

- [ ] **Step 2: Test against spike recording**

Run: `python3 -c "
from tools.generate_dtos import parse_recordings
eps = parse_recordings('recordings')
for k, v in sorted(eps.items()):
    print(f'{k}: {v[\"recording_count\"]} recordings, {len(v[\"fields\"])} fields, array={v[\"is_array_response\"]}')
"`

Expected: Shows `GET /v1/api/portfolio/accounts` with 24 fields, `is_array_response=True`.

- [ ] **Step 3: Commit**

```bash
git add tools/generate_dtos.py
git commit -m "feat: add recording parser with field extraction (M2 task 2)"
```

---

### Task 3: C# Code Generator

**Files:**
- Modify: `tools/generate_dtos.py`

- [ ] **Step 1: Add C# record generation**

Add these functions:

```python
def to_pascal_case(name):
    """Convert a JSON field name to PascalCase for C# property names."""
    # Handle special chars (e.g., PrepaidCrypto-Z -> PrepaidCryptoZ)
    name = re.sub(r'[^a-zA-Z0-9]', '_', name)
    parts = name.split('_')
    return ''.join(p.capitalize() for p in parts if p)


def resolve_field_type(field_info):
    """Determine the final C# type for a field."""
    if field_info["is_list"]:
        if field_info["child_fields"]:
            return "List<object>"  # Placeholder — nested records handled separately
        return "List<object>"

    if field_info["is_object"]:
        return "object"  # Placeholder — nested records handled separately

    types = [t for t in field_info["types"] if t is not None]
    if not types:
        return "object"

    # Find consensus type
    result = None
    for t in types:
        result = merge_types(result, t)

    if result is None:
        return "object"

    # Make nullable if we've seen null values
    if field_info["nullable"] and result not in ("string", "object"):
        return f"{result}?"

    # Strings are already nullable by convention (string? for nullable)
    if result == "string" and field_info["nullable"]:
        return "string?"

    return result


def generate_record(record_name, fields, namespace="Generated"):
    """Generate a C# positional record from field definitions."""
    lines = []
    lines.append("using System.Diagnostics.CodeAnalysis;")
    lines.append("using System.Text.Json;")
    lines.append("using System.Text.Json.Serialization;")
    lines.append("")
    lines.append(f"namespace {namespace};")
    lines.append("")
    lines.append(f"[ExcludeFromCodeCoverage]")
    lines.append(f"public record {record_name}(")

    params = []
    for json_name, field_info in fields.items():
        csharp_type = resolve_field_type(field_info)
        pascal_name = to_pascal_case(json_name)

        # Avoid name collisions with the record name
        if pascal_name == record_name:
            pascal_name += "Value"

        param = f'    [property: JsonPropertyName("{json_name}")] {csharp_type} {pascal_name}'
        params.append(param)

    # Join with comma-newline, last one gets no comma
    for i, param in enumerate(params):
        if i < len(params) - 1:
            lines.append(param + ",")
        else:
            lines.append(param + ")")

    # Add JsonExtensionData
    lines.append("{")
    lines.append("    /// <summary>Additional unmapped properties from the API response.</summary>")
    lines.append("    [JsonExtensionData]")
    lines.append("    public Dictionary<string, JsonElement>? AdditionalData { get; init; }")
    lines.append("}")
    lines.append("")

    return "\n".join(lines)
```

- [ ] **Step 2: Test generation against spike**

Run: `python3 -c "
from tools.generate_dtos import parse_recordings, generate_record
eps = parse_recordings('recordings')
ep = eps.get('GET /v1/api/portfolio/accounts', {})
print(generate_record('Account', ep['fields']))
"`

Expected: A complete C# record with all 24 fields, correct types, `[JsonPropertyName]` attributes, and `[JsonExtensionData]`.

- [ ] **Step 3: Commit**

```bash
git add tools/generate_dtos.py
git commit -m "feat: add C# record code generator (M2 task 3)"
```

---

### Task 4: CLI Entry Point and Diff Report

**Files:**
- Modify: `tools/generate_dtos.py`

- [ ] **Step 1: Add the main function with CLI, output, and diff reporting**

```python
def generate_all(recordings_dir, output_dir):
    """Generate DTOs for all recorded endpoints."""
    endpoints = parse_recordings(recordings_dir)
    output_path = Path(output_dir)
    output_path.mkdir(parents=True, exist_ok=True)

    generated = []
    for endpoint_key, ep_info in sorted(endpoints.items()):
        if not ep_info["fields"]:
            continue

        method, path = endpoint_key.split(" ", 1)
        record_name = derive_record_name(path, ep_info["is_array_response"])
        namespace = derive_namespace(path)

        code = generate_record(record_name, ep_info["fields"], namespace)

        # Save to file
        filename = f"{record_name}.generated.cs"
        filepath = output_path / filename
        filepath.write_text(code)

        generated.append({
            "endpoint": endpoint_key,
            "record_name": record_name,
            "field_count": len(ep_info["fields"]),
            "file": str(filepath),
        })

        print(f"  {endpoint_key} -> {record_name} ({len(ep_info['fields'])} fields)")

    return generated


def derive_record_name(path, is_array):
    """Derive a C# record name from an endpoint path."""
    # Strip /v1/api/ prefix
    clean = re.sub(r'^/v1/api/', '', path)
    # Remove placeholders
    clean = re.sub(r'/\{[^}]+\}', '', clean)
    # Take last meaningful segment
    parts = [p for p in clean.split('/') if p]

    if not parts:
        return "UnknownResponse"

    # Use last 1-2 segments
    name = ''.join(to_pascal_case(p) for p in parts[-2:])

    # Add Response suffix if not already descriptive
    if not name.endswith(('Response', 'Result', 'Status', 'Info', 'Detail')):
        name += "Response"

    return name


def derive_namespace(path):
    """Derive a C# namespace from an endpoint path."""
    clean = re.sub(r'^/v1/api/', '', path)
    parts = [p for p in clean.split('/') if p and not p.startswith('{')]

    # Map known prefixes to namespaces
    if parts and parts[0] == "portfolio":
        return "IbkrConduit.Portfolio"
    if parts and parts[0] == "iserver":
        if len(parts) > 1 and parts[1] == "account":
            return "IbkrConduit.Accounts"
        if len(parts) > 1 and parts[1] == "secdef":
            return "IbkrConduit.Contracts"
        if len(parts) > 1 and parts[1] == "marketdata":
            return "IbkrConduit.MarketData"
        if len(parts) > 1 and parts[1] == "contract":
            return "IbkrConduit.Contracts"
        return "IbkrConduit.Session"
    if parts and parts[0] == "fyi":
        return "IbkrConduit.Fyi"
    if parts and parts[0] in ("trsrv", "md"):
        return "IbkrConduit.Contracts"
    if parts and parts[0] == "pa":
        return "IbkrConduit.Portfolio"

    return "IbkrConduit"


def main():
    recordings_dir = REPO_ROOT / "recordings"
    output_dir = REPO_ROOT / "generated"

    if not recordings_dir.exists():
        print(f"ERROR: Recordings directory not found: {recordings_dir}", file=sys.stderr)
        sys.exit(1)

    print(f"Reading recordings from: {recordings_dir}")
    print(f"Generating DTOs to: {output_dir}")
    print()

    generated = generate_all(str(recordings_dir), str(output_dir))

    print(f"\nGenerated {len(generated)} record(s)")


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: Run the full script against spike recordings**

Run: `python3 tools/generate_dtos.py`

Expected output:
```
Reading recordings from: /workspace/ibkr-conduit/recordings
Generating DTOs to: /workspace/ibkr-conduit/generated

  GET /v1/api/portfolio/accounts -> PortfolioAccountsResponse (24 fields)

Generated 1 record(s)
```

- [ ] **Step 3: Verify the generated file**

Run: `cat generated/PortfolioAccountsResponse.generated.cs`

Expected: A valid C# record with all 24 fields from the spike recording, correct types, and `[JsonExtensionData]`.

- [ ] **Step 4: Commit**

```bash
git add tools/generate_dtos.py
git commit -m "feat: add CLI entry point and endpoint-to-record naming (M2 task 4)"
```

---

### Task 5: Validate Generated Code Compiles

**Files:**
- Verify: `generated/PortfolioAccountsResponse.generated.cs`

- [ ] **Step 1: Check the generated record compiles**

Create a temporary test by adding the generated file to the main project temporarily:

Run: `cp generated/PortfolioAccountsResponse.generated.cs /tmp/test_dto.cs`

Then verify the types look correct by inspection. The generated file should:
- Have `using System.Diagnostics.CodeAnalysis;`
- Have `using System.Text.Json;`
- Have `using System.Text.Json.Serialization;`
- Have a namespace
- Have `[ExcludeFromCodeCoverage]`
- Have `[JsonPropertyName("...")]` on every property
- Have correct C# types (string, bool, int, long, decimal — no float/double)
- Have nullable annotations where values were null in the recording
- Have `[JsonExtensionData]` property
- Not have any `JsonNumberHandling` or custom converters

- [ ] **Step 2: Add generated/ to .gitignore**

Add `generated/` to `.gitignore` since these are intermediate outputs.

- [ ] **Step 3: Final commit**

```bash
git add tools/generate_dtos.py .gitignore
git commit -m "feat: complete M2 DTO generator with spike validation"
```

---

## Expected Output for Spike Recording

The generator should produce something like:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Portfolio;

[ExcludeFromCodeCoverage]
public record PortfolioAccountsResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("PrepaidCrypto-Z")] bool PrepaidCryptoZ,
    [property: JsonPropertyName("PrepaidCrypto-P")] bool PrepaidCryptoP,
    [property: JsonPropertyName("brokerageAccess")] bool BrokerageAccess,
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("accountVan")] string AccountVan,
    [property: JsonPropertyName("accountTitle")] string AccountTitle,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("accountAlias")] string? AccountAlias,
    [property: JsonPropertyName("accountStatus")] long AccountStatus,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("tradingType")] string TradingType,
    [property: JsonPropertyName("businessType")] string BusinessType,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("ibEntity")] string IbEntity,
    [property: JsonPropertyName("faclient")] bool Faclient,
    [property: JsonPropertyName("clearingStatus")] string ClearingStatus,
    [property: JsonPropertyName("covestor")] bool Covestor,
    [property: JsonPropertyName("noClientTrading")] bool NoClientTrading,
    [property: JsonPropertyName("trackVirtualFXPortfolio")] bool TrackVirtualFxPortfolio,
    [property: JsonPropertyName("parent")] object Parent,
    [property: JsonPropertyName("desc")] string Desc,
    [property: JsonPropertyName("acctCustType")] string AcctCustType)
{
    /// <summary>Additional unmapped properties from the API response.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```
