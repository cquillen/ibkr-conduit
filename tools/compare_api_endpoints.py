#!/usr/bin/env python3
"""Compare endpoint URLs between ibkr-web-api-spec.md and ibkr-web-api-openapi.json."""

import json
import re
import sys
from pathlib import Path


def extract_from_markdown(path: str) -> dict[str, set[str]]:
    """Extract method+url pairs from the markdown spec.

    Looks for lines matching: - **Method:** `GET` followed by - **URL:** `/path`
    """
    endpoints: dict[str, set[str]] = {}
    method = None

    with open(path) as f:
        for line in f:
            line = line.strip()

            # Match: - **Method:** `GET` | `POST` | etc
            m = re.match(r'^- \*\*Method:\*\* `(\w+)`', line)
            if m:
                method = m.group(1).upper()
                continue

            # Match: - **URL:** `/some/path/{param}`
            m = re.match(r'^- \*\*URL:\*\* `([^`]+)`', line)
            if m and method:
                url = m.group(1)
                # Normalize: strip leading slash variations, ensure leading /
                url = '/' + url.lstrip('/')
                key = f"{method} {url}"
                endpoints.setdefault(method, set()).add(url)
                method = None

    return endpoints


def extract_from_openapi(path: str) -> dict[str, set[str]]:
    """Extract method+url pairs from the OpenAPI JSON spec."""
    with open(path) as f:
        spec = json.load(f)

    endpoints: dict[str, set[str]] = {}

    for url_path, methods in spec.get("paths", {}).items():
        for method in methods:
            if method.lower() in ("get", "post", "put", "delete", "patch"):
                m = method.upper()
                endpoints.setdefault(m, set()).add(url_path)

    return endpoints


def normalize_url(url: str) -> str:
    """Normalize URL for comparison by replacing {param} variations."""
    # Replace all {paramName} with a generic {param} for comparison
    return re.sub(r'\{[^}]+\}', '{param}', url)


def build_flat_sets(endpoints: dict[str, set[str]]) -> set[str]:
    """Build a flat set of 'METHOD /normalized/url' strings."""
    result = set()
    for method, urls in endpoints.items():
        for url in urls:
            result.add(f"{method} {normalize_url(url)}")
    return result


def build_flat_sets_raw(endpoints: dict[str, set[str]]) -> dict[str, str]:
    """Build a dict mapping normalized key to raw url for display."""
    result = {}
    for method, urls in endpoints.items():
        for url in urls:
            key = f"{method} {normalize_url(url)}"
            result[key] = f"{method} {url}"
    return result


def main():
    docs_dir = Path(__file__).parent.parent / "docs"
    md_path = docs_dir / "ibkr-web-api-spec.md"
    openapi_path = docs_dir / "ibkr-web-api-openapi.json"

    if not md_path.exists():
        print(f"ERROR: {md_path} not found")
        sys.exit(1)
    if not openapi_path.exists():
        print(f"ERROR: {openapi_path} not found")
        sys.exit(1)

    # Extract
    md_endpoints = extract_from_markdown(str(md_path))
    oa_endpoints = extract_from_openapi(str(openapi_path))

    # Build flat sets for comparison
    md_flat = build_flat_sets(md_endpoints)
    oa_flat = build_flat_sets(oa_endpoints)

    # Raw display maps
    md_raw = build_flat_sets_raw(md_endpoints)
    oa_raw = build_flat_sets_raw(oa_endpoints)

    # Compute differences
    only_in_md = sorted(md_flat - oa_flat)
    only_in_oa = sorted(oa_flat - md_flat)
    in_both = sorted(md_flat & oa_flat)

    # Count totals
    md_total = len(md_flat)
    oa_total = len(oa_flat)

    # Print report
    print("=" * 70)
    print("IBKR API Endpoint Comparison Report")
    print("=" * 70)
    print(f"\nMarkdown spec endpoints:  {md_total}")
    print(f"OpenAPI spec endpoints:   {oa_total}")
    print(f"In both (matched):        {len(in_both)}")
    print(f"Only in Markdown:         {len(only_in_md)}")
    print(f"Only in OpenAPI:          {len(only_in_oa)}")

    print(f"\n{'=' * 70}")
    print(f"MATCHED ENDPOINTS ({len(in_both)})")
    print(f"{'=' * 70}")
    for key in in_both:
        md_display = md_raw.get(key, key)
        oa_display = oa_raw.get(key, key)
        if md_display == oa_display:
            print(f"  {md_display}")
        else:
            print(f"  MD: {md_display}")
            print(f"  OA: {oa_display}")
            print()

    print(f"\n{'=' * 70}")
    print(f"ONLY IN MARKDOWN SPEC ({len(only_in_md)})")
    print(f"{'=' * 70}")
    if only_in_md:
        for key in only_in_md:
            print(f"  {md_raw.get(key, key)}")
    else:
        print("  (none)")

    print(f"\n{'=' * 70}")
    print(f"ONLY IN OPENAPI SPEC ({len(only_in_oa)})")
    print(f"{'=' * 70}")
    if only_in_oa:
        # Group by path prefix for readability
        for key in only_in_oa:
            print(f"  {oa_raw.get(key, key)}")
    else:
        print("  (none)")

    # Summary by method
    print(f"\n{'=' * 70}")
    print("SUMMARY BY HTTP METHOD")
    print(f"{'=' * 70}")
    all_methods = sorted(set(list(md_endpoints.keys()) + list(oa_endpoints.keys())))
    print(f"  {'Method':<8} {'Markdown':>10} {'OpenAPI':>10} {'Matched':>10}")
    print(f"  {'-'*8} {'-'*10} {'-'*10} {'-'*10}")
    for method in all_methods:
        md_count = len(md_endpoints.get(method, set()))
        oa_count = len(oa_endpoints.get(method, set()))
        md_norm = {normalize_url(u) for u in md_endpoints.get(method, set())}
        oa_norm = {normalize_url(u) for u in oa_endpoints.get(method, set())}
        matched = len(md_norm & oa_norm)
        print(f"  {method:<8} {md_count:>10} {oa_count:>10} {matched:>10}")


if __name__ == "__main__":
    main()
