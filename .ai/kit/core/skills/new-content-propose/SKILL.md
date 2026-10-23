---
name: new-content-propose
description: "Judge one idea for new content across the whole product before anyone adopts it: state it in one paragraph, reject it when its usage matches existing content, defer it when one use needs as many systems as the main content or runs long, gather value-flow, metric, and external evidence, compare its form and candidates, weigh production cost last, and write a proposal only when it passes. Use when the user brings an idea for new content or a feature and wants it judged before building, or asks for one to be found."
category: product
volatility: evolving
reviewed: 2026-10-23
---

# Propose New Content

## Use When
- The user brings an idea for content or a feature the product does not offer yet and wants it judged before building.
- The user asks for an idea for new content without naming one.

## Do Not Use When
- The idea adds an element inside one existing content → `content-expansion-propose`.
- The idea changes an existing unit of one content → `content-revision-propose`.
- The feature is already decided and needs requirements → `requirements-spec-write`.

## Inputs
- The idea in the user's words, or the request to find one.
- The project values of the new-content scope.

## Steps
1. Read the project values for the new-content scope from `.ai/project/wiki/product-planning.md`, per «Project Values» in [idea-evaluation.md](../../wiki/workflows/idea-evaluation.md). When the page is missing or a needed value is empty, stop and ask: list the empty values, candidate documents found in the project wiki and docs, and the fields of «Project Values». Done when every needed value is present or the user gave it for this run.
2. Write the idea as one paragraph. What the user does and what it achieves comes from the user; plan the rest (time of one use, unlock conditions and limits, what users gain, entry point) and mark it `provisional`. When not even the user's action can be read from the request, ask only that. When the user delegated the choice, follow «Delegated Choice» in idea-evaluation.md first. Read any earlier proposal or defer record on the same content per «Location» in [idea-records.md](../../wiki/workflows/idea-records.md).
3. Judge criterion 1: list every existing content from the inventory, unreleased ones included, with its usage in one line each, and compare. Done when every listed content has a usage line; on `reject`, go to step 7, naming the content and usage it duplicates.
4. Judge criteria 2 and 3: count the systems one use needs against the main content's, and estimate the time of one use against the main content's. Check both; on `defer`, go to step 7.
5. Gather evidence in parallel, with subagents when it is heavy, within «Capacity» in [concurrency.md](../../wiki/operating-model/concurrency.md): the value flow from its ledger, internal metrics per «Reading Metrics» in [product-evidence.md](../../wiki/workflows/product-evidence.md), and external examples for every form and candidate to be compared. Done when each claim about the chosen idea and every compared form and candidate has an evidence row or is marked `unread` or `none`.
6. Judge criteria 5, 6, 7, 8, and 4 in that order per «Criteria» in idea-evaluation.md, stopping at the first `defer`. On `pass`, write the proposal per «Proposal» in idea-records.md.
7. Report per «Report» in idea-records.md.

## Output
- The verdict with its criterion and evidence, and the path of the proposal or defer record.
- Values left `unread` or `undecided`, and any improvement handed over instead of new content.
