---
owns: "Skill schema and categories, the boundary between a skill, an agent, and a wiki page, and how a skill references policy"
volatility: evolving
reviewed: 2026-10-09
---

# Skill Spec

A skill is an executable procedure with a stable input and output: ordered steps any suitable owner can run. Skills hold order; the wiki holds rules.

## When

- adding or editing a skill (core or project-local),
- deciding whether a procedure belongs in a skill or a wiki page.

## Route Away When

- adding a skill step by step: the `kit-skill-add` skill,
- a role rather than a procedure: [agent-spec.md](agent-spec.md),
- a rule or policy: [page-spec.md](page-spec.md).

## Skill, Agent, or Page

| The thing is… | It is a… |
|---|---|
| Who decides and owns an artifact | Agent card |
| What must or must not be true | Wiki page (policy) |
| How to do a task, step by step, with inputs and outputs | Skill |

A skill links to the policy it applies instead of restating it. Example: `git-commit` says "compose the message per «Commit Convention» in git-workflow.md"; it does not copy the convention.

## Categories

<!-- agentkit:table skill-categories -->
| Category | Covers |
|---|---|
| `git` | Local Git operations: branches, commits, conflicts |
| `github` | Hosting-platform operations: issues, pull requests, reviews on the platform |
| `code-change` | Procedures that change code safely: refactor, migrate, upgrade, diagnose, test, optimize |
| `review` | Review procedures for each review lens |
| `docs` | Project documentation and decision records |
| `localization` | Translation and localization into other languages |
| `kit` | Creating, auditing, installing, updating, and evolving the kit and its overlay |
| `planning` | Decomposition, plans, onboarding to a codebase |
| `product` | Requirements and specification work |
| `release` | Versioning and release execution |
| `ops` | CI and operational diagnosis |

## Frontmatter

| Field | Required | Rule |
|---|---|---|
| `name` | yes | Lowercase kebab-case, ≤ 64 chars, equal to the folder name, unique across core and project. Prefix `git-`, `github-`, or `kit-` for those categories |
| `description` | yes | ≤ 600 chars. What it does, then "Use when …". Tools match requests against this text |
| `category` | yes | A value from «Categories» |
| `volatility` | yes | See [freshness-policy.md](../evolution/freshness-policy.md) |
| `reviewed` | yes | `YYYY-MM-DD` |

Only `name` and `description` are rendered into tool folders; the other fields are kit metadata.

## Body Shape

```markdown
# <Title>

## Use When
- <triggers>

## Do Not Use When
- <situation> → `<other skill>` or <page link>

## Inputs
- <what the runner must know or have before step 1>

## Steps
1. <imperative step; link policy instead of restating it>

## Output
- <what exists or is reported when the skill finishes>
```

- Keep skills within the budget in [context-budget.md](../operating-model/context-budget.md).
- Supporting files (checklists, scripts, templates used only by this skill) live in the skill folder and are linked relatively.
- A step that needs a project command names the profile key (for example `commands.test`), not a literal command.
- Project-local skills must not reuse a core skill name. To change a core skill, propose it upstream; to replace it locally, disable it in the profile and add a differently named local skill.

## Placement

- Core: `core/skills/<name>/SKILL.md`.
- Project: `.ai/project/skills/<name>/SKILL.md`.
