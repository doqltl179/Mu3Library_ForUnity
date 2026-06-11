---
name: editmode-test-addition
description: "Use when adding, revising, or planning Unity EditMode tests in this repository: choosing where tests should live, matching existing test patterns, deciding whether EditMode coverage is appropriate, or defining the smallest safe verification surface when full test execution is not feasible."
---

# EditMode Test Addition

## Purpose

Add or plan EditMode tests without repeatedly re-deriving placement, scope, and verification rules.

Shared routing and Unity/package boundary rules: `../../../docs/ai-agents/routing/unity-specialist-routing.md`, `../../instructions/unity-architecture.instructions.md`.

## Use This Skill When

- a runtime or editor change should gain focused EditMode coverage,
- the user asks for a test alongside a bug fix or logic change,
- it is unclear where a new test should live in this repository,
- a proposed test may be better handled by compile-only verification or static review instead,
- an existing comment or behavior references EditMode validation and the coverage needs to be extended.

## Workflow

1. Identify the code surface, nearest assembly, and any existing test files or comments that already validate similar behavior.
2. Decide whether EditMode coverage is the right fit versus compile-only verification, static review, or a different test surface.
3. Prefer following an existing local test pattern before inventing a new folder or assembly layout.
4. If no stable test location is obvious, state that explicitly and choose the smallest repository-consistent placement instead of creating a broad new test structure.
5. Keep the test focused on logic, boundaries, and regressions introduced by the current change; avoid widening into unrelated coverage.
6. Define the smallest meaningful verification plan and report any gap if Unity test execution cannot be completed.

## Local Guardrails

- Use the linked routing and architecture rules for package ownership and runtime/editor boundaries.
- Prefer EditMode tests for deterministic logic and editor-safe validation, not scene-wide behavioral smoke by default.
- Do not widen runtime/editor or assembly boundaries just to make a test compile.
- Treat public API, samples, and package-manifest changes as escalation surfaces.

## Output Expectations

- whether EditMode coverage is appropriate,
- proposed test location and rationale,
- nearest existing pattern or explicit lack of one,
- minimal test cases to add,
- verification plan and residual gap.
