---
owns: "How a product idea is judged before adoption: the three idea scopes, the project values they read, verdicts, the eight criteria and their order, hand-overs to another scope or owner, choosing when the user delegates the choice, and what idea work must not touch"
volatility: evolving
reviewed: 2026-10-23
---

# Idea Evaluation

An idea for new or changed content is judged before anyone builds it: cheap checks first, then evidence, then comparison with alternatives. Passing produces a proposal; adopting it stays the user's decision.

## When

- running `new-content-propose`, `content-expansion-propose`, or `content-revision-propose`,
- the user asks whether an idea for new or changed content is worth doing.

## Route Away When

- what counts as evidence and how product metrics are read: [product-evidence.md](product-evidence.md),
- where proposals and their reference files live, their sections, and the report: [idea-records.md](idea-records.md),
- writing requirements for an adopted idea: the `requirements-spec-write` skill,
- several user-owned decisions are open about an idea already chosen: the `decision-interview` skill.

## Scopes

Content is anything a user does in the product: a feature, mode, flow, or command. The user may be an end user or a developer.

| Scope | Target | Reference that comes first | Skill |
|---|---|---|---|
| New content | Something users do that the product does not offer yet | Every existing content and how it is used | `new-content-propose` |
| Expansion | A new element (rule, mechanic, option) inside one existing content, to widen its variety | The elements comparable products put in that content, surveyed before choosing | `content-expansion-propose` |
| Revision | One authored value or rule of a unit (a level, screen, or step) of one existing content, to raise its quality | The content's own status: authored values, measurements, and user signals per unit | `content-revision-propose` |

## Project Values

The project supplies the yardsticks; the kit names only what each one measures. They live on the project wiki page `.ai/project/wiki/product-planning.md` (create it with `agentkit.py new page product-planning`). A skill stops and asks when a value its scope needs is empty; `none` with a reason is a value.

| Value | What it states | Scopes |
|---|---|---|
| Main content | The content users spend most time in; its unit of one use (for example a round, a task, a request); the systems one use needs, with links; the time one use takes; and what runs out when users leave | New content |
| Content inventory | Where every existing content is listed, unreleased ones included, and where each content's elements and rules are defined | All |
| Value flow | The kind of value users gain or spend (an earned currency, a spending budget, or none), its names, and the ledger or code that records where it enters and leaves | All |
| Metrics | Metric sources in read order, accounts excluded from statistics, the sign-in command per source, and the metered-query limit above which to ask; `none` before launch | All |
| Production cost | The heavy cost axes (for example art, screens, translations, external calls, rebuild time) and how each is obtained | All |
| Forms | The forms new content can take, extending existing content included, and the kinds of lack users can have (something to do, to gain, to see, a reason to return) | New content, expansion |
| Unit pipeline | How units of a content are authored, built, verified, and measured; each tool's limits; the properties every unit must keep (for example reproducible state); and how persisted data refers to units | Expansion, revision |
| External sources | Where comparable products and research are found | All |

## Verdicts

- `reject`: not added in this form. The report names what it duplicates or where it belongs.
- `defer`: not now. A short defer record names what would lift it, so the idea can be judged again. A defer at criteria 5 to 8 means evidence is missing, not that the idea is wrong; judge again when the evidence exists.
- `pass`: a proposal is written. Passing is not adoption: a proposal is no product rule and leads to no code, issue, or pull request until the user adopts it.

## Criteria

Criteria 1 to 3 read differently per scope:

| Criterion | New content | Expansion | Revision |
|---|---|---|---|
| 1. Overlap or target | Usage (what the user does, to achieve what) identical to an existing content: `reject`. Shared rewards or limits are not overlap | What the element constrains or enables and what releases it, as a pair, identical to an existing element's: `reject`. An aid the user invokes with no constraint in place (a hint, an undo, a shortcut) is a helper, not an element: `reject`, naming the file that defines helpers | The change does not stand as one existing value or rule: a new rule goes to `content-expansion-propose`, anything outside the content (value-flow values, flow between units) to the file that owns it, and this idea is `reject` |
| 2. Structure | One use needs several systems working together, as the main content does: `defer` | An existing rule or helper whose interaction with the element is undecided, a unit pipeline that can no longer build, verify, or measure the units, or a unit property the pipeline requires that the element breaks: `defer` | The change renumbers units, changes a unit count, or moves a published boundary so that recorded progress would point elsewhere: `defer` |
| 3. Duration or limits | One use is long compared with one use of the main content: `defer` | Time the element adds that the content's time model cannot explain (waiting, chance): `defer` | The change needs the unit pipeline beyond its limits: `defer`; moving a limit is its own idea. State the range of units to rebuild |

Criteria 4 to 8 read the same in every scope:

| Criterion | Asks | Defer when |
|---|---|---|
| 4. Cost | The production cost per axis and how each is obtained | The time is not worth it against the value criteria 5 to 8 established and the cheaper candidates of criterion 8. Not counting an axis because it is small for this idea does not pass it |
| 5. Value flow | Four cells: what users gain, where it is spent, what is expected, and the evidence | Any cell is empty |
| 6. Goal | Every claim carries evidence per [product-evidence.md](product-evidence.md), and the problem fits in one sentence | The problem has no evidence |
| 7. Form or means | At least two other forms or means for the same problem, extending existing content included; for expansion and revision, the other two scopes and helpers too, and for revision also a change to a rule every unit shares | Nothing shows the chosen one fits the problem better. "Other products have it" shows only that it can work |
| 8. Candidate | At least two other candidates of the same form or for the same target, compared on criteria 1 to 4 and on how much of the problem each solves | The only reasons are conditions every candidate meets (short, cheap), or the evidence was found only after choosing |

Two winners end in `defer` with a hand-over:

- The winner of criterion 7 is another scope, or, with no option well supported, another scope can test the same problem more cheaply: hand the problem to that scope's skill.
- The winner of criterion 7 or 8 adds no new usage (it only changes the entry point, placement, or wording of existing content): the reason is "the better answer is not new content", and the report hands the improvement to the user as a follow-up for that content's owner.

## Order

Judge in this order: 1, then 2 and 3, then 5, 6, 7, 8, and 4 last.

- 1 is the only unconditional reject, so it goes before any research; 2 and 3 need no research and are both checked.
- Criteria 5 to 8 need evidence: gather it in parallel before 5, for the compared forms and candidates as well as the chosen one.
- 7 needs the problem 6 sets; 8 compares inside the form 7 chose; 4 weighs cost against the value 5 to 8 set.
- Stop at a `reject`. At a `defer`, stop after that step.

## Delegated Choice

When the user asks for an idea without naming one, do not pick the content first: a pick made first collects only evidence that it can work.

1. Set the problem from internal metrics (criterion 6); with no metrics, from repository records; with neither, stop and ask.
2. Compare forms or means for that problem (criterion 7), then at least three candidates (criterion 8), finding external evidence for each before choosing.
3. Judge the chosen idea from criterion 1, reusing the comparisons of 7 and 8.

Asked for several ideas, set one problem per idea or compare different ideas for one problem; never repeat one idea with only the problem changed.

## Boundaries

- Judging an idea changes no code, generated content, or checked-in units. Run a tool that rewrites tracked output only in a disposable copy outside the checkout.
- Device, deployment, and other long-running runs follow «Scaling The Order» in [request-lifecycle.md](../operating-model/request-lifecycle.md).
