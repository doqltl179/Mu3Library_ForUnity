---
name: kit-skill-add
description: "Add a skill: confirm the need is a procedure rather than a role or a rule, confirm no existing skill covers it, make sure every policy it applies has an owning page, scaffold it, write steps that link policy instead of restating it, list it on the cards that run it, then render and validate. Use when several owners repeat the same ordered task with stable inputs and outputs and no skill holds it."
category: kit
volatility: evolving
reviewed: 2026-10-09
---

# Add Skill

## Use When
- Owners repeat the same ordered task, with stable inputs and outputs, and no skill holds it.
- A project needs a local variant of a core skill under a different name.

## Do Not Use When
- The need is who decides or owns an artifact → `kit-agent-add`.
- The need is what must or must not be true → `kit-page-add`.
- An existing skill covers the task but is wrong → edit that skill in its layer, or `kit-upstream-propose` for a core skill in an installed project.

## Inputs
- Two or more concrete runs of the task: what started them, the steps taken, and what they produced.
- Target layer: core (kit repository only) or project (`.ai/project/skills/`).

## Steps
1. Classify the need with «Skill, Agent, or Page» in [skill-spec.md](../../wiki/authoring/skill-spec.md); continue only for a procedure.
2. Search for coverage: read the skills in `.ai/generated/catalog.md` and `rg` the key verbs over `core/skills/` and `.ai/project/skills/`. Extend a covering skill instead of adding a neighbor.
3. List the rules each step applies and find each rule's owning page. Write a missing rule first with `kit-page-add`.
4. Pick the category per «Categories» and the name per «Frontmatter» and «Body Shape» in skill-spec.md.
5. Scaffold: `agentkit.py new skill <name> --category <cat>`; add `--core` only for a core skill in the kit repository.
6. Write the skill per «Body Shape» in skill-spec.md, linking each policy section from step 3 with a «…» citation instead of restating it.
7. Add the skill to the `Skills` list of each agent card in the same layer that runs it.
8. For a local replacement of a core skill, add the core name to `skills.disabled` per «Local Skills» in [project-overlay.md](../../wiki/integration/project-overlay.md).
9. In the kit repository, list the new skill under Unreleased per «Changelog Format» in [versioning.md](../../wiki/evolution/versioning.md).
10. Run `agentkit.py sync` to render the skill into tool folders, then `agentkit.py check`; fix every error and each duplicate-sentence warning that points at a restated policy.

## Output
- The skill path, its category, the policy pages it links, and the cards that list it.
- The `check` result.
