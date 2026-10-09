---
name: asmdef-triage
description: "Diagnose a Unity assembly-definition problem in the Mu3Library packages and choose the narrowest fix: missing or wrong references, optional-package define gates, code in the wrong assembly, or compile fallout. Use when a compile error points at assembly boundaries or an .asmdef edit is proposed; it first decides whether the edit is needed at all."
---
<!-- agentkit:generated from .ai/project/skills/asmdef-triage/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Asmdef Triage

## Use When
- A compile error suggests a missing or wrong assembly reference, or a file in the wrong assembly.
- An optional integration may need a define-gated reference.
- Someone proposes an `.asmdef` edit, or asks whether one is needed.

## Do Not Use When
- The failure is not about assemblies → `bug-diagnose`.
- Upgrading the optional package itself → `dependency-upgrade`.

## Inputs
- The failing file or compile log, and the development project it was compiled in.

## Steps
1. Identify the failing file, the assembly it compiles into, and the nearest `.asmdef`.
2. Classify the cause: the code sits in the wrong surface, a reference is missing, a define gate is wrong, or no `.asmdef` change is needed.
3. Decide which surface the code belongs to per «Package Surfaces» and «Assembly Boundaries» in [package-architecture.md](.ai/project/wiki/package-architecture.md) before touching assembly metadata.
4. Take the narrowest fix first: move the code, then split an optional integration into its gated file per «Optional Integrations», and only then change references.
5. When the fix touches a public API, `package.json`, a sample, or README/CHANGELOG, hand that part to its owner instead of widening the fix.
6. Verify with the compile targets that include the affected assemblies per [unity-verification.md](.ai/project/wiki/unity-verification.md).

## Output
- The root cause and whether an `.asmdef` edit is necessary.
- The fix applied or proposed, the affected assemblies, any hand-offs, and the compile evidence.
