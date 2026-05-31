# Multi-Agent Architecture

## Intent

Mu3Library needs a multi-agent framework that can scale without turning into a set of overlapping prompts. The design goal is not to maximize agent count. The design goal is to create a small control plane that can coordinate several specialized agents safely.

The framework follows this development philosophy: "Break large features into multiple small features, and make each small feature an independent, non-overlapping capability. Develop with modularity and maintainability in mind." In practice, new work should be decomposed into small, independent, non-overlapping units so the system stays modular and maintainable.

## When

- you need the stable framework design rather than the current rollout snapshot,
- you need to understand why the framework is split into planes,
- you need to understand why the wiki is split into routing, contracts, workflow, and guides,
- you need design rationale before changing framework routing or ownership boundaries.

## Route Away When

- the question is about the current approved owner inventory: use [agent-catalog.md](routing/agent-catalog.md),
- the question is about the bounded framework-change loop: use [iteration-process.md](workflow/iteration-process.md),
- the question is about handoff packet format or memory-routing rules: use [handoff-contract.md](contracts/handoff-contract.md),
- the question is about workflow assets or specialized procedures instead of stable rationale: use [workflow/README.md](workflow/README.md) or [guides/README.md](guides/README.md).

## Owns

- stable framework design and plane boundaries.
- the rationale for why the wiki is split by question shape for AI-agent discovery.

## Design Principles

- Favor flow-style execution control for predictable sequencing.
- Favor role-specialized agents for bounded autonomy.
- Keep state and memory handling explicit.
- Keep CLI conventions small first, then scale through subcommands.
- When a capability is useful but does not own a durable repository surface, keep it in the workflow-asset plane instead of promoting it into the agent catalog.
- Organize framework docs by question shape so an AI agent can choose the next owning page quickly instead of re-reading broad summaries.

## Documentation Topology

The wiki structure is intentionally optimized for AI-agent discovery during live work.

- `routing/` answers ownership questions such as who should own the current task or which specialist should be selected.
- `contracts/` answers shared-format questions such as which packet, section contract, or persistence rule should be followed.
- `workflow/` answers repeatable-process questions such as which framework loop, workflow asset, or ideation flow should be used.
- `guides/` answers surface-specific procedure questions such as how to perform a concrete edit safely on a specialized surface.
- The root keeps the top-level wiki index and this stable design page so agents can recover when the next document is not obvious.

This split is not for human browsing convenience alone. It exists so an AI agent can map the current question shape to one documentation surface before opening any leaf page.

## Architecture Layers

### 1. Governance Plane

- A routing owner decomposes work, chooses the next specialist, and keeps sequencing coherent.
- A structural suitability owner audits overlap, missing ownership, duplication risk, and boundary fitness after non-trivial framework changes.

Governance owners do not own Unity feature implementation. They own coordination and structural fitness.

### 2. Execution Plane

- A planning owner turns the current assigned unit into verifiable steps and keeps progress records aligned.
- Domain specialists own bounded delivery surfaces such as runtime, editor, optional-package integration, docs synchronization, release execution, and sample integrity.
- A cross-boundary Unity owner exists only for tasks that genuinely span narrower specialists and cannot be held cleanly by one owner.
- Auxiliary tooling ownership stays inside tooling-safe roots such as repository-local Python bootstrap and CLI workflows.

Execution-plane owners should produce concrete artifacts. They should not redefine the whole system while working.

Current concrete owners live in [agent-catalog.md](routing/agent-catalog.md).

### 3. Quality Plane

- A review owner checks regressions, public API safety, define guards, assembly boundaries, docs sync, and release readiness.
- Future verification specialists can be added later if the review surface becomes too broad for one owner.

### 4. Workflow Asset Plane

- `instructions`: always-on or scoped operating rules.
- `skills`: reusable task bundles with stable inputs and outputs.
- `prompts`: narrow entry points for repeatable user-facing tasks.
- `hooks`: deterministic guardrails for preflight checks.

Current workflow assets include prompt entrypoints and supporting instructions for compile-only verification. Compile verification should use editor-safe `dotnet build` against the affected generated Unity `.csproj` files and remain reviewer evidence rather than a dedicated control-plane gate.

The workflow asset plane also includes a repository-shaped ideation skill and prompt for pre-unit idea-bank shaping and whitespace discovery when package direction is unclear. That asset maps current capability, preserves multiple viable directions, and returns control to the main agent or `orchestrator`; it does not create a new execution owner.

Supporting documents for this plane should also define:

- memory routing policy,
- structured handoff packets,
- review-aware persistence transitions.

### 5. Memory Plane

- repository memory stores durable project facts and conventions.
- session memory stores current-task state.
- user memory stores cross-project user preferences.

Orchestrator decides what is durable enough to persist after the requested review gates complete. If a handoff note is only useful until the current suitability review completes, keep it in session memory. Promote it to repository memory only after all requested review gates validate the convention.

The detailed routing and packet format are defined in `handoff-contract.md` and `.github/instructions/memory-policy.instructions.md`.

Source owners can propose persistence, but orchestrator records final promotion after review.

## Operational References

- For question-shape-based navigation across the wiki, start from [README.md](README.md).
- For the required bounded-unit review loop, use [iteration-process.md](workflow/iteration-process.md).
- For current approved owners and deferred candidates, use [agent-catalog.md](routing/agent-catalog.md).

## Agent Spec Contract

Every agent document should define the following fields explicitly:

- Mission
- Primary responsibilities
- Non-goals
- Required inputs
- Expected outputs
- Escalation triggers
- Coordination dependencies
- Review triggers

If any of those fields are vague, the agent is not ready to add.

## Boundary Examples

### Tooling Ownership

Tooling ownership is structurally valid only if it stays narrow:

- It owns Python environment bootstrap, CLI command architecture, and automation UX.
- It does not own Unity runtime/editor feature logic.
- It should prefer auxiliary tooling directories and package metadata instead of touching product assemblies.
- It must declare safe roots and forbidden roots so that tooling work does not leak into shipped package surfaces.

That boundary is why tooling automation stays in a narrow specialist role instead of a broad system administrator.

### Structural Governance Ownership

Structural governance ownership is valid only as a governance function:

- It audits role overlap and missing ownership.
- It recommends merge, split, remove, or re-scope actions.
- It does not implement the delegated work itself.
- It owns the structural continue-or-rework decision for each non-trivial framework iteration.

That boundary is why governance and delivery stay split instead of being bundled into one manager role.