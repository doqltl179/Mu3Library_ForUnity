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

| The change… | Gate |
|---|---|
| Is review-ready code, configuration, or docs | `code-reviewer` |
| Touches a trust boundary: authentication, authorization, input parsing, secrets, cryptography, dependencies, data exposure | `security-reviewer` in addition |
| Changes user-facing UI | `accessibility-reviewer` in addition |
| Changes roles, routing, or kit structure | `role-governor` in addition |

Trivial changes (typo fixes, comment-only edits) may skip review when the project allows it; say so in the report.

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
