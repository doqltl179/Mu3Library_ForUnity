---
name: code-review-perform
description: "Review exact commits or an exact diff against its stated intent for correctness, regressions, compatibility, maintainability, test adequacy, and doc sync, then report findings and an approve or rework disposition. Use when a unit is implemented and verified and needs the code review gate, or the user asks for a review of a pull request, branch, or commit range."
category: review
volatility: evolving
reviewed: 2026-10-09
---

# Perform Code Review

## Use When
- A unit is committed and verified, and its plan or the orchestrator assigns the code review gate.
- The user asks for a review of a pull request, branch, or commit range.

## Do Not Use When
- The change crosses a trust boundary → also run `security-review-perform`; this skill does not cover the security lens.
- The change alters a user interface → also run `accessibility-review-perform`.
- Fixing the findings → the unit's execution owner. Answering review comments received on a pull request → `github-pr-review-respond`.

## Inputs
- The commit range or pull request to review, and its base.
- The intent: linked issue, plan file, or spec, with acceptance criteria.
- The author's verification evidence.

## Steps
1. Apply «Review Protocol» in [review.md](../../wiki/workflows/review.md) throughout.
2. Pin the target to exact commit IDs: `git rev-parse <base> <head>` and `git log --oneline <base>..<head>`. For a pull request, read the head SHA from `gh pr view <number> --json headRefOid,baseRefName`. Record the IDs; never review a branch name that can move.
3. Read the intent and acceptance criteria before the diff, so every finding is judged against what was asked.
4. Read the diff: `git diff --stat <base>...<head>`, then each file in full. Open callers and contracts of any changed function whose use is not visible in the diff.
5. Name the riskiest claim (the change most likely to be wrong and costly) and run the cheapest check that would expose it, choosing from `commands.test`, `commands.typecheck`, `commands.lint`, or `commands.build`. Record it per «Evidence Format» in [verification.md](../../wiki/workflows/verification.md); if it cannot run, follow «When Verification Cannot Run» on the same page.
6. Walk «Checklist» below. Record each finding with file, line, the concrete failure, and its level per «Severity» in review.md.
7. Write the report per «Output Format» and choose the disposition per «Dispositions», both in review.md.
8. Post to the pull request only when the user or the dispatch packet asks: write the report to a temporary file, then `gh pr review <number> --request-changes|--approve|--comment --body-file <file>`.

## Checklist
- Correctness: each acceptance criterion is met; empty, null, boundary, concurrent, and error paths behave as intended.
- Regression: behavior outside the intent is unchanged; removed or renamed symbols have no remaining users (`rg` the name).
- Compatibility: changes to public APIs, CLI flags, config keys, file formats, persisted data, and wire protocols meet «Compatibility» in [code-changes.md](../../wiki/workflows/code-changes.md).
- Code-change rules: added or upgraded dependencies, migrations, and generated files meet «Dependencies», «Schema And Data Migrations», and «Generated Code» in code-changes.md.
- Maintainability: the change fits existing patterns; names state what things do; no duplicated logic, dead code, or edits unrelated to the intent.
- Tests: new behavior and fixed bugs have tests that fail without the change; no skipped tests, loosened assertions, or mocked-away failures per «Never Fake Success» in [integrity.md](../../wiki/principles/integrity.md).
- Doc sync: owning docs, the changelog, and localized variants changed with the behavior per «Doc Sync Rule» in [documentation.md](../../wiki/workflows/documentation.md).
- Evidence: every verification claim by the author is backed by a quoted command and result from this change.

## Output
- The report per «Output Format», naming the reviewed commit IDs and the disposition.
- The pull request review URL, when posted.
