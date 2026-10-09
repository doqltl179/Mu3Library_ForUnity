---
owns: "How to select the owner for a unit of work, break ties, and act when no owner fits"
volatility: evolving
reviewed: 2026-10-09
---

# Routing

Owner selection procedure. The inventory of owners is not here: it is generated into `.ai/generated/catalog.md` (active in this project) and [agents/CATALOG.md](../../agents/CATALOG.md) (every core card).

## When

- a unit of work needs an owner,
- two owners both look plausible,
- no owner seems to fit.

## Route Away When

- splitting a request into units first: the `work-decompose` skill,
- how the chosen owner then runs: [delegation.md](delegation.md),
- adding or changing an owner: [agent-spec.md](../authoring/agent-spec.md).

## Selection Procedure

1. Name the primary artifact or decision the unit produces (for example "a new API endpoint", "a migration", "a release tag").
2. Find the owners whose `Owns` covers that artifact: scan descriptions in `.ai/generated/catalog.md`, then read only the candidate cards.
3. Prefer a project-local card that `extends` the candidate when its narrowed scope covers the unit.
4. Prefer the narrowest owner: a surface specialist over a cross-cutting one, an execution owner over a governance one.
5. Check the candidate's `Does Not Own`. If the unit's dominant concern is listed there, follow the arrow to that owner.
6. If the unit still spans two owners, it is two units: split it, or send it to `orchestrator`.

## Tie-Breakers

| Situation | Owner |
|---|---|
| Work is mostly in one surface with small edits in another | The dominant surface's owner, handing the small edit off via the handoff packet |
| A shared contract (API schema, event format, data contract) changes | `software-architect` decides it; each side's surface owner implements it. Exception: contracts on a pipeline's own outputs belong to `data-engineer` |
| Persistent-connection code (WebSocket, SSE, streams) in any client or server | `realtime-engineer`; rendering the streamed data stays with the surface owner; firmware-side connection code belongs to `embedded-engineer` |
| Data access versus the database itself | ORM models and queries in application code: `backend-api-engineer`; schema, migrations, indexes, query tuning: `database-engineer` |
| Instrumentation and tracking events | The surface owner adds them; `observability-engineer` owns operational conventions; `data-analyst` owns business event names and properties (the tracking plan); `software-architect` owns event formats shared across components |
| Build, package, and publish | Pipelines, signing, and packaging jobs: `ci-cd-engineer`; versions, release notes, and publication: `release-manager` |
| Tests are the deliverable (framework, suite, harness) | `test-automation-engineer`; tests that accompany a change stay with the change owner |
| Performance is the stated goal with a measurable target | `performance-engineer`; incidental efficiency stays with the surface owner |
| The kit or overlay docs change | `kit-librarian`; structural ownership questions go to `role-governor` |
| Human-facing docs change without code | `technical-writer` |
| Text in another language (localized docs, UI strings, product or game text) | `localization-specialist` translates from the source; the source stays with `technical-writer` or `ux-designer`; i18n code stays with the surface owner |

## No Owner Fits

1. Check inactive core owners listed at the end of the Owners section in `.ai/generated/catalog.md`. If one fits, propose enabling it in `.ai/project/profile.toml`.
2. Otherwise adopt the closest owner for this unit only, state the stretch in the report, and record the gap for `role-governor`.
3. Never widen a card's scope silently to absorb the unit.

## Selecting Skills

After the owner is chosen, scan the skills in `.ai/generated/catalog.md` and run any whose trigger matches the unit.
