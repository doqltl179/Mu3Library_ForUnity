---
name: kit-agent-add
description: "Add an agent card: confirm no existing owner fits, choose between a stack pack, a project-local extends card, and a new core card, pass a role audit, scaffold and write the card, update neighbor boundaries, enable and bind it in the profile, then render and validate. Use when recurring work has no owner, or one project needs a narrower owner inside an existing surface."
---
<!-- agentkit:generated from .ai/kit/core/skills/kit-agent-add/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Add Agent

## Use When
- Recurring work routes to no owner, or routes ambiguously between two.
- One project needs a narrower owner inside a surface an existing card owns.

## Do Not Use When
- The gap is language or framework knowledge → `kit-stack-add`.
- The need is a repeatable procedure → `kit-skill-add`; a rule → `kit-page-add`.
- An existing card's boundaries are wrong → `kit-role-audit`, then edit that card.
- A core card is needed but you are in an installed project → `kit-upstream-propose`.

## Inputs
- Three or more real tasks that no current owner fits, with where each was routed.
- Target layer: core (kit repository only) or project (`.ai/project/agents/`).
- The generated catalog `.ai/generated/catalog.md`.

## Steps
1. Run «Selection Procedure» in [routing.md](.ai/kit/core/wiki/operating-model/routing.md) on each sample task. If one owner fits every task, stop and report that owner; if none fits, continue per «No Owner Fits».
2. Choose the asset per «What To Add» in [agent-spec.md](.ai/kit/core/wiki/authoring/agent-spec.md) and «Granularity Model». Leave this skill for any row other than a local `extends` card or a new core card.
3. Draft the proposal: name, department per «Departments», tier, access, mission, `Owns`, `Does Not Own` with neighbor names, and for a local card the `extends` base.
4. Have `role-governor` run `kit-role-audit` on the proposal. On `rework`, apply the required edits and audit again; when the audit rejects the proposal, stop and report its recommended alternative.
5. Scaffold a core card in the kit repository with `agentkit.py new agent <name> --department <dept> --core`, or a local card with `agentkit.py new agent <name> --extends <base>`.
6. Write the card per «Frontmatter» and «Body Shape» in [agent-spec.md](.ai/kit/core/wiki/authoring/agent-spec.md). A local card states only how it narrows its base, per «Local Agents» in [project-overlay.md](.ai/kit/core/wiki/integration/project-overlay.md).
7. Add a `Does Not Own` line that routes the new card's surface to it on each neighbor card the audit named, in the new card's layer only.
8. For project use, enable a core card in `agents.enabled` and add `[bindings.<agent>]` with its paths, stacks, and notes per «Profile» in project-overlay.md. Local cards are always active; bind them the same way.
9. In the kit repository, note the new agent under Unreleased per «Changelog Format» in [versioning.md](.ai/kit/core/wiki/evolution/versioning.md).
10. Run `agentkit.py sync` to render the tool agent files, then `agentkit.py check`; fix every error, including unknown `Does Not Own` targets.

## Output
- The card path, the audit verdict, neighbor cards changed, and profile changes.
- The `check` result.
