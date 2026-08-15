# Multi-Agent Architecture

## Intent

Mu3Library uses a small multi-agent framework to coordinate specialized work without overlapping prompts or writable workspaces.

Open this page only for stable design rationale. For current owner status, use [routing/README.md](routing/README.md).

## Design Principles

- The canonical development philosophy lives in `.github/instructions/agent-framework.instructions.md`.
- Prefer one owner per concern.
- Express complex execution as a dependency graph: independent nodes run in isolated worktrees, while real dependencies remain ordered edges.
- Keep graphs economical: node creation requires a distinct purpose and cost justification, and only `orchestrator` may admit nodes within recorded total/concurrent credit budgets.
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

## Execution Topology

Parallel worktree execution is a workflow topology, not an architecture plane or agent role. The `orchestrator` retains DAG control through closeout and dispatches only ready nodes; existing node implementation owners implement one exclusive artifact scope per worktree; `release-manager` operates the plan-declared Git lifecycle and integrates reviewed commits in topological order; a plan-named owner runs combined verification; and `reviewer` gates node and combined evidence. The canonical lifecycle and activation gate live in [graph-engineering.md](workflow/graph-engineering.md).

This preserves a single control plane while allowing fan-out and fan-in. A dependency is satisfied only after its node is integrated, and write-scope overlap converts would-be parallel nodes into an ordered edge or one combined unit.

## Documentation Topology

- `routing/README.md` owns both active owner inventory and owner selection.
- `.github/agents/*.agent.md` files are selected-owner role cards.
- `contracts/handoff-contract.md` owns the owner-to-owner packet shape.
- `workflow/` answers which repeatable process or workflow asset applies.
- `guides/` answers how to handle a specialized edit surface.
- The root wiki index exists only to recover when the next page is not obvious.

## Boundary Rules

- Governance owners coordinate and audit; they do not implement domain work.
- Graph coordination does not transfer artifact ownership: every artifact node still has exactly one routed implementation owner and one isolated write scope; planner and Git-integrator roles do not become artifact owners.
- Execution owners produce artifacts; they do not redefine framework ownership while executing.
- Quality owners approve evidence and risks; they do not decide structural ownership.
- Memory promotion happens only after requested review gates validate that a fact is durable.

## Update This Page When

- the stable plane model changes,
- a new plane is added or removed,
- wiki topology changes,
- governance, execution, quality, workflow asset, or memory ownership semantics change.
