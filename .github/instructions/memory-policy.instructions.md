---
applyTo: '**'
description: "Memory routing and handoff protocol for Mu3Library multi-agent work"
---

# Memory Policy Instructions

Use this instruction to decide whether information belongs in user memory, session memory, repository memory, or nowhere.

## Routing Rules

- User memory: stable cross-repository preferences only.
- Session memory: default home for current iteration state, routing notes, unresolved risks, and temporary handoff packets.
- Repository memory: durable conventions and verified project facts only.
- If persistence scope is unclear, keep it in session memory until requested review gates pass.

## Handoff Rule

- Use a structured packet for non-trivial owner changes.
- Open `docs/ai-agents/contracts/handoff-contract.md` only when the exact packet fields are needed.
- Free-form summaries are acceptable only for trivial handoffs.
- Keep command transcripts and repeated terminal noise out of handoff packets; record the result, affected files, and verification status instead.

## Promotion Rule

- Source owners may propose persistence.
- `orchestrator` confirms final promotion after review gates complete.
- If structural disposition is `rework`, keep state session-scoped and revise there.

## Do Not Persist

- secrets or credentials,
- speculative rules that have not passed review,
- repeated chatter or terminal noise,
- implementation details that will not help a future owner act.
