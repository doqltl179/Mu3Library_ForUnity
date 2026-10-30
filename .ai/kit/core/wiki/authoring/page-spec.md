---
owns: "Wiki page schema: placement by question shape, frontmatter, section shape, section indexes, and project wiki pages"
volatility: evolving
reviewed: 2026-10-09
---

# Wiki Page Spec

A wiki page owns one question. Section indexes are generated from page frontmatter, so a correctly written page is reachable without manual registration.

## When

- adding, splitting, merging, or editing a wiki page in `core/wiki/` or `.ai/project/wiki/`.

## Route Away When

- adding or updating a page step by step: the `kit-page-add` or `kit-page-update` skill,
- whether the content should exist at all: [ssot.md](../principles/ssot.md),
- phrasing: [agent-first-writing.md](../principles/agent-first-writing.md).

## Placement

1. Find the section whose index `owns` line matches the question shape (see [wiki README](../README.md)).
2. If a page in that section already owns the question, extend it instead of adding a page.
3. Add a new section only when at least three pages would belong to it and no section owns the question shape; that is a structural change reviewed by `role-governor`.

## Frontmatter

| Field | Required | Rule |
|---|---|---|
| `owns` | yes | One sentence naming the question this page answers. Section indexes show it verbatim |
| `volatility` | yes | See [freshness-policy.md](../evolution/freshness-policy.md) |
| `reviewed` | yes | `YYYY-MM-DD` of the last verification against reality and sources |
| `sources` | when `volatile` | URLs of the primary sources the page depends on |
| `applies_to` | project rules only | Path globs; the page is rendered as a path-scoped rule that tools load whenever matching files are touched. Only pages in `.ai/project/wiki/rules/` take it |

## Body Shape

```markdown
# <Title>

<1–2 lines: what the page is for.>

## When
- <situations that send a reader here>

## Route Away When
- <situation>: <link to the owner>

## <Rule sections>

## Notes  (optional)
```

- Mark tables that `agentkit.py` parses with `<!-- agentkit:table NAME -->` on the line before the table. Do not change their column order without updating the tool.
- Keep pages within the budget in [context-budget.md](../operating-model/context-budget.md). When a page outgrows it, split by question, not by length.

## Section Indexes

- Each section folder has a `README.md` with `owns` frontmatter, a short intro, and an empty generated region `<!-- agentkit:begin index -->` / `<!-- agentkit:end index -->`.
- `agentkit.py sync` fills the region with every page in the folder and its `owns` line. Never edit inside the region.
- The root [wiki README](../README.md) lists sections the same way.
- Add a row to [START.md](../../START.md) only when the page is the first stop for a common kind of task.

## Project Wiki

- Project knowledge (architecture overview, domain glossary, decision records, local conventions) lives in `.ai/project/wiki/` with the same spec.
- A project page never restates a core rule; it records project facts and the parameters core leaves open.
- Project rules tied to paths (for example rules for one package or engine folder) go in `.ai/project/wiki/rules/` with `applies_to`, so tools load them exactly when those files are touched. `agentkit.py new page rules/<name>` adds an empty `applies_to`, and `check` warns until it lists the paths. Rules for every task go in `project.guardrails` instead.
