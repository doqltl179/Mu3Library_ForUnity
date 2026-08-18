# AI Agent Wiki

This is the wiki root. Use it only when task analysis shows that a wiki route is needed and the next owning section is not already obvious. The way here is the shared entry point [.github/copilot-instructions.md](../../.github/copilot-instructions.md).

## Choose By Question Shape

| Question | Open |
|---|---|
| In what order do I handle this request? | [workflow/request-lifecycle.md](workflow/request-lifecycle.md) |
| Who owns this work? | [routing/README.md](routing/README.md) |
| Who owns a project or AI-agent documentation change? | [routing/README.md](routing/README.md) |
| What rules apply while writing code or docs? | [coding-rules.md](coding-rules.md) |
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

## Executors That Walk These Procedures

The prompts and skills below run the procedures on this wiki. **The canonical page always owns the rule; these files hold only the order.** After changing a page, confirm its executors still walk the same steps. The full inventory is [workflow/workflow-assets.md](workflow/workflow-assets.md).

| Executor | Follows |
|---|---|
| [/work-issues](../../.claude/commands/work-issues.md) | [workflow/git-workflow.md](workflow/git-workflow.md) 「Issue To Pull Request」 |
| [compile-unity.prompt.md](../../.github/prompts/compile-unity.prompt.md) | [verification.instructions.md](../../.github/instructions/verification.instructions.md) |
| [framework-next-unit.prompt.md](../../.github/prompts/framework-next-unit.prompt.md) | [workflow/iteration-process.md](workflow/iteration-process.md) |
| [adapt-external-guidance.prompt.md](../../.github/prompts/adapt-external-guidance.prompt.md) | [workflow/external-guidance-adaptation.md](workflow/external-guidance-adaptation.md) |
| [development-idea-bank](../../.github/skills/development-idea-bank/SKILL.md) | [workflow/development-idea-bank.md](workflow/development-idea-bank.md) |
| [agent-role-audit](../../.github/skills/agent-role-audit/SKILL.md) | [routing/README.md](routing/README.md), [workflow/iteration-process.md](workflow/iteration-process.md) |
| [asmdef-triage](../../.github/skills/asmdef-triage/SKILL.md) | [unity-architecture.instructions.md](../../.github/instructions/unity-architecture.instructions.md) |
| [editmode-test-addition](../../.github/skills/editmode-test-addition/SKILL.md) | [verification.instructions.md](../../.github/instructions/verification.instructions.md) |
| [bootstrap-python-cli](../../.github/skills/bootstrap-python-cli/SKILL.md) | [tools/README.md](../../tools/README.md) |

Related tooling lives outside this wiki: `tools/README.md` and the tracked Unity project `.code-workspace` files.
