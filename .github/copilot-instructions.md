# Mu3Library For Unity

This file is the central router for repository instructions.

## Core Guardrails

- This file is the single source of truth for repository instruction routing.
- Prefer package-first edits under `Mu3Library_Base` or `Mu3Library_URP`.
- Preserve Unity package stability: public APIs, `.asmdef` boundaries, `.meta` files, define symbols, samples, and package metadata.
- Follow the repository development philosophy in `.github/instructions/agent-framework.instructions.md`.
- Sync README/CHANGELOG files when behavior or public API changes.
- Keep Markdown prose docs (`README*`, `CHANGELOG*`, instruction files, and `docs/ai-agents/**`) encoded as UTF-8 BOM with LF line endings. Keep frontmatter-driven agent, prompt, and skill files parser-compatible.

## Token Budget Default

- Follow the interactive-response brevity rule in `.github/instructions/agent-framework.instructions.md`.
- Search first with `rg` or a narrow file list; open only the smallest relevant file or line range.
- Do not paste full logs, generated files, HTML, JSON, or broad command output. Inspect error patterns and surrounding lines instead.
- Keep progress updates short and summarize command output by result, failure point, and affected files.
- Use only the tools, MCP servers, or plugins needed for the current task.
- For long sessions, carry forward a compact state summary: decisions, touched files, unresolved risks, and verification status.
- Detailed procedure: `docs/ai-agents/workflow/token-budget.md`.

## Read Order

Always-loaded:
1. `.github/instructions/agent-framework.instructions.md`
2. `.github/instructions/memory-policy.instructions.md`
3. `.github/instructions/task-planner.instructions.md`
4. `.github/instructions/unity-architecture.instructions.md`
5. `.github/instructions/reviewer.instructions.md`
6. `.github/instructions/unity.instructions.md` only for C# files.

Conditionally loaded:
7. `.github/instructions/verification.instructions.md` when compile or safety verification is needed.
8. `.github/instructions/git-workflow.instructions.md` when branch, merge, push, or release flow is requested.
9. `.github/instructions/release.instructions.md` when release, version, tag, or changelog release scope is affected.
10. `.github/instructions/docs-sync.instructions.md` when README/CHANGELOG synchronization is needed.

Subagent specs are not startup reading. Open `.github/agents/*.agent.md` only when routing selects that owner:

- `orchestrator`, `task-planner`, `role-governor`, `reviewer`
- `unity`, `unity-runtime`, `unity-editor`, `package-integration`, `sample-integrity`
- `docs-sync`, `release-manager`, `cli-platform`

Workspace skills:

- `.github/skills/bootstrap-python-cli/SKILL.md`
- `.github/skills/agent-role-audit/SKILL.md`
- `.github/skills/development-idea-bank/SKILL.md`
- `.github/skills/asmdef-triage/SKILL.md`
- `.github/skills/editmode-test-addition/SKILL.md`

## AI-Agent Wiki Routing

- Start from `docs/ai-agents/README.md` only when the next page is unclear.
- Choose by question shape:
  - `routing/` for owner selection.
  - `packages/` for package-boundary navigation before local package rules.
  - `contracts/` for shared packets or section formats.
  - `workflow/` for repeatable process or workflow assets.
  - `plans/` for task plan templates, plan storage rules, and plan-writing conventions.
  - `guides/` for specialized edit procedures.
  - `architecture.md` for stable design rationale.
- Treat routing as navigation to one owning page, not permission to create duplicate procedures.
- Move repeated field meanings or sibling-owner routing rules into shared wiki pages such as `contracts/agent-spec-contract.md`, `routing/control-plane-routing.md`, or `routing/unity-specialist-routing.md`.

## Workflow Summary

- For multi-step work, keep a short plan, progress updates, and final verification summary.
- For non-trivial task execution, keep `tasks/todo.md` as the persistent index and store the full active plan in `tasks/plans/` using `docs/ai-agents/plans/plan-template.md`.
- When a bounded unit completes, clear `tasks/todo.md` back to the active state and delete the completed low-value plan unless explicit retention is needed.
- For agent-framework changes, work in one bounded unit and run the structural suitability gate through `role-governor` before continuing.
- For memory-routing or handoff changes, `.github/instructions/memory-policy.instructions.md` is operative; `docs/ai-agents/contracts/handoff-contract.md` is the human-facing contract.
- For direct Unity scene or prefab YAML edits, consult `docs/ai-agents/guides/unity-yaml-guide.md` first.
- For multilingual README/CHANGELOG work, use `docs-sync` for synchronization and keep release execution separate.
- For release, versioning, tagging, branch sync, or GitHub Release execution, prefer `release-manager`.
- For Unity-domain routing, prefer the narrowest owner: `unity-runtime`, `unity-editor`, `package-integration`, or `sample-integrity`; use `unity` only for genuine cross-boundary Unity work.
- For compile-only verification, prefer editor-safe `dotnet build` of affected generated Unity `.csproj` files.
