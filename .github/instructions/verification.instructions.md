---
description: "Static verification checklist for Mu3Library changes"
---

# Verification Instructions

## Goal

Define practical non-test verification steps per change type.

## Minimum Verification by Change Type

1. Runtime code changed (`Runtime/Scripts`):
   - Verify affected assemblies compile.
   - Verify changed code paths for null safety and define guards.
2. Editor code changed (`Editor/Scripts`):
   - Verify editor assembly compiles.
   - Verify no runtime assembly dependency leaks.
3. DI or core lifecycle changed:
   - Verify initialization/injection sequence assumptions.
   - Verify no ordering regressions in dependent samples.
4. Optional package integration changed:
   - Verify code is fully wrapped by corresponding define symbols.

## Compile-Only Workflow

- When the requested verification scope is compile-only, do not add or imply test execution.
- Use the repository compile gate workflow to run compile synchronously and wait for completion before the next task proceeds.
- Record compile state in `tasks/compile-status.json` and treat `running` as a hard stop for follow-up work.
- If compile fails or becomes `failed-stale`, stop the next unit, record triage in `tasks/todo.md`, acknowledge the status, and report the verification gap explicitly.

## If Full Verification Is Not Possible

- Perform static verification for impacted files and boundaries.
- Confirm no unintended public API signature changes.
- Report incomplete verification as an explicit risk.

## Reporting Format

Include in final report:
- What was verified.
- What could not be verified.
- Residual risk if verification is incomplete.
