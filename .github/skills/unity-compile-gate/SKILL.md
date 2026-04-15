---
name: unity-compile-gate
description: "Use when a Unity compile-only verification is needed in this repository: run compile without tests, wait for completion before proceeding, record compile status, and stop the next unit until the result is known."
---

# Unity Compile Gate

## Purpose

Run compile-only verification for Mu3Library without adding test ownership.

## Use This Skill When

- compile verification is requested but tests are explicitly out of scope,
- the next framework unit must wait for compile completion,
- a change touches Unity runtime, editor, sample, or package surfaces and needs compile evidence,
- you need a repeatable compile workflow for `UnityProject_BuiltIn` or `UnityProject_URP`.

## Workflow

1. Choose the target project: `builtin`, `urp`, or both.
2. Run `scripts/compile-gate/run-unity-compile.ps1` in synchronous mode.
3. Wait until the compile workflow exits and `tasks/compile-status.json` is updated.
4. If compile fails, record the failure in `tasks/todo.md`, preserve the retained log path, and acknowledge the status with `scripts/compile-gate/acknowledge-compile-failure.ps1` before continuing.
5. If compile succeeds, record the result and only then continue to the next unit.

## Constraints

- This workflow is compile-only. Do not add or imply test execution.
- Do not continue to the next task while `tasks/compile-status.json` reports `running`.
- Close any interactive Unity editor already open for the same target project before running the batch compile gate.
- Treat compile verification as evidence for `reviewer`, not as a substitute for reviewer approval.

## Output Expectations

- target project and editor path,
- compile exit status,
- updated `tasks/compile-status.json`,
- retained log path for failed runs,
- `tasks/todo.md` note describing what compile evidence was collected,
- explicit risk note if compile could not be completed.