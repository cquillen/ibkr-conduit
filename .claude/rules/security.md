---
description: Security rules for handling financial API credentials
---

- Never include credentials, tokens, private keys, or key material in code or test fixtures
- Never commit `.pem`, `.pfx`, or `.key` files — the `.gitignore` blocks these but remain vigilant
- Sanitize all log output — ensure no credential material appears in logs
- The financial disclaimer must appear in README.md and the NuGet package description
- Test fixtures must use synthetic/fake credential values that are clearly not real
