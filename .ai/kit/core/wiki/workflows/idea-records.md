---
owns: "Where idea proposals, defer records, and their reference files are kept, their file names, the sections a proposal has in each scope, judging an earlier idea again, and the report after a verdict"
volatility: evolving
reviewed: 2026-10-23
---

# Idea Records

Idea records are working records, like plans: they stay out of version control. An adopted idea enters the repository as a spec, never as its proposal.

## When

- writing a proposal, a defer record, a domain survey, or a status table,
- reporting the verdict on an idea.

## Route Away When

- the criteria and verdicts: [idea-evaluation.md](idea-evaluation.md),
- what each piece of evidence records: [product-evidence.md](product-evidence.md),
- the idea was adopted and needs requirements: the `requirements-spec-write` skill.

## Location

- Keep idea records in the main checkout's `.ai/tasks/ideas/`. Inside the repository a proposal reads as a product rule and duplicates the spec written on adoption, and a worktree's `.ai/tasks/` disappears with the worktree.
- Before writing, confirm the path is ignored: `git -C <main checkout> check-ignore -q .ai/tasks/ideas/probe.md`. When it is not, write nothing and report it.
- Closing a plan per «Closeout» in [planning.md](planning.md) leaves idea records in place; deleting one is the user's decision.
- Name a proposal `<YYYY-MM-DD>-<scope>-<kebab summary>.md` with scope `content`, `expansion`, or `revision`, and a defer record the same way with the suffix `-deferred`. Name reference files `<YYYY-MM-DD>-expansion-survey-<content>.md` and `<YYYY-MM-DD>-revision-status-<content>.md`.
- Judging the same idea or reference again writes a new file whose header names the earlier one under `Previous:`.
- Link repository files with paths relative to the record.

## Reference Files

- **Domain survey** (expansion): the elements comparable products put in this content. Cover at least five products from different makers and two from neighboring categories. For each element, record what it constrains or enables and what releases it, the products that use it, the source URL, where it first appears when the source says, and whether this product already has it (yes, similar with the difference, no), judged by the pair of criterion 1 in [idea-evaluation.md](idea-evaluation.md). List helpers apart. Reuse today's survey; otherwise extend the latest one into a new file.
- **Status table** (revision): one row per unit or group of units, with authored values, measured values, and user signals (reached, stalled, left) and their period and data date. Compare the same cells with the previous table. Reuse today's table.

## Proposal

The header states the verdict and its date, the scope, the baseline (integration branch and commit), and `Previous:`. Sections, in this order:

1. **One line**: what and why, in one sentence.
2. **What**: new content: what the user does, one use, its time, unlock conditions and limits, and the entry point. Expansion: what the element constrains or enables, what releases it, and where it sits. Revision: a table of each value, now and then.
3. **Why**: the problem, with evidence for every claim.
4. **Why this form or means**: a table of option, how it solves the problem, what it leaves unsolved, and evidence; then the reason for the choice.
5. **Why this candidate**: a table of candidate, usage, criteria 1 to 4, how much of the problem it solves, and evidence; then the reason.
6. **Scope section**: expansion: the integration table (each existing rule and helper, its current behavior, its behavior with the element) and the introduction (where the element first appears, how it ramps up, and whether users learn it by using it or need guidance, which then belongs to the idea). Revision: the reach (users before, at, and past the unit) and how the effect will be measured.
7. **How it is built**: a provisional estimate of the rules and systems, new or reused, and the cost per axis with how it is obtained. The design itself is decided after adoption.
8. **Value flow**: what users gain, where it is spent, what is expected, and the evidence.
9. **Expected effect**: what should change and how it can be seen after release; say so when no measurement exists.
10. **Criteria**: a table in judging order with each verdict and its evidence.
11. **Evidence**: internal, repository, and external, per «Evidence Kinds» in product-evidence.md.
12. **Undecided**: values not decided; values set only to proceed are marked `provisional`; with under a month of data, the condition for judging again.

## Defer Record

A `defer` writes a short record so the next judgment starts from it: the same header with the verdict `defer`, the one line, the criterion that deferred it with its evidence, what would lift it, the comparison tables of criteria 7 and 8 when they were reached, the evidence gathered so far with its queries and data dates, and the condition for judging again.

## Report

The report follows «Report Shape» in [handoff-contract.md](../operating-model/handoff-contract.md), with the verdict as its outcome.

- `pass`: the proposal path, its one line, what stayed `unread` or `undecided`, and that adoption is the user's call; once adopted, the next step is a spec with `requirements-spec-write`.
- `defer`: first write the defer record per «Defer Record»; then report its path, the criterion, what would lift it, and any problem or improvement handed over.
- `reject`: what it duplicates, or where it belongs. A `reject` leaves no record, so the report is its only record: keep its evidence complete.
- A value that is `unread` and that the user can unblock goes under what is left for the user: the unblocking command in one block, the error line, and what to do once it runs.
