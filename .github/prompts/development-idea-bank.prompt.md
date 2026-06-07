---
description: "Generate a Mu3Library idea bank of genuinely new package directions when the next direction is unclear."
name: "개발 아이디어 뱅크"
argument-hint: "Describe the audience, package lane, or future direction you want ideas for. Leave it broad if you want repository-wide whitespace discovery"
agent: "agent"
---

Produce a Mu3Library idea bank for the described area.

- Use the `development-idea-bank` skill.
- Follow `docs/ai-agents/workflow/development-idea-bank.md` for the output contract.
- Keep the work in pre-unit concept shaping.
- Start from repository evidence, then build a capability map and whitespace map before proposing ideas.
- Treat a named pain point or existing feature as evidence of adjacent whitespace unless the user explicitly asks for refinement.
- Use limited web research only after the whitespace map exists; repository constraints win.
- Generate 6 distinct ranked ideas and keep at most one low-priority incremental baseline idea.
- Preserve the option space; do not write a single concept brief unless the user asks to deepen one idea after seeing the bank.
- End with short selection hooks or follow-up questions that help choose which idea to deepen next.
- Do not produce execution sequencing or final owner selection.
