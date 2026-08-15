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
- Compile through the Unity Editor CLI: `./compile-unity.sh changed`, or an explicit target such as `./compile-unity.sh built-in`. `mu3-cli unity compile` wraps the same entrypoint.
- Unity builds the assemblies from `.asmdef` and the package sources, so a generated `.csproj` is never consulted and cannot go stale against the code under verification. Do not verify with `dotnet build`: it compiles a different input, so a pass or a failure there says nothing reliable about the Unity compile.
- The script compiles in place when the project is closed and mirrors it into a temporary project when the Editor holds the lock, so a run never disturbs an open Editor, the repository packages, or the repository `Library`.
- If Unity compile verification cannot be completed, report the verification gap explicitly before proceeding.

## If Full Verification Is Not Possible

- Perform static verification for impacted files and boundaries.
- Confirm no unintended public API signature changes.
- Report incomplete verification as an explicit risk.

## Reporting Format

Include in final report:
- What was verified.
- What could not be verified.
- Residual risk if verification is incomplete.
- Which compile targets were run, and the Unity exit status with the error and warning counts from its log.
