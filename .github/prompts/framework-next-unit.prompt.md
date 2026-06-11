---
description: "Continue the next bounded Mu3Library agent-framework unit with the work -> review -> continue or rework loop."
name: "Framework Next Unit"
argument-hint: "Describe the next bounded unit to expand"
agent: "agent"
---

Continue the next bounded Mu3Library agent-framework unit.

- Work on one bounded unit only.
- Create or update the detailed plan in `tasks/plans/` first, then keep `tasks/todo.md` aligned as the index.
- When the unit completes, remove the finished low-value plan unless reusable decision context still lives only there.
- If compile verification is requested, use editor-safe `dotnet build` on the affected generated Unity `.csproj` files before continuing.
- Run structural suitability review through `role-governor`.
- Run `reviewer` when quality, verification, docs, or release-sensitive surfaces changed.
- Report whether the unit is approved, needs rework, or remains blocked.
