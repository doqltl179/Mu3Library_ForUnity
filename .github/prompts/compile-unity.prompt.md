---
description: "Run compile-only verification for Mu3Library and wait for completion before continuing to the next task."
name: "Compile Unity"
argument-hint: "Target project: builtin, urp, or both"
agent: "agent"
---

Run compile-only verification for the requested Mu3Library Unity project.

- If the target Unity editor is open, use editor-safe `dotnet build` on the affected generated Unity `.csproj` files instead of starting a second Unity instance.
- Use the repository workflow in [unity-compile-gate](../skills/unity-compile-gate/SKILL.md) only when batch Unity compile evidence is needed and the editor is closed.
- When using the batch gate, run `scripts/compile-gate/run-unity-compile.ps1` in synchronous mode.
- Do not add or run tests.
- Do not continue to any follow-up task until compile completion is known.
- If a batch compile result is `failed` or `failed-stale`, record triage in `tasks/todo.md` and acknowledge it with `scripts/compile-gate/acknowledge-compile-failure.ps1` before moving on.
- Treat the compile result as evidence for `reviewer`, not as approval by itself.
- Summarize which verification path was used, the compile result, and any residual risk.