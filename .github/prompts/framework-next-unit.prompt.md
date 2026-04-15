---
description: "Continue the next bounded Mu3Library agent-framework unit with the work -> review -> continue or rework loop."
name: "Framework Next Unit"
argument-hint: "Describe the next bounded unit to expand"
agent: "agent"
---

Continue the next bounded Mu3Library agent-framework unit.

- Work on one bounded unit only.
- Update `tasks/todo.md` first.
- If compile verification is requested, route it through [unity-compile-gate](../skills/unity-compile-gate/SKILL.md) and wait for completion before continuing.
- If `tasks/compile-status.json` reports `failed` or `failed-stale`, stop, record triage in `tasks/todo.md`, and acknowledge it with `scripts/compile-gate/acknowledge-compile-failure.ps1` before starting the next unit.
- Run structural suitability review through `role-governor`.
- Run `reviewer` when quality, verification, docs, or release-sensitive surfaces changed.
- Report whether the unit is approved, needs rework, or remains blocked.