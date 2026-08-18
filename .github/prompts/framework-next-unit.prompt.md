---
description: "Continue the next bounded Mu3Library agent-framework unit with the work -> review -> continue or rework loop."
name: "Framework Next Unit"
argument-hint: "Describe the next bounded unit to expand"
agent: "agent"
---

Continue the next bounded Mu3Library agent-framework unit.

- Work on one bounded unit only.
- Open and follow the [Task Record Policy](../../docs/ai-agents/plans/task-record-policy.md) for the plan file, the `tasks/todo.md` index, and the completion closeout.
- If compile verification is requested, run the Unity Editor CLI through `./compile-unity.sh` on the affected targets before continuing.
- Run structural suitability review through `role-governor`.
- Run `reviewer` when quality, verification, docs, or release-sensitive surfaces changed.
- Report whether the unit is approved, needs rework, or remains blocked.
