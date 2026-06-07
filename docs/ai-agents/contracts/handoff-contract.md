# Handoff Contract

## When

- a non-trivial task moves between owners,
- session state needs to survive an owner change,
- you need the human-facing packet format for memory routing.

`.github/instructions/memory-policy.instructions.md` is the operative policy. This page is the compact contract reference.

## Memory Scope

| Scope | Use For | Do Not Store |
|---|---|---|
| User memory | Stable cross-repository user preferences | repo-specific task state |
| Session memory | Current iteration state, routing notes, open risks, temporary handoffs | durable conventions before review |
| Repository memory | Stable project facts and verified conventions | secrets, speculation, terminal noise |

If scope is unclear, keep it in session memory until review gates complete.

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

## Routing Rules

- `orchestrator` sends specialists a bounded unit, owner, constraints, and expected verification.
- Specialists return completed artifacts, verification status, risks, and next-owner recommendations.
- `role-governor` receives structural changes with overlap, missing-owner, routing, and catalog-update concerns.
- `reviewer` receives review-ready changes with verification evidence and known gaps.
- Review owners return findings, disposition, and whether any state should be persisted.

## Persistence Rule

- Source owners may propose persistence.
- `orchestrator` confirms repository-memory promotion after requested review gates pass.
- If disposition is `rework`, keep the packet session-scoped and revise the structure first.
- Never persist secrets, credentials, speculative rules, repeated chatter, or raw terminal noise.
