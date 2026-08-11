# Mu3Library For Unity

Use this file as the startup instruction SSOT. Read it once, analyze the task, then open only the smallest next instruction or wiki route it selects.

## Always-On Guardrails

- Answer with the smallest clear result that completes the request; search narrowly with `rg` or a small file list.
- Before implementing a requested task, assess its functional feasibility and intended scope.
- Before editing or committing, run a branch preflight: `git status --short --branch`, `git branch --show-current`, and the relevant upstream/ahead-behind check.
- Normal work is allowed only on `develop`; `main` is release-only. If any other branch is checked out, stop before editing or committing and report it for explicit resolution.
- Never create, switch to, merge, push, or delete a task branch automatically. A non-`main`/`develop` branch requires explicit user authorization with its exact name and cleanup destination.
- If the requested outcome is functionally impossible, stop and report why work cannot proceed; if it is feasible but requires a material scope expansion, major change, or unspecified user choice, pause and request the necessary decision.
- Do not invent workaround rules or alternate behavior to make an unimplementable or underspecified request appear complete; clarify or re-scope it so the original request can be fulfilled correctly.
- Keep one owner per concern and prefer bounded, non-overlapping work units.
- Prefer package-first edits under `Mu3Library_Base`, `Mu3Library_URP`, or `Mu3Library_Game_WatermelonGame`.
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
- `.github/instructions/docs-sync.instructions.md` when README/CHANGELOG synchronization or navigation changes are part of the task.

## Deferred Inventories

- Inventories: owner/spec at `docs/ai-agents/routing/README.md`; prompt/skill at `docs/ai-agents/workflow/workflow-assets.md`.
- Open `.github/agents/*.agent.md`, `.github/skills/*.md`, or `.github/prompts/*.md` only after a router or inventory page selects that artifact.

## Wiki Routing

- Open `docs/ai-agents/README.md` only when a wiki route is needed and the next section router (`routing/`, `packages/`, `contracts/`, `workflow/`, `plans/`, or `guides/`) is not already obvious.
- Open `architecture.md` directly only when stable design rationale is the question.
- In `packages/`, choose one package-family page and then the smallest matching surface on that page.
- Stop after the first owning child page is found, and keep shared repeated rules there instead of recreating parallel procedures.
