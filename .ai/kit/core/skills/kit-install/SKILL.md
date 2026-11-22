---
name: kit-install
description: "Install the kit into a project as one tracked unit: open its issue and task branch, run the installer, adopt every hand-written or unmanaged instruction file it reports into the overlay (deleting them only with user confirmation), fill the profile through codebase onboarding with the policy the user confirms, render and validate, settle the integration branch and open the pull request into it, and after it merges propose the remaining repository settings and a label review. Use when a project has no `.ai/kit/` yet and should start using the kit."
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
- The user, available to confirm deletions, policy values, and repository settings.

## Steps
1. Check every item in «Requirements» in [installation.md](../../wiki/integration/installation.md). Stop and report any unmet item.
2. Treat the installation as one unit of [request-lifecycle.md](../../wiki/operating-model/request-lifecycle.md). When the project uses a hosting platform, open its issue with `github-issue-create`, then cut a task branch from the repository's current default branch (`git switch -c <branch> --no-track origin/<default-branch>`). No worktree yet: the integration branch may not exist before step 10, nor the worktree settings before this unit merges. A repository with no commit yet has no default branch to cut from: ask the user to name its first branch, which becomes the default branch and is the planned `policy.release_branch` that step 8 confirms. On it, make the empty bootstrap commit that «Branch Policy» in [git-workflow.md](../../wiki/workflows/git-workflow.md) allows (`git switch -c <first-branch>`, then `git commit --allow-empty`), and name that commit in this unit's pull request, or in its merge commit with `hosting.platform = "none"`, as that rule requires. Push it when the repository has a remote (GitHub makes the first pushed branch the default branch; on another host, ask the user to set it), then cut the task branch from it.
3. From the kit checkout, run `agentkit.py install <target-dir> --tools <list>` per «Install» in installation.md. Exit code 2 means hand-written files block generation.
4. In the target, run `agentkit.py check`. The adoption candidates are every file install refused to overwrite and every "unmanaged instruction file" warning. Read each one in full.
5. Adopt each candidate per «Adopt Existing Instructions» in installation.md. Record the sorted statements in this unit's plan with `task-plan-write` until the file is gone.
6. Place each kept project fact in `.ai/project/` per «Layout» in [project-overlay.md](../../wiki/integration/project-overlay.md). Keep a rule that would help every project there too, and handle it per step 4 of «Promotion» in [memory-policy.md](../../wiki/operating-model/memory-policy.md).
7. Ask the user to settle each statement that contradicts a kit rule: follow the kit, or, when the kit rule is a policy, keep the project's rule as a `project.guardrails` replacement per «Layers and Precedence» in [ssot.md](../../wiki/principles/ssot.md). Delete a hand-written file only after the user confirms, per «Confirm Before Irreversible Or Outward Actions» in [integrity.md](../../wiki/principles/integrity.md).
8. Run `codebase-onboard` to fill the profile. Write every `[policy]`, `[hosting]`, and `[docs]` value the user confirms explicitly, even one that equals the default, and in the other sections only the keys this project sets differently from the [profile template](../../templates/project/profile.toml), per «Profile» in project-overlay.md.
9. Run `agentkit.py sync`, then `agentkit.py check`. Fix every error in `.ai/project/`, and resolve each warning: unmanaged files, editor settings, and keys outside `[policy]`, `[hosting]`, and `[docs]` that repeat a default. Keep the confirmed policy, hosting, and docs values.
10. Commit with `git-commit`, then settle `policy.integration_branch` and `policy.release_branch` before the unit leaves its task branch. Why: the installation then reaches the integration branch like every unit («Pull Request Targets» in [issues-and-prs.md](../../wiki/workflows/issues-and-prs.md)), and a separate release branch only through a release. Below, `origin/` names drop the prefix when the repository has no remote.
    - When the current default branch is neither `policy.integration_branch` nor `policy.release_branch`, stop and ask the user which branch the installation starts from, and use that branch for the current default branch below.
    - When either branch does not exist yet, create each missing one from the current default branch after the user confirms (`git branch --no-track <missing-branch> origin/<default-branch>`), and push it when the repository has a remote (`git push -u origin <missing-branch>`). A release branch made here starts without the kit, which reaches it with the first promotion. If the user declines, go back to step 8 to choose existing branches, or stop.
    - When the task branch started from a branch other than the integration branch, move this unit's commits onto it before the first push (`git rebase --onto origin/<integration_branch> origin/<start-branch> <branch>`, where `<start-branch>` is the branch step 2 cut the task branch from), then run step 9 again and commit what it fixes.
    - With `hosting.platform = "github"`, the confirmation is the proposal of the «Branches» and «Default branch» rows of «Repository Settings» in issues-and-prs.md: make the integration branch the default branch too, unless it already is, so the pull request's closing keyword closes the installation issue («Linking Issues» there). Then open the pull request into `policy.integration_branch` with `github-pr-create`.
    - Otherwise, confirm per «Confirm Before Irreversible Or Outward Actions» in integrity.md, merge into `policy.integration_branch` per the approved-merge rule for `hosting.platform = "none"` in «Scaling The Order» in request-lifecycle.md, and skip step 11.
11. After it merges, propose the rest of «Repository Settings» in issues-and-prs.md: turn on head-branch deletion. In the same proposal, review the project's existing labels against «Labels»: descriptions within 100 characters, no stale paths, and the kind and area labels that section calls for. Apply only what the user confirms.
12. From the next unit on, every unit follows request-lifecycle.md under the confirmed policy.

## Output
- The installed kit version, the tools rendered, and the issue and pull request numbers when the unit has them.
- Adopted facts with their new locations, deleted files with the user's confirmation, files kept in `tools.keep_unmanaged` with the reason, and conflicts with their resolution.
- The confirmed policy, hosting, and docs values; repository settings and label changes applied or declined; profile keys left empty; and the `check` result.
