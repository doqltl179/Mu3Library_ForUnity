---
owns: "Local Git policy: branch roles, preflight, per-unit worktrees and task branches, the work folders units make, commit convention, staging, integrating base changes, cleanup, and stop conditions"
volatility: evolving
reviewed: 2026-10-09
---

# Git Workflow

Branch names and switches come from the profile: `policy.integration_branch`, `policy.release_branch`, `policy.protected_branches`, `policy.worktrees`, `policy.worktree_root`, `policy.work_root`, `policy.branch_pattern`, `policy.commit_convention`, `policy.commit_language`.

## When

- before the first edit of a unit, before making a folder outside the checkout or a disposable copy of the project, and before any commit, merge, push, worktree, or branch deletion.

## Route Away When

- issues, pull request targets, and labels: [issues-and-prs.md](issues-and-prs.md),
- versions, tags, and promotion to the release branch: [release.md](release.md),
- parallel units and fan-in: [concurrency.md](../operating-model/concurrency.md).

## Branch Policy

| Branch | Role |
|---|---|
| `policy.integration_branch` (default `develop`) | Development. Every unit starts from it and its pull request targets it |
| `policy.release_branch` (default `main`) | Release. Changes arrive only through a promotion pull request from the integration branch ([release.md](release.md)) |
| Task branch | One per unit, named by `policy.branch_pattern`, living in its own worktree |

- Never commit directly to a branch in `policy.protected_branches`; changes arrive through merged pull requests.
- With Conventional Commits, the branch `<type>` is one of `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, and the scope matches the commit scope.
- One task branch carries one bounded unit and, when `policy.issue_first` is on, one issue.

## Preflight

Run in the main checkout before creating a unit's worktree, and in the worktree before committing:

```bash
git status --short --branch
git fetch origin
```

Stop if the checkout is not the one expected for this step, holds changes you cannot attribute, or its branch diverged from its remote.

## Worktrees

Every unit runs in its own worktree when `policy.worktrees` is on (the default), and also when it runs at the same time as another unit, whatever the policy says:

```bash
git fetch origin
git worktree add --no-track -b <branch> <worktree_root>/<branch-with-slashes-as-dashes> origin/<integration_branch>
```

- The main checkout stays on the integration branch, clean. Use it only to coordinate: read, create issues and worktrees, keep a multi-unit plan and idea records in its ignored `.ai/tasks/`. Never edit project files there. Why: `git-worktree-cleanup` switches the main checkout to the integration branch after each merge, which would move a unit working there.
- Run every command of the unit inside its worktree. Dependencies and build outputs are per worktree; install them there with `commands.install`.
- Never share a worktree between units or reuse one for a different branch.
- `--no-track` keeps the task branch from tracking the integration branch; the first `git push -u origin <branch>` sets its own upstream.
- A fresh worktree holds only tracked files. It lacks dependency folders, build outputs, engine import caches, local configuration, and untracked links or junctions. `commands.worktree_setup` recreates what the project needs and runs right after the worktree is created. `commands.worktree_teardown` undoes what would block removal; the `git-worktree-cleanup` skill runs it inside the worktree before `git worktree remove`.
- Before relying on worktrees, weigh the setup cost per worktree: disk (`agentkit.py capacity` shows free disk) and first-setup time, such as a full engine re-import. When it outweighs the isolation, set `policy.worktrees = false`: a unit that runs alone then works in the main checkout.

When `policy.worktrees` is off and no other unit is open or will start before this one is cleaned up, cut the task branch in the current checkout: `git switch -c <branch> --no-track origin/<integration_branch>`. While a unit works in the main checkout, start no other unit until `git-worktree-cleanup` has run for it after its pull request merged; if another unit must start sooner, stop and ask the user.

## Work Folders

A work folder holds what a unit makes for its own run and throws away afterward: an engine mirror or cache, a disposable copy of the project, a tool's output or logs. The project's usual ignored build outputs and caches, and single temporary files such as a commit message, are not work folders.

- Keep it inside the unit's worktree, in a folder the project's `.gitignore` already ignores, when the work can run there: removing the worktree removes it.
- Otherwise put it at `<policy.work_root>/<project.name>/<unit>`: when it must lie outside the checkout, for example because of a path-length limit or because a tool must not see the repository, or when the unit has no worktree. `<unit>` is the unit's task branch with slashes as dashes. A project whose stack limits path length may give `<unit>` a shorter form in its overlay, derived from the task branch alone so cleanup can rebuild it. Work with no task branch, such as judging an idea, uses a short kebab-case name of the task and deletes its folder before its report.
- `policy.work_root` is an absolute path or starts with `~` for the home folder; when it is relative, stop and ask the user. When it or `project.name` is empty, ask the user for the missing value before making a work folder there. Do not set the answer in the profile within the unit: a profile change is read by every later unit, so it runs first and alone («Ordering Rules» in [concurrency.md](../operating-model/concurrency.md)). Propose it as a follow-up per «Leftovers Become Issues» in [issues-and-prs.md](issues-and-prs.md), or, for work with no issue such as judging an idea, in the report.
- Never make a work folder directly in a shared location, such as a drive root, the home folder, or the system temp folder.
- Each unit uses its own `<unit>` folder; units never share one. When the path already exists and this unit did not make it, stop and ask the user.
- Why: the path shows which unit owns the folder. Without that, neither cleanup nor the user can tell what may be deleted, and the folders pile up.
- The unit's pull request body names every work folder it made outside its checkout, by absolute path («Pull Request Body» in issues-and-prs.md), so cleanup finds it even when `policy.work_root` was empty or has changed since. A folder made after the pull request opened is added to the body.
- Delete a work folder by its exact path, never `<work_root>` or `<work_root>/<project.name>`, after making sure nothing is using it, including programs opened on it such as an engine editor. In the check and the delete below, write `<folder>` so that it ends with the folder's own name, with nothing after it: no trailing `/` or `\`, and no trailing `.` segment. With anything after the name, `test -L` reads the folder a link points to and reports no link; with a trailing `/` or `\`, `rm -rf` then deletes the contents of that folder. When the folder itself is a link or junction (`test -L "<folder>"` succeeds; in PowerShell, `(Get-Item -LiteralPath "<folder>" -Force).Attributes` includes `ReparsePoint`), stop and ask the user. Otherwise, in a POSIX shell, `rm -rf -- "<folder>"` removes it with the links and junctions inside it, without touching what they point to. Without a POSIX shell, list those first with `Get-ChildItem -LiteralPath "<folder>" -Recurse -Force -Attributes ReparsePoint`: when it lists none, run `Remove-Item -LiteralPath "<folder>" -Recurse -Force`; otherwise stop and ask the user.

## Task Branch

Always cut from the freshly fetched remote integration branch, never from whatever is checked out: a branch cut from an already-merged branch starts beside other work and conflicts late. To build on an unmerged unit, stack the new branch on that unit's branch and state the merge order in both pull requests.

## Commit Convention

- With `policy.commit_convention = "conventional"`: `<type>(<scope>): <subject>`, type and scope in English and lowercase, subject and body in `policy.commit_language` (or `project.language` when empty).
- One concern per commit. The body explains why, not what. Reference the unit's issue (`Refs #<n>`).
- Never rewrite commits that are already pushed.

## Staging

- Stage explicit paths for one concern. Never `git add .` or `git add -A` on a tree you have not inspected.
- Inspect `git diff --cached --stat` before every commit.
- Treat untracked files you did not create as the user's: do not stage, move, or delete them without asking.
- Never stage secrets, build outputs, or files the project ignores.

## Integrating Base Changes

- To bring a pushed branch up to date, merge the base into it (`git merge origin/<base>`). Do not rebase a pushed branch: it detaches review comments and breaks collaborators.
- Rebasing is acceptable only on local, unpushed commits.
- Resolve conflicts by reading both sides' intent (commit messages, linked issues, docs); never drop a side silently. The `git-conflict-resolve` skill walks this.
- Re-run verification after every merge; two passing sides do not guarantee a passing merge.

## Cleanup

With head-branch deletion on («Repository Settings» in [issues-and-prs.md](issues-and-prs.md)), the platform deletes the remote task branch at merge. After a unit's pull request merged, the `git-worktree-cleanup` skill removes its worktree when it has one, its work folders («Work Folders»), and its task branch, and updates the main checkout, so the next unit starts from the latest integration branch. It deletes nothing else outside the worktree; it reports any other folder the unit made outside its checkout instead. This needs no further confirmation when every condition holds; otherwise stop and ask:

- the pull request is merged and `git log origin/<integration_branch>..<branch>` is empty,
- the worktree has no uncommitted or untracked changes,
- no other worktree lies inside the worktree,
- the main checkout is on the integration branch, or on the task branch for a unit without a worktree,
- no open pull request is stacked on the branch,
- this task created the worktree and branch.

## Stop Conditions

Stop and report instead of improvising when:

- a commit is about to land on a protected branch, or project files are about to be edited in the main checkout while `policy.worktrees` is on or another unit runs at the same time,
- the branch or worktree does not match the unit or the naming pattern,
- local and remote state diverged, or a push would overwrite remote work,
- files cannot be attributed to the current concern,
- verification or a required review is missing for what is about to be pushed.
