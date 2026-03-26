# Security Policy

## Reporting a Vulnerability

IbkrConduit handles financial API credentials (OAuth tokens, RSA private keys, Flex tokens). We take security seriously.

### Preferred: GitHub Private Vulnerability Reporting

Please use [GitHub's private vulnerability reporting](https://github.com/cquillen/ibkr-conduit/security/advisories/new) to report security issues. This ensures the report is visible only to maintainers.

### Fallback: Email

If you cannot use GitHub's reporting, email [craig@thequillens.com](mailto:craig@thequillens.com) with details.

## Response Timeline

- **Acknowledge:** Within 48 hours
- **Assessment:** Within 7 days
- **Resolution target:** Within 30 days for confirmed vulnerabilities

## Scope

**In scope:**

- Credential handling vulnerabilities (token leakage, key material exposure)
- Authentication/signing implementation flaws
- Session management issues
- Log sanitization failures (credentials appearing in logs)
- Dependency vulnerabilities that affect IbkrConduit's security

**Out of scope:**

- Interactive Brokers' own API security — report to [IBKR directly](https://www.interactivebrokers.com/en/index.php?f=ibgStrength&p=report)
- Vulnerabilities in consuming applications
- Trading logic or financial risk concerns

## Disclosure

We follow coordinated disclosure. Please do not file security issues as public GitHub issues.
