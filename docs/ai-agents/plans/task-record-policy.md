# Task Record Policy

## Purpose

Keep planning lightweight while preserving one durable place to find current work.

## Canonical Split

- `tasks/todo.md` is the persistent local index.
- `tasks/plans/*.md` stores the detailed execution plans for non-trivial tasks.
- `tasks/lessons.md` stores durable lessons only after a correction, durable approach change, or explicit user preference.

## Usage Rules

1. Create or update a plan file in `tasks/plans/` for non-trivial work.
2. Add one entry in `tasks/todo.md` that links to the active plan and records its current status.
3. Delete a completed plan by default once its durable outcome is reflected in wiki pages, instructions, code, or `tasks/lessons.md`.
4. Keep a completed plan only when the user explicitly requests retention or when the file still holds reusable decision context not captured elsewhere.
5. Do not duplicate the full plan table in `tasks/todo.md`; keep only the active index-level summary there.

## Recommended Layout

```text
tasks/
  todo.md
  lessons.md
  plans/
    2026-06-11-active-task.md
```

## Naming

- Use `YYYY-MM-DD-<short-slug>.md`.
- Prefer concise slugs that describe one bounded unit.

## Completion Rule

- When closing a task, first promote any durable lesson into the owning wiki page, instruction file, code comment, or `tasks/lessons.md`.
- If no such durable residue remains, delete the completed plan instead of archiving it in the repository.
