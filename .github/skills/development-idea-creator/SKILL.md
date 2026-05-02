---
name: development-idea-creator
description: "Use when the user is stuck on Mu3Library package direction and needs concrete development ideas, ranked options, or a concept brief grounded in repo context and optional web research."
---

# Development Idea Creator

## Purpose

Generate package-aligned development ideas for Mu3Library before a bounded implementation unit exists.

## Use This Skill When

- the user says they are stuck, blocked, or ideas feel repetitive,
- the next package improvement direction is unclear,
- a rough concept should become a small concept brief before planning,
- external patterns may help widen the option space without overriding repository constraints.

## Boundary

This skill is pre-unit concept shaping only.
It does not choose the next owner, create task sequencing, or replace implementation planning.

## Research-Informed Structure

This skill combines four patterns:

- Prompt Engineering Guide "Agent Components": keep planning, tool use, and memory explicit.
- AI Agent Engineering in Practice: turn ambiguous asks into prompt contracts and durable context before execution.
- Google Gemini Enterprise "Idea Generation": generate broadly first, then rank using user-defined criteria.
- Agentic Patterns "Iterative Multi-Agent Brainstorming": explore diverse perspectives, then synthesize into one brief.

Use these patterns to widen the option space first, then narrow it into one concept brief.

## Workflow

1. Summarize Mu3Library intent from repository evidence relevant to the request.
2. Extract constraints: package fit, public API stability, `.asmdef` boundaries, optional define-gate impact, sample or docs impact, and verification cost.
3. If repository context is not enough, use limited web research to collect adjacent patterns or comparable package ideas.
4. Generate 3 to 5 distinct ideas across different lenses such as quick win, developer UX, optional integrations, sample expansion, runtime or editor utility, or package maintainability.
5. Score each idea against explicit fit criteria and note major risks.
6. Write a concept brief for the top idea:
   - problem or opportunity,
   - target user or maintainer value,
   - likely package surfaces,
   - hard constraints,
   - acceptance signals,
   - open questions.
7. Return control to the main agent or `orchestrator` for bounded-unit acceptance and owner selection.

## Guardrails

- Repository constraints beat web research.
- Do not emit step-by-step implementation plans, verification ownership, or final routing decisions.
- Advisory downstream-owner notes are optional annotations only.
- Keep rejected or speculative ideas session-scoped unless later approved as durable policy.

## Output Expectations

- package-intent summary,
- 3 to 5 ranked ideas with fit scores,
- risk notes for each serious option,
- one top-idea concept brief,
- optional likely downstream owners and validation questions.