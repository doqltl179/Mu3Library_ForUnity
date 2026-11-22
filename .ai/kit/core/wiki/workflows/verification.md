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
| UI | Component or end-to-end tests where they exist, plus each changed state reached in the running app (`commands.run` or `commands.e2e`) with a screenshot, recording, or captured output of it; when the app cannot run here, the states checked and how, per «When Verification Cannot Run» |
| Build, CI, or tooling config | Run the affected command or pipeline step locally where possible |
| Schema or data migration | Forward and rollback on a copy |
| Docs only | Links resolve and examples match real signatures; `agentkit.py check` for agent docs |
| Performance | Before and after measurements under the same conditions |
| Any of the above that must hold an escape sequence as literal text | Also the bytes that landed, per «Escape Sequences As Literal Text» |

Narrow test runs to the changed area first, then run the broader suite when the change crosses module boundaries. When the profile defines named commands for several surfaces (for example an engine compile and tool tests), run the ones listed in the «Project Binding» of the role that owns the touched paths.

## Escape Sequences As Literal Text

When a change must contain an escape sequence as literal text (for example a Unicode escape in a string literal, JSON, a regular expression, or a doc example), verify the bytes that landed, not what you typed. The path from an agent to its tools may decode typed escape text into the character itself, in file edits and shell commands alike, and a typed search needle is decoded the same way, so a plain text search finds the decoded character and reports success even when the file holds the character instead of the escape. Build the escape text and each search needle from code points in a script (for example the backslash as `chr(92)` in Python) and write the file from that same script, count the needle's hits against the escapes written, and confirm that lines meant to be ASCII hold only bytes 32–126 plus tab, LF, and CR.

## Evidence Format

For each check: the exact command, the result (pass or fail, counts), and for failures the first relevant error. For a UI state checked in the running app, also the following, because its files last only as long as the work folder and only on the machine that made them:

- The steps actually taken to reach the state, starting from the term of its screen's feature-map entry («Project Wiki» in [project-overlay.md](../integration/project-overlay.md)) when the project has one, and what the state showed, written so it stands without the files, in the report and in the pull request body (the merge commit body with `hosting.platform = "none"`), which keep it after the files are gone.
- The artifact itself (a screenshot, recording, or captured output), saved as a file in a work folder per «Work Folders» in [git-workflow.md](git-workflow.md) and named by its absolute path, so a reviewer on that machine judges the behavior from it instead of rebuilding it from the diff. Work with no task branch deletes its folder before its report, so that report names no path.
- With a hosting platform (`hosting.platform` is not `"none"`), what lets its reviewers see each file: the link to the CI run when the project's CI runs the same check and publishes its artifacts; otherwise the file, listed by its absolute path in the report for the user to attach before the merge, since cleanup deletes the work folder after it. Never commit the file: a work folder is never staged («Staging» in git-workflow.md).

Never paraphrase a failing run as passing, and never report a check you did not run in this task. With `hosting.ci = false`, the pull request body is the only record of verification: it lists every command run on the final commit and its result.

## Flaky Tests And Disabled Checks

- Call a test flaky only with evidence: it both passed and failed on the same commit with no code change.
- Never disable, skip, or loosen a check to get a green run. When a check must be disabled, that needs the user's approval, a tracking issue, and a note in the pull request.

## When Verification Cannot Run

- If a command is not configured (empty `commands.*`) or cannot run here, say so explicitly, perform the strongest static check available (type check, targeted reading of call sites), and report the residual risk.
- When no test level can reproduce a bug (it needs an interaction the test setup cannot build), the bug-fix row of «Verification By Change Type» cannot be met: say so, show the original reproduction failing before the fix and passing after it, and register the missing test seam as a follow-up.
- Do not substitute a different tool that checks a different input (for example a plain compiler run when the project's real build differs) and present it as equivalent.
