# Mu3Library For Unity — Work Entry Point

This file is the **shared entry point**. Copilot reads it directly, Codex arrives through [`AGENTS.md`](../AGENTS.md), Claude Code through [`CLAUDE.md`](../CLAUDE.md); those two carry directions only, never a copy. **This file only lays out routes** — each rule's body belongs to the canonical page beside it. Read this once, analyze the task, then open only the smallest route it selects, and stop at the first owning page.

## Where To Go

| What you are doing | Open |
|---|---|
| Handling one request from start to report | [request-lifecycle.md](../docs/ai-agents/workflow/request-lifecycle.md) |
| Choosing which owner handles the work | [routing/README.md](../docs/ai-agents/routing/README.md) |
| Writing code, docs, or package changes | [coding-rules.md](../docs/ai-agents/coding-rules.md) |
| Unity package code, assets, samples, `.asmdef`, or metadata | [unity-architecture.instructions.md](instructions/unity-architecture.instructions.md), [packages/README.md](../docs/ai-agents/packages/README.md) |
| C# file edits | [unity.instructions.md](instructions/unity.instructions.md) |
| Compile or safety verification | [verification.instructions.md](instructions/verification.instructions.md) |
| Branch, commit, push, merge, hotfix, issue, or label work | [git-workflow.md](../docs/ai-agents/workflow/git-workflow.md) |
| Release, version, tag, or changelog scope | [release.instructions.md](instructions/release.instructions.md) |
| README/CHANGELOG synchronization | [docs-sync.instructions.md](instructions/docs-sync.instructions.md) |
| Splitting a complex request across worktrees | [graph-engineering.md](../docs/ai-agents/workflow/graph-engineering.md) |
| Planning non-trivial work, and where the plan lives | [task-planner.instructions.md](instructions/task-planner.instructions.md), [plans/README.md](../docs/ai-agents/plans/README.md) |
| A review request or focused regression/API/docs audit | [reviewer.instructions.md](instructions/reviewer.instructions.md) |
| Framework routers and ownership, memory scope, or outside guidance | [agent-framework.instructions.md](instructions/agent-framework.instructions.md), [memory-policy.instructions.md](instructions/memory-policy.instructions.md), [external-guidance.instructions.md](instructions/external-guidance.instructions.md) |
| Handoff packets, low-token work, design rationale, repository overview | [handoff-contract.md](../docs/ai-agents/contracts/handoff-contract.md), [token-budget.md](../docs/ai-agents/workflow/token-budget.md), [architecture.md](../docs/ai-agents/architecture.md), [README.md](../README.md) |

Wiki sections not named above are reached through [docs/ai-agents/README.md](../docs/ai-agents/README.md). Owner specs, prompts, and skills are opened only after a router or inventory page selects them: [routing/README.md](../docs/ai-agents/routing/README.md) and [workflow-assets.md](../docs/ai-agents/workflow/workflow-assets.md).

## Stop First

Hold these before the canonical pages are read. Each line's body and exceptions live on the page after the dash.

- **Do not commit or push on `main`.** `main` is release-only; normal serial work happens on `develop` — git-workflow
- **Run the branch preflight before the first edit,** and stop on an unexpected branch instead of working through it — git-workflow
- **Do not create a branch or worktree on your own, push a graph branch, or auto-delete either.** Only a plan-declared `agent/<graph-id>/...` branch in its exact worktree, or one the user authorized by name, destination, and cleanup plan — git-workflow, graph-engineering
- **Confirm exact paths and sizes and get explicit approval before deleting or moving anything.** The sole approval-free exception is the current task's own completed plan under `tasks/plans/` — task-record-policy
- **Stop when the request is functionally impossible; pause when it needs a scope expansion or a decision that is not yours** — request-lifecycle
- **Do not fake success.** No special case, temporary fallback, or hidden correction to cover a contract error; leave the failing state and a TODO visible — coding-rules
- **One rule has one owner.** Do not copy a value, list, or procedure into a second page — coding-rules
- **Land behavior in the packages, never break package stability, and sync affected docs in the same task** — coding-rules
- **Answer with the smallest clear result and search narrowly** — token-budget

## When You Edit Docs

- One fact has one owner; every other page links to it instead of restating it.
- Adding a route here means adding a row, not a rule body. If a new rule has nowhere to live, create or extend its canonical page first, then link it.
- If you changed the implementation, update the affected docs in the same task.
- Keep this file within the line budget and format in [token-budget.md](../docs/ai-agents/workflow/token-budget.md) and [coding-rules.md](../docs/ai-agents/coding-rules.md) — UTF-8 with BOM, LF endings.
