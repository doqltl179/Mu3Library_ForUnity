---
owns: "Where each kind of knowledge is stored (session, plan, project lessons and wiki, kit upstream, tool memory), what is never persisted, and how knowledge is promoted"
volatility: evolving
reviewed: 2026-10-09
---

# Memory Policy

## When

- deciding whether something learned should outlive the session,
- capturing a lesson after a correction,
- a tool offers its own persistent memory.

## Route Away When

- the procedure for capturing a lesson: the `kit-lesson-capture` skill,
- the handoff packet format: [handoff-contract.md](handoff-contract.md).

## Where Knowledge Goes

| Knowledge | Store | Lifetime |
|---|---|---|
| Current reasoning, scratch notes, temporary handoffs | Session context | The session |
| Progress, decisions, and open risks of active work | Plan file in `.ai/tasks/` | Until the task closes |
| A durable project fact (architecture, domain terms, local convention) | A page in `.ai/project/wiki/` | Until it stops being true |
| A project parameter (commands, branches, languages, roles) | `.ai/project/profile.toml` | Until changed |
| A project-specific lesson from a correction | `.ai/project/lessons.md` | Until promoted or obsolete |
| A lesson or rule that would help every project | The kit, via the `kit-upstream-propose` skill | Kit release cycle |
| A user's personal cross-project preference | The tool's user-level memory | The user's choice |

Tool-managed memory (for example auto-memory features) holds only personal preferences and pointers; project rules always live in the files above, where every tool and teammate can see them.

## Do Not Persist

- Secrets, credentials, tokens, personal data.
- Speculative rules that no review or repeated evidence supports.
- Command transcripts, terminal noise, and conversation chatter.
- Facts already recorded in code, Git history, or an owning page.
- Implementation details that will not help a future owner act.

## Promotion

1. Session → plan: anything the next step or a resumed session needs.
2. Plan → project wiki or lessons: at closeout, only what stays true after the task.
3. Lessons → wiki page: when a lesson becomes a rule the project follows, move it to the owning page and delete the lesson.
4. Project → kit: when a lesson is not project-specific, propose it upstream; keep it locally until the kit release contains it.
