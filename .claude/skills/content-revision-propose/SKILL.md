---
name: content-revision-propose
description: "Judge one idea for changing an existing part of one content (an authored value or rule of a unit such as a level, screen, or step) before anyone adopts it: lay out a status table of the content's units first, reject a change that needs a new rule or reaches outside the content, defer one that breaks recorded progress or exceeds the unit pipeline's limits, gather evidence including the value's history, compare it with other means and other changes to the same target, weigh rebuild cost last, and write a proposal only when it passes. Use when a unit's user signals fall short."
---
<!-- agentkit:generated from .ai/kit/core/skills/content-revision-propose/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Propose Content Revision

## Use When
- A unit's user signals (reach, stalls, exits) fall short, or the user asks which unit of a content to change.
- The user brings an idea for changing a unit of one existing content and wants it judged before building.

## Do Not Use When
- The unit behaves wrongly or crashes → `bug-diagnose`.
- The unit is too slow or uses too many resources → `performance-investigate`.
- The change needs a new rule or element → `content-expansion-propose`; the idea is new content → `new-content-propose`.

## Inputs
- The content, and the unit and change in the user's words, or the request to find them.
- The project values of the revision scope.

## Steps
1. Read the project values for the revision scope from `.ai/project/wiki/product-planning.md` per «Project Values» in [idea-evaluation.md](.ai/kit/core/wiki/workflows/idea-evaluation.md), including the content's unit pipeline and how persisted data refers to units. When the page is missing or a needed value is empty, stop and ask with the fields of «Project Values». Done when every needed value is present or the user gave it for this run.
2. Lay out the status table per «Reference Files» in [idea-records.md](.ai/kit/core/wiki/workflows/idea-records.md): reuse today's, otherwise build it from the authored and measured values in this checkout and the user signals read per [product-evidence.md](.ai/kit/core/wiki/workflows/product-evidence.md). Under it, note per unit what else happens at that point (messages, guidance, unlocks, interruptions), and follow users over time per «Interpreting Metrics» there. Done when every unit has its row and the same cells are compared with the previous table.
3. Write the change as one paragraph. Which unit and what to change comes from the user; plan the new value and the range of units to rebuild, and mark them `provisional`. When the user delegated the choice, follow «Delegated Choice» in idea-evaluation.md, taking the units where the status table shows a problem and separating what else happens there; when only the change was delegated, start at criterion 7. Read any earlier proposal or defer record on the same unit.
4. Judge criterion 1: the change must stand as one existing value or rule of the content. On `reject`, go to step 8, naming the scope or file it belongs to.
5. Judge criteria 2 and 3: check that persisted references to units stay valid and that the change fits the unit pipeline's limits, and write the range of units to rebuild. Check both; on `defer`, go to step 8.
6. Gather evidence in parallel: the value flow, internal metrics starting from the status table, external examples of how comparable products build the same point for every candidate (or `none`), and the value's history (`git log -p` on the file that holds it, and the record that set it). Measure a candidate with the project's tools only as «Boundaries» in idea-evaluation.md allows; when it cannot be measured, keep the command for the proposal and mark the value `undecided`. Done when each claim about every candidate has an evidence row or is marked `unread`, `undecided`, or `none`.
7. Judge criteria 5, 6, 7, 8, and 4 in that order per «Criteria» in idea-evaluation.md, estimating the rebuild time for 4 from the range in step 5; stop at the first `defer`. On `pass`, write the proposal per «Proposal» in idea-records.md, taking the "now" values from the status table.
8. Report per «Report» in idea-records.md, including the status table path.

## Output
- The verdict with its criterion and evidence, the status table path, and the path of the proposal or defer record.
- Values left `unread` or `undecided`, and any problem handed to another scope or owner.
