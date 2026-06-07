# AI Agent Wiki

This is a routing index, not a framework summary. Open one smallest owning page, then stop reading broadly.

## Token Budget Rule

- Start here only when the next page is unclear.
- Match the question shape, open the linked folder index or leaf page, and avoid nearby summaries.
- If two pages repeat the same rule, keep one owner page and replace the duplicate with a link.

## Choose By Question Shape

| Question | Open |
|---|---|
| Who owns this work? | [routing/README.md](routing/README.md) |
| What packet, section contract, or shared format applies? | [contracts/README.md](contracts/README.md) |
| What repeatable process or workflow asset applies? | [workflow/README.md](workflow/README.md) |
| What specialized edit procedure applies? | [guides/README.md](guides/README.md) |
| Why is the framework structured this way? | [architecture.md](architecture.md) |

## Direct Routes

| Need | Open |
|---|---|
| Approved owner inventory | [routing/agent-catalog.md](routing/agent-catalog.md) |
| Control-plane owner selection | [routing/control-plane-routing.md](routing/control-plane-routing.md) |
| Unity specialist owner selection | [routing/unity-specialist-routing.md](routing/unity-specialist-routing.md) |
| Agent spec section rules | [contracts/agent-spec-contract.md](contracts/agent-spec-contract.md) |
| Handoff packet and memory scope | [contracts/handoff-contract.md](contracts/handoff-contract.md) |
| Token-budget operating mode | [workflow/token-budget.md](workflow/token-budget.md) |
| Framework change loop | [workflow/iteration-process.md](workflow/iteration-process.md) |
| Workflow asset inventory | [workflow/workflow-assets.md](workflow/workflow-assets.md) |
| Release execution details | [workflow/release-execution.md](workflow/release-execution.md) |
| Git branch and push flow | [workflow/git-workflow.md](workflow/git-workflow.md) |
| Package idea-bank workflow | [workflow/development-idea-bank.md](workflow/development-idea-bank.md) |
| Direct Unity scene or prefab YAML edits | [guides/unity-yaml-guide.md](guides/unity-yaml-guide.md) |

## Ownership Boundaries

- `routing/` owns owner selection and rollout inventory.
- `contracts/` owns shared packet and section formats.
- `workflow/` owns repeatable processes, prompts, skills, hooks, and scripts that do not create durable owners.
- `guides/` owns concrete edit procedures.
- `architecture.md` owns stable rationale only.

Related tooling lives outside this wiki: `tools/mu3_cli/README.md` and the tracked Unity project `.code-workspace` files.
