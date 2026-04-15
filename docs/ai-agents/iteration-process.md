# Iteration Process

## Required Loop

All non-trivial AI-agent framework work in this repository follows the same loop.

### 1. Preceding Work

Work on one feature unit at a time.

Examples:

- one instruction file,
- one new agent,
- one skill,
- one control-plane rule,
- one human-facing architecture document.

The unit should be small enough that the review can answer whether it belongs in the system.

### 2. Suitability Review

After the unit is added or revised, review it against the whole structure.

Owner of the structural gate:

- `role-governor` for continue-or-rework on framework fit.
- `reviewer` when the unit also changes quality, verification, release, or compatibility expectations.

Review questions:

- Does the new artifact overlap with an existing owner?
- Does it create a second control plane by accident?
- Does it blur repository boundaries such as runtime/editor, package/tooling, or docs/release?
- Does it add process complexity without adding meaningful coordination value?
- Does it require a catalog or routing update?

### 3-1. If Suitable

Proceed to the next bounded unit only after the structural gate returns `continue`.

Required actions:

- mark the current unit complete,
- update the catalog if the set of agents changed,
- carry forward only the unresolved structural risks.

### 3-2. If Not Suitable

Stop expansion and rework the structure first.

Required actions:

- document the conflict or ambiguity,
- decide whether the new artifact should be split, merged, narrowed, or removed,
- review whether the preceding unit must be reworked,
- restart the loop only after the boundary is clear again.

## Decision Table

| Situation | Decision |
|---|---|
| Clear owner, clear boundary, measurable value | Continue |
| Useful idea but blurred responsibility | Narrow scope and review again |
| Same responsibility already owned elsewhere | Merge or reject |
| Orchestrator and planner both try to route work | Narrow task-planner to unit planning and keep routing in orchestrator |
| New manager tries to govern and execute the same work | Split into governance and execution roles |
| Adds repo tooling without touching product code | Acceptable if isolated under tooling scope |

## Stop Conditions

Pause the rollout when any of the following appears:

- two agents claim the same primary artifact,
- an agent cannot state its non-goals,
- a governance agent starts implementing domain work,
- an execution agent starts redefining routing rules,
- the reviewer cannot tell who should own the next change.