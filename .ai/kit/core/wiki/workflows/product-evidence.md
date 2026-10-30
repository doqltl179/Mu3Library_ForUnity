---
owns: "What counts as evidence for a product claim and how production data is read for analysis or evidence: evidence kinds and their fields, time windows, production data reads and privacy, read order by cost, blocked sign-in, and attributing an effect"
volatility: evolving
reviewed: 2026-10-23
---

# Product Evidence

A product claim stands only on evidence someone can check again. Idea evaluation, specifications, and reports that argue from users' behavior use this page.

## When

- a proposal, spec, or report claims something about users, usage, or comparable products,
- reading product metrics to support or test such a claim,
- reading production data for analysis, a report, or a claim, by any owner.

## Route Away When

- defining metrics, writing new analytical queries, reading out experiments, or delivering a report or dashboard as an artifact: `data-analyst`,
- personal data and secrets in general: «Secrets And Sensitive Data» in [integrity.md](../principles/integrity.md),
- sources for kit guidance itself: [external-guidance.md](../evolution/external-guidance.md).

## Evidence Kinds

| Kind | Record with it |
|---|---|
| Internal metric | What was measured, the period, the value, how it was read (source and query or filter), and the date of the data |
| Repository record | A link to the file that owns the fact |
| External example | Product, maker, what the thing does in that product, and the source URL; for research, author, year, and source |

- Memory that cannot be sourced is not evidence. Drop a claim that has no evidence, or mark it `undecided`.
- Mark a metric that could not be read `unread`, and one that does not exist `none`.
- Read at least the last month of a metric, up to three months: a few days swing with one campaign or event. With less data, use what exists, state the period, and record when to judge again.
- Before launch, with no metrics, evidence comes from repository records and external examples.

## Production Data

These hold for every read of production data for analysis, a report, or a claim, by any owner. Inspecting one record to diagnose a defect follows the `bug-diagnose` skill instead.

- Read through the warehouse, a read replica, exports, dashboards, or the analytics service, bounded by date or partition filters; never record by record from the operational store.
- Report aggregates only: suppress groups small enough to identify a person, and exclude the accounts the project lists. Other personal data follows «Secrets And Sensitive Data» in integrity.md.

## Reading Metrics

This covers reading existing sources to back a claim: local copies, exports, dashboards, saved queries, and filtered reads of them. A new analytical query, a metric definition, or an experiment readout is `data-analyst` work: hand it off per [handoff-contract.md](../operating-model/handoff-contract.md), or mark the value `unread`.

1. Read in the order the project's metric sources give. Without one, read by cost: free local copies and exports, then quota-limited services, then metered queries.
2. Move to a costlier source only when the cheaper one cannot answer the question. A stale or blocked cheaper source is not a reason to ask the same question of a costlier one.
3. Estimate a metered read before running it (a dry run or the source's size estimate). Above the project's limit, or when no limit is set, stop and ask, stating the estimate and the question it answers.
4. When sign-in blocks a source, stop and end the turn with the error line and the project's sign-in command; never reach the same data with other credentials. Continue with the value `unread` only when the user says so.
5. Record the query and the data date next to each value, so a later judgment can read the same value again.

## Interpreting Metrics

- Confirm what a signal records before counting with it: when it is written and what triggers it. A counter written only on success cannot count failures.
- Before crediting a change in a metric at one place to one element, split by the other factors that also differ there (account age, another change at the same point). When the effect disappears after the split, or another change coincides, the attribution has no evidence.
- To tell why users stop, follow the same users over time instead of reading one snapshot: stopping after a failure and stopping without one have different causes.
