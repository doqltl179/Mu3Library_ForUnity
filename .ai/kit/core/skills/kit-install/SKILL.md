---
name: kit-install
description: "Install the kit into a project as one tracked unit: open its issue and task branch, run the installer, adopt every hand-written or unmanaged instruction file it reports into the overlay (deleting them only with user confirmation), fill a minimal profile through codebase onboarding, render and validate, open the pull request, and after it merges propose repository settings and a label review. Use when a project has no `.ai/kit/` yet and should start using the kit."
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
- The AI tools the project uses, for `--tools`.
- The user, available to confirm deletions and repository settings.

## Steps
1. Check every item in «Requirements» in [installation.md](../../wiki/integration/installation.md). Stop and report any unmet item.
2. Treat the installation as one unit of [request-lifecycle.md](../../wiki/operating-model/request-lifecycle.md). When the project uses a hosting platform, open its issue with `github-issue-create`, then cut a task branch from the repository's current default branch (`git switch -c <branch> --no-track origin/<default-branch>`). No worktree yet: the integration branch and worktree settings may not exist before this unit merges.
3. From the kit checkout, run `agentkit.py install <target-dir> --tools <list>` per «Install» in installation.md. Exit code 2 means hand-written files block generation.
4. In the target, run `agentkit.py check`. The adoption candidates are every file install refused to overwrite and every "unmanaged instruction file" warning. Read each one in full.
5. Adopt each candidate per «Adopt Existing Instructions» in installation.md. Record the sorted statements in this unit's plan with `task-plan-write` until the file is gone.
6. Place each kept project fact in `.ai/project/` per «Layout» in [project-overlay.md](../../wiki/integration/project-overlay.md), and send each rule that helps every project to `kit-upstream-propose`.
7. Ask the user to settle each statement that contradicts a kit rule. Delete a hand-written file only after the user confirms, per «Confirm Before Irreversible Or Outward Actions» in [integrity.md](../../wiki/principles/integrity.md).
8. Run `codebase-onboard` to fill the profile. Write only the keys this project sets differently from the [profile template](../../templates/project/profile.toml), per «Profile» in project-overlay.md.
9. Run `agentkit.py sync`, then `agentkit.py check`. Fix every error in `.ai/project/`, and resolve each warning: redundant profile keys, unmanaged files, editor settings.
10. Commit with `git-commit`, then open the pull request into the current default branch with `github-pr-create`.
11. After it merges, propose «Repository Settings» in [issues-and-prs.md](../../wiki/workflows/issues-and-prs.md): create the integration branch from the merged default branch when it is missing, make it the default branch, and turn on head-branch deletion. In the same proposal, review existing labels against «Labels»: descriptions within 100 characters, no stale paths, one kind and one area axis. Apply only what the user confirms. From the next unit on, the full lifecycle applies: issue, worktree from the integration branch, pull request back into it.

## Output
- The installed kit version, the tools rendered, the issue and pull request numbers.
- Adopted facts with their new locations, deleted files with the user's confirmation, files kept in `tools.keep_unmanaged` with the reason, and conflicts with their resolution.
- Repository settings and label changes applied or declined, profile keys left empty, and the `check` result.
