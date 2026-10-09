---
name: kit-role-audit
description: "Audit agent boundaries for role-governor: list the agents involved, compare owned artifacts, decisions, and outputs pairwise, grade overlap, find missing ownership and routing ambiguity, decide keep, narrow, split, merge, or reject per agent, and issue a continue or rework verdict with required edits. Use when an agent is proposed, split, merged, or narrowed, or when routing keeps picking two owners or none."
---
<!-- agentkit:generated from .ai/kit/core/skills/kit-role-audit/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Audit Roles

## Use When
- `kit-agent-add` submits a proposed card, or a card is being split, merged, or narrowed.
- Routing repeatedly finds two owners for one task, or none.
- A department is added, removed, or has its coverage changed.

## Do Not Use When
- Picking an owner for one task → «Selection Procedure» in [routing.md](.ai/kit/core/wiki/operating-model/routing.md).
- The question is a stack pack, skill, or page placement only → the matching `kit-*-add` skill.

## Inputs
- The proposal or the cards under question, and the evidence that triggered the audit: sample tasks and where each was routed.
- The generated catalog `.ai/generated/catalog.md` and the profile's `[bindings.*]`.

## Steps
1. List the agents involved: the subject card, its `extends` base, every agent its `Does Not Own` names, and every card whose `Owns` or description shares a key noun with it (`rg` over core and project agents).
2. For each agent, note its primary artifacts, decisions, outputs, and bound paths.
3. Compare every pair and grade the overlap:

   | Grade | Meaning |
   |---|---|
   | none | Nothing shared |
   | handoff | A shared boundary that both cards route explicitly via `Does Not Own` |
   | partial | At least one artifact or decision claimed by both |
   | full | One card's `Owns` is contained in the other's |
4. Check missing ownership: each `Does Not Own` target exists, each handed-off concern has an owner, and each evidence task has one.
5. Check routing: run «Selection Procedure» in [routing.md](.ai/kit/core/wiki/operating-model/routing.md) on each evidence task using only the descriptions; each must resolve to exactly one agent.
6. Check boundary violations: list each evidence task where a role acted outside its card.
7. Check a new or changed core card against the criteria under «What To Add» and the «Granularity Model» in [agent-spec.md](.ai/kit/core/wiki/authoring/agent-spec.md).
8. Decide per agent:

   | Decision | When |
   |---|---|
   | keep | No partial or full overlap, and routing resolves uniquely |
   | narrow | Partial overlap that moving items to `Does Not Own` removes |
   | split | One card owns two surfaces that route to different tasks |
   | merge | Full overlap, or two cards always selected together |
   | reject | The proposal owns nothing new, or another «What To Add» row fits |
9. Issue `continue` when every decision is keep and no gap or violation remains; otherwise issue `rework`.
10. For `rework`, list each required edit as: file, section, change, reason. Do not apply the edits; the requester does.

## Output
- An audit record: agents, the pairwise grade table, findings by type (overlap, gap, ambiguity, violation) with file references, and the decision per agent.
- The verdict (`continue` or `rework`) and the required edits.
