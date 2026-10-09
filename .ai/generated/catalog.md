<!-- agentkit:generated from core/, .ai/project/. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Project Catalog

Active owners, skills, and stack packs for this project. Choose an owner with [routing.md](.ai/kit/core/wiki/operating-model/routing.md).

## Owners

### governance (governance)

| Owner | Use when | Binding |
|---|---|---|
| [`orchestrator`](.ai/kit/core/agents/governance/orchestrator.md) | Decomposes a request into bounded units, selects the owner for each, orders them as a dependency graph, and sequences review gates through closeout. Use when a request spans several owners or surfaces, or when the right owner is unclear; not for implementing, writing plans, or approving quality. | — |
| [`role-governor`](.ai/kit/core/agents/governance/role-governor.md) | Judges the structural fitness of the agent, skill, and stack catalog and its routing: overlap, missing ownership, routing ambiguity, and boundary violations. Returns `continue` or `rework` on framework changes and approves new agents and sections. Use when a catalog or routing change is proposed or no owner fits a task; not for writing the docs, content quality review, or task decomposition. | — |
| [`task-planner`](.ai/kit/core/agents/governance/task-planner.md) | Records and updates plan files and progress in `.ai/tasks/` for units whose scope and owner are already decided. Use when a decomposed request needs a durable plan, or a unit's status, blocker, decision, or verification result changes; not for choosing owners or dependency order, structural verdicts, or implementing. | — |

### client (execution)

| Owner | Use when | Binding |
|---|---|---|
| [`game-runtime-engineer`](.ai/kit/core/agents/client/game-runtime-engineer.md) | Implements gameplay and runtime systems inside a game engine (for example Unity, Unreal, Godot): game logic, entity and component systems, input, physics usage, in-game UI, save/load, and runtime asset loading. Use when the dominant change runs in the shipped game build; not for editor tooling or asset pipelines, rendering pipelines or shaders, or multiplayer netcode. | `Mu3Library_Base/Runtime/**`, `Mu3Library_Base/Tests/**`, `Mu3Library_Game_WatermelonGame/Runtime/**`, `Mu3Library_Game_WatermelonGame/Tests/**`; csharp, unity |
| [`game-tools-engineer`](.ai/kit/core/agents/client/game-tools-engineer.md) | Implements game-engine editor tooling (for example Unity, Unreal, Godot): editor extensions, inspectors and editor windows, asset import and processing pipelines, content build tooling, and level or data authoring tools. Use when the dominant change runs inside the engine editor or the content build; not for runtime gameplay, shaders, or CI pipeline definitions. | `Mu3Library_Base/Editor/**`, `Mu3Library_URP/Editor/**`; csharp, unity |
| [`graphics-engineer`](.ai/kit/core/agents/client/graphics-engineer.md) | Implements rendering technology on any engine or graphics API: render pipelines and passes, shaders, material system code, GPU resource management, visual-effects technology, and GPU frame-time optimization. Use when the dominant change is how frames are produced on the GPU; not for gameplay logic, in-game UI behavior, or cross-system CPU and memory performance. | `Mu3Library_URP/Runtime/**`; csharp, unity |
| [`optional-integration-engineer`](.ai/project/agents/optional-integration-engineer.md) (extends `game-runtime-engineer`) | Implements the define-gated optional package integrations of the Mu3Library packages (UniTask, Addressables, Localization, Input System): split files, define symbols, versionDefines, and gated asmdef references. Use when the gate, the split file, or the optional dependency is the dominant concern; not for non-gated runtime or editor work. | `**/*.UniTask.cs`, `**/*.Addressables.cs`, `Mu3Library_Base/Runtime/Scripts/Addressable/**`, `Mu3Library_Base/Runtime/Scripts/Localization/**`, `Mu3Library_Base/Runtime/Scripts/IS/**`; csharp, unity |
| [`sample-integrity-engineer`](.ai/project/agents/sample-integrity-engineer.md) (extends `game-tools-engineer`) | Maintains the importable package samples under Mu3Library_*/Samples~: sample scenes, sample scripts, sample assets, the samples entries in package.json, and what a consuming project gets when it imports them. Use when sample content or its import integrity is the dominant concern; not for package runtime, editor tooling, or optional integration code. | `Mu3Library_*/Samples~/**`; csharp, unity |

### platform (execution)

| Owner | Use when | Binding |
|---|---|---|
| [`devtools-engineer`](.ai/kit/core/agents/platform/devtools-engineer.md) | Maintains developer tooling inside the repository: local scripts and CLIs, engine-independent tooling, dev containers and toolchain pins, cross-compilation and flashing tools, linter, formatter, build-tool, and monorepo configuration, and git hooks. Use when the change is to how developers build, lint, flash, or run the project locally; not for CI pipelines, test frameworks, or product code. | `tools/**`, `compile-unity.sh`, `unity-cli-packages.tsv`, `.editorconfig`, `.gitattributes`; python, shell |

### documentation (execution)

| Owner | Use when | Binding |
|---|---|---|
| [`kit-librarian`](.ai/kit/core/agents/documentation/kit-librarian.md) | Maintains agent-facing docs: kit core files in the kit repository and the project overlay `.ai/project/`; keeps links valid, runs `agentkit.py sync` and `check`, and updates `reviewed` stamps after verification. Use when a wiki page, agent card, skill, stack pack, or profile must be added, changed, or regenerated; not for structural verdicts, external research, human docs, or editing `.ai/kit/`. | `.ai/project/**` |
| [`localization-specialist`](.ai/kit/core/agents/documentation/localization-specialist.md) | Translates and localizes text into target languages directly from the source language: localized docs, UI strings, store listings, product and game text, plus the glossary that keeps terms and tone consistent. Use when text needs a version in another language or translations must follow a changed source; not for writing source text or i18n code. | `docs/readme/**`, `docs/changelog/**` |
| [`technical-writer`](.ai/kit/core/agents/documentation/technical-writer.md) | Writes and maintains human-facing project docs: README, guides, tutorials, API reference prose, and changelog wording, in the source language. Use when user- or developer-facing documentation must be created, corrected, or synced with shipped behavior; not for translations, agent-facing kit or overlay docs, documenting unverified behavior, or executing releases. | `README.md`, `CHANGELOG.md`, `Mu3Library_*/README.md`, `Mu3Library_*/CHANGELOG.md`, `docs/repository/**` |

### quality (quality)

| Owner | Use when | Binding |
|---|---|---|
| [`code-reviewer`](.ai/kit/core/agents/quality/code-reviewer.md) | Reviews review-ready changes to code, configuration, or docs for correctness, regressions, compatibility, maintainability, test adequacy, and doc sync, and returns approve or rework. Use when a unit is implemented and verified and needs a quality gate; not for fixing the findings or security-specific review. | — |

### release (execution)

| Owner | Use when | Binding |
|---|---|---|
| [`release-manager`](.ai/kit/core/agents/release/release-manager.md) | Cuts releases and integrates multi-owner work: version bumps, changelog finalization, tags, release notes, publication and update channels, and fan-in of approved branches in dependency order. Use when approved work is ready to ship or several owners' branches must be merged; not for implementing features, adding changelog entries, release pipelines, approving quality, or semantic conflicts. | `Mu3Library_*/package.json` |

Inactive core owners (enable in `.ai/project/profile.toml` when work needs them): `accessibility-reviewer`, `ai-application-engineer`, `android-engineer`, `backend-api-engineer`, `ci-cd-engineer`, `cloud-infrastructure-engineer`, `cross-platform-app-engineer`, `data-analyst`, `data-engineer`, `database-engineer`, `desktop-app-engineer`, `embedded-engineer`, `ios-engineer`, `ml-engineer`, `observability-engineer`, `performance-engineer`, `realtime-engineer`, `requirements-analyst`, `security-reviewer`, `software-architect`, `test-automation-engineer`, `trend-scout`, `ux-designer`, `web-frontend-engineer`

## Skills

### git

| Skill | Use when |
|---|---|
| [`git-branch-start`](.ai/kit/core/skills/git-branch-start/SKILL.md) | Start a unit's workspace: run the preflight in the main checkout, then create the unit's own worktree and task branch from the freshly fetched integration branch (or, with worktrees off, cut the branch in place), named from the unit's issue, stopping on any unexpected repository state. Use when a unit with an issue is about to change tracked files and has no worktree or task branch yet. |
| [`git-commit`](.ai/kit/core/skills/git-commit/SKILL.md) | Record changes as commits: group working-tree changes by concern, stage explicit paths per group, inspect the staged set, and write each message per the commit convention in the commit language. Use when verified changes on a task branch are ready to record, or when the working tree mixes changes for more than one concern. |
| [`git-conflict-resolve`](.ai/kit/core/skills/git-conflict-resolve/SKILL.md) | Bring base-branch changes into a task branch by merging, read the intent of both sides before editing any conflict, resolve, re-verify, and record every override in the merge commit. Use when a task branch lags its base, a pull request reports conflicts, or a merge stopped with conflicted paths. |
| [`git-worktree-cleanup`](.ai/kit/core/skills/git-worktree-cleanup/SKILL.md) | Remove a finished unit's worktree and task branch after its pull request merged: confirm the merge, confirm the worktree holds nothing unsaved and nothing is stacked on the branch, then remove the worktree and delete the local and remote branch, and update the main checkout. Use when a unit's pull request has merged, or the user asks to clean up merged worktrees. |

### github

| Skill | Use when |
|---|---|
| [`github-issue-create`](.ai/kit/core/skills/github-issue-create/SKILL.md) | Open a GitHub issue with the gh CLI: search for duplicates first, write the body from the required fields, apply existing labels, and mark it blocked when it depends on unfinished work. Use when a defect, request, or leftover task needs a tracked record on GitHub. |
| [`github-issue-triage`](.ai/kit/core/skills/github-issue-triage/SKILL.md) | Review open GitHub issues with the gh CLI: find duplicates, correct labels, mark or unmark blocked issues, close issues only with evidence and a stated reason, and propose a work order, leaving product decisions to the user. Use when the backlog needs grooming, before planning a batch of work, or when asked which issue to take next. |
| [`github-pr-create`](.ai/kit/core/skills/github-pr-create/SKILL.md) | Open a pull request for a finished task branch: confirm branch state, compose the body from the required fields, link issues, apply labels, and check mergeability. Use when a bounded unit is committed and verified and is ready for review. |
| [`github-pr-review-respond`](.ai/kit/core/skills/github-pr-review-respond/SKILL.md) | Answer review feedback on an open GitHub pull request with the gh CLI: fetch every comment, classify it as defect, question, or preference, fix in new commits without rewriting history, reply to each thread, re-verify, and re-request review. Use when a pull request has unanswered review comments or a changes-requested review. |

### code-change

| Skill | Use when |
|---|---|
| [`asmdef-triage`](.ai/project/skills/asmdef-triage/SKILL.md) | Diagnose a Unity assembly-definition problem in the Mu3Library packages and choose the narrowest fix: missing or wrong references, optional-package define gates, code in the wrong assembly, or compile fallout. Use when a compile error points at assembly boundaries or an .asmdef edit is proposed; it first decides whether the edit is needed at all. |
| [`bug-diagnose`](.ai/kit/core/skills/bug-diagnose/SKILL.md) | Find and fix the root cause of a defect: reproduce it, minimize the reproducer, locate the fault by bisecting or targeted logging, explain the cause-to-symptom chain, write a failing regression test, fix the cause rather than the symptom, and verify. Use when behavior is wrong, a crash or error is reported, or a test fails for an unknown reason. |
| [`code-migration`](.ai/kit/core/skills/code-migration/SKILL.md) | Move code from one API, framework, library, or pattern to another across many files: map old usage to new, inventory every call site, choose codemod or manual and incremental or big-bang, migrate in verified batches, and remove the old path last. Use when a replacement touches many call sites or a deprecated API must be phased out. |
| [`dependency-upgrade`](.ai/kit/core/skills/dependency-upgrade/SKILL.md) | Upgrade a library, framework, runtime, or toolchain: read the official release notes and migration guides for every version crossed, upgrade one dependency or one coupled set at a time with the lockfile's package manager, fix breakages in the code, and verify. Use when a dependency needs a newer version for a fix, feature, security advisory, or end of support. |
| [`performance-investigate`](.ai/kit/core/skills/performance-investigate/SKILL.md) | Improve performance by measurement: define the metric, workload, and target, reproduce with a benchmark or profile, find the dominant cost, change one thing at a time, re-measure, and report before and after numbers, stopping when the target is met. Use when something is too slow, uses too much memory or other resources, or exceeds a performance budget. |
| [`refactor-safely`](.ai/kit/core/skills/refactor-safely/SKILL.md) | Restructure code without changing its behavior: pin current behavior with characterization tests, apply small mechanical steps that are each verified and committed, and keep every behavior change out of the refactor. Use when code must be renamed, extracted, moved, split, inlined, or simplified while its observable behavior stays identical. |
| [`test-add`](.ai/kit/core/skills/test-add/SKILL.md) | Add focused tests for a change: follow the nearest existing test pattern, choose the smallest test level that observes the behavior, cover the behavior and its edge cases, and prove each new test fails without the change. Use when a fix, feature, or refactor needs tests, or a behavior has no test. |

### review

| Skill | Use when |
|---|---|
| [`code-review-perform`](.ai/kit/core/skills/code-review-perform/SKILL.md) | Review exact commits or an exact diff against its stated intent for correctness, regressions, compatibility, maintainability, test adequacy, and doc sync, then report findings and an approve or rework disposition. Use when a unit is implemented and verified and needs the code review gate, or the user asks for a review of a pull request, branch, or commit range. |

### docs

| Skill | Use when |
|---|---|
| [`adr-write`](.ai/kit/core/skills/adr-write/SKILL.md) | Write an architecture decision record in docs.adr_dir with context, options considered and their trade-offs, the decision, consequences, and status, numbered sequentially; supersede an accepted record instead of editing it. Use when a decision about structure, technology, interfaces, data, or cross-cutting conventions is made or proposed and is costly to reverse. |
| [`project-docs-sync`](.ai/kit/core/skills/project-docs-sync/SKILL.md) | Update the human-facing docs that own a changed behavior or API in the same change: README sections, guides, API reference, the changelog Unreleased entry, and localized variants listed in docs.locales, documenting only verified behavior. Use when a unit changes user-visible behavior, a public interface, configuration, or setup steps. |

### localization

| Skill | Use when |
|---|---|
| [`translate`](.ai/kit/core/skills/translate/SKILL.md) | Translate text into one or more target languages directly from its single source language: profile the genre, purpose, and tone, settle every term in the glossary, translate each target from the source (never through another translation; Chinese script variants excepted), localize instead of translating literally, protect code and placeholders, and verify completeness, terminology, and naturalness. Use when docs, UI strings, product or game content, or messages need a version in another language, or their source changed. |

### kit

| Skill | Use when |
|---|---|
| [`kit-agent-add`](.ai/kit/core/skills/kit-agent-add/SKILL.md) | Add an agent card: confirm no existing owner fits, choose between a stack pack, a project-local extends card, and a new core card, pass a role audit, scaffold and write the card, update neighbor boundaries, enable and bind it in the profile, then render and validate. Use when recurring work has no owner, or one project needs a narrower owner inside an existing surface. |
| [`kit-external-adapt`](.ai/kit/core/skills/kit-external-adapt/SKILL.md) | Adapt an external prompt, article, policy, or workflow into kit guidance: record provenance, split it into single claims, separate durable patterns from vendor-specific details, keep or reject each, map kept items to their owning page, skill, card, or pack, paraphrase them with facts and inferences marked, and route structural changes to role-governor. Use when someone brings outside guidance and asks the kit to adopt it. |
| [`kit-freshness-review`](.ai/kit/core/skills/kit-freshness-review/SKILL.md) | Review overdue kit and overlay files: list them with the freshness command, batch them by volatility, verify each against its sources and against reality, fix or confirm it, bump the reviewed date, record the review, and validate. Use when the freshness command or check reports overdue files, before a kit release, or when the user asks for a freshness review. |
| [`kit-install`](.ai/kit/core/skills/kit-install/SKILL.md) | Install the kit into a project: check requirements, run the installer, adopt any hand-written instruction files it refused to overwrite by moving their unique project facts into the overlay and deleting them with user confirmation, fill the profile through codebase onboarding, then render and validate. Use when a project has no `.ai/kit/` yet and should start using the kit. |
| [`kit-lesson-capture`](.ai/kit/core/skills/kit-lesson-capture/SKILL.md) | Capture a lesson after a user correction or a durable discovery: screen out what must not be persisted, check whether an owner already states it, choose its destination (project lessons file, project wiki page, or an upstream kit proposal), and write one concise lesson with its reason. Use when the user corrects an agent's approach, or a task reveals a fact or pitfall that future tasks in this project will need. |
| [`kit-page-add`](.ai/kit/core/skills/kit-page-add/SKILL.md) | Add a wiki page to the core kit or the project wiki: confirm no page owns the question yet, place it by question shape, scaffold it, write it to the page spec, replace duplicate copies elsewhere with links, then regenerate indexes and validate. Use when a rule, policy, or project fact has no owning page, or when a page must be split by question. |
| [`kit-page-update`](.ai/kit/core/skills/kit-page-update/SKILL.md) | Change an existing wiki page: locate the single owning page, edit only that owner, classify the rule type, update links and dependent skills and cards, bump the reviewed date only after verification, then regenerate and validate. Invariant changes go through role-governor first. Use when a rule, fact, or route on an existing page is wrong, outdated, incomplete, or contradicted by another file. |
| [`kit-role-audit`](.ai/kit/core/skills/kit-role-audit/SKILL.md) | Audit agent boundaries for role-governor: list the agents involved, compare owned artifacts, decisions, and outputs pairwise, grade overlap, find missing ownership and routing ambiguity, decide keep, narrow, split, merge, or reject per agent, and issue a continue or rework verdict with required edits. Use when an agent is proposed, split, merged, or narrowed, or when routing keeps picking two owners or none. |
| [`kit-skill-add`](.ai/kit/core/skills/kit-skill-add/SKILL.md) | Add a skill: confirm the need is a procedure rather than a role or a rule, confirm no existing skill covers it, make sure every policy it applies has an owning page, scaffold it, write steps that link policy instead of restating it, list it on the cards that run it, then render and validate. Use when several owners repeat the same ordered task with stable inputs and outputs and no skill holds it. |
| [`kit-update`](.ai/kit/core/skills/kit-update/SKILL.md) | Update the kit installed in a project: record the current version, clear local kit edits, run the updater against the upstream or a local path, read the migration notes between versions, apply the required overlay changes, then render and validate. Use when a newer kit release exists, a migration note or fix is needed, or the user asks to update the kit. |
| [`kit-upstream-propose`](.ai/kit/core/skills/kit-upstream-propose/SKILL.md) | Propose a kit change from an installed project to the upstream kit repository: confirm the change is generic, check it is not already fixed upstream, strip project details and secrets, and file an issue or pull request with evidence, the intended owning file, and an impact class. Never edits `.ai/kit/` locally. Use when a core rule, skill, card, or pack is wrong, missing, or outdated for every project, not just this one. |

### planning

| Skill | Use when |
|---|---|
| [`codebase-onboard`](.ai/kit/core/skills/codebase-onboard/SKILL.md) | Map an unfamiliar codebase quickly while reading narrowly: entry points, build and test commands, directory map, architecture, and conventions; propose values for .ai/project/profile.toml (commands, bindings, stacks) and draft .ai/project/wiki/overview.md. Use when the kit is newly installed in a project, or work starts in a codebase or area that no project doc describes. |
| [`task-plan-write`](.ai/kit/core/skills/task-plan-write/SKILL.md) | Create or update the plan file in .ai/tasks/ with `agentkit.py new plan` from the kit plan template: scope with done-when and out-of-scope, the unit table with owners, verification, gates, and statuses, budget, risks, and a running summary kept current after every unit, then close it out when the work ends. Use when work needs a plan under the planning policy, when a planned unit changes state, or when planned work finishes. |
| [`work-decompose`](.ai/kit/core/skills/work-decompose/SKILL.md) | Break a request into bounded units with one owner each: extract the goal and acceptance criteria, map the surfaces touched, select owners from the project catalog, find dependency edges, choose serial or parallel execution within the node budget, assign verification and review gates, and produce a unit table. Use when a request spans several owners or surfaces, or its owner is unclear. |
| [`work-issues`](.ai/project/skills/work-issues/SKILL.md) | Work a bounded batch of open GitHub issues in order, each one through to its pull request: fix the batch size first (one by default, at most three), choose and order the candidates, run each issue as its own unit, and keep a progress plan so a compacted session can resume. Use when the user asks to work through open issues rather than one named change. |

### product

| Skill | Use when |
|---|---|
| [`development-idea-bank`](.ai/project/skills/development-idea-bank/SKILL.md) | Generate an idea bank of genuinely new Mu3Library package directions before any implementation unit exists: map capability and whitespace from repository evidence, then rank six distinct ideas without collapsing into refinements of existing features. Use when the next package direction is unclear or past ideas feel repetitive; not for scoping or executing a chosen idea. |
| [`requirements-spec-write`](.ai/kit/core/skills/requirements-spec-write/SKILL.md) | Write a requirements specification in docs.specs_dir: problem, users, scope and out-of-scope, numbered functional requirements, testable acceptance criteria, non-functional requirements with thresholds, and open questions, asking the user only for decisions they own. Use when a feature or change needs agreed requirements before design or implementation, or an existing spec must change. |

### release

| Skill | Use when |
|---|---|
| [`release-cut`](.ai/kit/core/skills/release-cut/SKILL.md) | Cut a requested release: confirm scope, choose the version, finalize the changelog, bump version files, commit, tag with release notes, and publish only after explicit confirmation. Use when the user or an approved plan asks to release integrated work from the release branch. |

## Stack Packs

| Pack | Kind | Paths | Bound to |
|---|---|---|---|
| [`csharp`](.ai/kit/core/stacks/languages/csharp.md) | language | `**/*.cs` | game-runtime-engineer, game-tools-engineer, graphics-engineer, optional-integration-engineer, sample-integrity-engineer |
| [`python`](.ai/kit/core/stacks/languages/python.md) | language | `**/*.py`, `**/*.pyi` | devtools-engineer |
| [`shell`](.ai/kit/core/stacks/languages/shell.md) | language | `**/*.sh`, `**/*.bash`, `**/*.ps1`, `**/*.psm1` | devtools-engineer |
| [`unity`](.ai/kit/core/stacks/frameworks/unity.md) | framework | `Mu3Library_*/**/*.cs`, `UnityProject_*/Assets/**/*.cs`, `**/*.asmdef`, `**/*.asmref`, `**/*.unity`, `**/*.prefab`, `**/*.asset` | game-runtime-engineer, game-tools-engineer, graphics-engineer, optional-integration-engineer, sample-integrity-engineer |
