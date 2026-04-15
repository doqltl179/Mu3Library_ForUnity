---
applyTo: '**'
description: "Memory routing and handoff protocol for Mu3Library multi-agent work"
---

# Memory Policy Instructions

## Scope

Use this instruction when deciding whether information belongs in user memory, session memory, repository memory, or nowhere at all.

## Memory Routing Rules

- User memory is only for stable cross-repository preferences and habits.
- Session memory is the default home for current iteration state, routing notes, unresolved risks, and temporary handoff packets.
- Repository memory is only for durable conventions and verified project facts.

If persistence scope is unclear, prefer session memory until the review gate is complete.

## Handoff Rule

For any non-trivial handoff, capture a structured packet that includes:

- feature unit,
- source and target owner,
- objective,
- status,
- completed work,
- relevant artifacts,
- constraints,
- open questions,
- risks,
- requested review,
- persistence proposal.

Free-form summaries are acceptable only for trivial handoffs. A structured packet may be expressed inline in the working context or persisted in session memory when the next owner needs it to survive the current exchange.

## Promotion Rule

- The source owner may propose persistence, but orchestrator confirms final promotion.
- Do not promote temporary state to repository memory before all requested review gates pass.
- If the structural disposition is `rework`, keep the state in session memory and revise it there.
- Promote to repository memory only when the fact is both stable and likely to help future iterations.

## Do Not Persist

- secrets or credentials,
- speculative rules that have not passed review,
- repeated chatter or terminal noise,
- implementation detail that will not help a future owner act.