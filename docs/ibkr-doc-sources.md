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
**Overlaps:** DOC-01/DOC-02 — endpoint coverage overlaps but DOC-03 is broader (per operator, new docs are incomplete relative to this); DOC-04 — DOC-04's Introduction/Auth/Getting-Started prose is a condensed subset of what DOC-03 covers in far more depth; DOC-05 — DOC-03's Endpoints h3 subsections (Orders, Market Data, Portfolio, FYIs/Alerts) cover the same ground DOC-05 rewrites as the trading-focused "new docs" version — DOC-05 is the current curated source, DOC-03 the older exhaustive one; DOC-07 — DOC-03's top-level Flex Web Service h2 section is the older, more exhaustive counterpart to DOC-07's focused single-topic rewrite

#### DOC-04 — Web API (top-level landing page)
**URL:** https://www.interactivebrokers.com/campus/ibkr-api-page/web-api/
**Kind:** html · **Fetch:** curl-ok (needs desktop-browser User-Agent; WebFetch gets 403 from the IBKR Campus WAF) · **Size:** ~391KB
**Registered:** 2026-07-07 · **Last verified:** 2026-07-07
**Covers:** high-level orientation only — Connectivity, Authentication (conceptual), Data Transmission, References, Getting Started (Retail vs Institutional/Third Party), Feedback. No per-endpoint request/response detail.
**Authority notes:** Operator: "top level page of the 'new' api documentation. has some info about auth and general ops but not really api endpoint details." Use for orientation/getting-started framing only; route to DOC-01/DOC-02 for endpoint contracts and DOC-03 for narrative depth.
**Structure:** single HTML page. `<title>Web API | IBKR API | IBKR Campus</title>`. Top-level `h2.section-title-1` anchors: Introduction (`#introduction-0`, with `h3` subsections Connectivity `#connectivity-0`, Authentication `#authentication-1`, Data Transmission `#data-transmission-2`), References (`#references-1`), Getting Started (`#getting-started-2`, with Retail `#retail-3` and Institutional or Third Party `#institutional-or-third-party-4`), Feedback (`#feedback-3`). No self-describing version field found.
**Overlaps:** DOC-03 — this page's Introduction/Authentication/Getting-Started sections are a shorter, higher-level version of material DOC-03 covers in depth; DOC-05 — DOC-05 embeds this exact page's Introduction/References/Getting-Started/Feedback nav verbatim (shared landing-page component) before adding trading-specific body content this page lacks; no overlap with DOC-01/DOC-02's endpoint-level content

#### DOC-05 — Trading Web API (new docs, trading slice)
**URL:** https://www.interactivebrokers.com/campus/ibkr-api-page/web-api-trading/
**Kind:** html · **Fetch:** curl-ok (needs desktop-browser User-Agent; WebFetch gets 403 from the IBKR Campus WAF) · **Size:** ~495KB
**Registered:** 2026-07-07 · **Last verified:** 2026-07-07
**Covers:** OAuth 1.0a/2.0 cookie management, Sessions in the Web API (status indicators), Cookie Management, Instrument Discovery, Market Data (maximums, top-of-book snapshots, streaming), Orders (new order example, reply messages, reply suppression, rejections, previewing, modifying, canceling, bracket orders, combos/spreads, monitoring live orders, monitoring executions), Portfolio and Positions (accounts, currency balances, equity and margin), Advisor Features, FYIs/Alerts/Bulletins, Usage and Support (API support contact, scheduled server maintenance incl. weekday IServer reset timing, pacing limitations incl. per-endpoint rate limits)
**Authority notes:** Operator: "the 'new docs' for the trading api." This is the trading-focused slice of the "new docs" family (sibling of DOC-04's landing page) — organized by feature area (Market Data, Orders, Portfolio, FYIs) rather than per-endpoint like DOC-03. Deeper than DOC-04 (which is orientation-only) but does not fully replace DOC-01/DOC-02's machine-readable request/response schemas or DOC-03's per-endpoint exhaustiveness — treat as the current curated prose/example reference, cross-check against DOC-01 for exact field shapes.
**Structure:** `<title>Trading Web API | IBKR API | IBKR Campus</title>`. Top-level `h2.section-title-1` nav duplicates DOC-04's landing sections verbatim (Introduction, References, Getting Started, Feedback) before the real body: Introduction, Feedback, Getting Started (Web API Access for Organizations/Individuals/Third Parties), Usage and Support, Sessions in the Web API, Cookie Management, Instrument Discovery, Market Data, Orders, Portfolio and Positions, Advisor Features, FYIs/Alerts/Bulletins — 34 `h3.section-title-2` subsections plus several `h4` sub-subsections (e.g. "Weekday IServer Reset Timing", "Per-Endpoint Request Rate Limits", "Values needed:" blocks under Orders). No self-describing version field found.
**Overlaps:** DOC-04 — shares its Introduction/References/Getting-Started/Feedback nav verbatim (same shared landing component), DOC-05 adds trading-specific body content; DOC-03 — DOC-03's Endpoints h3 subsections (Orders, Market Data, Portfolio, FYIs/Alerts) cover the same ground in the older exhaustive per-endpoint narrative, DOC-05 is the current curated rewrite; DOC-01/DOC-02 — same REST surface at the request/response schema level, DOC-05 is prose/example-oriented while DOC-01 is the machine contract
