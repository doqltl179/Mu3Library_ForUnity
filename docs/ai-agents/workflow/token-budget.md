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
4. Keep static rules in stable early instructions and move detailed procedures to leaf docs.
5. Enable MCP, plugins, and large tool surfaces only when the task needs them.
6. Compact long sessions into decisions, touched files, unresolved risks, and verification status.
7. Final reports should focus on changed files, verification, and remaining risks.
8. For non-interactive measurement or smoke-test runs, avoid automatic repository instruction loading unless the task needs repository edits or instruction compliance.

## Documentation Pattern

- Startup instructions state only the operating rule and link here.
- Folder indexes route to one owning page.
- Leaf pages own detailed procedure.
- Agent specs keep owner-specific deltas only.
- Prompt and skill files should not duplicate a long contract already owned by a wiki page.

## Runner Pattern

- Run lightweight smoke tests from an isolated working directory so `AGENTS.md` and router docs are not loaded just to answer a trivial prompt.
- Pass the repository root as an explicit path and read repository files only when the task asks for them.
- Keep a switch or equivalent escape hatch for full repository-instruction runs when coding, documentation editing, release, or policy compliance is required.
- Record `input_tokens`, `cached_input_tokens`, `output_tokens`, and `reasoning_output_tokens`; compare uncached input first, then total context size.
- Treat high cached input as a context-capacity risk even when direct cost is reduced.
