---
name: git-conflict-resolve
description: "Bring base-branch changes into a task branch by merging, read the intent of both sides before editing any conflict, resolve, re-verify, and record every override in the merge commit. Use when a task branch lags its base, a pull request reports conflicts, or a merge stopped with conflicted paths."
category: git
volatility: evolving
reviewed: 2026-10-09
---

# Resolve Conflicts

## Use When
- The task branch lags its base and the base changes must come in before review or merge.
- A pull request reports conflicts with its base.
- A `git merge` stopped with conflicted paths.

## Do Not Use When
- No task branch exists yet → `git-branch-start`.
- The head branch is the integration branch or a protected branch → stop; it receives changes only through pull requests.
- Resolving needs a decision about product behavior → pause per «Stop, Pause, or Proceed» in [integrity.md](../../wiki/principles/integrity.md).

## Inputs
- The head (task) branch and its base (`policy.integration_branch` unless the pull request names another).
- The linked pull request and issues, if any.
- The verification commands for the affected change types.

## Steps
1. Run «Preflight» in [git-workflow.md](../../wiki/workflows/git-workflow.md) on the head branch; stop per «Stop Conditions» on anything unexpected.
2. Merge the base into the head per «Integrating Base Changes»: `git fetch origin`, then `git merge --no-commit origin/<base>`.
3. If nothing conflicts, go to step 8.
4. List conflicted paths: `git diff --name-only --diff-filter=U`.
5. For each path, read both sides' intent before editing:
   - commits on either side that touched it: `git log --merge --oneline -- <path>`, then `git show <commit>`;
   - the three versions: `git show :1:<path>` (common ancestor), `:2:<path>` (head), `:3:<path>` (base);
   - the issues, pull requests, and docs those commits reference or change.
6. Edit the file so the result keeps both intents. When one side's change must be dropped or rewritten, note the path, what was dropped, and why. When both intents cannot hold and the choice is not technical, run `git merge --abort` and pause.
7. Stage each resolved path explicitly per «Staging»: `git add -- <path>`. Confirm `git diff --cached --check` reports no leftover conflict markers.
8. Re-verify per «Verification By Change Type» in [verification.md](../../wiki/workflows/verification.md), covering code either side touched. Fix failures before committing; never weaken a test to pass (see «Never Fake Success»).
9. Commit the merge with `git commit -F <file>` (nothing to commit if it fast-forwarded). Compose the message per «Commit Convention», listing each conflicted path and each override with its reason.
10. Push if the branch has an upstream: `git push`. For an open pull request, re-run «Mergeability Check» in [issues-and-prs.md](../../wiki/workflows/issues-and-prs.md).

## Output
- The merge commit hash, the conflicted paths, and each override with its reason.
- Verification evidence per «Evidence Format».
- Any paused decision, with the options found.
