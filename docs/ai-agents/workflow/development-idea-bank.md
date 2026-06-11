# Development Idea Bank

## When

- the next package direction is unclear,
- current roadmap discussion keeps circling the same surfaces,
- one pain point likely signals a missing adjacent workflow,
- a shortlist of new directions is needed before choosing a bounded implementation unit.

## Route Away When

- this ideation contract is no longer the question: use [../README.md](../README.md) for the top-level router.
- the need is specifically workflow-asset inventory or stable framework rationale: use [workflow-assets.md](workflow-assets.md) or [architecture.md](../architecture.md).

## Owns

- the role contract for the `development-idea-bank` workflow asset.
- repository-shaped ideation that widens option space before bounded implementation work is chosen.

## Default Role

Unless the user explicitly asks for refinement work:

- treat a named feature or pain point as evidence, not the destination,
- map capability and whitespace before proposing solutions,
- keep the top 3 ideas in `net-new` or `adjacent-new` territory,
- keep docs-only, sample-only, and helper-polish ideas out of the top ranks,
- preserve multiple viable directions instead of converging on one brief too early.

## Required Inputs

Before ranking ideas, gather:

1. current package intent from repository evidence,
2. a capability map across runtime, editor, optional integrations, samples, docs, and tooling,
3. a whitespace map of missing workflows, adoption wedges, ecosystem bridges, repetitive manual work, or absent package surfaces,
4. hard constraints on package fit, public API stability, `.asmdef` boundaries, optional define gates, docs or sample impact, and verification cost.

## Output Contract

The default output should return:

1. a package-intent summary,
2. a capability map,
3. a whitespace map,
4. six ranked ideas, each with a bucket, novelty class, why-new evidence, likely package surfaces, and primary risk,
5. short selection hooks that help decide which idea to deepen next.

Only write a single concept brief when the user explicitly asks for a deeper pass after seeing the bank.

## Anti-Patterns

Avoid these failure modes:

- turning a broad ask into a near-term backlog item too early,
- producing several variations of the same existing-feature enhancement,
- ranking docs-only, sample-only, or helper-polish ideas first,
- letting another optional-package expansion dominate just because it is adjacent,
- collapsing the response into a single winner before the user has chosen one.

## Refinement Mode

If the user explicitly asks for safe incremental refinement:

- state that the output is in refinement mode,
- keep the work inside the named surfaces,
- allow additive improvements inside current package families,
- avoid introducing a new package family in the top 2 ideas.

## Lightweight Checks

When the prompt, skill, or routing changes, confirm that:

- the bank still starts from repository evidence and whitespace mapping,
- the top 3 ideas are not mostly incremental,
- at most one incremental baseline remains in the list,
- the default output preserves multiple directions,
- concept-brief expansion appears only on explicit request.
