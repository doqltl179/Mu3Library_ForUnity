---
description: "Analyze an external AI prompt, policy, or workflow and adapt only its durable parts into Mu3Library guidance."
name: "Adapt External Guidance"
argument-hint: "Describe the external source and the repository surface to update"
agent: "agent"
---

Adapt the external guidance for Mu3Library.

- Start with `.github/copilot-instructions.md` and the smallest relevant workflow or instruction file.
- Use `docs/ai-agents/workflow/external-guidance-adaptation.md`.
- Separate durable operating rules from vendor-specific identity, product, tool, and runtime details.
- Prefer shared instructions or workflow docs over adding a new owner.
- If you add an always-loaded instruction, update `.github/copilot-instructions.md`.
- If you add a prompt or skill, update `docs/ai-agents/workflow/workflow-assets.md`.
- Open and follow the [Task Record Policy](../../docs/ai-agents/plans/task-record-policy.md) for the plan file and the `tasks/todo.md` index.
- Cite the external source, paraphrase it, and label verified repository facts separately from adaptation inferences.
- End with structural suitability review through `role-governor` and a quality pass through `reviewer`.
