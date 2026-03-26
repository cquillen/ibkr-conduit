---
description: Git workflow and commit message conventions
---

- Use Conventional Commits format for all commit messages:
  - `feat:` — new feature
  - `fix:` — bug fix
  - `docs:` — documentation only
  - `chore:` — maintenance, dependencies, tooling
  - `refactor:` — code change that neither fixes nor adds
  - `test:` — adding or updating tests
  - `ci:` — CI/CD pipeline changes
- Follow GitHub Flow: feature branches off `main`, merge via pull request
- CI must pass before merge — build, lint, and tests are required status checks
- Never commit secrets, `.pem` files, private keys, tokens, or credentials
- Never commit `bin/`, `obj/`, or build artifacts
