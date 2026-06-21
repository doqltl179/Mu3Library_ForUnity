# Token Budget Workflow

## When

- an AI-agent task may read many files, logs, docs, tool schemas, or long command outputs,
- context is growing during a multi-step session,
- a workflow or doc change affects startup instructions, tools, MCP, skills, or agent specs.

## Route Away When

- choosing the current owner: [routing/README.md](../routing/README.md),
- editing shared packet or section contracts: [contracts/README.md](../contracts/README.md),
- following a specialized edit procedure: [guides/README.md](../guides/README.md).

## Rules

1. Narrow first with `rg`, file lists, symbols, or error patterns.
2. Open only the smallest file, section, or line range that can answer the current question.
3. Summarize command output by result, failure point, affected files, and next action.
4. In interactive sessions, answer with the smallest clear result that completes the request.
5. Keep static rules in stable early instructions and move detailed procedures to leaf docs.
6. Enable MCP, plugins, and large tool surfaces only when the task needs them.
7. Compact long sessions into decisions, touched files, unresolved risks, and verification status.
8. Final reports should focus on changed files, verification, and remaining risks.
9. For non-interactive measurement or smoke-test runs, avoid automatic repository instruction loading unless the task needs repository edits or instruction compliance.
10. When revising docs, move any content that is not needed on every task behind a one-line description plus a link to its owning leaf or inventory page.

## Documentation Pattern

- Startup instructions keep only the minimum always-on guardrails and links to the smallest owning instruction, router, workflow, or inventory page needed for the next decision.
- Folder indexes route to one owning page.
- Leaf pages own detailed procedure.
- Agent specs keep owner-specific deltas only.
- Prompt and skill files should not duplicate a long contract already owned by a wiki page.

## Document Revision Pattern

- Early-read docs such as `AGENTS.md`, `.github/copilot-instructions.md`, and section `README.md` files may keep only the rules needed on nearly every task.
- Everything else in early-read docs should focus on when to open the next page, what that next page owns, and when to stop routing.
- Replace repeated inventories, examples, and long rule blocks with a short purpose line and one link to the owning inventory or leaf page.
- Keep inventories in dedicated inventory pages such as `routing/agent-catalog.md` and `workflow/workflow-assets.md`.
- Keep package-family pages as routers and keep package-surface rule bodies in leaf pages.
- After editing a startup doc, check whether a typical task can stop after selecting one next page; if not, the startup doc is still too heavy.

## Runner Pattern

- Run lightweight smoke tests from an isolated working directory so `AGENTS.md` and router docs are not loaded just to answer a trivial prompt.
- Pass the repository root as an explicit path and read repository files only when the task asks for them.
- Keep a switch or equivalent escape hatch for full repository-instruction runs when coding, documentation editing, release, or policy compliance is required.
- Record `input_tokens`, `cached_input_tokens`, `output_tokens`, and `reasoning_output_tokens`; compare uncached input first, then total context size.
- Treat high cached input as a context-capacity risk even when direct cost is reduced.
