# Handoff Contract

## Purpose

This document defines how Mu3Library agents pass work, state, and review context between one another.

The contract intentionally follows common patterns used in stateful multi-agent systems:

- short-term versus long-term memory separation,
- structured state packets instead of free-form summaries,
- explicit review gates before durable state is promoted,
- a single control plane that routes the next owner.

## Design Goals

- Make every handoff small enough to audit.
- Keep temporary reasoning out of durable repository memory.
- Let the next agent act without re-reading the whole conversation.
- Preserve traceability between decisions, artifacts, and review outcomes.

## Memory Scope Matrix

| Scope | Store Here | Do Not Store Here | Promotion Rule |
|---|---|---|---|
| User memory | Cross-repository preferences and stable collaboration habits | Repo-specific architecture facts, current task state, transient routing notes | Only when it is useful beyond this repository |
| Session memory | Current iteration state, handoff packets, unresolved risks, active routing decisions, temporary assumptions | Stable repository conventions, obsolete packets, repetitive chatter | Promote to repo memory only after review confirms the rule is durable |
| Repo memory | Stable architecture conventions, approved routing rules, validated tooling roots, settled verification facts | Unreviewed proposals, speculative designs, one-off task notes | Create or update only after the current iteration is accepted |

## What Must Not Be Persisted

- secrets, tokens, credentials, or private endpoints,
- raw chain-of-thought or speculative reasoning that was not accepted,
- transient logs or terminal noise,
- duplicate summaries that do not help the next agent act.

## Required Handoff Packet

Every non-trivial handoff should be representable as the following packet.

```md
## Handoff Packet

- Unit: <feature unit name>
- Source: <agent or workflow stage>
- Target: <agent or workflow stage>
- Objective: <what the target must achieve>
- Status: <ready for work | ready for review | blocked>
- Completed Work: <artifacts or decisions already finished>
- Relevant Artifacts: <files, docs, memory entries, commands>
- Constraints: <repo boundaries, defines, docs sync, tooling limits>
- Open Questions: <questions the target must resolve>
- Risks: <known structural or verification risks>
- Requested Review: <none | role-governor | reviewer | both>
- Persistence Proposal: <stay in session | promote to repo after requested reviews>
```

## Required Fields

- `Unit` keeps the work bounded.
- `Source` and `Target` make ownership explicit.
- `Completed Work` prevents repeated exploration.
- `Relevant Artifacts` should point to concrete files or memory entries.
- `Requested Review` makes the next gate explicit.
- `Persistence Proposal` is advisory from the source owner. Final session -> repo promotion is confirmed by orchestrator after all requested review gates pass.

## Routing Rules

### Orchestrator -> Specialist

Use when a feature unit is ready for implementation or planning.

- Include the current unit,
- the selected owner,
- repository constraints that matter now,
- the expected output for the unit,
- whether a review gate will immediately follow.

### Specialist -> Orchestrator

Use when implementation, planning, or research for the current unit is complete and the next routing decision belongs to the control plane.

- Include completed work, unresolved questions, requested review, and the source owner's persistence proposal.

### Orchestrator -> Role Governor

Use when a framework change needs structural fitness review.

- Focus on overlap, missing ownership, routing ambiguity, and whether the unit should continue or be reworked.
- The resulting disposition returns to orchestrator.

### Orchestrator -> Reviewer

Use when quality, verification, compatibility, release, or docs safety must be checked.

- Include verification evidence or explicitly state that only static review is available.
- The resulting disposition returns to orchestrator.

### Review Owner -> Orchestrator

Use when `role-governor` or `reviewer` completes a gate.

- Return the disposition,
- blocking findings if any,
- required rework,
- whether the source owner's persistence proposal can now be accepted.

## Review-Aware Persistence

- Before suitability review, store current routing state in session memory only.
- The source owner may propose promotion, but final promotion is recorded by orchestrator.
- Promote state to repository memory only after all requested review gates pass.
- If `role-governor` returns `rework`, keep the packet in session memory and update it instead of promoting it.
- If `reviewer` finds blocking quality issues, do not promote related facts to repo memory until the blocker is resolved.

## Minimal Packet Rule

Not every step needs a stored handoff packet.

Skip memory persistence when all are true:

- the handoff is trivial,
- the next agent can act from the current local context,
- no durable rule or unresolved risk needs to survive the current exchange.

## Example: Orchestrator To Role Governor

```md
## Handoff Packet

- Unit: iteration-2 memory-handoff-protocol
- Source: orchestrator
- Target: role-governor
- Objective: Decide whether the proposed memory-policy instruction and handoff contract fit the framework without introducing overlap.
- Status: ready for review
- Completed Work: Drafted repository memory scope rules and a standard handoff packet.
- Relevant Artifacts: docs/ai-agents/handoff-contract.md, .github/instructions/memory-policy.instructions.md
- Constraints: Must not create a second control plane; must preserve session vs repo memory separation.
- Open Questions: Should any of these rules live in agent-framework.instructions instead?
- Risks: Memory policy may overlap with existing agent-framework guidance if too broad.
- Requested Review: role-governor
- Persistence Proposal: stay in session
```

## Example: Specialist To Reviewer

```md
## Handoff Packet

- Unit: iteration-4 cli-bootstrap-implementation
- Source: cli-platform
- Target: reviewer
- Objective: Check that the proposed Python CLI bootstrap stays inside tooling-safe roots and does not leak into Unity package surfaces.
- Status: ready for review
- Completed Work: Added tooling-local Python package layout and usage docs.
- Relevant Artifacts: tools/mu3_cli/, pyproject.toml, docs/ai-agents/architecture.md
- Constraints: No `.asmdef` edits, no package manifest edits, no runtime/editor assembly changes.
- Open Questions: None
- Risks: Tooling dependency may be broader than necessary.
- Requested Review: reviewer
- Persistence Proposal: promote to repo after requested reviews
```