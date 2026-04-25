---
description: "Continue the next bounded Mu3Library agent-framework unit with the work -> review -> continue or rework loop."
name: "Framework Next Unit"
argument-hint: "Describe the next bounded unit to expand"
agent: "agent"
---

Continue the next bounded Mu3Library agent-framework unit.

- Work on one bounded unit only.
- Update `tasks/todo.md` first.
- If compile verification is requested and the target Unity editor is already open, use editor-safe `dotnet build` on the affected generated Unity `.csproj` files before continuing.
- If batch Unity compile evidence is specifically needed and the editor is closed, route it through [unity-compile-gate](../skills/unity-compile-gate/SKILL.md) and wait for completion before continuing.
- Only treat `tasks/compile-status.json` `failed` or `failed-stale` states as blocking when they came from the current unit's batch compile attempt. Pre-existing triaged batch failures do not block editor-safe assembly verification.
- Run structural suitability review through `role-governor`.
- Run `reviewer` when quality, verification, docs, or release-sensitive surfaces changed.
- Report whether the unit is approved, needs rework, or remains blocked.