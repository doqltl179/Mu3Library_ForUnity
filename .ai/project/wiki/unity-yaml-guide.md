---
owns: "Scene and prefab edits in this repository: the procedure for a declared direct-YAML edit and the extra checks after it"
volatility: evolving
reviewed: 2026-11-20
---

# Unity YAML Guide

The `unity` stack pack owns how serialized assets change: the route order (live Editor, then a batch-mode Editor script, then direct YAML only as a declared exception), what direct edits never touch, and the checks after one. This page adds this repository's procedure for the declared direct-YAML edit. The repository has tracked no scene or prefab since Base dropped its last sample (ADR 0001 D14, #556), so the procedure waits for the next one.

## When

- changing a scene or prefab.

## Route Away When

- the route order and the post-edit checks: «Conventions» in [unity.md](../../kit/core/stacks/frameworks/unity.md).

## Declared Direct-YAML Edit

Only when the pack's third route applies, and under its limits. For a subtree:

1. Find the nearest working subtree that already behaves like the target, and clone the whole subtree, not only its root.
2. Give every cloned block a new `&<id>` unused in the file, and remap the internal `{fileID: ...}` references to the new ids. Keep external references unless the new subtree must point elsewhere.
3. Retarget names, serialized fields, script GUIDs, and persistent event methods to the new subtree. On a cloned component, set `m_Script` to the GUID in the target script's `.cs.meta` and `m_EditorClassIdentifier` to the target type, and leave unrelated serialized fields alone: a stale `m_Script` makes Unity deserialize the wrong component or drop fields. Keep `m_Target`, `m_TargetAssemblyTypeName`, and `m_MethodName` of each persistent call in agreement; otherwise the Inspector shows a missing callback even though the YAML parses.
4. Add the new root to its parent `Transform`'s `m_Children`, and keep the cloned root's `m_Father` when the template already sits in the right container.
5. Run the pack's post-edit checks, then the extra checks below, then `commands.build` for the affected target.

## Extra Checks

After the pack's post-edit checks:

- the new object names appear in the file,
- each new `m_Script` and `m_EditorClassIdentifier` matches its script,
- each persistent call names the intended target, assembly type, and method.

When work finds a new reliable pattern or failure mode here, update this page in the same unit.
