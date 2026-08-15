# Task Record Policy

## Purpose

Keep planning lightweight while preserving one durable place to find current work. A plan file represents active work only, so completed work cannot be mistaken for an active request in a later session.

## Canonical Split

- `tasks/todo.md` is the persistent local index.
- `tasks/plans/*.md` stores detailed execution plans only while their non-trivial task is active.
- `tasks/lessons.md` stores durable lessons only after a correction, durable approach change, or explicit user preference.

## Usage Rules

1. Create or update a plan file in `tasks/plans/` for non-trivial work. Name each new file according to the required timestamp format below.
2. Add one entry in `tasks/todo.md` that links to the active plan and records its current status.
3. At task completion, first reflect every durable outcome in its owning wiki page, instruction, code, or `tasks/lessons.md`, then delete the plan file in the same closeout.
4. Never retain, archive, or rename a completed plan file. If it contains reusable decision context, promote that context to its owning record before deletion.
5. Do not duplicate the full plan table in `tasks/todo.md`; keep only the active index-level summary there.

## Approval-Free Closeout

The executing agent may delete a completed temporary plan without an additional user confirmation only when every condition below is true:

- the agent created the plan for the current active task,
- the target is one exact Markdown file directly under `tasks/plans/` and is linked by the current task's `tasks/todo.md` entry,
- all durable outcomes and lessons have already moved to their owning records,
- implementation and required verification are complete,
- the matching `tasks/todo.md` entry is removed in the same closeout.

This exception does not cover a pre-existing or different task's plan, any directory, branch, worktree, Unity `Library`, or any other file. Those targets keep their normal inspection and explicit-approval requirements. If authorship, task identity, completion, or the exact target is uncertain, do not use the exception.

## Recommended Layout

```text
tasks/
  todo.md
  lessons.md
  plans/
    2026-08-03T14-30-52-123Z-active-task.md
```

## Naming

- Use `YYYY-MM-DDTHH-mm-ss-fffZ-<short-slug>.md`, where the timestamp is the plan creation time in UTC. For example: `2026-08-03T14-30-52-123Z-active-task.md`.
- Keep the creation timestamp unchanged while updating an active plan. Lexical filename order must reflect the order in which work was requested and began across sessions.
- Prefer concise slugs that describe one bounded unit.

## Completion Rule

- When closing a task, first promote any durable lesson into the owning wiki page, instruction file, code comment, or `tasks/lessons.md`.
- Delete the plan in the same closeout and remove or update its `tasks/todo.md` entry so it no longer appears active.
- When all Approval-Free Closeout conditions hold, perform that exact plan deletion automatically instead of pausing for confirmation.
- A task is not fully closed while its completed plan file remains in the repository. Do not archive completed plans.
