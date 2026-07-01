# Multi-Agent Architecture

## Intent

Mu3Library uses a small multi-agent framework to coordinate specialized work without overlapping prompts.

Open this page only for stable design rationale. For current owner status, use [routing/README.md](routing/README.md).

## Design Principles

- The canonical development philosophy lives in `.github/instructions/agent-framework.instructions.md`.
- Prefer one owner per concern.
- Keep governance, execution, quality, workflow assets, and memory as separate planes.
- Keep workflow assets as prompts, skills, hooks, or scripts until a durable ownership gap is proven.
- Organize docs by question shape so agents can stop after one owner page, role card, or workflow page.
- Keep `.github/agents/*.agent.md` as compact role cards, not full policy documents.
- Use path-specific `applyTo` only for rules that file paths can select safely; route process rules manually from startup instructions.

## Architecture Planes

| Plane | Owns | Current Reference |
|---|---|---|
| Governance | Decomposition, sequencing, structural suitability | [routing/README.md](routing/README.md) |
| Execution | Bounded delivery surfaces such as Unity runtime/editor, docs sync, release, samples, and tooling | [routing/README.md](routing/README.md) |
| Quality | Regression, API, assembly, define-gate, docs, release, and verification review | [reviewer.agent.md](../../.github/agents/reviewer.agent.md) |
| Workflow Asset | Reusable flows that support work without becoming owners | [workflow-assets.md](workflow/workflow-assets.md) |
| Memory | Handoff packet contract and memory-routing entrypoint | [handoff-contract.md](contracts/handoff-contract.md) |

## Documentation Topology

- `routing/README.md` owns both active owner inventory and owner selection.
- `.github/agents/*.agent.md` files are selected-owner role cards.
- `contracts/handoff-contract.md` owns the owner-to-owner packet shape.
- `workflow/` answers which repeatable process or workflow asset applies.
- `guides/` answers how to handle a specialized edit surface.
- The root wiki index exists only to recover when the next page is not obvious.

## Boundary Rules

- Governance owners coordinate and audit; they do not implement domain work.
- Execution owners produce artifacts; they do not redefine framework ownership while executing.
- Quality owners approve evidence and risks; they do not decide structural ownership.
- Memory promotion happens only after requested review gates validate that a fact is durable.

## Update This Page When

- the stable plane model changes,
- a new plane is added or removed,
- wiki topology changes,
- governance, execution, quality, workflow asset, or memory ownership semantics change.
