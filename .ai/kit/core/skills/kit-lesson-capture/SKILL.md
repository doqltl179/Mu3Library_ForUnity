---
name: kit-lesson-capture
description: "Capture a lesson after a user correction or a durable discovery: screen out what must not be persisted, check whether an owner already states it, choose its destination in order (a code change, an automated check, or a written store: project lessons file, project wiki page, or an upstream kit proposal), and write one concise lesson with its reason. Use when the user corrects an agent's approach, or a task reveals a fact or pitfall that future tasks in this project will need."
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
- A rule's owning page needs changing → `kit-page-update`, except a core page in an installed project, which is read-only: a correction that departs from it is a `project.guardrails` replacement per «Where Knowledge Goes» in memory-policy.md.

## Inputs
- What happened: the correction or discovery, and the evidence.
- What would have prevented the mistake or saved the effort.

## Steps
1. State the lesson as one testable imperative with a one-line reason, per [agent-first-writing.md](../../wiki/principles/agent-first-writing.md). Bound it by the condition the evidence supports: one corrected case yields a rule for the cases that share its cause, not a ban that reaches every case.
2. Screen it against «Do Not Persist» in [memory-policy.md](../../wiki/operating-model/memory-policy.md). On a match, stop and report that it was not persisted and why.
3. `rg` the key terms over `.ai/project/lessons.md`, `.ai/project/wiki/`, and the core wiki. When an owner already states it, write nothing new: report the owner, and run `kit-page-update` if its wording caused the miss. When that owner is a written store, the mistake happened anyway, and a code change or automated check can hold the rule, continue with step 4 instead, which moves the rule up per step 3 of «Promotion». When an older lesson conflicts, replace that lesson. When the lesson departs from a core policy, continue with step 4, which stores it as a replacement per «Layers and Precedence» in [ssot.md](../../wiki/principles/ssot.md); when it contradicts a core invariant, stop and report the conflict.
4. Choose the store per «Where Knowledge Goes» in memory-policy.md, in the order it gives for a rule learned from a correction, then act on it. Whatever store holds it here, also name or propose a lesson that would help every project per step 4 of «Promotion».
   - a code change that makes the mistake impossible: make it in the current unit only when its request covers that code; otherwise register it as its own unit with `github-issue-create`, restructuring per `refactor-safely` when the change is a restructuring. After making it, skip to step 6; after registering it, keep the lesson until the change lands: continue with step 5 when no lesson or page states the rule yet, otherwise with step 6;
   - an automated check: add it in the current unit only when its request covers that configuration; otherwise register it with `github-issue-create` for the configuration's owner. When the code already breaks the rule, exempt those existing places by a baseline or exclusion list so the check fails only on new ones, and register their cleanup as its own unit per «Leftovers Become Issues» in [issues-and-prs.md](../../wiki/workflows/issues-and-prs.md): the pattern stops spreading now without the cleanup blocking this unit. The baseline only shrinks: adding a place to it later disables the check there, per «Flaky Tests And Disabled Checks» in [verification.md](../../wiki/workflows/verification.md). Make the check's failure message name the fix the rule wants, so the next agent repairs the cause instead of suppressing the check. After adding it, skip to step 6; after registering it, keep the lesson until the check lands: continue with step 5 when no lesson or page states the rule yet, otherwise with step 6;
   - `.ai/project/lessons.md`: continue with step 5;
   - a page in `.ai/project/wiki/`: run `kit-page-add` or `kit-page-update`, then skip to step 7;
   - `.ai/project/profile.toml`: set the key or add the `project.guardrails` entry, run `agentkit.py sync`, then skip to step 7;
   - the kit: propose it with `kit-upstream-propose`, or name it as a candidate, per step 4 of «Promotion»; keep the lesson locally either way, continuing with step 5;
   - any other store in that table: write it there, then skip to step 7.
5. Append the lesson to `.ai/project/lessons.md` in the format given by «Lessons» in [project-overlay.md](../../wiki/integration/project-overlay.md).
6. Check the existing lessons on the same topic against «Promotion» in memory-policy.md and promote each one that qualifies.
7. Run `agentkit.py check` and fix any duplicate-sentence warning the lesson caused.

## Output
- The lesson text and where it went, or the reason it was not persisted.
- Any page update or upstream proposal it triggered.
