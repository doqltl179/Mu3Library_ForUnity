---
name: kit-install
description: "Install the kit into a project: check requirements, run the installer, adopt any hand-written instruction files it refused to overwrite by moving their unique project facts into the overlay and deleting them with user confirmation, fill the profile through codebase onboarding, then render and validate. Use when a project has no `.ai/kit/` yet and should start using the kit."
category: kit
volatility: evolving
reviewed: 2026-10-09
---

# Install Kit

## Use When
- A project has no `.ai/kit/` and the user wants it to use the kit.

## Do Not Use When
- `.ai/kit/` already exists → `kit-update`.
- Only the profile or overlay needs changes → edit `.ai/project/` and run `agentkit.py sync`.

## Inputs
- A kit checkout at a released version, and the target project directory.
- The AI tools the project uses, for `--tools` and `tools.targets`.
- The user, available to confirm deletions.

## Steps
1. Check every item in «Requirements» in [installation.md](../../wiki/integration/installation.md). Stop and report any unmet item.
2. Inventory existing instruction files in the target: `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, and the tool paths in «Generated Files» in [tool-adapters.md](../../wiki/integration/tool-adapters.md). Read each one in full.
3. From the kit checkout, run `agentkit.py install <target-dir> --tools <list>` per «Install» in installation.md.
4. For each hand-written file the install refused to overwrite, follow «Adopt Existing Instructions» in installation.md. Record the sorted statements in this task's plan with `task-plan-write` until the file is gone.
5. Place each kept project fact in `.ai/project/` per «Layout» in [project-overlay.md](../../wiki/integration/project-overlay.md), and send each rule that helps every project to `kit-upstream-propose`.
6. Ask the user to settle each statement that contradicts a kit rule. Delete a hand-written file only after the user confirms, per «Confirm Before Irreversible Or Outward Actions» in [integrity.md](../../wiki/principles/integrity.md).
7. Run `codebase-onboard` to fill the profile: `[project]`, `[commands]`, `[policy]`, `[hosting]`, `[agents]`, `[skills]`, `[stacks]`, and `[bindings.<agent>]`. Keys and defaults are in the [profile template](../../templates/project/profile.toml).
8. Run `agentkit.py sync` to render the tool files from the filled profile, then `agentkit.py check`; fix every error in `.ai/project/`.
9. Propose the hosting settings in «Repository Settings» in [issues-and-prs.md](../../wiki/workflows/issues-and-prs.md) when `hosting.platform` is `github`; apply them only after the user confirms.
10. Commit only when the user asks, with `git-commit`.

## Output
- The installed kit version and the tools rendered.
- Adopted facts with their new locations, deleted files with the user's confirmation, and conflicts with their resolution.
- Profile keys left empty, and the `check` result.
