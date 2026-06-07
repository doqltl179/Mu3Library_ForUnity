# Workflow Assets

## When

- a repeatable flow supports work but should not become a durable owner,
- you need the prompt, skill, hook, or script inventory,
- you are deciding whether a reusable procedure belongs here instead of in `agent-catalog.md`.

## Route Away When

- durable owner inventory is needed: [agent-catalog.md](../routing/agent-catalog.md),
- stable framework rationale is needed: [architecture.md](../architecture.md),
- the bounded framework-change loop is needed: [iteration-process.md](iteration-process.md).

## Asset Inventory

| Type | Artifact | Purpose |
|---|---|---|
| Prompt | `.github/prompts/compile-unity.prompt.md` | Synchronous compile-only verification entrypoint |
| Prompt | `.github/prompts/development-idea-bank.prompt.md` | Repository-shaped package idea-bank entrypoint |
| Prompt | `.github/prompts/framework-next-unit.prompt.md` | Bounded work -> review -> continue/rework entrypoint |
| Skill | `.github/skills/bootstrap-python-cli/SKILL.md` | Repository-local Python CLI bootstrap |
| Skill | `.github/skills/agent-role-audit/SKILL.md` | Structural role audit workflow |
| Skill | `.github/skills/development-idea-bank/SKILL.md` | Package whitespace and idea-bank workflow |

## Rules

- Prefer workflow assets when the repository needs a repeatable flow but not a long-lived owner.
- Promote a flow to an agent only after a durable ownership gap passes `role-governor` suitability review.
- Compile-only verification is evidence for `reviewer`, not a replacement for reviewer approval.
- Ideation stays in the workflow-asset plane unless it reveals a new ownership gap.
- Token-budget procedure lives in [token-budget.md](token-budget.md); it is a workflow rule, not a new owner.

## Compile Verification

- Prefer editor-safe `dotnet build` on affected generated Unity `.csproj` files.
- For VS Code and C# Dev Kit support, start from the tracked Built-In workspace and use `mu3-cli csdevkit` helpers when needed.

## Idea-Bank Contract

`development-idea-bank.md` owns the detailed role, output, anti-pattern, and refinement rules. Keep this inventory page as a router only.
