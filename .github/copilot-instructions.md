# Mu3Library For Unity

Use this file as the startup instruction SSOT. Read it once, analyze the task, then open only the smallest next instruction or wiki route it selects.

## Always-On Guardrails

- Answer with the smallest clear result that completes the request.
- Search narrowly with `rg` or a small file list; open only the smallest relevant file, section, or line range.
- Keep progress updates and command-output summaries short.
- Keep one owner per concern and prefer bounded, non-overlapping work units.
- Prefer package-first edits under `Mu3Library_Base` or `Mu3Library_URP`.
- Preserve Unity package stability: public APIs, `.asmdef` boundaries, `.meta` files, define symbols, samples, and package metadata.
- Sync README/CHANGELOG files when behavior or public API changes.
- Keep Markdown prose docs (`README*`, `CHANGELOG*`, instruction files, and `docs/ai-agents/**`) encoded as UTF-8 BOM with LF line endings. Keep frontmatter-driven agent, prompt, and skill files parser-compatible.
- Detailed token-budget procedure: `docs/ai-agents/workflow/token-budget.md`.

## Open Next Only When Needed

- `.github/instructions/agent-framework.instructions.md` for agent-framework docs, routers, ownership, or instruction topology.
- `.github/instructions/memory-policy.instructions.md` for memory routing, persistence scope, or handoff shape.
- `.github/instructions/external-guidance.instructions.md` for current external facts or adapted outside guidance.
- `.github/instructions/task-planner.instructions.md` for non-trivial work that needs a plan.
- `.github/instructions/unity-architecture.instructions.md` for Unity package code, assets, samples, `.asmdef`, or package metadata.
- `.github/instructions/reviewer.instructions.md` for review requests or focused regression/API/docs audits.
- `.github/instructions/unity.instructions.md` only for C# file edits.
- `.github/instructions/verification.instructions.md` for compile or safety verification.
- `.github/instructions/git-workflow.instructions.md` for branch, merge, push, or release sequencing.
- `.github/instructions/release.instructions.md` for release, version, tag, or changelog release scope.
- `.github/instructions/docs-sync.instructions.md` when README/CHANGELOG synchronization is part of the task.

## Deferred Inventories

- Owner/spec inventory: `docs/ai-agents/routing/agent-catalog.md`
- Prompt/skill inventory: `docs/ai-agents/workflow/workflow-assets.md`
- Open `.github/agents/*.agent.md`, `.github/skills/*.md`, or `.github/prompts/*.md` only after a router or inventory page selects that artifact.

## Wiki Routing

- Open `docs/ai-agents/README.md` only when a wiki route is needed and the next section is not already obvious.
- Choose one section router by question shape: `routing/`, `packages/`, `contracts/`, `workflow/`, `plans/`, or `guides/`.
- Open `architecture.md` directly only when stable design rationale is the question.
- In `packages/`, treat the first step as package-family selection before one package-surface leaf page.
- Stop after the first owning child page is found.
- Keep shared repeated rules in the smallest owning page instead of recreating parallel procedures.
