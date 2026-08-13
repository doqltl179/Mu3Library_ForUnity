# Task Plans

This folder stores detailed execution plans for active non-trivial repository work; it is not an archive.

- Use the standard template from [docs/ai-agents/plans/plan-template.md](../../docs/ai-agents/plans/plan-template.md).
- Name each new plan file according to the UTC creation-timestamp rule in the [Task Record Policy](../../docs/ai-agents/plans/task-record-policy.md), and do not change that timestamp while the plan is active.
- Keep `tasks/todo.md` as the index that links to the active plan.
- Prefer keeping at most one active plan file here during normal work.
- Use one file per bounded serial task. For graph work, keep the whole request in one plan using the [graph plan contract](../../docs/ai-agents/workflow/graph-engineering.md); never create one plan per node.
- At task completion, first move durable outcomes to their owning record, then delete the plan in the same closeout.
- The current task's executing agent may delete only its own completed plan without further approval when every [Approval-Free Closeout](../../docs/ai-agents/plans/task-record-policy.md#approval-free-closeout) condition holds.
- Do not retain, archive, or rename completed plan files. See the [Task Record Policy](../../docs/ai-agents/plans/task-record-policy.md).
