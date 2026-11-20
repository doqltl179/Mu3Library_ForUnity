---
owns: "The required order for handling one request, from confirming the request to the final report and cleanup"
volatility: evolving
reviewed: 2026-10-09
---

# Request Lifecycle

This page owns the order of steps. Each step links to the page or skill that owns its rules.

## When

- any request starts,
- you are unsure what comes next.

## Route Away When

- the kit itself is being changed: follow this order, and also [change-intake.md](../evolution/change-intake.md) for kit-level changes,
- only owner selection is unclear: [routing.md](routing.md).

## Required Order

1. **Confirm.** Extract the goal, exclusions, and completion conditions; ask about anything ambiguous, with `decision-interview` when several user-owned decisions are open. The current request outranks any plan or record in `.ai/tasks/`.
2. **Feasibility.** Apply «Stop, Pause, or Proceed» in [integrity.md](../principles/integrity.md) before any edit.
3. **Route.** Pick the smallest owner per [routing.md](routing.md). Several surfaces or an unclear owner → `orchestrator` splits the request into units with `work-decompose`.
4. **Issue.** When `policy.issue_first` is on, every unit gets an issue before any edit: reuse an open issue that already covers it, otherwise run `github-issue-create`.
5. **Worktree.** Start the unit's worktree and task branch from the integration branch with `git-branch-start`, per «Worktrees» in [git-workflow.md](../workflows/git-workflow.md). All remaining steps run inside it.
6. **Plan.** Non-trivial work gets a plan per [planning.md](../workflows/planning.md).
7. **Execute.** Each owner works inside its write scope under the role protocol in [delegation.md](delegation.md), and keeps each edit within [change-scope.md](../workflows/change-scope.md); units run at the same time only as [concurrency.md](concurrency.md) allows. Read the bound stack packs before editing their files.
8. **Sync docs.** Behavior changes update their owning docs in the same unit: [documentation.md](../workflows/documentation.md) for human docs, [authoring/README.md](../authoring/README.md) for agent docs.
9. **Verify.** Run the checks [verification.md](../workflows/verification.md) selects for the touched surfaces.
10. **Review.** Send review-ready work through the gates that «Choosing Gates» in [review.md](../workflows/review.md) selects for `policy.review_gate`, which may be none.
11. **Pull request.** Commit with `git-commit`, then open the pull request into the integration branch with `github-pr-create`, linking the unit's issue when it has one, per «Pull Request Targets» in [issues-and-prs.md](../workflows/issues-and-prs.md).
12. **Follow-ups.** Register work discovered along the way as new issues per «Leftovers Become Issues» with `github-issue-create`; do not widen the current unit to absorb it.
13. **Report.** Report per «Report Shape» in [handoff-contract.md](handoff-contract.md), including the issue and pull request numbers the unit has.
14. **Close.** After a correction or durable discovery, run `kit-lesson-capture` when it is active in the project (listed under Skills in `.ai/generated/catalog.md`). Close the plan per «Closeout» in [planning.md](../workflows/planning.md). Once the pull request merges, run `git-worktree-cleanup` (for a unit without a worktree it cleans up the task branch and work folders only).

## Scaling The Order

- A question or analysis with no tracked file changes, judging an idea included, needs only steps 1, 2, and 13.
- With `hosting.platform = "none"`, skip the issue and pull request steps. The goal and its done-when, which the issue would hold, stay in the unit's plan while it runs, when it has one. To finish, work in the main checkout on the integration branch (`git switch <integration_branch>` when the unit worked there on its task branch): bring it up to date (`git pull --ff-only` when it has a remote); when the task branch lags it, bring the base in per «Integrating Base Changes» in [git-workflow.md](../workflows/git-workflow.md) and verify again; then show the user the change and its record and ask for approval of both the merge and the push. Only after that approval, merge the task branch with `git merge --no-ff <branch>` and push the integration branch when it has a remote. The merge commit body is the unit's record: the goal, the done-when, and what «Pull Request Body» in [issues-and-prs.md](../workflows/issues-and-prs.md) asks of a pull request body. Why the merge commit: it stays on the integration branch, while the plan is deleted at «Closeout» in [planning.md](../workflows/planning.md) and a small unit has none.
- With `hosting.platform = "none"`, `github-issue-create` is not rendered: an item that a step, page, or skill would register with it, such as a leftover, a follow-up, or a deferred change, is listed in the unit's report to the user instead, and in its plan while the unit runs, when it has one.
- With `policy.worktrees` off, step 5 cuts the task branch in the current checkout, unless «Worktrees» in [git-workflow.md](../workflows/git-workflow.md) still gives the unit a worktree.
- Promotion from the integration branch to the release branch is never part of a unit; it is a release, started only on request ([release.md](../workflows/release.md)).
- Device, deployment, or other long-running runs happen only when the request or the plan names them.
