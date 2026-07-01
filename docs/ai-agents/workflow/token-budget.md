# Token Budget Workflow

## When

- an AI-agent task may read many files, logs, docs, tool schemas, or long command outputs,
- context is growing during a multi-step session,
- a workflow or doc change affects startup instructions, tools, MCP, skills, or agent specs.

## Route Away When

- choosing the current owner: [routing/README.md](../routing/README.md),
- writing an owner-to-owner packet: [handoff-contract.md](../contracts/handoff-contract.md),
- following a specialized edit procedure: [guides/README.md](../guides/README.md).

## Rules

1. Narrow first with `rg`, file lists, symbols, or error patterns.
2. Open only the smallest file, section, or line range that can answer the current question.
3. Summarize command output by result, failure point, affected files, and next action.
4. In interactive sessions, answer with the smallest clear result that completes the request.
5. Keep static rules in stable early instructions and move detailed procedures to owning docs.
6. Enable MCP, plugins, and large tool surfaces only when the task needs them.
7. Compact long sessions into decisions, touched files, unresolved risks, and verification status.
8. Final reports should focus on changed files, verification, and remaining risks.
9. For non-interactive measurement or smoke-test runs, avoid automatic repository instruction loading unless the task needs repository edits or instruction compliance.
10. When revising docs, move any content that is not needed on every task behind a one-line description plus a link to its owning page or inventory.

## Documentation Pattern

- Startup instructions keep only the minimum always-on guardrails and links to the smallest owning instruction, router, workflow, or inventory page needed for the next decision.
- Folder indexes route to one owning page.
- Owning pages hold detailed procedure.
- Agent specs keep owner-specific deltas only.
- Prompt and skill files should not duplicate a long contract already owned by a wiki page.

## Context Budget Policy

- Treat root and startup instruction files as route maps, not manuals.
- Keep `.github/copilot-instructions.md` within 45 lines unless a temporary migration note is unavoidable.
- Keep `.github/instructions/*.instructions.md` within 65 lines and move procedure details here or to a narrower owner page.
- Keep `docs/ai-agents/routing/README.md` within 70 lines; if owner selection needs more room, reduce overlap before adding another routing page.
- Keep `.github/agents/*.agent.md` as role cards within 30 lines; frontmatter and the `description` field should carry selection cues.
- Keep `.github/prompts/*.prompt.md` within 30 lines and `.github/skills/*/SKILL.md` within 60 lines, with frontmatter `name` and `description`.
- Keep `docs/ai-agents/**/*.md` below 24 files by default. Add a new page only for a distinct question shape, boundary, reusable contract, or workflow that an existing owner cannot answer cleanly.
- Use `applyTo` only when a file path alone safely selects the instruction. Process instructions should keep a `description` and be routed by `.github/copilot-instructions.md` instead of using broad `applyTo: '**'`.
- Any durable exception should be paired with a CI or CLI guard update, or a short note explaining why the budget is intentionally exceeded.

## Document Revision Pattern

- Early-read docs such as `AGENTS.md`, `.github/copilot-instructions.md`, and section `README.md` files may keep only the rules needed on nearly every task.
- Everything else in early-read docs should focus on when to open the next page, what that next page owns, and when to stop routing.
- Replace repeated inventories, examples, and long rule blocks with a short purpose line and one link to the owning inventory or page.
- Keep inventories in one owning place such as `routing/README.md` or `workflow/workflow-assets.md`.
- Keep package-family pages compact; merge tiny package-surface rule bodies when a single page can answer the question.
- After editing a startup doc, check whether a typical task can stop after selecting one next page; if not, the startup doc is still too heavy.

## Runner Pattern

- Run lightweight smoke tests from an isolated working directory so `AGENTS.md` and router docs are not loaded just to answer a trivial prompt.
- Pass the repository root as an explicit path and read repository files only when the task asks for them.
- Keep a switch or equivalent escape hatch for full repository-instruction runs when coding, documentation editing, release, or policy compliance is required.
- Record `input_tokens`, `cached_input_tokens`, `output_tokens`, and `reasoning_output_tokens`; compare uncached input first, then total context size.
- Treat high cached input as a context-capacity risk even when direct cost is reduced.
