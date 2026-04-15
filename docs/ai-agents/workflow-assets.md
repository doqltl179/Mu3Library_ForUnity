# Workflow Assets

## Purpose

This document maps the reusable workflow entrypoints that support the Mu3Library agent framework without adding new domain owners.

## Current Assets

| Type | Artifact | Purpose |
|---|---|---|
| Skill | `.github/skills/unity-compile-gate/SKILL.md` | Compile-only verification workflow that waits for completion before the next unit proceeds |
| Prompt | `.github/prompts/compile-unity.prompt.md` | Chat entrypoint for synchronous compile-only verification |
| Prompt | `.github/prompts/framework-next-unit.prompt.md` | Chat entrypoint for the bounded work -> review -> continue or rework loop |
| Hook | `.github/hooks/compile-gate.json` | Blocks the next prompt while a compile is still running or until a failed compile result is triaged |
| Script | `scripts/compile-gate/run-unity-compile.ps1` | Launches Unity batch compile and records result status |
| Script | `scripts/compile-gate/check-compile-gate.ps1` | Reads compile status and enforces the compile wait rule for hooks |
| Script | `scripts/compile-gate/acknowledge-compile-failure.ps1` | Marks a failed compile result as triaged after evidence is recorded |

## Compile-Only Gate

- The compile gate is compile-only by design. It does not imply or run tests.
- The gate is considered open only after `tasks/compile-status.json` leaves `running`, and failed results must be triaged before the next unit proceeds.
- If the target Unity project is already open in an interactive editor instance, the runner fails early with an explicit error instead of attempting a second batch instance.
- If the tracked compile process disappears while the status file still says `running`, the hook converts that stale state to `failed-stale` and blocks the current prompt so triage happens before the next unit continues.
- Failed runs retain their log path in `tasks/compile-status.json` so reviewer evidence survives the compile step.
- The compile gate collects verification evidence for `reviewer`, but `reviewer` still owns approval.

## Design Rule

- Prefer workflow assets when the repository needs a repeatable flow but not a new long-lived execution owner.
- Promote workflow assets before adding a new agent when the change is procedural rather than ownership-driven.