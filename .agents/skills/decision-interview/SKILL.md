---
name: decision-interview
description: "Settle the open decisions behind a request, plan, or design with the user: list the decisions and their prerequisites, look up every fact yourself, ask in numbered rounds only the decisions whose prerequisites are settled, each with a recommended answer, repeat until none is open, and confirm the shared understanding. Use when several user-owned decisions are open before work starts, or the user asks to be questioned about a plan."
---
<!-- agentkit:generated from .ai/kit/core/skills/decision-interview/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Interview For Decisions

## Use When
- A request, plan, spec, or design leaves two or more decisions open that the user owns per «Stop, Pause, or Proceed» in [integrity.md](.ai/kit/core/wiki/principles/integrity.md).
- The user asks to be questioned about, or to stress-test, a plan before work starts.

## Do Not Use When
- One question settles it → ask that question directly, with options and a recommendation.
- Every open point has a conventional default → proceed per «Stop, Pause, or Proceed».
- Writing down what was settled → `requirements-spec-write` for requirements, `adr-write` for a design decision, `task-plan-write` for a plan.

## Inputs
- The request, plan, or design, and any linked issue, spec, or decision record.
- The glossary at `docs.glossary` and the records in `docs.adr_dir` that cover the area, when they exist.

## Steps
1. List every open decision that could change the result. For each, note its prerequisites: the other decisions whose answers change its options. Done when nothing you would otherwise have to guess is missing from the list.
2. Take the facts off the list. A fact (current behavior, configuration, versions, what the code does) is yours to find per «Source Priority» in [external-guidance.md](.ai/kit/core/wiki/evolution/external-guidance.md); never ask the user for it. When something the user said contradicts what you find, keep it on the list as a question that states both. While a slow lookup runs, hold back only the questions that depend on it.
3. Ask one round: every open decision whose prerequisites are settled, and no question that depends on another question in the same round. Number the questions. For each, give the options when there are several and your recommended answer with a one-line reason, worded so that "yes" accepts the recommendation. Name concepts per «Using Terms» in [domain-terms.md](.ai/kit/core/wiki/workflows/domain-terms.md); an overloaded or conflicting term is itself a decision for the round.
4. Wait for the answers. Record each settled decision with its reason in the plan, in the spec or record being drafted, or in the report when none of them exists, and each settled term per «Glossary Entries» in domain-terms.md.
5. Recompute the list: answers settle prerequisites, add decisions, or remove them. Repeat steps 2 to 4 until no decision is open.
6. Summarize the settled decisions and ask the user to confirm them as the shared understanding. Start the work only after they confirm.

## Output
- The settled decisions with their reasons, and where each is recorded.
- Decisions the user deferred, each with who decides and what waits on it.
