# Unity Specialist Routing

## When

- you need to choose between `unity-runtime`, `unity-editor`, `package-integration`, `sample-integrity`, and `unity`,
- a Unity task touches multiple package surfaces and the primary owner is unclear,
- you want the shared owner-selection and split rules without re-reading several agent specs.

## Route Away When

- the question is about approved owner inventory or rollout status: use [agent-catalog.md](agent-catalog.md),
- the question is about the direct YAML edit procedure: use [unity-yaml-guide.md](../guides/unity-yaml-guide.md),
- the question is about stable framework rationale instead of Unity owner selection: use [architecture.md](../architecture.md).

## Owns

- the shared owner-selection matrix and shared routing rules for the Unity specialist agent family.

## Owner Selection Matrix

| Task Shape | Primary Owner | Route Away When |
|---|---|---|
| Non-gated runtime behavior under `Mu3Library_Base/Runtime/Scripts` or `Mu3Library_URP/Runtime/Scripts` | `unity-runtime` | editor tooling, define-gated optional integration, sample integrity, or a genuinely cross-boundary Unity change becomes dominant |
| Non-gated editor tooling under `Mu3Library_Base/Editor/Scripts` or `Mu3Library_URP/Editor/Scripts` | `unity-editor` | runtime behavior, define-gated optional integration, sample integrity, or a genuinely cross-boundary Unity change becomes dominant |
| Define-gated optional integration across runtime, editor, or package surfaces | `package-integration` | the change is mostly non-gated runtime or editor work, or the task is no longer integration-centric |
| Sample manifests, `Samples~`, import footprints, or sample smoke checks | `sample-integrity` | the dominant work is a core runtime, editor, or optional-integration fix rather than a sample-owned surface |
| A Unity package task that genuinely spans narrower specialists and cannot be cleanly held by one narrower owner | `unity` | the work can be split into bounded narrower units, or one narrower owner is clearly dominant |

## Shared Routing Rules

- Prefer the narrowest owner first.
- If one surface is clearly dominant, keep ownership there and coordinate with adjacent owners instead of widening ownership.
- If a sample exposes a core defect, keep sample-owned edits with `sample-integrity` and route the core fix to the owning specialist.
- If define-gated optional behavior is the primary concern, `package-integration` owns the task even when runtime or editor files are touched.
- Use `unity` only when a narrower split would hide real cross-boundary package coupling.

## Shared Coordination Rules

- Use [handoff-contract.md](../contracts/handoff-contract.md) for handoff packets and persistence guidance.
- Use `reviewer` when public APIs, `.asmdef` boundaries, define gates, docs sync, or release-related surfaces are affected.
- Use [unity-yaml-guide.md](../guides/unity-yaml-guide.md) before editing serialized scene or prefab YAML directly.
- If the change is actually about framework ownership structure instead of Unity package delivery, route to [iteration-process.md](../workflow/iteration-process.md) and the framework-control agents instead.

## Duplication Rule

- Do not restate this owner-selection matrix in every Unity agent spec.
- Individual Unity agent docs should link here and document only the deltas that are unique to that owner.