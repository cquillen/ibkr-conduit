---
description: Rules for Bash tool usage to avoid permission prompt friction
alwaysApply: true
---

- Never chain commands with `&&`, `||`, or `;` — run each command as a separate Bash tool call so each matches the allowed permission patterns
- Use parallel tool calls for independent commands (e.g., `dotnet build` and `dotnet test` as two separate calls)
- Use sequential tool calls only when one command's output is needed by the next
- Avoid `cd` — use absolute paths in all commands instead
