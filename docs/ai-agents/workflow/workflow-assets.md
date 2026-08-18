# Workflow Assets

## When

- a repeatable flow supports work but should not become a durable owner,
- you need the prompt, skill, hook, or script inventory,
- you are deciding whether a reusable procedure belongs here instead of the owner table in [routing/README.md](../routing/README.md).

## Route Away When

- durable owner inventory is needed: [routing/README.md](../routing/README.md),
- stable framework rationale is needed: [architecture.md](../architecture.md),
- task plan templates or task-record rules are needed: [../plans/README.md](../plans/README.md),
- the bounded framework-change loop is needed: [iteration-process.md](iteration-process.md).

## Asset Inventory

| Type | Artifact | Purpose | Follows |
|---|---|---|---|
| Prompt | [adapt-external-guidance.prompt.md](../../../.github/prompts/adapt-external-guidance.prompt.md) | External prompt/policy/workflow -> repository-safe adaptation entrypoint | [external-guidance-adaptation.md](external-guidance-adaptation.md) |
| Prompt | [compile-unity.prompt.md](../../../.github/prompts/compile-unity.prompt.md) | Synchronous compile-only verification entrypoint | [verification.instructions.md](../../../.github/instructions/verification.instructions.md) |
| Prompt | [development-idea-bank.prompt.md](../../../.github/prompts/development-idea-bank.prompt.md) | Repository-shaped package idea-bank entrypoint | [development-idea-bank.md](development-idea-bank.md) |
| Prompt | [framework-next-unit.prompt.md](../../../.github/prompts/framework-next-unit.prompt.md) | Bounded work -> review -> continue/rework entrypoint | [iteration-process.md](iteration-process.md) |
| Command | [work-issues](../../../.claude/commands/work-issues.md) | Open issues -> implementation -> pull request, in order | [git-workflow.md](git-workflow.md) 「Issue To Pull Request」 |
| Skill | [bootstrap-python-cli](../../../.github/skills/bootstrap-python-cli/SKILL.md) | Repository-local Python CLI bootstrap | [tools/README.md](../../../tools/README.md) |
| Skill | [agent-role-audit](../../../.github/skills/agent-role-audit/SKILL.md) | Structural role audit workflow | [routing/README.md](../routing/README.md), [iteration-process.md](iteration-process.md) |
| Skill | [development-idea-bank](../../../.github/skills/development-idea-bank/SKILL.md) | Package whitespace and idea-bank workflow | [development-idea-bank.md](development-idea-bank.md) |
| Skill | [asmdef-triage](../../../.github/skills/asmdef-triage/SKILL.md) | Assembly-definition diagnosis and safe change planning | [unity-architecture.instructions.md](../../../.github/instructions/unity-architecture.instructions.md) |
| Skill | [editmode-test-addition](../../../.github/skills/editmode-test-addition/SKILL.md) | EditMode test planning, placement, and minimal verification | [verification.instructions.md](../../../.github/instructions/verification.instructions.md) |
## Notes

- Keep this page as inventory only. The `Follows` column names the canonical page that owns the rule; the asset holds only the order.
- `Prompt` and `Skill` load in Copilot and Codex; `Command` is a Claude Code slash command under `.claude/commands/`.
- Detailed behavior stays in the owning prompt, skill, or linked workflow page.
- Promote a repeatable flow to a durable owner only after `role-governor` suitability review.
- Startup docs should link here for prompt/skill inventory instead of embedding long asset lists.
