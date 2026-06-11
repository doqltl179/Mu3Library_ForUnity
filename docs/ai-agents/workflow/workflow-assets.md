# Workflow Assets

## When

- a repeatable flow supports work but should not become a durable owner,
- you need the prompt, skill, hook, or script inventory,
- you are deciding whether a reusable procedure belongs here instead of in `agent-catalog.md`.

## Route Away When

- durable owner inventory is needed: [agent-catalog.md](../routing/agent-catalog.md),
- stable framework rationale is needed: [architecture.md](../architecture.md),
- task plan templates or task-record rules are needed: [../plans/README.md](../plans/README.md),
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
| Skill | `.github/skills/asmdef-triage/SKILL.md` | Assembly-definition diagnosis and safe change planning |
| Skill | `.github/skills/editmode-test-addition/SKILL.md` | EditMode test planning, placement, and minimal verification |

## Notes

- Keep this page as inventory only.
- Detailed behavior stays in the owning prompt, skill, or linked workflow page.
- Promote a repeatable flow to a durable owner only after `role-governor` suitability review.
