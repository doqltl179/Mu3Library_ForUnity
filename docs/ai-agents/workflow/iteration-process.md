# Iteration Process

## When

- non-trivial AI-agent framework work changes routing, owners, instructions, prompts, skills, or wiki structure,
- a proposed workflow asset might become a durable owner,
- a framework unit needs continue-or-rework disposition.

## Route Away When

- choosing the owner: [routing/README.md](../routing/README.md),
- writing a handoff packet: [handoff-contract.md](../contracts/handoff-contract.md),
- checking stable rationale: [architecture.md](../architecture.md).

## Required Loop

1. Pick one bounded unit.
2. Edit only that unit and directly required router/catalog references.
3. Run structural suitability through `role-governor`.
4. If `continue`, proceed to the next unit.
5. If `rework`, stop expansion and narrow, merge, remove, or re-scope the unit first.

Examples of a bounded unit: one instruction file, one agent spec, one workflow asset, one router page, or one stable architecture update.

## Suitability Questions

- Does the unit overlap with an existing owner?
- Does it leave a missing owner or ambiguous route?
- Does it create a second control plane?
- Does it blur repository boundaries such as runtime/editor, package/tooling, docs/release, or samples/core?
- Does it require a catalog, router, instruction, prompt, or skill update?
- Does it add process complexity without meaningful coordination value?

## Decision Table

| Situation | Decision |
|---|---|
| Clear owner, boundary, and value | Continue |
| Useful idea with blurred responsibility | Narrow and review again |
| Responsibility already owned elsewhere | Merge or reject |
| Governance owner starts delivery work | Split governance from execution |
| Execution owner starts redefining routing | Route back to `orchestrator` |
| Tooling stays auxiliary and isolated | Acceptable with explicit safe roots |

## Stop Conditions

- two agents claim the same primary artifact,
- an agent cannot state non-goals,
- governance starts implementing domain work,
- execution starts changing routing ownership,
- reviewer cannot tell who owns the next change.
