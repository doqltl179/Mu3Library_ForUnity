---
owns: "Review policy: which review gates a change needs, the review protocol, severity levels, dispositions, and the review output format"
volatility: evolving
reviewed: 2026-10-09
---

# Review

Lens-specific checklists live in the review skills (`code-review-perform`, `security-review-perform`, `accessibility-review-perform`); this page owns what is common to all lenses.

## When

- deciding which reviews a unit needs,
- performing or receiving a review.

## Route Away When

- what evidence the author must provide: [verification.md](verification.md),
- answering review comments on a pull request: «Review Feedback» in [issues-and-prs.md](issues-and-prs.md).

## Choosing Gates

`policy.review_gate` decides which review-ready changes go through the gates:

| Value | Changes that go through the gates |
|---|---|
| `all` | Every review-ready change |
| `non-trivial` (default) | Every review-ready change except a trivial one |
| `none` | None: no agent gate runs, the security and accessibility gates included. The only review is the project's own review of the pull request, if it has one; with `hosting.platform = "none"`, the user's approval of the merge («Scaling The Order» in [request-lifecycle.md](../operating-model/request-lifecycle.md)) |

Under every value, `none` included, a change in the kit repository that the `role-governor` row below matches still gets that gate: no other review checks the kit's structure.

For this gate, a trivial change is only one of:

- a typo or formatting fix that changes no meaning;
- a comment-only edit, unless a comment it touches is read by a tool, for example a lint or type suppression (`# noqa`, `eslint-disable`, `@ts-ignore`), a build tag (`//go:build`), a pragma, or a doctest;
- regenerated output reproduced from unchanged sources by an unchanged generator, never a lockfile or other output of dependency resolution.

Any other change is non-trivial, whatever its size.

A change that goes through the gates gets each gate whose row matches:

| The change… | Gate |
|---|---|
| Is review-ready code, configuration, or docs | `code-reviewer` |
| Touches a trust boundary: authentication, authorization, input parsing, secrets, cryptography, dependencies, data exposure | `security-reviewer` in addition |
| Changes user-facing UI | `accessibility-reviewer` in addition |
| Changes roles, routing, structure, or invariants of the kit's core, in the kit repository | `role-governor` in addition |

When a matching gate is skipped, the report names the gate and why: the `policy.review_gate` value, and for a trivial change which kind it is.

## Review Protocol

1. Review exact commit IDs or an exact diff, never a moving branch.
2. Read the intent first (issue, plan, pull request body), then the diff.
3. Re-run the cheapest check that would expose the riskiest claim.
4. Judge against the change's intent and the owning rules; preferences never block.
5. The reviewer does not fix findings. The author fixes them and the reviewer re-reviews the new commits.

## Severity

| Level | Meaning |
|---|---|
| `blocker` | Wrong behavior, data loss, security exposure, broken build, or a rule violation that must not merge |
| `major` | Likely defect or missing required evidence |
| `minor` | Maintainability or clarity issue worth fixing now |
| `note` | Preference or suggestion; never blocks |

## Dispositions

- `approve` — no `blocker` or `major` findings remain; names the approved commit IDs.
- `rework` — at least one `blocker` or `major`; lists what must change.
- A reviewer that could not verify something says so instead of approving around it.

## Output Format

1. Findings, most severe first: severity, `file:line`, the concrete failure, and the expected fix direction.
2. Open questions and verification gaps.
3. Disposition with the reviewed commit IDs.
