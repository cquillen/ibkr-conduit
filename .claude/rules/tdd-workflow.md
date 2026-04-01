---
description: Test-driven development workflow for all code changes
---

- Follow the Red-Green-Refactor cycle for all new code and bug fixes:
  1. **Red:** Write a failing test that defines the expected behavior before writing any implementation code
  2. **Green:** Write the minimum implementation code needed to make the test pass
  3. **Refactor:** Clean up the implementation while keeping tests green
- Never write implementation code without a corresponding failing test first
- Each cycle should be small and focused — one behavior per iteration
- Run tests after each step to confirm the expected state (failing, then passing, then still passing)
- When fixing a bug, first write a test that reproduces the bug, then fix the implementation
- Do not skip the refactor step — use it to eliminate duplication and improve clarity
- Tests drive the public API design — if something is hard to test, reconsider the design
- This applies to autonomous/subagent work — TDD is not optional when working without human oversight
- Implementation plans must include explicit TDD steps (write failing test, run to verify failure, implement, run to verify pass) for every task
- Commits should reflect TDD ordering — test commits before or alongside implementation commits, never implementation-only commits without corresponding tests
