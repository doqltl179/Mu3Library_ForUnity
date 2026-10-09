---
name: kit-lesson-capture
description: "Capture a lesson after a user correction or a durable discovery: screen out what must not be persisted, check whether an owner already states it, choose its destination (project lessons file, project wiki page, or an upstream kit proposal), and write one concise lesson with its reason. Use when the user corrects an agent's approach, or a task reveals a fact or pitfall that future tasks in this project will need."
category: kit
volatility: evolving
reviewed: 2026-10-09
---

# Capture Lesson

## Use When
- The user corrected how an agent worked, and the correction applies beyond this task.
- A task revealed a durable project fact or pitfall that no file records.

## Do Not Use When
- The knowledge only matters inside the current task → the session, or the task's plan via `task-plan-write`, per «Where Knowledge Goes» in [memory-policy.md](../../wiki/operating-model/memory-policy.md).
- A rule already has an owning page that needs changing → `kit-page-update`.

## Inputs
- What happened: the correction or discovery, and the evidence.
- What would have prevented the mistake or saved the effort.

## Steps
1. State the lesson as one testable imperative with a one-line reason, per [agent-first-writing.md](../../wiki/principles/agent-first-writing.md).
2. Screen it against «Do Not Persist» in [memory-policy.md](../../wiki/operating-model/memory-policy.md). On a match, stop and report that it was not persisted and why.
3. `rg` the key terms over `.ai/project/lessons.md`, `.ai/project/wiki/`, and the core wiki. When an owner already states it, write nothing new: report the owner, and run `kit-page-update` if its wording caused the miss. When an older lesson conflicts, replace that lesson.
4. Choose the store per «Where Knowledge Goes» in memory-policy.md, then act on it:
   - `.ai/project/lessons.md`: continue with step 5;
   - a page in `.ai/project/wiki/`: run `kit-page-add` or `kit-page-update`, then skip to step 7;
   - `.ai/project/profile.toml`: set the key and run `agentkit.py sync`, then skip to step 7;
   - the kit: run `kit-upstream-propose` and also keep the lesson locally per «Promotion», continuing with step 5;
   - any other store in that table: write it there, then skip to step 7.
5. Append the lesson to `.ai/project/lessons.md` in the format given by «Lessons» in [project-overlay.md](../../wiki/integration/project-overlay.md).
6. Check the existing lessons on the same topic against «Promotion» in memory-policy.md and promote each one that qualifies.
7. Run `agentkit.py check` and fix any duplicate-sentence warning the lesson caused.

## Output
- The lesson text and where it went, or the reason it was not persisted.
- Any page update or upstream proposal it triggered.
