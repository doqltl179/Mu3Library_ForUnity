---
name: bug-diagnose
description: "Find and fix the root cause of a defect: reproduce it, minimize the reproducer, locate the fault by bisecting or targeted logging, explain the cause-to-symptom chain, write a failing regression test, fix the cause rather than the symptom, and verify. Use when behavior is wrong, a crash or error is reported, or a test fails for an unknown reason."
category: code-change
volatility: evolving
reviewed: 2026-10-09
---

# Diagnose Bug

## Use When
- Observed behavior differs from expected behavior, or a crash or error is reported.
- A test fails and the cause is unknown.

## Do Not Use When
- A CI pipeline fails and the cause may be the pipeline or its environment → `ci-failure-triage`.
- The output is right but slow or heavy → `performance-investigate`.
- The defect may be a security vulnerability → also run `security-review-perform`.

## Inputs
- The symptom: expected and actual behavior, steps, environment, version or commit, frequency.
- Logs, stack traces, or failing test names.
- `commands.test`, `commands.run`, and a known-good commit if one exists.

## Steps
1. Reproduce the symptom with the reported steps, using `commands.run` or `commands.test`. Record the exact command and output. If it does not reproduce, compare environments and inputs; if it still does not, stop and report what was tried and what data would help.
2. Minimize: cut inputs and steps until removing anything more makes the symptom disappear. Turn the reproducer into an automated test when possible.
3. Locate the fault:
   - With a known-good commit: `git bisect start <bad> <good>`, `git bisect run <reproducer command>`, then `git bisect reset`.
   - Otherwise: read the stack trace, add temporary logging or assertions, and halve the suspect region until one fault point remains.
4. State the root cause as a chain: cause → mechanism → symptom. Keep asking why the faulty state arose until the answer is a defect in code, data, or configuration, not another symptom.
5. Search for the same faulty pattern elsewhere with `rg`.
6. Write the regression test with `test-add` and confirm it fails for the root-cause reason before the fix.
7. Fix the root cause. Do not special-case the reported input or mask the error (see «Never Fake Success» in [integrity.md](../../wiki/principles/integrity.md)). Remove the temporary logging.
8. Verify: the regression test passes, the original reproduction no longer shows the symptom, and the checks per «Verification By Change Type» in [verification.md](../../wiki/workflows/verification.md) pass.
9. Commit the test and fix with `git-commit`. Fix other occurrences from step 5 when they are in scope; otherwise register them with `github-issue-create`.

## Output
- The root cause chain, the minimal reproducer, and the regression test name.
- The fix commits and verification evidence per «Evidence Format».
- Other occurrences found, and where each was fixed or recorded.
