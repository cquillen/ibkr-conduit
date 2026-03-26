# IbkrConduit

[![CI](https://github.com/cquillen/ibkr-conduit/actions/workflows/ci.yml/badge.svg)](https://github.com/cquillen/ibkr-conduit/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/IbkrConduit)](https://www.nuget.org/packages/IbkrConduit)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A C#/.NET client library for the Interactive Brokers Client Portal Web API (CPAPI 1.0) with OAuth 1.0a authentication, multi-tenant session management, rate limiting, and Flex Web Service integration.

## Disclaimer

**IbkrConduit is an independent, community-developed open source library. It is not affiliated with, endorsed by, or supported by Interactive Brokers LLC or any of its subsidiaries.** Interactive Brokers, IBKR, and related marks are trademarks of Interactive Brokers LLC.

Financial trading involves substantial risk of loss. IbkrConduit is provided as infrastructure software only — it does not provide investment advice and is not responsible for trading decisions or financial outcomes. Use at your own risk. Always test thoroughly with a paper trading account before connecting to a live account.

## Features

- OAuth 1.0a authentication (fully headless, no browser/Selenium required)
- Multi-tenant session management — multiple IBKR accounts in a single process
- Automatic session lifecycle (token refresh, tickle, brokerage session init)
- Rate limiting with adaptive 429 response handling
- Order submission with automatic question/reply flow handling
- Portfolio and position data retrieval
- Market data snapshots and history
- WebSocket streaming (orders, market data, P&L)
- Flex Web Service integration (Trade Confirmations, Activity Statements)
- Storage-agnostic credential handling

## Quick Start

```bash
dotnet add package IbkrConduit
```

Full usage documentation and DI registration examples coming soon. See the [design document](docs/ibkr_conduit_design.md) for architectural details.

## IbkrConduit vs IBKR.Sdk.Client

| Aspect | IbkrConduit | IBKR.Sdk.Client |
|---|---|---|
| API target | Web API 1.0 (CPAPI 1.0) | Web API 2.0 (newer, beta) |
| Auth method | OAuth 1.0a (first-party self-service) | OAuth 2.0 (private_key_jwt) |
| Status | Targets stable, fully documented API | Targets API still in beta |
| Multi-tenant | First-class design consideration | Not specifically documented |
| Flex Web Service | Included | Not included |
| Open source | MIT licensed, community driven | Unclear governance |

## Documentation

See the [design document](docs/ibkr_conduit_design.md) for detailed architecture, API behaviors, and implementation guidance.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, code style, and contribution guidelines.

## Security

See [SECURITY.md](SECURITY.md) for vulnerability reporting.

## License

MIT License. See [LICENSE](LICENSE) for details.
