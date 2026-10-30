---
name: development-idea-bank
description: "Generate a bank of six ranked, distinct Mu3Library package directions for the user to choose from, before any idea is judged or adopted: map capability and whitespace from repository evidence, then rank the ideas without collapsing into refinements of existing features. Use when the user wants several directions to choose between because the next package direction is unclear or past ideas feel repetitive; not for judging one idea, or scoping or executing an adopted one."
---
<!-- agentkit:generated from .ai/project/skills/development-idea-bank/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Development Idea Bank

## Use When
- The next package direction is unclear, or roadmap discussion keeps circling the same surfaces.
- A pain point may signal a missing adjacent workflow rather than a direct refinement.

## Do Not Use When
- One idea, named or picked from the bank, needs judging before adoption, or the user wants a single idea found and judged → `new-content-propose`.
- A new element for one existing package surface needs judging → `content-expansion-propose`.
- An idea is adopted and needs scope and acceptance criteria → `requirements-spec-write`.
- An adopted idea needs units and owners → `work-decompose`.

## Inputs
- The audience, package lane, or direction the user named; broad means repository-wide.
- Whether the user explicitly asked for refinement mode.

## Steps
1. From repository evidence only, gather the package intent (README, `package.json`) and the hard constraints: package fit, public API stability, assembly boundaries, define gates, docs and sample impact, and verification cost ([package-architecture.md](.ai/project/wiki/rules/package-architecture.md)).
2. Build a capability map across runtime, editor, optional integrations, samples, docs, and tooling.
3. Build a whitespace map: missing workflows, adoption wedges, ecosystem bridges, repetitive manual work, and absent package surfaces. Treat a named feature or pain point as evidence, not as the destination.
4. Only after the whitespace map exists, use limited web research to widen adjacent patterns; repository constraints win.
5. Rank six distinct ideas, each with its bucket, novelty class (`net-new`, `adjacent-new`, or `incremental`), why-new evidence, likely package surfaces, and primary risk.
   - Keep the top three `net-new` or `adjacent-new`, and at most one incremental baseline in the list.
   - Keep docs-only, sample-only, and helper-polish ideas out of the top ranks, and do not let another optional-package integration dominate only because it is adjacent.
   - Do not collapse the list into a single winner before the user chooses.
6. End with short selection hooks or questions that help choose which idea to deepen. Write a single concept brief only when the user asks for it after seeing the bank; a brief carries no verdict, and judging the picked idea before adoption runs `new-content-propose`.
7. In refinement mode, say so, stay inside the named surfaces, allow additive improvements within the current package families, and keep a new package family out of the top two.

## Output
- A package-intent summary, the capability map, the whitespace map, six ranked ideas, and selection hooks.
- No execution sequencing and no owner selection.
