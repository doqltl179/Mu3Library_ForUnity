---
name: development-idea-bank
description: "Use when the user needs a Mu3Library idea bank of genuinely new package directions and wants to avoid collapsing back into refinement of current features."
---

# Development Idea Bank

## Purpose

Generate a package-aligned idea bank for Mu3Library before a bounded implementation unit exists.
Default toward discovering new package surfaces, workflow leverage, ecosystem bridges, and adoption wedges rather than polishing current features.
Preserve multiple viable directions instead of collapsing early into a single concept brief.

## Use This Skill When

- the user says they are stuck, blocked, or ideas feel repetitive,
- the next package direction is unclear,
- the user wants a repository-shaped idea bank or shortlist before planning,
- external patterns may help widen the option space without overriding repository constraints,
- the user wants future-facing package bets instead of another backlog-refinement pass.

## Default Stance

Unless the user explicitly asks for incremental refinement:

- treat "development ideas" as whitespace discovery and option-bank expansion,
- treat a named pain point or existing feature as a signal, then widen one hop to adjacent workflows before ranking ideas,
- prefer ideas that create a new reusable surface, new workflow leverage, or a new adoption path,
- keep the top 3 ideas in `net-new` or `adjacent-new` territory,
- allow at most one incremental baseline idea for comparison,
- avoid auto-converging to one implementation-ready brief.

## Failure Mode To Avoid

Do not stop at "improve an existing feature".
Classify an option as incremental if it is mostly:

- more convenience methods or wrappers around an existing surface,
- minor editor UX polish,
- documentation-only or sample-only cleanup,
- another toggle or option for a current capability,
- narrowing a broad request into a near-term backlog item too early.

Incremental ideas can appear only as a low-priority baseline unless the user explicitly wants that class of work.
They cannot occupy the top 3 by default.

## Boundary

This skill is pre-unit concept shaping only.
It produces an idea bank or shortlist, not a chosen implementation unit.
It does not choose the next owner, create task sequencing, or replace implementation planning.
Deepen one idea into a concept brief only if the user explicitly asks after seeing the bank.

## Research-Informed Structure

This skill combines four patterns:

- Prompt Engineering Guide "Agent Components": keep repository evidence, tools, and constraints explicit.
- AI Agent Engineering in Practice: turn ambiguous asks into a repeatable prompt contract before execution.
- Google Gemini Enterprise "Idea Generation": diverge first, then rank with explicit criteria.
- Agentic Patterns "Iterative Multi-Agent Brainstorming": force distinct lenses before synthesis.

Use these patterns to widen the option space first, then narrow only as far as a reusable idea bank or shortlist.

## Required Discovery Pass

Before generating ideas:

1. Summarize current package intent from repository evidence relevant to the request.
2. Build a quick capability map across the relevant runtime, editor, optional integration, sample, docs, and tooling surfaces.
3. Build a whitespace map that lists missing workflows, repeated manual work, missing bridges, absent teaching assets, missing ecosystem integrations, or future-facing utility surfaces.
4. Use limited web research only after the whitespace map exists and only to widen adjacent patterns.

## Divergence Rules

Generate ideas across distinct buckets.
Unless the user narrows the scope so tightly that this is impossible, include:

- one net-new package surface,
- one workflow or tooling multiplier,
- one ecosystem bridge or optional integration,
- one adoption or discoverability bet,
- one higher-risk strategic bet,
- at most one low-novelty baseline idea for contrast.

For each idea, explicitly state:

- idea-bank bucket,
- novelty class,
- why it is not just an improvement of an existing feature,
- likely package surfaces,
- primary risk,
- the user or maintainer value unlocked.

If an idea duplicates a current capability, mark it as duplicate and replace it.

## Ranking Rules

Score each serious option against explicit fit criteria:

- package fit,
- novelty,
- leverage,
- feasibility,
- verification cost or operational risk.

Novelty must materially affect rank.
A low-novelty idea cannot reach the top 3 unless the user explicitly asked for safe incremental work.
The ranking should keep multiple viable directions alive instead of pretending the first pick is already selected.

## Workflow

1. Summarize Mu3Library intent and the current capability map from repository evidence.
2. Extract hard constraints: package fit, public API stability, `.asmdef` boundaries, optional define-gate impact, sample or docs impact, and verification cost.
3. List 4 to 8 whitespace opportunities before proposing solutions.
4. If repository context is not enough, use limited web research to collect adjacent patterns or comparable package ideas.
5. Generate 6 distinct ideas across the required buckets.
6. Score each idea against the ranking criteria, note major risks, and explain the novelty evidence.
7. Return an idea bank that includes:
   - a ranked shortlist,
   - 1 to 2 lines of rationale per idea,
   - tags or buckets that show the spread of directions,
   - short follow-up hooks for which idea to deepen next.
8. Only write a concept brief for one idea if the user explicitly asks for that deeper pass after seeing the bank.
9. Return control to the main agent or `orchestrator` for bounded-unit acceptance and owner selection.

## Guardrails

- Repository constraints beat web research.
- Do not emit step-by-step implementation plans, verification ownership, or final routing decisions.
- Advisory downstream-specialist notes are optional annotations only.
- If the user explicitly requests refinement work, state that the output is in incremental-refinement mode, relax the novelty quota, and keep the work inside the named surfaces.
- Keep rejected or speculative ideas session-scoped unless later approved as durable policy.

## Output Expectations

- package-intent summary,
- capability map,
- whitespace map,
- 6 ranked ideas with bucket, novelty class, and fit scores,
- risk notes for each serious option,
- short selection hooks or validation questions,
- optional single-idea concept brief only on request.