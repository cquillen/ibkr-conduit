# IBKR live documentation source registry

Live-first doc authority for this repo (see docs/superpowers/specs/2026-07-07-ibkr-doc-scouting-design.md).
Consumed by the scout-ibkr-docs skill; entries added via the register-ibkr-doc-source skill.
Doc content is IBKR's CLAIM — recordings/ and attended probes remain the only verification tier.

#### DOC-01 — OpenAPI JSON (api.ibkr.com gateway)
**URL:** https://api.ibkr.com/gw/api/v3/api-docs
**Kind:** openapi-json · **Fetch:** curl-ok · **Size:** ~864KB
**Registered:** 2026-07-07 · **Last verified:** 2026-07-07
**Covers:** full REST endpoint surface (168 paths) — accounts, orders, contracts/secdef, market data snapshots, alerts, watchlists, scanner, PnL, FYI, Flex-adjacent endpoints — plus 453 request/response schemas
**Authority notes:** Operator: "one would hope it's authoritative, but it is NOT — it has gaps; treat as a peer source, never the sole authority."
**Structure:** single machine-readable OpenAPI 3.0.0 document, `info.title` = "IB REST API", `info.version` = 2.35.0 (self-describing version field — free drift detector: re-check this on each re-verify). Not a shell; served directly as JSON with no browser rendering needed. Reachable with plain `curl` (no special User-Agent required — this host is not behind the IBKR Campus WAF that blocks WebFetch).
**Overlaps:** none registered yet
