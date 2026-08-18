# Workflow Wiki

Use this section when the question is which repeatable process or workflow asset applies.

## Open

| If you need to... | Open | Why |
|---|---|---|
| Handle one request from scope extraction to final report | [request-lifecycle.md](request-lifecycle.md) | Defines the required order of one request: feasibility stop, owner selection, single unit vs graph, verification, reporting |
| Keep an AI-agent task low-token while reading docs, logs, tools, or command output | [token-budget.md](token-budget.md) | Defines the scope-narrowing, startup-doc compression, output-summarizing, and compacting workflow |
| Execute a complex request as a dependency graph across isolated Git worktrees | [graph-engineering.md](graph-engineering.md) | Defines activation, node ownership, branch/worktree isolation, ready-wave dispatch, deterministic fan-in, and cleanup gates |
| Change the agent framework itself through bounded unit -> review -> continue or rework | [iteration-process.md](iteration-process.md) | Defines the framework-change process and stop conditions; ordinary requests use [request-lifecycle.md](request-lifecycle.md) |
| Adapt an external prompt, article, policy, or workflow into repository-safe guidance | [external-guidance-adaptation.md](external-guidance-adaptation.md) | Separates durable patterns from vendor-specific details and maps them to the right repository artifact |
| Execute release details after release policy is already in scope | [release-execution.md](release-execution.md) | Defines package tags, GitHub Release commands, and release note format |
| Execute branch, push, release-sync, or hotfix details | [git-workflow.md](git-workflow.md) | Defines branch sequencing and pushable checks |
| Check whether a repeatable flow should stay as a workflow asset instead of becoming a new agent | [workflow-assets.md](workflow-assets.md) | Tracks reusable prompts, skills, hooks, and supporting workflow assets |
| Widen option space before choosing a bounded package direction | [development-idea-bank.md](development-idea-bank.md) | Defines the repository-shaped ideation workflow contract |

## Notes

- If workflow selection is no longer the question, return to [../README.md](../README.md).
- Keep bounded process rules here, and link to the smallest owning workflow page instead of restating it elsewhere.
- `request-lifecycle.md` owns the order of ordinary work; `iteration-process.md` owns changes to the framework itself. Keep the two separate.
