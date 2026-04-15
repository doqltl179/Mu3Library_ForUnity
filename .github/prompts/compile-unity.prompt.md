---
description: "Run compile-only verification for Mu3Library and wait for completion before continuing to the next task."
name: "Compile Unity"
argument-hint: "Target project: builtin, urp, or both"
agent: "agent"
---

Run compile-only verification for the requested Mu3Library Unity project.

- Use the repository workflow in [unity-compile-gate](../skills/unity-compile-gate/SKILL.md).
- Run `scripts/compile-gate/run-unity-compile.ps1` in synchronous mode.
- Do not add or run tests.
- Do not continue to any follow-up task until compile completion is known.
- If the compile result is `failed` or `failed-stale`, record triage in `tasks/todo.md` and acknowledge it with `scripts/compile-gate/acknowledge-compile-failure.ps1` before moving on.
- Treat the compile result as evidence for `reviewer`, not as approval by itself.
- Summarize the compile result and any residual risk.