# AI Agent Wiki

This is the wiki root. Use it only when task analysis shows that a wiki route is needed and the next owning section is not already obvious.

## Choose By Question Shape

| Question | Open |
|---|---|
| Who owns this work? | [routing/README.md](routing/README.md) |
| Who owns a project or AI-agent documentation change? | [routing/README.md](routing/README.md) |
| Which package family and surface owns this work? | [packages/README.md](packages/README.md) |
| What owner-to-owner packet format applies? | [handoff-contract.md](contracts/handoff-contract.md) |
| What repeatable process or workflow asset applies? | [workflow/README.md](workflow/README.md) |
| What task plan template, plan storage rule, or plan-writing convention applies? | [plans/README.md](plans/README.md) |
| What specialized edit procedure applies? | [guides/README.md](guides/README.md) |
| Why is the framework structured this way? | [architecture.md](architecture.md) |

## Navigation Rules

- Prefer `README.md -> section README -> smallest owning page`.
- Let the root router choose sections; let each section README advertise only its direct child owners.
- Let owning pages hold detailed rules.
- Replace repeated rules with links to the smallest owning page.

Related tooling lives outside this wiki: `tools/README.md` and the tracked Unity project `.code-workspace` files.
