# Parallel Worktree Graph Engineering

## When

Use this workflow when a complex request can be expressed as a dependency graph with at least two ready, independently writable nodes. Keep simple work serial, and serialize only the edges whose artifacts, decisions, files, Unity assets, or verification resources actually overlap.

This is a workflow asset, not a new owner or control plane. Owner selection still comes from [routing/README.md](../routing/README.md).

## Roles

| Role | Graph responsibility | Must not do |
|---|---|---|
| `orchestrator` | Own the DAG, readiness, dispatch waves, stop/replan decisions, and gate sequence through graph closeout or cancellation | Implement node changes or self-approve structural expansion |
| `task-planner` | Record orchestrator-approved topology, allocation, node state, and verification expectations | Choose owners, change graph control decisions, or dispatch work |
| Node implementation owner | Implement and commit exactly one assigned artifact node in its worktree | Edit another node's write scope or integrate sibling branches |
| `release-manager` | Create plan-declared Git resources, cherry-pick reviewed node commits, perform the final local fast-forward, and execute approved cleanup | Implement features, resolve semantic conflicts by guesswork, push graph branches automatically, approve quality, or clean up without confirmation |
| `reviewer` | Review node evidence and the integrated result | Run implementation fixes, act as the combined-verification executor, or redefine graph ownership |
| `role-governor` | Review framework/routing changes when the graph changes ownership topology | Act as node integrator or quality reviewer |

## Activation Gate

The `orchestrator` activates worktree mode by default only when all of these are true:

- the requested outcome is feasible and its acceptance criteria are known,
- at least two nodes can be ready at the same time,
- every node has one owner and an explicit, non-overlapping write scope,
- shared build, Unity, test, port, credential, or generated-output resources can be isolated or scheduled,
- the smallest useful node set has a clear elapsed-time, ownership, or risk-reduction benefit that exceeds its coordination and credit cost,
- `develop` is clean, synchronized with `origin/develop`, and recorded at an exact base commit,
- the user has not forbidden branches, worktrees, delegation, or parallel execution.

Once this gate passes, the repository policy authorizes only the local branches and worktrees named in the active graph plan. A second confirmation is not required to create those exact local resources. Push, destructive cleanup, release, and any branch outside the plan remain unauthorized unless separately requested.

If one edge fails the gate, serialize that edge and continue parallel execution for the remaining independent ready nodes. Do not claim parallel execution when all implementation nodes share a write scope or must wait on the same unresolved decision.

## Node Admission and Credit Control

Node creation is a budgeted control decision, not the default response to complexity. `orchestrator` must use the fewest nodes that preserve meaningful independence and must reject speculative, duplicate, or convenience-only fan-out.

Before a node can be created or dispatched, the graph record must state:

- the concrete purpose and acceptance criterion,
- why the work should be a separate node instead of remaining in an existing node or serial edge,
- the expected artifact, decision, or verification evidence,
- the exclusive owner and write scope,
- the dependencies and the reason it is ready now,
- an estimated credit/tool-cost class (`low`, `medium`, or `high`) and the expected coordination benefit.

The graph header sets both a maximum total-node budget and a maximum concurrent-node budget. Unknown cost is recorded explicitly and still consumes the budget. Adding a node requires `orchestrator` to re-check the remaining budget, reuse or merge existing nodes where possible, and record why the added cost is justified.

Node implementation owners must not recursively spawn, delegate, or invent child nodes. They report newly discovered scope to `orchestrator`, which alone may admit, merge, defer, or reject a node. Do not create parallel duplicate implementations, broad exploratory agents, or one reviewer agent per trivial change unless the comparison or independent review is an explicit acceptance requirement. Batch small related work with the same owner and write scope.

## Graph Plan Contract

Keep one active graph plan under `tasks/plans/`; do not create a nested plan per node. The graph header records:

- graph ID and objective,
- monotonically increasing graph revision and the authoritative plan's absolute coordinator-worktree path,
- acceptance criteria,
- coordinator worktree,
- base branch and immutable base commit,
- revisioned integration branch and absolute integration-worktree path,
- integration destination (`develop` unless the user explicitly authorizes another destination),
- maximum total nodes, maximum concurrency, expected credit/tool-cost envelope, and reserved shared resources,
- combined-verification owner and commands,
- exact cleanup candidates and the required confirmation gate.

Use one row per node:

| Field | Required meaning |
|---|---|
| ID | Stable short node ID unique within the graph |
| Graph revision | Plan revision authorizing this allocation and dispatch |
| Objective | One independently verifiable outcome |
| Admission / cost | Why a separate node is justified and its `low`/`medium`/`high` cost class |
| Owner | One selected node implementation owner |
| Depends on | Node IDs whose integrated artifacts or decisions are required |
| Read scope | Minimum context the node may inspect |
| Write scope | Exact paths or path families this node exclusively owns |
| Branch / worktree | Exact local branch name and absolute worktree path |
| Base | Exact commit from which the node starts |
| Deliverable | Expected artifacts and focused commit(s) |
| Verification | Node-local checks and evidence |
| Status | `planned`, `ready`, `running`, `review`, `approved`, `integrated`, `rework`, `blocked`, `failed`, or `cancelled` |

Use deterministic local names:

- integration branch: `agent/<graph-id>/integration-r<revision>`,
- node branch: `agent/<graph-id>/<node-id>-r<revision>`.

The plan is a coordinator-local, ignored artifact and is not expected to appear in added worktrees. Every dispatch packet must therefore copy the authoritative plan path, graph revision, node allocation, admission reason, budgets, base, scope, and verification contract. A packet authorizes only that exact revision, branch, and worktree.

Record the exact resolved names and paths before creation. Never place a worktree in any Unity `Library` directory. Treat a Unity asset and its `.meta` file as one indivisible write scope, and serialize shared manifests, project settings, lockfiles, generated catalogs, or other high-conflict files unless one node owns them for the whole graph.

## Execution Lifecycle

1. **Preflight and freeze the base.** Run the Git preflight from [git-workflow.md](git-workflow.md), classify existing user changes, fetch, require `develop` to match `origin/develop`, and record the base commit.
2. **Build and budget the DAG.** Define the smallest useful set of nodes and edges from artifact or decision dependencies. Apply node admission, total/concurrent budgets, duplicate-ownership checks, and hidden-shared-write checks before creating worktrees.
3. **Allocate isolation.** Under `orchestrator` control, `release-manager` creates the revisioned integration branch/worktree from the graph base. A root node starts from that base; a dependent node is created only when ready and starts from the then-current integration tip containing its integrated predecessors. Record that exact node base before dispatch. Keep the primary `develop` worktree free of node edits.
4. **Dispatch a ready wave.** Dispatch only nodes whose dependencies are integrated and whose resource reservations do not conflict. Use one agent and one branch per worktree.
5. **Complete a node.** The node implementation owner verifies its scope, makes focused commit(s), checks for out-of-scope changes, and submits exact candidate commit IDs with disposition `pending`. Dirty, uncommitted, or scope-expanding output is not ready for review.
6. **Run the node gate.** `reviewer` checks the exact candidate IDs against the recorded base, objective, scope, and evidence, then records `approved` with exact IDs or `rework` with findings. A failed node blocks only its descendants; independent nodes may continue.
7. **Fan in deterministically.** `release-manager` cherry-picks only approved IDs into the integration branch in topological order. It records the new integration tip, and `orchestrator` alone marks affected descendants ready. A conflict, unknown file, or base mismatch aborts the cherry-pick and blocks the affected node for re-graphing.
8. **Validate the graph result.** The plan-named owner runs combined checks on an exact integration tip. `reviewer` re-reads that same tip and records `approved` or `rework`; promotion is allowed only while the approved tip remains integration `HEAD`.
9. **Promote locally.** `release-manager` re-fetches and confirms `develop` still matches both the recorded base and `origin/develop`, then fast-forwards `develop` to the validated integration tip. If the target moved, do not rebase or force the graph automatically; `orchestrator` replans a rebuilt integration result on the new base and repeats affected verification.
10. **Close and propose cleanup.** `release-manager` records the integrated commit and verification outcome, then inventories exact worktree paths/sizes, dirty state, unique commits, and every Unity `Library` path/size inside them. General worktree cleanup and each exact Unity `Library` require their applicable explicit confirmations; never force-remove a dirty worktree. After approval it cleans only named targets and returns evidence to `orchestrator` for closeout.

Graph branches stay local unless the user explicitly requests a push. Promotion to `develop` does not authorize a release or a push of `develop`.

After graph closeout evidence is recorded, the coordinator-local graph plan follows the [Task Record Policy's approval-free closeout](../plans/task-record-policy.md#approval-free-closeout). This exception applies only to that plan file and its `tasks/todo.md` entry, never to graph branches, worktrees, or contained Unity `Library` directories.

## Scheduling Invariants

- A node is `ready` only after all dependency nodes are `integrated`, not merely implemented.
- No two `running` nodes may have overlapping write scopes or share an unisolated mutable resource.
- Read overlap is allowed; write overlap creates an edge or a single combined owner.
- A node that discovers necessary out-of-scope edits stops before making them and asks `orchestrator` to decide and authorize any graph update; `task-planner` records the decision.
- Only `orchestrator` may admit or remove graph nodes. Implementation nodes never create child nodes or delegate recursively.
- Every running node must have an admission reason and cost class; reaching either the total or concurrent node budget blocks further creation or dispatch.
- Reserve coordinator capacity; do not dispatch every available agent slot if that would prevent orchestration, review, or integration.
- Limit concurrent Unity imports, compiles, and tests according to machine resources. Never share or relocate a Unity `Library` directory between worktrees.
- Keep decisions and generated artifacts on explicit edges. Do not rely on sibling agents observing another worktree's uncommitted state.

## State Transitions

- `planned -> ready` only after admission remains justified, budget/resources are available, and every predecessor is `integrated`.
- `ready -> running -> review`; node review produces `approved` or `rework`. A corrected node returns `rework -> running -> review` with new candidate IDs.
- `approved -> integrated` only after a clean cherry-pick of the exact approved IDs. Conflict or base mismatch produces `blocked`; `failed` and `cancelled` are terminal for that revision.
- Integration review produces `approved` or `rework`; only an approved, unchanged tip may be promoted.
- A changed remote base increments the graph revision and creates new `-r<revision>` integration/node resources from the new base. Preserve earlier revisions until cleanup approval; never reset, force-update, or silently reuse their names.

## Handoff and Recovery

Use the graph context in [handoff-contract.md](../contracts/handoff-contract.md). A node handoff identifies its graph revision/node, base, branch/worktree, owned scope, candidate/approved commits, verification, and dependency impact.

- On node failure, preserve evidence, mark the node `failed`, block descendants, and continue unrelated ready nodes.
- On semantic or textual conflict, abort the integration operation and re-graph ownership; do not make speculative conflict resolutions.
- On dirty worktree or unknown files, preserve them and stop integration until ownership is clear.
- On stale base or remote movement, increment the graph revision, create new revisioned resources from the synchronized base, reapply still-valid approved commits in topological order, and re-run every affected gate.
- On cancellation, stop dispatching new nodes and report every live worktree, branch, dirty path, unique commit, and safe recovery option.

## Completion Evidence

A graph task is complete only when the final report includes:

- the graph ID and integrated node list,
- the final `develop` commit,
- node-local and combined verification actually run,
- skipped checks and residual risks,
- unpushed branches or worktrees still present,
- cleanup status and whether explicit deletion approval is still pending.
