#!/usr/bin/env python3
"""Generate sanitized WireMock fixture files from IBKR recording JSON files.

Reads all *.json recordings from recordings/ recursively, sanitizes PII,
and writes clean fixture files to tests/IbkrConduit.Tests.Integration/Fixtures/.
"""

import json
import os
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
RECORDINGS_DIR = REPO_ROOT / "recordings"
FIXTURES_DIR = (
    REPO_ROOT / "tests" / "IbkrConduit.Tests.Integration" / "Fixtures"
)

# Fields whose values should be replaced with "Test User"
NAME_FIELDS = frozenset(
    {"accountTitle", "displayName", "accountAlias", "companyName", "company_name"}
)

# Account ID pattern: DU followed by word chars, or U followed by digits
ACCOUNT_ID_PATTERN = re.compile(r"\bDU\w+\b|\bU\d+\b")

SANITIZED_ACCOUNT_ID = "U1234567"
SANITIZED_NAME = "Test User"


def classify_module(path: str) -> str:
    """Derive the fixture module directory from the API path."""
    # Normalize: strip leading /v1/api if present
    p = path
    if p.startswith("/v1/api"):
        p = p[len("/v1/api"):]

    # Order matters — more specific patterns first
    if re.match(r"/iserver/account/[^/]+/alert", p):
        return "Alerts"
    if p.startswith("/iserver/account/allocation"):
        return "Allocation"
    if p.startswith("/iserver/account/orders") or p.startswith("/iserver/account/order"):
        return "Orders"
    if p.startswith("/iserver/account"):
        return "Accounts"
    if p.startswith("/iserver/watchlist"):
        return "Watchlists"
    if (
        p.startswith("/iserver/secdef")
        or p.startswith("/iserver/contract")
        or p.startswith("/trsrv")
    ):
        return "Contracts"
    if (
        p.startswith("/iserver/marketdata")
        or p.startswith("/iserver/scanner")
        or p.startswith("/hmds")
        or p.startswith("/md")
    ):
        return "MarketData"
    if (
        p.startswith("/iserver/auth")
        or p.startswith("/tickle")
        or p.startswith("/logout")
        or p.startswith("/sso")
    ):
        return "Session"
    if p.startswith("/fyi"):
        return "Fyi"
    if p.startswith("/portfolio"):
        return "Portfolio"

    return "Other"


def path_to_slug(path: str) -> str:
    """Convert an API path to a filename slug."""
    p = path
    if p.startswith("/v1/api"):
        p = p[len("/v1/api"):]
    # Strip leading slash, replace slashes with dashes
    return p.strip("/").replace("/", "-")


def sanitize_value(key: str, value):
    """Sanitize a single value based on its field name."""
    if key in NAME_FIELDS and isinstance(value, str) and value:
        return SANITIZED_NAME
    if key == "desc" and isinstance(value, str) and value:
        return SANITIZED_ACCOUNT_ID
    if isinstance(value, str) and ACCOUNT_ID_PATTERN.search(value):
        return ACCOUNT_ID_PATTERN.sub(SANITIZED_ACCOUNT_ID, value)
    return value


def sanitize_json(obj, parent_key: str = ""):
    """Recursively sanitize PII from a JSON object."""
    if isinstance(obj, dict):
        return {k: sanitize_json(sanitize_value(k, v), k) for k, v in obj.items()}
    if isinstance(obj, list):
        return [sanitize_json(item, parent_key) for item in obj]
    # Also sanitize top-level string values that match account ID patterns
    if isinstance(obj, str) and ACCOUNT_ID_PATTERN.search(obj):
        return ACCOUNT_ID_PATTERN.sub(SANITIZED_ACCOUNT_ID, obj)
    return obj


def process_recording(recording_path: Path) -> tuple[Path | None, str]:
    """Process a single recording file. Returns (output_path, message)."""
    with open(recording_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    status_code = data.get("Response", {}).get("StatusCode", 0)
    if status_code != 200:
        return None, f"  SKIP (status {status_code}): {recording_path}"

    request = data["Request"]
    response = data["Response"]
    api_path = request["Path"]
    methods = request.get("Methods", ["GET"])
    method = methods[0] if methods else "GET"

    module = classify_module(api_path)
    slug = path_to_slug(api_path)
    filename = f"{method}-{slug}.json"

    # Build fixture
    fixture = {
        "Request": {
            "Path": api_path,
            "Methods": methods,
        },
        "Response": {
            "StatusCode": response["StatusCode"],
            "Headers": response.get("Headers", {}),
            "Body": sanitize_json(response.get("Body")),
        },
    }

    # Keep request body for POST/PUT
    if method in ("POST", "PUT") and request.get("Body") is not None:
        fixture["Request"]["Body"] = sanitize_json(request["Body"])

    output_dir = FIXTURES_DIR / module
    output_dir.mkdir(parents=True, exist_ok=True)
    output_path = output_dir / filename

    with open(output_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(fixture, f, indent=2, ensure_ascii=False)
        f.write("\n")

    return output_path, f"  {recording_path.name} -> {module}/{filename}"


def main():
    if not RECORDINGS_DIR.exists():
        print(f"ERROR: Recordings directory not found: {RECORDINGS_DIR}")
        sys.exit(1)

    recording_files = sorted(RECORDINGS_DIR.rglob("*.json"))
    if not recording_files:
        print("No recording files found.")
        sys.exit(0)

    print(f"Found {len(recording_files)} recording(s) in {RECORDINGS_DIR}\n")

    generated = 0
    skipped = 0

    for rec in recording_files:
        output_path, message = process_recording(rec)
        print(message)
        if output_path:
            generated += 1
        else:
            skipped += 1

    print(f"\nSummary: {generated} fixture(s) generated, {skipped} skipped.")
    print(f"Output: {FIXTURES_DIR}")


if __name__ == "__main__":
    main()
