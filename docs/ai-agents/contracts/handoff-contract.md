# Handoff Contract

## When

- a non-trivial task moves between owners,
- session state needs to survive an owner change,
- you need the human-facing packet format for memory routing.

`.github/instructions/memory-policy.instructions.md` owns memory scope, promotion, and do-not-persist policy. This page owns the handoff packet shape.

## Required Packet

```markdown
## Handoff Packet

- Feature unit:
- Source owner:
- Target owner:
- Objective:
- Status:
- Completed work:
- Relevant artifacts:
- Constraints:
- Open questions:
- Risks:
- Requested review:
- Persistence proposal:
```

## Field Rules

- `Feature unit`: the bounded unit, not the whole project.
- `Status`: `not-started`, `in-progress`, `blocked`, `review-needed`, or `complete`.
- `Completed work`: concise facts only; avoid command transcripts.
- `Relevant artifacts`: file paths, commands, or generated outputs needed by the next owner.
- `Constraints`: public API, `.asmdef`, define symbols, docs-sync, release, samples, or tool boundaries.
- `Open questions` and `Risks`: unresolved items that affect the next decision.
- `Requested review`: `role-governor`, `reviewer`, both, or none.
- `Persistence proposal`: `session`, `repository`, `user`, or `none`, with a short reason.

## Graph Context

For every node, Git-lifecycle, combined-verification, or review assignment in [parallel worktree graph engineering](../workflow/graph-engineering.md), append this dispatch context and use `n/a` only for fields that do not apply:

```markdown
## Graph Context

- Graph ID:
- Graph revision / authoritative plan:
- Assignment / node or gate ID:
- Admission reason / cost class:
- Total / concurrent budget state:
- Depends on:
- Base commit:
- Branch:
- Worktree:
- Authorized operations / owned write scope:
- Verification contract:
- Candidate commits:
- Review disposition / approved commits:
- Dependency impact:
```

- `Candidate commits` contains the exact IDs submitted by the implementation owner; its initial disposition is `pending`.
- A matching dispatch packet has the same graph revision, allocation ID, branch, worktree, base, and authorization recorded by the coordinator plan. It grants nothing beyond those fields.
- `reviewer` re-reads those exact commits and appends `approved` plus the exact approved IDs, or `rework` plus findings. `release-manager` accepts only approved IDs.
- `Dependency impact` states which descendants would become eligible after successful integration or which edges must be replanned. Only `orchestrator` marks them ready after fan-in.
- A dirty worktree, unknown file, scope expansion, or missing verification must be reported explicitly and keeps the node out of integration.

## Handoff Expectations

- `orchestrator` sends specialists a bounded unit, owner, constraints, and expected verification.
- For graph work, `orchestrator` sends this complete context to every participant, including `release-manager`, combined-verification owner, and `reviewer`; node packets also include exclusive write scope and deliverable contract.
- Specialists return completed artifacts, verification status, risks, and next-owner recommendations.
- `role-governor` receives structural changes with overlap, missing-owner, routing, and catalog-update concerns.
- `reviewer` receives review-ready changes with verification evidence and known gaps.
- Review owners return findings, disposition, and whether any state should be persisted.
