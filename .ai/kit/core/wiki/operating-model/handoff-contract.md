---
owns: "The packet passed between owners and the shape and language of the final report to the user"
volatility: evolving
reviewed: 2026-10-09
---

# Handoff Contract

## When

- work moves between owners, or is dispatched to a subagent,
- an owner returns results,
- the final report goes to the user.

## Route Away When

- whether information should be persisted beyond the session: [memory-policy.md](memory-policy.md).

## Packet

Use for every non-trivial dispatch or return. A one-line handoff is enough for trivial work.

```markdown
## Handoff
- Unit: <one bounded unit, not the whole request>
- From → To: <owner> → <owner>
- Objective: <outcome and acceptance criteria>
- Status: not-started | in-progress | blocked | review-needed | complete
- Write scope: <paths this owner may change>
- Done: <facts only, no transcripts>
- Artifacts: <files, commits, commands, outputs the next owner needs>
- Verification: <commands run and results; what was not run>
- Constraints: <contracts, boundaries, budgets that must hold>
- Open questions / Risks: <items that affect the next decision>
- Requested gate: <reviewer(s) or none>
- Persist: none | lesson | project wiki | upstream — <reason>
```

## Field Rules

- Facts, not narration. Summarize command output to the result, the failure point, and affected files.
- `Verification` lists only what actually ran in this task.
- A `blocked` status names what unblocks it.

## Report Shape

The final report to the user, written in `project.language`:

1. The outcome in one or two sentences.
2. What changed: files or artifacts, grouped by unit.
3. Verification: what ran and the result; what did not run and why.
4. Decisions made on the user's behalf, and anything left for the user (merges, approvals, choices).
5. Follow-ups registered (issue numbers) or deferred.

Keep it to what the user needs to act on; omit process narration.
