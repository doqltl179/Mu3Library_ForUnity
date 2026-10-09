---
name: github-pr-create
description: "Open a pull request for a finished task branch: confirm branch state, compose the body from the required fields, link issues, apply labels, and check mergeability. Use when a bounded unit is committed and verified and is ready for review."
---
<!-- agentkit:generated from .ai/kit/core/skills/github-pr-create/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Create Pull Request

## Use When
- A task branch holds committed, verified work for one bounded unit.
- The user or the plan asks for a pull request.

## Do Not Use When
- Changes are uncommitted → `git-commit` first.
- Verification has not run → run it per [verification.md](.ai/kit/core/wiki/workflows/verification.md) first.
- Responding to review comments on an existing pull request → `github-pr-review-respond`.

## Inputs
- The task branch name and its base (`policy.integration_branch` in the profile).
- The unit's issue number (required when `policy.issue_first` is on) and any other linked issues.
- Verification commands and their results from this task.

## Steps
1. Work inside the unit's worktree. Confirm the branch matches «Branch Policy» in [git-workflow.md](.ai/kit/core/wiki/workflows/git-workflow.md), is not a protected branch, and that head and base fit «Pull Request Targets» in [issues-and-prs.md](.ai/kit/core/wiki/workflows/issues-and-prs.md).
2. Confirm the working tree is clean and the branch contains only this unit's commits: `git status --short` and `git log --oneline <base>..HEAD`.
3. Push the branch with upstream tracking if it is not pushed yet: `git push -u origin <branch>`.
4. Compose the title and body per «Pull Request Body» in [issues-and-prs.md](.ai/kit/core/wiki/workflows/issues-and-prs.md), in the language set by `project.language`.
5. Link issues per «Linking Issues» and choose labels per «Labels» on the same page.
6. Create the pull request: `gh pr create --base <base> --head <branch> --title "<title>" --body-file <file>` plus `--label` flags. Write the body to a temporary file to preserve formatting.
7. Check mergeability per «Mergeability Check»: `gh pr view <number> --json mergeable,mergeStateStatus`. On conflict, run `git-conflict-resolve`, re-verify, and push.
8. Register leftover work as issues per «Leftovers Become Issues» with `github-issue-create`.

## Output
- The pull request number and URL, its labels, linked issues, mergeability state, and any follow-up issues created.
- The worktree stays until the pull request merges; then run `git-worktree-cleanup`.
