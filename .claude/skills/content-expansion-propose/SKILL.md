---
name: content-expansion-propose
description: "Judge one idea for a new element (rule, mechanic, option) inside one existing content before anyone adopts it: survey the elements comparable products put in that content first, reject an element whose constraint and release match an existing one, defer one with an undecided interaction or a pipeline that can no longer build, verify, or measure units, gather evidence, compare it with other means and surveyed candidates, weigh cost last, and write a proposal only when it passes. Use when one content needs more variety, or the user asks for a new element."
---
<!-- agentkit:generated from .ai/kit/core/skills/content-expansion-propose/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Propose Content Expansion

## Use When
- The user brings an idea for a new element inside one existing content and wants it judged before building.
- One content needs more variety and the user asks for a new element without naming one.

## Do Not Use When
- The idea is new content beside the existing ones → `new-content-propose`.
- The idea changes an existing value or rule of a unit → `content-revision-propose`.
- The element is already decided and needs requirements → `requirements-spec-write`.

## Inputs
- The content to expand, and the element in the user's words or the request to find one.
- The project values of the expansion scope.

## Steps
1. Read the project values for the expansion scope from `.ai/project/wiki/product-planning.md` per «Project Values» in [idea-evaluation.md](.ai/kit/core/wiki/workflows/idea-evaluation.md), including where this content's elements and helpers are defined and its unit pipeline. When the page is missing or a needed value is empty, stop and ask with the fields of «Project Values». Done when every needed value is present or the user gave it for this run.
2. Lay out the domain survey per «Reference Files» in [idea-records.md](.ai/kit/core/wiki/workflows/idea-records.md): reuse today's, otherwise extend the latest into a new file, otherwise build it. Hand long web research to a subagent and read the value flow meanwhile. Done when the survey meets the coverage that section sets.
3. Write the element as one paragraph. What it constrains or enables and what releases it comes from the user; plan how it appears in a unit, the first unit and the ramp, and any guidance, and mark them `provisional`. When the user delegated the choice, follow «Delegated Choice» in idea-evaluation.md, taking candidates from survey entries this product lacks. Read any earlier proposal or defer record on the same element.
4. Judge criterion 1: start from the survey's "this product has it" column, then compare the pair with every element and helper defined for the content. Done when each is compared; on `reject`, go to step 8, naming the matching element or the file that defines helpers.
5. Judge criteria 2 and 3. Write the integration table: for every existing rule and helper of the content, its current behavior and its behavior with the element. Then state where the element enters the unit pipeline (unit data, build, verification, measurement) and explain its time with the content's time model. Run pipeline tools only as «Boundaries» in idea-evaluation.md allows. Done when the table has no undecided cell or the verdict is `defer`; on `defer`, go to step 8.
6. Gather evidence in parallel: the value flow (how the element changes the use of what users gain or spend) and internal metrics on the problem inside the content (where users stall, leave, or retry) per «Reading Metrics» in [product-evidence.md](.ai/kit/core/wiki/workflows/product-evidence.md). The survey already holds the external evidence; deepen it only for the elements being compared. Done when each claim about the value flow and the problem has an evidence row or is marked `unread` or `none`.
7. Judge criteria 5, 6, 7, 8, and 4 in that order per «Criteria» in idea-evaluation.md, taking the candidates for 8 from survey entries this product lacks; stop at the first `defer`. On `pass`, write the proposal per «Proposal» in idea-records.md, with the integration table from step 5.
8. Report per «Report» in idea-records.md, including the survey path.

## Output
- The verdict with its criterion and evidence, the survey path, and the path of the proposal or defer record.
- Values left `unread` or `undecided`, and any problem handed to another scope.
