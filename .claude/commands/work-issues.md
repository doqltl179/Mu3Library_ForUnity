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

## 3. One pull request at a time

This repository has no feature-branch path. All work lands on `develop`, and [`branch-strategy.yml`](../../.github/workflows/branch-strategy.yml) accepts a pull request into `main` only from `develop`. **Two open pull requests are therefore impossible** — once a pull request from `develop` is open, every later push joins it.

So the unit still ends at a pull request, but the pull request accumulates:

- Open it after the first issue is committed and pushed.
- For each later issue, push to `develop` and add its `Closes #<number>` line to the same pull request body.
- Stop and ask before starting an issue that does not belong in the open pull request's scope. The alternative is waiting for a merge, which is the user's call.

## 4. Work each issue

Follow [«Issue To Pull Request»](../../docs/ai-agents/workflow/git-workflow.md) exactly. Per issue:

1. **Confirm `develop` is checked out and synchronized.** Run the branch preflight; stop on any unexpected state rather than working through it.
2. Implement, then **run the verification the touched surface needs.** [`verification.instructions.md`](../../.github/instructions/verification.instructions.md) owns which checks apply — `compile-unity.sh` for compile impact, EditMode tests where they exist. Record the commands and results for step 5.
3. **Put the affected docs in the same commit.** A public API or behavior change also moves `README.md` and `CHANGELOG.md` plus their localized files; [`docs-sync.instructions.md`](../../.github/instructions/docs-sync.instructions.md) owns that set.
4. **Commit with Conventional Commits** — type and scope in English, subject and body in Korean, lowercase scope, one concern per commit.
5. **Open or update the pull request into `main`.** Put `Closes #<number>` and the verification commands and their results in the body, and apply one kind label and one area label.
6. **Check for conflicts and resolve them in place** — `gh pr view <number> --json mergeable,mergeStateStatus`; on `CONFLICTING`, merge rather than rebase. Re-run the verification after merging.
7. **Register what is left as new issues** (`follow-up` plus the area label), per «Leftovers Belong In Issues».
8. **Send the completion notification** — what changed, the pull request number, and the next issue, in one line.

**Never move to the next issue while the change sits only in the working tree.**

## 5. Keep the context small between issues

A long list makes one session long, and a swollen context slows requests until they time out. After each pull request update, before the next issue:

1. **Update the progress file under `tasks/plans/`**, named by the UTC timestamp rule in [`task-record-policy.md`](../../docs/ai-agents/plans/task-record-policy.md): the chosen order, issues finished with their numbers, what is next, and what was deferred and why. `.gitignore` keeps that folder out of Git, so it never lands in a commit. It is what lets you resume after the context is compacted.
2. Do not re-read files or command output you no longer need.

**Do not suggest `/compact`.** Compaction happens on its own; the progress file is what makes resuming possible.

**When resuming**, restore position from that file plus `gh pr view --json number,body` and `git log origin/main..develop` before touching anything. Do not re-take an issue already covered by the open pull request.

## 6. When the list is done

Report the issues handled, the pull request number, what still needs the user (a merge, a decision), and the deferred issues with their reasons. **Send one closing notification**, including when you stopped early — say where you stopped.

Delete the progress file under `tasks/plans/` once the list is finished, per the Task Record Policy closeout.

$ARGUMENTS
