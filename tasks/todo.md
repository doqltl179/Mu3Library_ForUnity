## Task Plan

<!--
Task Plan guidance:
- Keep this table limited to the current actionable work for the active task or unit.
- Use short, verifiable steps instead of broad epics or session transcripts.
- Task: concise action title.
- Status: Not Started / In Progress / Completed / Blocked / Failed.
- Details: concrete scope, touched files, or verification note.
- Do not store terminal output, timestamps, or investigation history here.
-->

| # | Task | Status | Details |
|---|------|--------|---------|
| 1 | Clarify tracked-file ignore approach | Completed | Confirmed this file is still tracked and that a post-commit local-only ignore should use a tracked-file strategy rather than `.gitignore` |
| 2 | Add Task Plan guidance comment | Completed | Added a hidden comment describing what belongs in the `Task Plan` section |
| 3 | Add Review Summary guidance comment | Completed | Added a hidden comment describing what belongs in the `Review Summary` section |
| 4 | Record durable workflow note | Completed | Updated `tasks/lessons.md` with the todo template and local-ignore guidance for future work |

## Shared Notes

- Keep this file limited to the current actionable plan and short durable review notes.
- Do not store transient compile log paths, branch divergence snapshots, or investigation transcripts here.
- Runtime compile status remains local-only in `tasks/compile-status.json`.

## Review Summary

<!--
Review Summary guidance:
- Record only durable outcomes from the completed task.
- Capture what changed, what was verified, and any explicit residual risk or follow-up constraint.
- Keep this section concise; do not paste raw logs, branch snapshots, or step-by-step transcripts.
-->

- Added hidden template comments to `Task Plan` and `Review Summary` so the baseline file explains what each section should contain.
- Updated the current task record to reflect this todo-template maintenance work.
- Did not apply ignore in this step because the baseline file still needs to be committed first; after that, use a tracked-file local ignore such as `git update-index --skip-worktree tasks/todo.md` rather than `.gitignore`.