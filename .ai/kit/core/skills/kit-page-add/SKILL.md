---
name: kit-page-add
description: "Add a wiki page to the core kit or the project wiki: confirm no page owns the question yet, place it by question shape, scaffold it, write it to the page spec, replace duplicate copies elsewhere with links, then regenerate indexes and validate. Use when a rule, policy, or project fact has no owning page, or when a page must be split by question."
category: kit
volatility: evolving
reviewed: 2026-10-09
---

# Add Wiki Page

## Use When
- A rule, policy, or project fact keeps being needed and no page owns it.
- A page outgrew its line budget and must be split by question.

## Do Not Use When
- A page already owns the question → `kit-page-update`.
- The content is a role or a procedure, not a rule → see «Skill, Agent, or Page» in [skill-spec.md](../../wiki/authoring/skill-spec.md), then `kit-agent-add` or `kit-skill-add`.
- The page belongs in the core kit but you are in an installed project, where `.ai/kit/` is read-only → `kit-upstream-propose`.
- A single correction or discovery, not yet a rule → `kit-lesson-capture`.

## Inputs
- The question the page answers, as one sentence: the future `owns` line.
- Target layer: core (kit repository only) or project (`.ai/project/wiki/`).
- Primary sources for every external fact the page will state.

## Steps
1. Search for an existing owner per rule 3 of «Rules» in [ssot.md](../../wiki/principles/ssot.md): `rg` the key terms over the core wiki and `.ai/project/wiki/`, then read the section indexes. If a page owns the question, stop and run `kit-page-update`.
2. For a core page, choose the section per «Placement» in [page-spec.md](../../wiki/authoring/page-spec.md); a new section goes to `role-governor` first, as that section requires.
3. Scaffold a core page with `agentkit.py new page <section>/<name> --core`, or a project page with `agentkit.py new page <name>`.
4. Write frontmatter and body per [page-spec.md](../../wiki/authoring/page-spec.md); a project page also follows «Project Wiki» there. Phrase it per [agent-first-writing.md](../../wiki/principles/agent-first-writing.md).
5. Set `volatility` per «Metadata» in [freshness-policy.md](../../wiki/evolution/freshness-policy.md) and list `sources` for a volatile page. Tag scaffold rules per «Rule Types» in ssot.md.
6. Fill `Route Away When` with links to neighbor pages; add a route back to this page on each neighbor whose readers will need it.
7. `rg` distinctive phrases of the new rules across `core/` and `.ai/project/`; replace every copy with a link to the new page in the same change.
8. Add a row to [START.md](../../START.md) only when «Section Indexes» in page-spec.md calls for it (core pages only).
9. In the kit repository, record the new page under Unreleased per «Changelog Format» in [versioning.md](../../wiki/evolution/versioning.md).
10. Run `agentkit.py sync` so the section index lists the page, then `agentkit.py check`; fix every error and every duplicate-sentence warning the new page caused.

## Output
- The page path and its `owns` line.
- Files changed to link to it instead of copying it.
- The `check` result.
