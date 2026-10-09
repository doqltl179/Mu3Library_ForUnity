<!-- agentkit:generated from core/skills/. Do not edit: change the source, then run `python tools/agentkit.py sync`. Paths are relative to the project root. -->

# Skill Catalog

Every core skill, by category.

## git

| Skill | Use when |
|---|---|
| [`git-branch-start`](git-branch-start/SKILL.md) | Start a unit's workspace: run the preflight in the main checkout, then create the unit's own worktree and task branch from the freshly fetched integration branch (or, with worktrees off, cut the branch in place), named from the unit's issue, stopping on any unexpected repository state. Use when a unit with an issue is about to change tracked files and has no worktree or task branch yet. |
| [`git-commit`](git-commit/SKILL.md) | Record changes as commits: group working-tree changes by concern, stage explicit paths per group, inspect the staged set, and write each message per the commit convention in the commit language. Use when verified changes on a task branch are ready to record, or when the working tree mixes changes for more than one concern. |
| [`git-conflict-resolve`](git-conflict-resolve/SKILL.md) | Bring base-branch changes into a task branch by merging, read the intent of both sides before editing any conflict, resolve, re-verify, and record every override in the merge commit. Use when a task branch lags its base, a pull request reports conflicts, or a merge stopped with conflicted paths. |
| [`git-worktree-cleanup`](git-worktree-cleanup/SKILL.md) | Remove a finished unit's worktree and task branch after its pull request merged: confirm the merge, confirm the worktree holds nothing unsaved and nothing is stacked on the branch, then remove the worktree and delete the local and remote branch, and update the main checkout. Use when a unit's pull request has merged, or the user asks to clean up merged worktrees. |

## github

| Skill | Use when |
|---|---|
| [`github-issue-create`](github-issue-create/SKILL.md) | Open a GitHub issue with the gh CLI: search for duplicates first, write the body from the required fields, apply existing labels, and mark it blocked when it depends on unfinished work. Use when a defect, request, or leftover task needs a tracked record on GitHub. |
| [`github-issue-triage`](github-issue-triage/SKILL.md) | Review open GitHub issues with the gh CLI: find duplicates, correct labels, mark or unmark blocked issues, close issues only with evidence and a stated reason, and propose a work order, leaving product decisions to the user. Use when the backlog needs grooming, before planning a batch of work, or when asked which issue to take next. |
| [`github-pr-create`](github-pr-create/SKILL.md) | Open a pull request for a finished task branch: confirm branch state, compose the body from the required fields, link issues, apply labels, and check mergeability. Use when a bounded unit is committed and verified and is ready for review. |
| [`github-pr-review-respond`](github-pr-review-respond/SKILL.md) | Answer review feedback on an open GitHub pull request with the gh CLI: fetch every comment, classify it as defect, question, or preference, fix in new commits without rewriting history, reply to each thread, re-verify, and re-request review. Use when a pull request has unanswered review comments or a changes-requested review. |

## code-change

| Skill | Use when |
|---|---|
| [`bug-diagnose`](bug-diagnose/SKILL.md) | Find and fix the root cause of a defect: reproduce it, minimize the reproducer, locate the fault by bisecting or targeted logging, explain the cause-to-symptom chain, write a failing regression test, fix the cause rather than the symptom, and verify. Use when behavior is wrong, a crash or error is reported, or a test fails for an unknown reason. |
| [`code-migration`](code-migration/SKILL.md) | Move code from one API, framework, library, or pattern to another across many files: map old usage to new, inventory every call site, choose codemod or manual and incremental or big-bang, migrate in verified batches, and remove the old path last. Use when a replacement touches many call sites or a deprecated API must be phased out. |
| [`dependency-upgrade`](dependency-upgrade/SKILL.md) | Upgrade a library, framework, runtime, or toolchain: read the official release notes and migration guides for every version crossed, upgrade one dependency or one coupled set at a time with the lockfile's package manager, fix breakages in the code, and verify. Use when a dependency needs a newer version for a fix, feature, security advisory, or end of support. |
| [`performance-investigate`](performance-investigate/SKILL.md) | Improve performance by measurement: define the metric, workload, and target, reproduce with a benchmark or profile, find the dominant cost, change one thing at a time, re-measure, and report before and after numbers, stopping when the target is met. Use when something is too slow, uses too much memory or other resources, or exceeds a performance budget. |
| [`refactor-safely`](refactor-safely/SKILL.md) | Restructure code without changing its behavior: pin current behavior with characterization tests, apply small mechanical steps that are each verified and committed, and keep every behavior change out of the refactor. Use when code must be renamed, extracted, moved, split, inlined, or simplified while its observable behavior stays identical. |
| [`schema-migration`](schema-migration/SKILL.md) | Change a database schema or its stored data safely: write a forward migration with a rollback, split breaking changes into expand-and-contract phases, backfill in batches, verify forward and back on a disposable copy, and never edit an applied migration. Use when a change adds, alters, renames, or drops tables, columns, indexes, or constraints, or transforms existing rows. |
| [`test-add`](test-add/SKILL.md) | Add focused tests for a change: follow the nearest existing test pattern, choose the smallest test level that observes the behavior, cover the behavior and its edge cases, and prove each new test fails without the change. Use when a fix, feature, or refactor needs tests, or a behavior has no test. |

## review

| Skill | Use when |
|---|---|
| [`accessibility-review-perform`](accessibility-review-perform/SKILL.md) | Review exact commits or an exact diff through the accessibility lens: find the changed UI surfaces, check them against WCAG 2.2 AA criteria and platform guidelines with automated checkers plus manual reasoning, and report findings and an approve or rework disposition. Use when a change adds or alters a user interface on web, mobile, desktop, or game clients. |
| [`code-review-perform`](code-review-perform/SKILL.md) | Review exact commits or an exact diff against its stated intent for correctness, regressions, compatibility, maintainability, test adequacy, and doc sync, then report findings and an approve or rework disposition. Use when a unit is implemented and verified and needs the code review gate, or the user asks for a review of a pull request, branch, or commit range. |
| [`security-review-perform`](security-review-perform/SKILL.md) | Review exact commits or an exact diff through the security lens: map the trust boundaries it touches, threat-model each one, walk a vulnerability checklist, and report findings and an approve or rework disposition without exploiting live systems. Use when a change touches external input, authentication, authorization, secrets, cryptography, outbound calls, file paths, serialization, dependencies, or sensitive data. |

## docs

| Skill | Use when |
|---|---|
| [`adr-write`](adr-write/SKILL.md) | Write an architecture decision record in docs.adr_dir with context, options considered and their trade-offs, the decision, consequences, and status, numbered sequentially; supersede an accepted record instead of editing it. Use when a decision about structure, technology, interfaces, data, or cross-cutting conventions is made or proposed and is costly to reverse. |
| [`project-docs-sync`](project-docs-sync/SKILL.md) | Update the human-facing docs that own a changed behavior or API in the same change: README sections, guides, API reference, the changelog Unreleased entry, and localized variants listed in docs.locales, documenting only verified behavior. Use when a unit changes user-visible behavior, a public interface, configuration, or setup steps. |

## localization

| Skill | Use when |
|---|---|
| [`translate`](translate/SKILL.md) | Translate text into one or more target languages directly from its single source language: profile the genre, purpose, and tone, settle every term in the glossary, translate each target from the source (never through another translation; Chinese script variants excepted), localize instead of translating literally, protect code and placeholders, and verify completeness, terminology, and naturalness. Use when docs, UI strings, product or game content, or messages need a version in another language, or their source changed. |

## kit

| Skill | Use when |
|---|---|
| [`kit-agent-add`](kit-agent-add/SKILL.md) | Add an agent card: confirm no existing owner fits, choose between a stack pack, a project-local extends card, and a new core card, pass a role audit, scaffold and write the card, update neighbor boundaries, enable and bind it in the profile, then render and validate. Use when recurring work has no owner, or one project needs a narrower owner inside an existing surface. |
| [`kit-external-adapt`](kit-external-adapt/SKILL.md) | Adapt an external prompt, article, policy, or workflow into kit guidance: record provenance, split it into single claims, separate durable patterns from vendor-specific details, keep or reject each, map kept items to their owning page, skill, card, or pack, paraphrase them with facts and inferences marked, and route structural changes to role-governor. Use when someone brings outside guidance and asks the kit to adopt it. |
| [`kit-freshness-review`](kit-freshness-review/SKILL.md) | Review overdue kit and overlay files: list them with the freshness command, batch them by volatility, verify each against its sources and against reality, fix or confirm it, bump the reviewed date, record the review, and validate. Use when the freshness command or check reports overdue files, before a kit release, or when the user asks for a freshness review. |
| [`kit-install`](kit-install/SKILL.md) | Install the kit into a project: check requirements, run the installer, adopt any hand-written instruction files it refused to overwrite by moving their unique project facts into the overlay and deleting them with user confirmation, fill the profile through codebase onboarding, then render and validate. Use when a project has no `.ai/kit/` yet and should start using the kit. |
| [`kit-lesson-capture`](kit-lesson-capture/SKILL.md) | Capture a lesson after a user correction or a durable discovery: screen out what must not be persisted, check whether an owner already states it, choose its destination (project lessons file, project wiki page, or an upstream kit proposal), and write one concise lesson with its reason. Use when the user corrects an agent's approach, or a task reveals a fact or pitfall that future tasks in this project will need. |
| [`kit-page-add`](kit-page-add/SKILL.md) | Add a wiki page to the core kit or the project wiki: confirm no page owns the question yet, place it by question shape, scaffold it, write it to the page spec, replace duplicate copies elsewhere with links, then regenerate indexes and validate. Use when a rule, policy, or project fact has no owning page, or when a page must be split by question. |
| [`kit-page-update`](kit-page-update/SKILL.md) | Change an existing wiki page: locate the single owning page, edit only that owner, classify the rule type, update links and dependent skills and cards, bump the reviewed date only after verification, then regenerate and validate. Invariant changes go through role-governor first. Use when a rule, fact, or route on an existing page is wrong, outdated, incomplete, or contradicted by another file. |
| [`kit-role-audit`](kit-role-audit/SKILL.md) | Audit agent boundaries for role-governor: list the agents involved, compare owned artifacts, decisions, and outputs pairwise, grade overlap, find missing ownership and routing ambiguity, decide keep, narrow, split, merge, or reject per agent, and issue a continue or rework verdict with required edits. Use when an agent is proposed, split, merged, or narrowed, or when routing keeps picking two owners or none. |
| [`kit-skill-add`](kit-skill-add/SKILL.md) | Add a skill: confirm the need is a procedure rather than a role or a rule, confirm no existing skill covers it, make sure every policy it applies has an owning page, scaffold it, write steps that link policy instead of restating it, list it on the cards that run it, then render and validate. Use when several owners repeat the same ordered task with stable inputs and outputs and no skill holds it. |
| [`kit-stack-add`](kit-stack-add/SKILL.md) | Add a stack pack to the kit repository: confirm a pack is the right asset and none exists, gather official sources first, scaffold it, write Detect, Conventions, Verify, Pitfalls, and dated Version Notes with the sources that keep it current, then render and validate. Use when an owned surface uses a language, framework, or infrastructure tool the kit has no pack for. |
| [`kit-trend-scan`](kit-trend-scan/SKILL.md) | Scan the tech radar watchlist for trend-scout: for each item due, read official release notes and changelogs, identify changes that affect kit files (tool formats, model capabilities, deprecations, new major versions, new practices), file an intake record per change with source and date, and update the radar's last-checked date and status. Edits nothing outside the radar. Use when watchlist items are due for a check, or a major release lands in a watched tool, standard, or model. |
| [`kit-update`](kit-update/SKILL.md) | Update the kit installed in a project: record the current version, clear local kit edits, run the updater against the upstream or a local path, read the migration notes between versions, apply the required overlay changes, then render and validate. Use when a newer kit release exists, a migration note or fix is needed, or the user asks to update the kit. |
| [`kit-upstream-propose`](kit-upstream-propose/SKILL.md) | Propose a kit change from an installed project to the upstream kit repository: confirm the change is generic, check it is not already fixed upstream, strip project details and secrets, and file an issue or pull request with evidence, the intended owning file, and an impact class. Never edits `.ai/kit/` locally. Use when a core rule, skill, card, or pack is wrong, missing, or outdated for every project, not just this one. |

## planning

| Skill | Use when |
|---|---|
| [`codebase-onboard`](codebase-onboard/SKILL.md) | Map an unfamiliar codebase quickly while reading narrowly: entry points, build and test commands, directory map, architecture, and conventions; propose values for .ai/project/profile.toml (commands, bindings, stacks) and draft .ai/project/wiki/overview.md. Use when the kit is newly installed in a project, or work starts in a codebase or area that no project doc describes. |
| [`task-plan-write`](task-plan-write/SKILL.md) | Create or update the plan file in .ai/tasks/ with `agentkit.py new plan` from the kit plan template: scope with done-when and out-of-scope, the unit table with owners, verification, gates, and statuses, budget, risks, and a running summary kept current after every unit, then close it out when the work ends. Use when work needs a plan under the planning policy, when a planned unit changes state, or when planned work finishes. |
| [`work-decompose`](work-decompose/SKILL.md) | Break a request into bounded units with one owner each: extract the goal and acceptance criteria, map the surfaces touched, select owners from the project catalog, find dependency edges, choose serial or parallel execution within the node budget, assign verification and review gates, and produce a unit table. Use when a request spans several owners or surfaces, or its owner is unclear. |

## product

| Skill | Use when |
|---|---|
| [`requirements-spec-write`](requirements-spec-write/SKILL.md) | Write a requirements specification in docs.specs_dir: problem, users, scope and out-of-scope, numbered functional requirements, testable acceptance criteria, non-functional requirements with thresholds, and open questions, asking the user only for decisions they own. Use when a feature or change needs agreed requirements before design or implementation, or an existing spec must change. |

## release

| Skill | Use when |
|---|---|
| [`release-cut`](release-cut/SKILL.md) | Cut a requested release: confirm scope, choose the version, finalize the changelog, bump version files, commit, tag with release notes, and publish only after explicit confirmation. Use when the user or an approved plan asks to release integrated work from the release branch. |

## ops

| Skill | Use when |
|---|---|
| [`ci-failure-triage`](ci-failure-triage/SKILL.md) | Diagnose a failing CI run: fetch the failed logs, classify the failure as code defect, test flake, environment, configuration, or external outage, reproduce it locally with the matching profile command, then fix it or hand it to its owner without disabling checks or labeling flakes without evidence. Use when a CI check fails on a pull request, branch, or release, or a check is suspected to be flaky. |
