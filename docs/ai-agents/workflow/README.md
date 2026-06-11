# Workflow Wiki

Use this section when the question is which repeatable process or workflow asset applies.

## Open

| If you need to... | Open | Why |
|---|---|---|
| Keep an AI-agent task low-token while reading docs, logs, tools, or command output | [token-budget.md](token-budget.md) | Defines the scope-narrowing, output-summarizing, and compacting workflow |
| Follow the bounded unit -> review -> continue or rework loop | [iteration-process.md](iteration-process.md) | Defines the required framework-change process and stop conditions |
| Execute release details after release policy is already in scope | [release-execution.md](release-execution.md) | Defines package tags, GitHub Release commands, and release note format |
| Execute branch, push, release-sync, or hotfix details | [git-workflow.md](git-workflow.md) | Defines branch sequencing and pushable checks |
| Check whether a repeatable flow should stay as a workflow asset instead of becoming a new agent | [workflow-assets.md](workflow-assets.md) | Tracks reusable prompts, skills, hooks, and supporting workflow assets |
| Widen option space before choosing a bounded package direction | [development-idea-bank.md](development-idea-bank.md) | Defines the repository-shaped ideation workflow contract |

## Notes

- If workflow selection is no longer the question, return to [../README.md](../README.md).
- Keep bounded process rules here, and link to the smallest owning workflow page instead of restating it elsewhere.
