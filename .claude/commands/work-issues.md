---
description: Work open GitHub issues in an efficient order, each one through to the pull request
argument-hint: "[extra filter — e.g. \"base label only\", \"up to 3\"]"
---

Work the open GitHub issues that do not carry a `blocked` label, one at a time, each through to the pull request.

**The rules live in [`docs/ai-agents/workflow/git-workflow.md`](../../docs/ai-agents/workflow/git-workflow.md); this file only holds the order.** Read that page when a step needs its detail.

## 1. Collect the targets

```bash
gh issue list --state open --limit 200 --search "-label:blocked" \
  --json number,title,labels --jq '.[] | "\(.number)\t\(.title)\t\([.labels[].name]|join(","))"'
```

Read each issue body to learn what actually closes it. Do not judge from the title.

## 2. Decide the order

- **Group issues that touch the same place.** Issues here cluster by package (`Mu3Library_Base`, `Mu3Library_URP`, `Mu3Library_Game_WatermelonGame`), by the agent docs (`docs/ai-agents/`, `.github/`), and by tooling (`tools/`, `compile-unity.sh`). Handle a group back to back.
- **Do the certain ones first.** The point is to shorten the list, so start with what you can definitely finish.
- **Defer what you cannot finish here**: anything labelled `analysis` that still needs a user decision, anything that ends outside the repository, and anything waiting on another issue's result. Record why and skip it.
- Follow dependency order. **Issue number order is not dependency order.**
- **Do not take two issues that move the same SSOT.** [`coding-rules.md`](../../docs/ai-agents/coding-rules.md) owns those boundaries. Two edits to one owner do not conflict in Git; they leave both values behind.

Report the chosen order and the reason once, then start.

## 3. One branch and one pull request per issue

Each issue gets its own task branch cut from `origin/develop` and its own pull request into `develop`. Several can be open at the same time.

- **Cut from `origin/develop`, never from the branch you just finished.** A branch cut from an already-merged one starts beside other people's work instead of on top of it, and the conflict only appears at review time.
- **Stack only when an issue continues the same files as the previous one.** Then cut from that branch — `git switch -c <type>/<scope>-<next> <previous-branch>` — and write the merge order in the pull request body. Do not delete the branch underneath until its own pull request merges.
- **Releasing `develop` to `main` is a separate unit.** It is not part of working an issue; leave it to an explicit release request.

## 4. Work each issue

Follow [«Issue To Pull Request»](../../docs/ai-agents/workflow/git-workflow.md) exactly. Per issue:

1. **Cut the task branch from `origin/develop`** after the branch preflight — `git fetch origin && git switch -c <type>/<scope>-<summary> origin/develop`. Stop on any unexpected state rather than working through it.
2. Implement, then **run the verification the touched surface needs.** [`verification.instructions.md`](../../.github/instructions/verification.instructions.md) owns which checks apply — `compile-unity.sh` for compile impact, EditMode tests where they exist. Record the commands and results for step 5.
3. **Put the affected docs in the same commit.** A public API or behavior change also moves `README.md` and `CHANGELOG.md` plus their localized files; [`docs-sync.instructions.md`](../../.github/instructions/docs-sync.instructions.md) owns that set.
4. **Commit with Conventional Commits** — type and scope in English, subject and body in Korean, lowercase scope, one concern per commit.
5. **Push the branch and open the pull request into `develop`.** Put `Closes #<number>` and the verification commands and their results in the body, and apply one kind label and one area label.
6. **Check for conflicts and resolve them in place** — `gh pr view <number> --json mergeable,mergeStateStatus`; on `CONFLICTING`, `git merge origin/develop` rather than rebase. Re-run the verification after merging.
7. **Register what is left as new issues** (`follow-up` plus the area label), per «Leftovers Belong In Issues».
8. **Send the completion notification** — what changed, the pull request number, and the next issue, in one line.
9. **Clean up the task branch once its pull request merges**, per «Task Branch Cleanup». Keep a branch that another open pull request was stacked on.

**Never move to the next issue while the change sits only in the working tree.**

## 5. Keep the context small between issues

A long list makes one session long, and a swollen context slows requests until they time out. After each pull request update, before the next issue:

1. **Update the progress file under `tasks/plans/`**, named by the UTC timestamp rule in [`task-record-policy.md`](../../docs/ai-agents/plans/task-record-policy.md): the chosen order, issues finished with their branch and pull request numbers, what is next, and what was deferred and why. `.gitignore` keeps that folder out of Git, so it never lands in a commit. It is what lets you resume after the context is compacted.
2. Do not re-read files or command output you no longer need.

**Do not suggest `/compact`.** Compaction happens on its own; the progress file is what makes resuming possible.

**When resuming**, restore position from that file plus `gh pr list --state open` and `git branch -vv` before touching anything. Do not re-take an issue that already has a pull request.

## 6. When the list is done

Report the issues handled with their pull request numbers and required merge order, what still needs the user (a merge, a decision), and the deferred issues with their reasons. **Send one closing notification**, including when you stopped early — say where you stopped.

Delete the progress file under `tasks/plans/` once the list is finished, per the Task Record Policy closeout.

$ARGUMENTS
