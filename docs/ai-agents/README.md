# AI Agent Wiki

This directory is the wiki-style entrypoint for the Mu3Library AI-agent framework. Every page in this tree exists for AI-agent work, so the main job of this index is simple: identify the current question shape and jump to the smallest owning page.

## Development Philosophy

- Break large features into multiple small features, and make each small feature an independent, non-overlapping capability.
- Apply the same rule to the docs: keep each page focused on one concern, remove duplicated summaries, and link to the owning page instead.
- Keep the framework and its documentation modular, reviewable, and maintainable.

## How To Use This Wiki

1. Start here only when the next page is not obvious.
2. Match the current question to one folder or one stable page.
3. Open the smallest linked page that owns that concern.
4. If two pages repeat the same rule, keep one owner page and replace the duplicate with a link.

## Choose By Question Shape

| Question Shape | Open First |
|---|---|
| Who owns this work? | [routing/README.md](routing/README.md) |
| What packet, section contract, or shared format should I follow? | [contracts/README.md](contracts/README.md) |
| What repeatable process or workflow asset should I follow? | [workflow/README.md](workflow/README.md) |
| What specialized procedure should I follow for a concrete surface? | [guides/README.md](guides/README.md) |
| Why is the framework shaped this way? | [architecture.md](architecture.md) |

## Routing Rule

- Routing is allowed: links connect the reader from the wiki index or a nearby page to the single owning page for a concern.
- Workaround-style documentation paths are not allowed: do not create a second page that explains the same procedure or decision surface in parallel.
- If another page needs the same content, link to the owner page or move the rule there.

## Folder Map

- [routing/README.md](routing/README.md): owner inventory and shared owner-selection rules.
- [contracts/README.md](contracts/README.md): shared packet, section, and persistence contracts.
- [workflow/README.md](workflow/README.md): repeatable framework process and workflow assets.
- [guides/README.md](guides/README.md): specialized procedures for concrete edit surfaces.
- [architecture.md](architecture.md): stable design rationale for the whole system.

## Quick Routes By Task

| If you need to... | Open | Why |
|---|---|---|
| Choose the current owner for a task or confirm what is rollout-approved | [agent-catalog.md](routing/agent-catalog.md) | Tracks approved agents, statuses, and rollout snapshot |
| Browse the routing pages before choosing a specific owner document | [routing/README.md](routing/README.md) | Gives the local index for owner inventory and shared routing matrices |
| Open the detailed operating contract for a specific agent | [agent-catalog.md](routing/agent-catalog.md) | Links the approved inventory to each agent spec under `.github/agents/` |
| Understand the shared structure of agent spec documents | [agent-spec-contract.md](contracts/agent-spec-contract.md) | Defines what belongs in individual agent specs versus shared wiki pages |
| Browse the contract pages before opening a specific shared contract | [contracts/README.md](contracts/README.md) | Gives the local index for shared spec and handoff contracts |
| Choose among control-plane agents with one owner matrix | [control-plane-routing.md](routing/control-plane-routing.md) | Centralizes shared owner-selection and gate-order rules for the control plane |
| Choose among Unity specialist agents with one owner matrix | [unity-specialist-routing.md](routing/unity-specialist-routing.md) | Centralizes shared Unity owner-selection and split rules |
| Understand the stable framework design and plane boundaries | [architecture.md](architecture.md) | Explains the control, execution, quality, workflow-asset, and memory planes |
| Browse workflow pages before opening a specific process or workflow asset | [workflow/README.md](workflow/README.md) | Gives the local index for framework process, workflow assets, and ideation contracts |
| Browse specialized procedural guides before opening a concrete edit workflow | [guides/README.md](guides/README.md) | Gives the local index for surface-specific editing procedures and validation rules |
| Follow the required loop for non-trivial framework changes | [iteration-process.md](workflow/iteration-process.md) | Defines the bounded unit -> suitability review -> continue or rework process |
| Prepare a handoff or decide what belongs in session memory versus repo memory | [handoff-contract.md](contracts/handoff-contract.md) | Defines the handoff packet, routing rules, and review-aware persistence |
| Decide whether a reusable flow should be a prompt, skill, or hook instead of a new agent | [workflow-assets.md](workflow/workflow-assets.md) | Tracks reusable non-owner workflow assets and their boundaries |
| Widen option space before choosing a bounded implementation unit | [development-idea-bank.md](workflow/development-idea-bank.md) | Defines the repository-shaped ideation workflow asset |
| Edit Unity scene or prefab YAML directly | [unity-yaml-guide.md](guides/unity-yaml-guide.md) | Captures verified YAML-editing patterns and validation rules |

## Document Boundaries

| Page | Owns | Do not use it for |
|---|---|---|
| [README.md](README.md) | Wiki navigation, quick routing, and page boundaries | Repeating full framework summaries |
| [agent-spec-contract.md](contracts/agent-spec-contract.md) | Shared agent spec structure and field ownership | Per-agent scope deltas or rollout status |
| [control-plane-routing.md](routing/control-plane-routing.md) | Shared control-plane owner-selection matrix and gate-order rules | Stable whole-framework design or handoff packet format |
| [unity-specialist-routing.md](routing/unity-specialist-routing.md) | Shared Unity owner-selection matrix and split rules | Stable whole-framework design or non-Unity routing |
| [architecture.md](architecture.md) | Stable framework design and plane boundaries | Rollout status or handoff packet details |
| [agent-catalog.md](routing/agent-catalog.md) | Approved owner inventory, rollout snapshot, and direct links to agent specs | Deep architecture rationale |
| [workflow/README.md](workflow/README.md) | Local index for process and workflow-asset pages | Whole-framework architecture or owner-selection matrices |
| [guides/README.md](guides/README.md) | Local index for specialized edit procedures | Whole-framework routing or shared contracts |
| [iteration-process.md](workflow/iteration-process.md) | The bounded-unit review loop | Owner inventory or memory policy |
| [handoff-contract.md](contracts/handoff-contract.md) | Handoff packet structure, memory scope, and persistence rules | System-wide architecture summary |
| [workflow-assets.md](workflow/workflow-assets.md) | Reusable prompts, skills, hooks, and scripts that do not create a durable owner | Long-lived agent ownership |
| [development-idea-bank.md](workflow/development-idea-bank.md) | Ideation workflow contract and output rules | General framework routing |
| [unity-yaml-guide.md](guides/unity-yaml-guide.md) | Verified direct scene and prefab YAML workflow | General AI-agent governance |

## Related Tooling

- [tools/mu3_cli/README.md](../../tools/mu3_cli/README.md): bootstrap and usage guide for the repository-local Python CLI, including the `mu3-cli csdevkit` support surface.
- [UnityProject_BuiltIn/Mu3Library_ForUnity.code-workspace](../../UnityProject_BuiltIn/Mu3Library_ForUnity.code-workspace): default VS Code entrypoint for Base and Built-In maintenance, with tracked C# Dev Kit recommendations and default solution selection.
- [UnityProject_URP/Mu3Library_ForUnity.code-workspace](../../UnityProject_URP/Mu3Library_ForUnity.code-workspace): URP-focused VS Code entrypoint with tracked C# Dev Kit recommendations and default solution selection.