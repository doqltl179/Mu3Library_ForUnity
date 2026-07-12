# Routing Wiki

Use this section when the question is which owner should handle the current work.

## Open

| If you need to... | Open | Why |
|---|---|---|
| Choose the owner for the current task | This page | Centralizes owner-selection rules so startup flow stops after one router |
| Open the selected owner spec | This page | The active owner table links directly to each role card |

## Active Owners

| Owner | Plane | Owns | Spec |
|---|---|---|---|
| `orchestrator` | Governance | Decomposition, owner selection, gate sequencing | [spec](../../../.github/agents/orchestrator.agent.md) |
| `role-governor` | Governance | Structural suitability for ownership and routing changes | [spec](../../../.github/agents/role-governor.agent.md) |
| `task-planner` | Execution | Current-unit plans and progress records | [spec](../../../.github/agents/task-planner.agent.md) |
| `unity-runtime` | Execution | Non-gated runtime package work | [spec](../../../.github/agents/unity-runtime.agent.md) |
| `unity-editor` | Execution | Non-gated editor tooling work | [spec](../../../.github/agents/unity-editor.agent.md) |
| `package-integration` | Execution | Define-gated optional package integrations | [spec](../../../.github/agents/package-integration.agent.md) |
| `sample-integrity` | Execution | Package sample integrity | [spec](../../../.github/agents/sample-integrity.agent.md) |
| `unity` | Execution | Genuine cross-boundary Unity package work | [spec](../../../.github/agents/unity.agent.md) |
| `docs-sync` | Execution | Project documentation delivery and multilingual README/CHANGELOG sync | [spec](../../../.github/agents/docs-sync.agent.md) |
| `release-manager` | Execution | Versioning, tags, branches, and GitHub Releases | [spec](../../../.github/agents/release-manager.agent.md) |
| `cli-platform` | Execution | Repository-local Python and CLI tooling | [spec](../../../.github/agents/cli-platform.agent.md) |
| `reviewer` | Quality | Regression, compatibility, docs, release, and verification review | [spec](../../../.github/agents/reviewer.agent.md) |

## Control Plane

| Task Shape | Primary Owner | Route Away When |
|---|---|---|
| Broad request must be decomposed or the next owner is unclear | `orchestrator` | the current unit already has a clear owner and only needs planning, structural review, or quality review |
| Current unit needs a step plan, progress tracking, or bounded replanning | `task-planner` | cross-agent routing, structural fit, or quality approval is the main problem |
| Framework change needs continue-or-rework disposition for overlap, missing ownership, or routing ambiguity | `role-governor` | the issue is domain implementation or quality approval |
| Review-ready change needs regression, compatibility, verification, docs, or release-quality review | `reviewer` | the issue is structural fit or cross-agent routing |

## Unity Specialists

| Task Shape | Primary Owner | Route Away When |
|---|---|---|
| Non-gated runtime behavior under Base or URP runtime scripts | `unity-runtime` | editor tooling, define-gated integration, sample integrity, or cross-boundary Unity work dominates |
| Non-gated editor tooling under Base or URP editor scripts | `unity-editor` | runtime behavior, define-gated integration, sample integrity, or cross-boundary Unity work dominates |
| Define-gated optional integration across runtime, editor, or package surfaces | `package-integration` | the task is mostly non-gated runtime or editor work |
| Package samples, imported footprints, manifests, or smoke checks | `sample-integrity` | the dominant work is core runtime, editor, or optional integration |
| Unity package work genuinely spans narrower specialists and cannot be split cleanly | `unity` | a narrower owner is dominant or the work can be split into bounded units |

## Documentation

| Task Shape | Primary Owner | Route Away When |
|---|---|---|
| Project or AI-agent documentation needs clearer content, links, navigation, or multilingual README/CHANGELOG sync | `docs-sync` | role ownership, routing topology, or framework boundaries change (`role-governor`); tooling command design dominates (`cli-platform`); release execution dominates (`release-manager`); quality approval is needed (`reviewer`) |

## Shared Rules

- Prefer the narrowest owner first.
- `orchestrator` coordinates gates but does not self-approve structural expansion.
- `task-planner` plans only after the current unit and owner are clear.
- `role-governor` handles structural suitability only.
- `reviewer` handles quality and verification only.
- Use [handoff-contract.md](../contracts/handoff-contract.md) for owner-to-owner packets.
- Use [unity-yaml-guide.md](../guides/unity-yaml-guide.md) before direct scene or prefab YAML edits.
- Add a new owner only after `role-governor` finds a bounded ownership gap.

## Notes

- If owner selection is no longer the question, return to [../README.md](../README.md).
- Keep one owner-selection summary per decision and link to the smallest routing page that already owns it.
