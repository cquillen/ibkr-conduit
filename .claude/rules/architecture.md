---
description: Architectural principles for IbkrConduit implementation
globs: "src/**"
---

- Multi-tenant by design — no global or static mutable state. Each tenant has independent session, rate limiters, and credentials.
- Storage-agnostic — credentials are accepted as pre-loaded objects (`RSA`, strings), not file paths or cloud-specific constructs
- All cryptographic operations use `System.Security.Cryptography` only — no external crypto libraries
- Refer to `docs/ibkr_conduit_design.md` for detailed design decisions, API behaviors, and implementation guidance
- The library handles IBKR API quirks (session lifecycle, question/reply flow, rate limits) so consumers don't have to
