<!-- agentkit:generated from .ai/kit/core/START.md, .ai/project/profile.toml. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

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
| Handle any request from start to report | [request-lifecycle.md](.ai/kit/core/wiki/operating-model/request-lifecycle.md) |
| Choose who does the work | [routing.md](.ai/kit/core/wiki/operating-model/routing.md), then `.ai/generated/catalog.md` |
| Act as an owner, delegate, or hand off | [delegation.md](.ai/kit/core/wiki/operating-model/delegation.md), [handoff-contract.md](.ai/kit/core/wiki/operating-model/handoff-contract.md) |
| Plan non-trivial work | [planning.md](.ai/kit/core/wiki/workflows/planning.md) |
| Run several units or agents at the same time | [concurrency.md](.ai/kit/core/wiki/operating-model/concurrency.md) |
| Add a dependency, change a public interface, or migrate data | [code-changes.md](.ai/kit/core/wiki/workflows/code-changes.md) |
| Start a unit (worktree, task branch) or commit | [git-workflow.md](.ai/kit/core/wiki/workflows/git-workflow.md) |
| Open or handle an issue or pull request | [issues-and-prs.md](.ai/kit/core/wiki/workflows/issues-and-prs.md) |
| Verify a change | [verification.md](.ai/kit/core/wiki/workflows/verification.md) |
| Review a change | [review.md](.ai/kit/core/wiki/workflows/review.md) |
| Change human-facing docs | [documentation.md](.ai/kit/core/wiki/workflows/documentation.md) |
| Translate text into another language | [translation.md](.ai/kit/core/wiki/workflows/translation.md) |
| Release, or promote the integration branch to the release branch | [release.md](.ai/kit/core/wiki/workflows/release.md) |
| Edit agent docs (kit or overlay) | [authoring/README.md](.ai/kit/core/wiki/authoring/README.md) |
| Store a fact, lesson, or decision | [memory-policy.md](.ai/kit/core/wiki/operating-model/memory-policy.md) |
| Keep the kit current, or adopt outside guidance | [evolution/README.md](.ai/kit/core/wiki/evolution/README.md) |
| Install, update, or configure the kit | [integration/README.md](.ai/kit/core/wiki/integration/README.md) |
| Anything else | [wiki/README.md](.ai/kit/core/wiki/README.md) |

Skills are listed in `.ai/generated/catalog.md`; run one when its trigger matches instead of improvising the procedure.

## Stop First

Each line's full rule lives on the linked page.

- Never fake success; never claim a check you did not run — [integrity.md](.ai/kit/core/wiki/principles/integrity.md)
- Stop when the request is impossible; pause for scope expansion or a decision the user owns — [integrity.md](.ai/kit/core/wiki/principles/integrity.md)
- Confirm before destructive, irreversible, or outward-facing actions — [integrity.md](.ai/kit/core/wiki/principles/integrity.md)
- Each unit: issue first, its own worktree from the integration branch, pull request back into it — [request-lifecycle.md](.ai/kit/core/wiki/operating-model/request-lifecycle.md)
- Never commit directly to a protected branch or edit files in the main checkout — [git-workflow.md](.ai/kit/core/wiki/workflows/git-workflow.md)
- One fact, one owner: link instead of copying; never edit generated or kit files — [ssot.md](.ai/kit/core/wiki/principles/ssot.md)
- Read narrowly and answer with the smallest complete result — [context-budget.md](.ai/kit/core/wiki/operating-model/context-budget.md)
- Report to the user in the language set by `project.language` — [handoff-contract.md](.ai/kit/core/wiki/operating-model/handoff-contract.md)

## This Project

- Project: **Mu3Library_ForUnity** — Reusable Unity UPM packages (Base, URP, Watermelon Game) for external Unity projects; behavior lands in the Mu3Library_* packages, and the UnityProject_* projects only consume and exercise them (.ai/project/wiki/overview.md).
- Kit: version 0.1.0, `.ai/kit/` (read-only here). CLI: `python .ai/kit/tools/agentkit.py <sync|check|freshness|new|update>`
- Overlay: `.ai/project/` · Owners and skills: `.ai/generated/catalog.md` · Project wiki: `.ai/project/wiki/README.md` · Lessons: `.ai/project/lessons.md`
- Human-facing language: `ko`

### Commands

| Key | Command |
|---|---|
| `commands.build` | `bash ./compile-unity.sh changed` |

Not available (report the gap, do not guess): `install`, `test`, `lint`, `format`, `typecheck`, `e2e`, `run`

### Parameters

| Key | Value |
|---|---|
| `policy.integration_branch` | develop |
| `policy.release_branch` | main |
| `policy.protected_branches` | develop, main |
| `policy.issue_first` | true |
| `policy.worktrees` | false |
| `policy.worktree_root` | .worktrees |
| `policy.max_parallel_units` | 0 |
| `policy.branch_pattern` | <type>/<scope>-<summary> |
| `policy.commit_convention` | conventional |
| `policy.commit_language` | — |
| `hosting.platform` | github |
| `hosting.area_labels` | base, urp, watermelon, agents, tooling |
| `docs.readme` | README.md |
| `docs.changelog` | CHANGELOG.md |
| `docs.source_locale` | en |
| `docs.locales` | ko, ja |
| `docs.locale_pattern` | docs/{stem-lower}/{stem}.{locale}.md |
| `docs.specs_dir` | docs/specs |
| `docs.adr_dir` | docs/adr |
| `docs.glossary` | .ai/project/wiki/glossary.md |
