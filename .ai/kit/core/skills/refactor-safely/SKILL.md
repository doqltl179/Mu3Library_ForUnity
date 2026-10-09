---
name: refactor-safely
description: "Restructure code without changing its behavior: pin current behavior with characterization tests, apply small mechanical steps that are each verified and committed, and keep every behavior change out of the refactor. Use when code must be renamed, extracted, moved, split, inlined, or simplified while its observable behavior stays identical."
category: code-change
volatility: evolving
reviewed: 2026-10-09
---

# Refactor Safely

## Use When
- Code must be renamed, extracted, moved, split, inlined, or simplified, and its observable behavior must stay identical.
- A planned change becomes simpler after restructuring first.

## Do Not Use When
- The behavior is wrong → `bug-diagnose`.
- Call sites move from one API, framework, or pattern to another → `code-migration`.
- The goal is speed or memory → `performance-investigate`.

## Inputs
- The code area and the structural goal.
- The observable behavior that must not change: public interfaces, outputs, side effects, errors.
- `commands.test`, `commands.typecheck`, and `commands.lint` from the profile.

## Steps
1. Run `commands.test` before touching code. If it fails, stop and report; a refactor needs a passing baseline.
2. Read the tests that cover the area. Where observable behavior is untested, add characterization tests with `test-add` that assert what the code does now, odd results included. Commit them alone with `git-commit`.
3. Split the refactor into small steps, each one mechanical transformation (for example: extract a function, then move it, then rename it). A step that renames, moves, or removes a public interface follows «Compatibility» in [code-changes.md](../../wiki/workflows/code-changes.md); generated output changes only through its source per «Generated Code» there.
4. For each step: apply it, run `commands.typecheck`, `commands.lint`, and `commands.test` (report any that are empty), then commit with `git-commit`. On failure, undo this step's edits and split it smaller.
5. Change no behavior. When you find a bug or want a behavior change, keep the current behavior and record it for a separate unit: `bug-diagnose` after the refactor, or `github-issue-create` per «Leftovers Become Issues» in [issues-and-prs.md](../../wiki/workflows/issues-and-prs.md).
6. Review the whole diff: `git diff <integration_branch>...HEAD`. Confirm no existing test assertion changed; a changed assertion means a behavior change.
7. Run the final checks per «Verification By Change Type» in [verification.md](../../wiki/workflows/verification.md).
8. Update docs that name moved or renamed code per «Doc Sync Rule» in [documentation.md](../../wiki/workflows/documentation.md).

## Output
- The commits in order: characterization tests first, then one per step.
- Verification evidence per «Evidence Format».
- Behavior issues found and left unchanged, with where each was recorded.
