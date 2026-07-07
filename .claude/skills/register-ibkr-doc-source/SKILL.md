---
name: register-ibkr-doc-source
description: Use when the operator provides a new live IBKR documentation web endpoint (URL + description) to add to the doc-source registry, or asks to re-verify/update an existing registry entry. Attended only.
---

# Register an IBKR doc source

Adds one live documentation endpoint to `docs/ibkr-doc-sources.md` — the registry the `scout-ibkr-docs` skill selects sources from. Registration **probes and characterizes** the source so scouts never rediscover how to fetch it.

**Scope guard:** registration touches ONLY the registry file, then commits. Do NOT edit CLAUDE.md, README.md, `.claude/rules/*`, or design docs as part of registering — pipeline wiring is one-time work that already exists, and per-source edits to shared files are churn.

## Procedure

1. **Probe the fetch path.** Try in order, recording what works:
   - `WebFetch` (expect 403 from IBKR Campus — that's the WAF, not the page being gone);
   - `curl -s -A "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36" <url>` with `-w "http=%{http_code} bytes=%{size_download}"`;
   - only if both fail meaningfully: browser tools (claude-in-chrome).
2. **Characterize the content** (from a saved temp copy; delete it after):
   - Kind: `html` narrative, `openapi-json`, or JS shell? A shell (e.g. Redoc: look for `spec-url=`) usually wraps an underlying data URL — record that URL in `Structure`; the scout should fetch it, not the shell. Consider asking the operator whether the underlying URL should be its own entry.
   - Structure: single page vs tree; heading/anchor scheme (map top-level sections); any self-describing version field (e.g. OpenAPI `info.version`) — record it, it's a free drift detector.
   - Size in KB/MB.
3. **Map coverage** into a short topic list a source-selection step can match against (endpoint families, WS topics, auth, Flex, error codes, …).
4. **Check overlaps:** which existing registry entries cover the same topics? Record the relationship and any known authority lean (e.g. "narrative is older but more complete than the OpenAPI"). The operator's description often carries the authority call — quote it.
5. **Append the entry** (create the registry file from the template below if missing). Fill EVERY field — a blank `Fetch` or `Structure` recreates the rediscovery cost the registry exists to kill.
6. **Commit:** `docs: register IBKR doc source <id>` (registry file only). Registering several sources in one session? One source per commit, in order — never batch entries into one commit.

## Conventions

- **IDs:** `DOC-NN`, zero-padded, next free number. Never re-use or re-order existing ids.
- **Overlaps are bidirectional:** when a new source overlaps an existing entry, update the existing entry's `Overlaps` line in the same commit. A commit may touch the new entry plus overlap-backfills — still registry file only. Never reference a source that isn't registered yet — that backfill belongs in the future source's own commit.
- **The browser User-Agent is for the Campus WAF** (`interactivebrokers.com/campus/...`, `ibkrcampus.com`). Other hosts (e.g. `api.ibkr.com`) may work with plain fetches — record whatever the probe actually proved, per source.

## Entry format (exact — scout-ibkr-docs parses these fields)

```markdown
#### <id> — <title>
**URL:** <live URL>
**Kind:** html | openapi-json · **Fetch:** curl-ok | webfetch-ok | needs-browser · **Size:** ~<n>
**Registered:** YYYY-MM-DD · **Last verified:** YYYY-MM-DD
**Covers:** <comma-separated topics>
**Authority notes:** <per-source caveats; quote the operator's description>
**Structure:** <single page vs tree; anchors; version field; underlying data URL if shell; quirks>
**Overlaps:** <other entry ids + relationship, or none>
```

Registry file header, when creating it fresh:

```markdown
# IBKR live documentation source registry

Live-first doc authority for this repo (see docs/superpowers/specs/2026-07-07-ibkr-doc-scouting-design.md).
Consumed by the scout-ibkr-docs skill; entries added via the register-ibkr-doc-source skill.
Doc content is IBKR's CLAIM — recordings/ and attended probes remain the only verification tier.
```

## Common mistakes

| Mistake | Reality |
|---|---|
| Editing CLAUDE.md/rules/README "while I'm here" | Registration = registry file + commit. Nothing else. |
| Treating a WebFetch 403 or empty JS shell as "page gone" | It's the WAF / client-side rendering. Follow step 1's fallback chain. |
| Registering a Redoc/SPA shell as `needs-browser` without looking inside | Shells usually name their data URL (`spec-url=`). Recording that URL makes the source `curl-ok` in practice. |
| Prose instead of the field lines | scout-ibkr-docs selects sources by walking `Covers`/`Overlaps`/`Fetch` mechanically. Keep the schema. |
| Skipping the live probe ("the operator described it, good enough") | The probe is the product: fetch strategy + structure is what scouts reuse every time. |
