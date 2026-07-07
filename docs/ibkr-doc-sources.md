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
**Overlaps:** DOC-02 (Redoc shell whose `spec-url` points at this exact URL — DOC-02 is a rendering of this document, not independent content); DOC-03 (older, broader narrative HTML doc covering the same endpoint families plus material this OpenAPI doc omits — operator notes new docs are incomplete, so DOC-03 fills gaps here)

#### DOC-02 — Web API Reference (Redoc rendering)
**URL:** https://www.interactivebrokers.com/campus/ibkr-api-page/webapi-ref/
**Kind:** html · **Fetch:** curl-ok (needs desktop-browser User-Agent; WebFetch gets 403 from the IBKR Campus WAF) · **Size:** ~371KB (HTML shell only — actual spec content is fetched client-side from the `spec-url`)
**Registered:** 2026-07-07 · **Last verified:** 2026-07-07
**Covers:** same surface as DOC-01 (it renders DOC-01) — full REST endpoint reference, once resolved through the shell
**Authority notes:** Operator: "looks like an openapi rendering." Confirmed: this is a Redoc single-page shell, `<title>Web API Reference | IBKR API | IBKR Campus</title>`, containing `spec-url='https://api.ibkr.com/gw/api/v3/api-docs'`. Do not fetch this URL for content — fetch DOC-01 directly instead; this entry exists so a scout that lands here (e.g. via search/link) knows to redirect to DOC-01 rather than treating the shell as needs-browser.
**Structure:** JS-rendered Redoc shell around the DOC-01 OpenAPI document; no independent structure of its own. No self-describing version beyond what DOC-01 carries.
**Overlaps:** DOC-01 — full overlap, DOC-02 is strictly a rendering of DOC-01's data; DOC-01 is the source of truth, fetch it instead of this shell

#### DOC-03 — Web API v1.0 Documentation (Client Portal API, narrative)
**URL:** https://www.interactivebrokers.com/campus/ibkr-api-page/cpapi-v1/
**Kind:** html · **Fetch:** curl-ok (needs desktop-browser User-Agent; WebFetch gets 403 from the IBKR Campus WAF) · **Size:** ~1.2MB
**Registered:** 2026-07-07 · **Last verified:** 2026-07-07
**Covers:** broad narrative coverage — Client Portal Gateway setup, Authentication (session, brokerage sessions, multi-session, paper accounts, FAQ), Pacing Limitations, Regular Server Maintenance, Endpoints (Alerts, Accounts, Contract/secdef, and many more endpoint families — 246 `h3` subsections total under 11 top-level `h2` sections), Websockets, OAuth 1.0a, Flex Web Service
**Authority notes:** Operator: "old and massive v1 documentation, but still relevant due to new docs being incomplete and the actual API is not version separated afaik." Treat as the deepest narrative source; use to fill gaps left by DOC-01/DOC-02, especially for OAuth, Websockets, Flex, and session-lifecycle prose that the OpenAPI doc doesn't capture.
**Structure:** single large HTML page (not a tree — one page, deep-linked via anchors). `<title>Web API v1.0 Documentation | IBKR API | IBKR Campus</title>` — "v1.0" is the only version marker found (no machine-readable version field). Top-level `h2.section-title-1` anchors: Introduction, Requirements & Limitations, WebAPI Basics Tutorial, Client Portal Gateway, Authentication, Pacing Limitations, Regular Server Maintenance, Endpoints, Websockets, OAuth 1.0a, Flex Web Service. Endpoint detail lives under `h3.section-title-2` (246 of them, e.g. "Alerts", "Accounts", "Contract", "Search the security definition by Contract ID").
**Overlaps:** DOC-01/DOC-02 — endpoint coverage overlaps but DOC-03 is broader (per operator, new docs are incomplete relative to this); DOC-04 — DOC-04's Introduction/Auth/Getting-Started prose is a condensed subset of what DOC-03 covers in far more depth

#### DOC-04 — Web API (top-level landing page)
**URL:** https://www.interactivebrokers.com/campus/ibkr-api-page/web-api/
**Kind:** html · **Fetch:** curl-ok (needs desktop-browser User-Agent; WebFetch gets 403 from the IBKR Campus WAF) · **Size:** ~391KB
**Registered:** 2026-07-07 · **Last verified:** 2026-07-07
**Covers:** high-level orientation only — Connectivity, Authentication (conceptual), Data Transmission, References, Getting Started (Retail vs Institutional/Third Party), Feedback. No per-endpoint request/response detail.
**Authority notes:** Operator: "top level page of the 'new' api documentation. has some info about auth and general ops but not really api endpoint details." Use for orientation/getting-started framing only; route to DOC-01/DOC-02 for endpoint contracts and DOC-03 for narrative depth.
**Structure:** single HTML page. `<title>Web API | IBKR API | IBKR Campus</title>`. Top-level `h2.section-title-1` anchors: Introduction (`#introduction-0`, with `h3` subsections Connectivity `#connectivity-0`, Authentication `#authentication-1`, Data Transmission `#data-transmission-2`), References (`#references-1`), Getting Started (`#getting-started-2`, with Retail `#retail-3` and Institutional or Third Party `#institutional-or-third-party-4`), Feedback (`#feedback-3`). No self-describing version field found.
**Overlaps:** DOC-03 — this page's Introduction/Authentication/Getting-Started sections are a shorter, higher-level version of material DOC-03 covers in depth; no overlap with DOC-01/DOC-02's endpoint-level content
