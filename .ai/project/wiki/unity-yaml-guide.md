---
owns: "Scene and prefab edits in this repository's samples: how the kit's edit routes reach a sample here, the procedure for a declared direct-YAML edit, and the verified ScreenEffect sample anchors and wiring"
volatility: evolving
reviewed: 2026-10-09
---

# Unity YAML Guide

The `unity` stack pack owns how serialized assets change: the route order (live Editor, then a batch-mode Editor script, then direct YAML only as a declared exception), what direct edits never touch, and the checks after one. This page adds what is specific to this repository's samples.

## When

- changing a sample scene or prefab,
- extending the URP ScreenEffect sample's panels, toggles, or slider events.

## Route Away When

- the route order and the post-edit checks: «Conventions» in [unity.md](../../kit/core/stacks/frameworks/unity.md),
- what a sample folder is and how samples import: [unity-upm.md](../../kit/core/stacks/frameworks/unity-upm.md),
- who owns samples: `sample-integrity-engineer` in `.ai/generated/catalog.md`.

## Reaching A Sample

- Samples live in `Mu3Library_*/Samples~/`, which Unity does not import. In the main checkout each development project exposes them through a Git-ignored junction: `UnityProject_BuiltIn/Assets/Mu3LibrarySamples`, `UnityProject_URP/Assets/Mu3LibrarySamplesURP`, `UnityProject_Game_WatermelonGame/Assets/Mu3LibrarySamplesWatermelonGame`.
- The Editor and batch routes therefore edit a sample in place through that path. There is no copy to bring back, and the `.meta` files Unity writes land in `Samples~`, where they are tracked.
- A fresh worktree has no junction: work in the main checkout, or recreate the junction before taking the Editor or batch route.
- The scene owns the ScreenEffect sample's wiring. Never replace scene wiring with runtime UI generation to avoid a scene edit.

## Declared Direct-YAML Edit

Only when the pack's third route applies, and under its limits. For a UI subtree:

1. Find the nearest working subtree that already behaves like the target, and clone the whole subtree, not only its root.
2. Give every cloned block a new `&<id>` unused in the file, and remap the internal `{fileID: ...}` references to the new ids. Keep external references unless the new subtree must point elsewhere.
3. Retarget names, serialized fields, script GUIDs, and persistent event methods per «ScreenEffect Wiring».
4. Add the new root to its parent `RectTransform`'s `m_Children`, and keep the cloned root's `m_Father` when the template already sits in the right container.
5. Run the pack's post-edit checks, then the extra checks below, then `commands.build` for the affected target.

## ScreenEffect Anchors

In `Mu3Library_URP/Samples~/ScreenEffect/Scenes/Sample_ScreenEffect.unity`, verified 2026-10-09:

| Object | Class | Anchor |
|---|---|---|
| `ScreenEffectCore` handler owner | `MonoBehaviour` (114) | `1345111599` |
| `SettingsPanel` container | `RectTransform` (224) | `1237334836` |
| Toggle list `Content` | `RectTransform` (224) | `1605344097` |
| `ShakePanel` root | `GameObject` (1) | `913385146` |
| `GrayscalePanel` root | `GameObject` (1) | `1101376261` |
| `GaussianBlurPanel` root | `GameObject` (1) | `3000000403` |
| `DepthOutlinePanel` root | `GameObject` (1) | `3000000865` |
| `ShakeToggle` root | `GameObject` (1) | `1223465003` |

- A panel subtree holds the root `GameObject`, its `RectTransform`, the effect handler `MonoBehaviour`, a title text, an `Options` container of slider rows, and an `ActiveToggle` with a persistent `SetActive` call.
- A top-level toggle holds its root, `RectTransform`, toggle `MonoBehaviour`, label text, and a persistent call targeting its panel root.
- Anchors change when the scene is saved by the Editor; re-check one with `rg -n "^--- !u!\d+ &<anchor>$"` before relying on it.

## ScreenEffect Wiring

- A new handler is discovered only through a serialized field on `ScreenEffectCore`: add the field in code first, then set it in the `ScreenEffectCore` block to the new handler's component id. Without it the panel shows but the sample never initializes it.
- Keep the handler component inside its panel subtree so panel-local events can target it.
- On a cloned handler, set `m_Script` to the GUID in the target script's `.cs.meta` and `m_EditorClassIdentifier` to the target type; leave unrelated serialized fields alone. A stale `m_Script` makes Unity deserialize the wrong component or drop fields.
- Persistent calls: a top-level toggle targets its panel root; the panel's `ActiveToggle` targets the handler with `SetActive`; a slider row targets the handler with its setter, such as `SetValueWeight`, and its `m_Value` holds the effect's initial value.
- Keep `m_Target`, `m_TargetAssemblyTypeName`, and `m_MethodName` in agreement; otherwise the Inspector shows a missing callback even though the YAML parses.
- When a cloned panel needs fewer rows than its template, remove the extra rows from `Options` and resize `Options` to fit the rows that remain.

## Extra Checks

After the pack's post-edit checks:

- the new panel and toggle names appear in the file,
- each new field on `ScreenEffectCore` points at the intended component id,
- each new `m_Script` and `m_EditorClassIdentifier` matches its script,
- each persistent call names the intended target, assembly type, and method.

When work finds a new reliable pattern or failure mode here, update this page in the same unit.
