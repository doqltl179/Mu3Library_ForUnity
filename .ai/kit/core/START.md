---
owns: "Session startup: path conventions, the route map to the smallest owning page, and stop-first guardrails"
volatility: evolving
reviewed: 2026-10-09
---

# Agent Start

This project uses agentkit: a shared wiki of rules, agent roles, skills, and stack packs. Read this once per session, classify the task, open only the smallest route below, and stop at the first page that owns your question. Do not read the whole wiki.

## Path Conventions

- Kit: read-only rules, roles, skills, and stack packs. Its root is shown in «This Project» (`.ai/kit/` in installed projects).
- Overlay `.ai/project/`: project-owned facts. `profile.toml` (parameters, commands, active roles, bindings), `agents/`, `skills/`, `wiki/`, `lessons.md`.
- Generated, never edit: `AGENTS.md`, `CLAUDE.md`, `.ai/generated/`, and the tool folders. Change the source, then run `agentkit.py sync`.
- Working records, not committed: `.ai/tasks/`.
- Paths in rendered agent files are relative to the project root.

## Where To Go

| You are about to… | Open |
|---|---|
| Handle any request from start to report | [request-lifecycle.md](wiki/operating-model/request-lifecycle.md) |
| Choose who does the work | [routing.md](wiki/operating-model/routing.md), then `.ai/generated/catalog.md` |
| Act as an owner, delegate, or hand off | [delegation.md](wiki/operating-model/delegation.md), [handoff-contract.md](wiki/operating-model/handoff-contract.md) |
| Plan non-trivial work | [planning.md](wiki/workflows/planning.md) |
| Run several units or agents at the same time | [concurrency.md](wiki/operating-model/concurrency.md) |
| Add a dependency, change a public interface, or migrate data | [code-changes.md](wiki/workflows/code-changes.md) |
| Start a unit (worktree, task branch) or commit | [git-workflow.md](wiki/workflows/git-workflow.md) |
| Open or handle an issue or pull request | [issues-and-prs.md](wiki/workflows/issues-and-prs.md) |
| Verify a change | [verification.md](wiki/workflows/verification.md) |
| Review a change | [review.md](wiki/workflows/review.md) |
| Change human-facing docs | [documentation.md](wiki/workflows/documentation.md) |
| Translate text into another language | [translation.md](wiki/workflows/translation.md) |
| Release, or promote the integration branch to the release branch | [release.md](wiki/workflows/release.md) |
| Edit agent docs (kit or overlay) | [authoring/README.md](wiki/authoring/README.md) |
| Store a fact, lesson, or decision | [memory-policy.md](wiki/operating-model/memory-policy.md) |
| Keep the kit current, or adopt outside guidance | [evolution/README.md](wiki/evolution/README.md) |
| Install, update, or configure the kit | [integration/README.md](wiki/integration/README.md) |
| Anything else | [wiki/README.md](wiki/README.md) |

Skills are listed in `.ai/generated/catalog.md`; run one when its trigger matches instead of improvising the procedure.

## Stop First

Each line's full rule lives on the linked page.

- Never fake success; never claim a check you did not run — [integrity.md](wiki/principles/integrity.md)
- Stop when the request is impossible; pause for scope expansion or a decision the user owns — [integrity.md](wiki/principles/integrity.md)
- Confirm before destructive, irreversible, or outward-facing actions — [integrity.md](wiki/principles/integrity.md)
- Each unit: issue first, its own worktree from the integration branch, pull request back into it — [request-lifecycle.md](wiki/operating-model/request-lifecycle.md)
- Never commit directly to a protected branch or edit files in the main checkout — [git-workflow.md](wiki/workflows/git-workflow.md)
- One fact, one owner: link instead of copying; never edit generated or kit files — [ssot.md](wiki/principles/ssot.md)
- Read narrowly and answer with the smallest complete result — [context-budget.md](wiki/operating-model/context-budget.md)
- Report to the user in the language set by `project.language` — [handoff-contract.md](wiki/operating-model/handoff-contract.md)
