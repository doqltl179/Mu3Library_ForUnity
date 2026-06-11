---
name: asmdef-triage
description: "Use when diagnosing or planning changes around Unity assembly definition files in this repository: missing references, optional-package define gates, assembly boundary drift, compile fallout, or deciding whether an `.asmdef` edit is actually needed."
---

# Asmdef Triage

## Purpose

Diagnose `.asmdef` problems and keep assembly-definition edits smaller, safer, and less repetitive.

Shared routing and Unity/package boundary rules: `../../../docs/ai-agents/routing/unity-specialist-routing.md`, `../../instructions/unity-architecture.instructions.md`.

## Use This Skill When

- a compile error suggests a missing or incorrect assembly reference,
- a runtime or editor file may be in the wrong assembly,
- an optional package integration may need define-gated assembly references,
- a proposed fix might widen dependencies or break package boundaries,
- the user asks whether an `.asmdef` change is necessary.

## Workflow

1. Identify the failing file, assembly, and nearest existing `.asmdef`.
2. Decide whether the issue is ownership, missing reference, wrong define gate, or an unnecessary `.asmdef` change request.
3. Check whether the change belongs in base runtime, base editor, URP runtime, URP editor, samples, or tooling before editing assembly metadata.
4. Prefer the narrowest fix: move code, split optional integration, or adjust references only when simpler ownership fixes fail.
5. Treat public API, package manifests, sample integrity, and docs-sync as escalation surfaces when the `.asmdef` change affects them.
6. Define the smallest meaningful verification surface, usually compile-only on affected generated Unity `.csproj` files.

## Local Guardrails

- Use the linked routing and architecture rules for package ownership, define-gate boundaries, and runtime/editor separation.
- Prefer the narrowest fix before editing assembly metadata.
- Treat public API, package manifests, samples, and docs sync as escalation surfaces when the assembly change touches them.

## Output Expectations

- suspected root cause,
- whether an `.asmdef` edit is necessary,
- narrowest safe fix options,
- affected assemblies and escalation surfaces,
- verification plan.
