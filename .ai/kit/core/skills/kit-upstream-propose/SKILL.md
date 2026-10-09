---
name: kit-upstream-propose
description: "Propose a kit change from an installed project to the upstream kit repository: confirm the change is generic, check it is not already fixed upstream, strip project details and secrets, and file an issue or pull request with evidence, the intended owning file, and an impact class. Never edits `.ai/kit/` locally. Use when a core rule, skill, card, or pack is wrong, missing, or outdated for every project, not just this one."
category: kit
volatility: evolving
reviewed: 2026-10-09
---

# Propose Upstream Change

## Use When
- A file under `.ai/kit/` is wrong, missing a case, or outdated, and the fix holds for every project.
- A project lesson or local page turns out to be generic, per «Promotion» in [memory-policy.md](../../wiki/operating-model/memory-policy.md).

## Do Not Use When
- The change is specific to this project → `kit-lesson-capture`, or `kit-page-add` in `.ai/project/wiki/`.
- You are working in the kit repository itself → the owning `kit-*` skill directly.
- The fix has shipped upstream → `kit-update`.

## Inputs
- The problem and its evidence: the task, the failure or correction, and the `check` output when relevant.
- `evolution.upstream` from the profile, and `.ai/kit/VERSION`.

## Steps
1. Confirm the change is generic: it must hold without this project's stack, paths, team, or domain. If not, stop and route it to the overlay.
2. Search upstream for an existing issue, pull request, or CHANGELOG entry covering it: `gh issue list --repo <upstream> --search "<terms>"`. When one matches, the proposal becomes a comment on it carrying the new evidence.
3. Name the intended owning file and section in the kit, using its path from the kit root, and its rule type per «Rule Types» in [ssot.md](../../wiki/principles/ssot.md).
4. Classify the impact per «Impact Classes» in [change-intake.md](../../wiki/evolution/change-intake.md).
5. Strip project names, paths, code, internal URLs, and people; replace them with a generic example. Remove secrets per «Secrets And Sensitive Data» in [integrity.md](../../wiki/principles/integrity.md).
6. Write the proposal as an intake record per «Intake Record» in change-intake.md, in an issue body that also meets «Issue Body» in [issues-and-prs.md](../../wiki/workflows/issues-and-prs.md). Include the sanitized evidence, the owning file and section, the proposed text, the rule type, and `.ai/kit/VERSION`. Write the proposed kit text in English per [agent-first-writing.md](../../wiki/principles/agent-first-writing.md).
7. Show the user the final text and target repository; post only after they confirm, per «Confirm Before Irreversible Or Outward Actions» in integrity.md.
8. Post it: an issue with `github-issue-create` against the upstream repository, or a pull request from a separate kit checkout with `github-pr-create`. When `hosting.platform` is `none` or upstream is unreachable, record it offline as «Intake Record» directs and hand it to the user.
9. Leave `.ai/kit/` untouched. When the project needs relief now, add a workaround in `.ai/project/` that does not contradict a core invariant, and link the proposal from it.

## Output
- The issue or pull request URL (or the saved proposal path), the owning file, and the impact class.
- Any interim overlay workaround and where it lives.
