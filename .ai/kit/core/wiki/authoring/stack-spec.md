---
owns: "Stack pack schema and kinds: how language, framework, and infrastructure knowledge is written, bound to paths, and kept current"
volatility: evolving
reviewed: 2026-10-09
---

# Stack Pack Spec

A stack pack holds the knowledge an owner needs to work correctly in one language, framework, or infrastructure tool. Packs are composed into agents through the project binding and rendered as path-scoped rules for tools that support them.

## When

- adding or editing a stack pack,
- binding a stack to paths or owners in a project.

## Route Away When

- adding a pack step by step: the `kit-stack-add` skill,
- binding syntax in the profile: [project-overlay.md](../integration/project-overlay.md),
- how path-scoped rules are rendered per tool: [tool-adapters.md](../integration/tool-adapters.md).

## Kinds

<!-- agentkit:table stack-kinds -->
| Kind | Folder | Covers |
|---|---|---|
| `language` | `core/stacks/languages/` | A programming or query language and its ecosystem tooling |
| `framework` | `core/stacks/frameworks/` | An application, UI, game-engine, data, or ML framework |
| `infra` | `core/stacks/infra/` | Containers, orchestration, infrastructure as code, CI services |

## Frontmatter

| Field | Required | Rule |
|---|---|---|
| `id` | yes | Lowercase kebab-case, equal to the file name without `.md` |
| `title` | yes | Display name |
| `kind` | yes | A value from «Kinds» |
| `applies_to` | yes | Default path globs where the pack is relevant; `[]` when paths cannot identify it. The profile may override them |
| `related` | no | Ids of packs often used together |
| `volatility` | yes | Normally `volatile`; see [freshness-policy.md](../evolution/freshness-policy.md) |
| `reviewed` | yes | `YYYY-MM-DD` |
| `sources` | yes | Official documentation and release-note URLs used to verify the pack |

## Body Shape

```markdown
# <Title>

## Detect
- <how to learn the project's actual setup: config files, versions, package manager, scripts>

## Conventions
- <durable, idiomatic rules an expert follows>

## Verify
- <typical checks; prefer the profile's `commands.*` when set>

## Pitfalls
- <mistakes agents commonly make in this stack, each with the correct alternative>

## Version Notes
- <version-sensitive facts, each with "as of <date>" and a source>
```

## Rules

- Lead with `Detect`: the project's real configuration outranks the pack's defaults.
- Prefer durable conventions. Put anything that changes between releases in `Version Notes` with a date and source, so freshness review can find it.
- Do not restate surface boundaries or general engineering rules; agent cards and wiki pages own those.
- Packs may be auto-loaded whenever a matching file is touched, so keep them within the budget in [context-budget.md](../operating-model/context-budget.md) and keep `applies_to` narrow.
