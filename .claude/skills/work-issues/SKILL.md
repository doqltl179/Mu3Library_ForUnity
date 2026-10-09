---
name: work-issues
description: "Work a bounded batch of open GitHub issues in order, each one through to its pull request: fix the batch size first (one by default, at most three), choose and order the candidates, run each issue as its own unit, and keep a progress plan so a compacted session can resume. Use when the user asks to work through open issues rather than one named change."
---
<!-- agentkit:generated from .ai/project/skills/work-issues/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Work Issues

## Use When
- The user asks to work open issues, with or without a count or a filter such as one package or one label.

## Do Not Use When
- One named change → the ordinary order in [request-lifecycle.md](.ai/kit/core/wiki/operating-model/request-lifecycle.md).
- Relabeling, deduplicating, or closing issues without implementing them → `github-issue-triage`.

## Inputs
- The count and filter from the request, if any.
- `gh` passing «Platform Preflight» in [issues-and-prs.md](.ai/kit/core/wiki/workflows/issues-and-prs.md).

## Steps
1. Fix the batch before reading any issue. No count means one issue; a count means up to three in this session, with the rest reported as the next queue. A filter narrows the candidates and never raises the cap. Why: each issue's verification runs the Unity Editor CLI for minutes, and a session that carries many issues slows down until requests time out.
2. If the context was compacted mid-batch, finish the current issue's pull request and stop; the rest resumes from the plan in step 6.
3. List the candidates that are not blocked:
   `gh issue list --state open --limit 200 --search "-label:blocked" --json number,title,labels --jq '.[] | "\(.number)\t\(.title)\t\([.labels[].name]|join(","))"'`
   Narrow by title and label, then read the bodies of those candidates and their named prerequisites, five at a time at most. Never judge from the title alone.
4. Order the batch: group issues by area label and work a group back to back; take the certain ones first; follow dependency order, never issue number order; never take two issues that change the same owning page or value in one batch. Defer what cannot finish here by applying `blocked` per «Blocked Issues» in issues-and-prs.md. Report the order and the reasons once, then start.
5. Run each issue as its own unit through steps 5–12 of request-lifecycle.md: cut its branch from the integration branch with `git-branch-start` (stack on the previous branch only when the issue continues the same files, and state the merge order in both pull requests), verify per [unity-verification.md](.ai/project/wiki/unity-verification.md), commit with `git-commit`, open the pull request with `github-pr-create` using `Closes #<number>` and one kind and one area label, and register leftovers with `github-issue-create`. Never start the next issue while the change sits only in the working tree.
6. After each pull request, update one plan in `.ai/tasks/` with `task-plan-write`: the chosen order, finished issues with branch and pull request numbers, the next issue, and what was deferred and why. On resume, restore position from that plan, `gh pr list --state open`, and `git branch -vv` before touching anything; never re-take an issue that already has a pull request. Do not suggest `/compact`.
7. After each pull request, send a one-line notification when the environment can: what changed, the pull request number, and the next issue.
8. When the batch ends, report per «Report Shape» in [handoff-contract.md](.ai/kit/core/wiki/operating-model/handoff-contract.md), send one closing notification (also when stopping early, saying where), and close the plan per «Closeout» in [planning.md](.ai/kit/core/wiki/workflows/planning.md) unless a context boundary cut the batch short.

## Output
- One pull request per finished issue with its number and any required merge order.
- Issues deferred with `blocked` and why, what still needs the user, and the first issue of the next queue.
