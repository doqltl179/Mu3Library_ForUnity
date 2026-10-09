---
name: asmdef-triage
description: "Diagnose a Unity assembly-definition problem in the Mu3Library packages and choose the narrowest fix: missing or wrong references, optional-package define gates, code in the wrong assembly, or compile fallout. Use when a compile error points at assembly boundaries or an .asmdef edit is proposed; it first decides whether the edit is needed at all."
category: code-change
volatility: evolving
reviewed: 2026-10-09
---

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
3. Decide which surface the code belongs to per «Package Surfaces» and «Assemblies And Optional Integrations» in [package-architecture.md](../../wiki/rules/package-architecture.md) before touching assembly metadata.
4. Take the narrowest fix first: move the code, then split an optional integration into its gated file, and only then change references.
5. When the fix touches a public API, `package.json`, a sample, or README/CHANGELOG, hand that part to its owner instead of widening the fix.
6. Verify with the compile targets that include the affected assemblies per [unity-verification.md](../../wiki/unity-verification.md).

## Output
- The root cause and whether an `.asmdef` edit is necessary.
- The fix applied or proposed, the affected assemblies, any hand-offs, and the compile evidence.
