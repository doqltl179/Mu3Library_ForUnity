---
owns: "What verification each kind of change needs, how evidence is reported, flaky tests and disabled checks, and what to do when verification cannot run"
volatility: evolving
reviewed: 2026-10-09
---

# Verification

Commands come from `commands.*` in the profile (shown in `AGENTS.md` «This Project»). Stack packs add stack-specific checks in their `Verify` sections.

## When

- before reporting any change as done, and before handing it to review.

## Route Away When

- judging whether evidence is sufficient: [review.md](review.md),
- the integrity rules behind this page: [integrity.md](../principles/integrity.md).

## Verification By Change Type

| Change | Minimum verification |
|---|---|
| Code logic | `commands.typecheck` (if set), `commands.lint`, and the tests covering the changed code via `commands.test` |
| Bug fix | A regression test that fails before the fix and passes after it |
| Public interface or data format | Tests of every consumer in the repository, plus a compatibility note |
| UI | Component or end-to-end tests where they exist, plus a manual description of states checked |
| Build, CI, or tooling config | Run the affected command or pipeline step locally where possible |
| Schema or data migration | Forward and rollback on a copy |
| Docs only | Links resolve and examples match real signatures; `agentkit.py check` for agent docs |
| Performance | Before and after measurements under the same conditions |

Narrow test runs to the changed area first, then run the broader suite when the change crosses module boundaries.

## Evidence Format

For each check: the exact command, the result (pass or fail, counts), and for failures the first relevant error. Never paraphrase a failing run as passing, and never report a check you did not run in this task.

## Flaky Tests And Disabled Checks

- Call a test flaky only with evidence: it both passed and failed on the same commit with no code change.
- Never disable, skip, or loosen a check to get a green run. When a check must be disabled, that needs the user's approval, a tracking issue, and a note in the pull request.

## When Verification Cannot Run

- If a command is not configured (empty `commands.*`) or cannot run here, say so explicitly, perform the strongest static check available (type check, targeted reading of call sites), and report the residual risk.
- Do not substitute a different tool that checks a different input (for example a plain compiler run when the project's real build differs) and present it as equivalent.
