---
description: "Testing and verification checklist for Mu3Library changes"
---

# Testing Instructions

## Goal

Define practical verification steps per change type.

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

## Suggested Commands

- Use available local build/test tooling for the environment.
- If commands are unavailable, perform static verification and report limitations.

Examples (adapt paths and executable name per environment):

```powershell
# Unity edit-mode tests (example)
Unity.exe -batchmode -nographics -quit `
  -projectPath . `
  -runTests -testPlatform EditMode `
  -testResults .\\Logs\\editmode-test-results.xml

# Unity play-mode tests (example)
Unity.exe -batchmode -nographics -quit `
  -projectPath . `
  -runTests -testPlatform PlayMode `
  -testResults .\\Logs\\playmode-test-results.xml
```

If Unity CLI execution is unavailable:
- Verify changed files are in correct assemblies and define-guarded where required.
- Confirm no unintended public API signature changes.
- Report this as a verification gap in the final summary.

## Reporting Format

Include in final report:
- What was verified.
- What could not be verified.
- Residual risk if verification is incomplete.
