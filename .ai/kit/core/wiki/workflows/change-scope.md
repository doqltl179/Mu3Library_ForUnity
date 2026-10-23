---
owns: "How much a change may touch: assumptions to name before editing, the smallest change that meets the request, edit boundaries inside touched files, cleanup of what the change leaves unused, and findings outside the request"
volatility: evolving
reviewed: 2026-10-23
---

# Change Scope

Every changed line traces to the request. Unrequested changes cost review time, hide the intended change, and break behavior nobody asked to touch.

## When

- starting to edit code, configuration, or docs for a request,
- about to change something the request did not name,
- a change leaves code, settings, or text unused.

## Route Away When

- the request is ambiguous, or needs more scope or a decision the user owns: «Stop, Pause, or Proceed» in [integrity.md](../principles/integrity.md),
- restructuring without a behavior change is the work itself: the `refactor-safely` skill,
- dependencies, public interfaces, migrations, and generated code: [code-changes.md](code-changes.md),
- naming new code or text: [domain-terms.md](domain-terms.md).

## Before Editing

- Name the assumptions the change rests on: which interpretation of the request, the inputs and callers the code expects, and the environment it runs in. Settle each per «Stop, Pause, or Proceed» in integrity.md, and record the ones you proceed on in the plan when one exists, and in the report.
- When the request prescribes an approach and a simpler one meets the same goal (an existing mechanism, fewer moving parts, no new dependency), the choice between them is a decision the user owns: put both, with the trade-off, per «Stop, Pause, or Proceed» before building.

## Smallest Sufficient Change

- Build what the request and its acceptance criteria need. Add no feature, option, configuration key, or extension point that nobody asked for.
- Add an abstraction (interface, base class, wrapper, plugin point, parameter) only when two real uses exist in the change or the code it touches (a test double counts as one), or a project rule or bound stack pack requires it; otherwise write the code directly. A seam with one use is indirection every reader pays for.
- Handle the failures that can happen: external input, I/O, and calls documented to fail. Leave out handling for states the surrounding code already rules out.
- Before review, reread the diff and remove every part that neither the acceptance criteria nor the owning rules (tests, doc sync, changelog) require.

## Edit Boundaries

- Change only the lines the request needs. Leave neighboring code, comments, formatting, and names as they are, even when you would write them differently.
- Write new lines to the project's rules and the bound stack packs; where those leave a choice open, match the surrounding code.
- Update a comment or doc that your change makes false; leave the others.
- Restructure code the request does not name only when that makes the requested change simpler or safer, with `refactor-safely` and before the change: as its own unit when it is planned up front, as its own commits first when it is found mid-unit.
- Formatting that the project's format check requires for the lines you touch is part of the change. A formatting or style sweep beyond that is its own unit, never part of a behavior change.

## Cleanup

- Remove what your change made unused: imports, variables, functions, configuration keys, and doc sections. Deleting a whole file this task did not create follows «Confirm Before Irreversible Or Outward Actions» in integrity.md.
- Leave what was already unused, wrong, or untidy outside the request. Name it in the report and handle it per «Leftovers Become Issues» in [issues-and-prs.md](issues-and-prs.md).
