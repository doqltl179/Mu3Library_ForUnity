# AI Agent Wiki

This is the wiki root. Use it only when task analysis shows that a wiki route is needed and the next owning section is not already obvious.

## Choose By Question Shape

| Question | Open |
|---|---|
| Who owns this work? | [routing/README.md](routing/README.md) |
| Which package family should this work enter before opening one package-surface leaf page? | [packages/README.md](packages/README.md) |
| What packet, section contract, or shared format applies? | [contracts/README.md](contracts/README.md) |
| What repeatable process or workflow asset applies? | [workflow/README.md](workflow/README.md) |
| What task plan template, plan storage rule, or plan-writing convention applies? | [plans/README.md](plans/README.md) |
| What specialized edit procedure applies? | [guides/README.md](guides/README.md) |
| Why is the framework structured this way? | [architecture.md](architecture.md) |

## Navigation Rules

- Prefer `README.md -> section README -> leaf page`.
- Let the root router choose sections; let each section README mainly advertise child routers or leaf pages inside its own section.
- Let leaf pages own the detailed rules.
- Replace repeated rules with links to the smallest owning page.

Related tooling lives outside this wiki: `tools/mu3_cli/README.md` and the tracked Unity project `.code-workspace` files.
