---
owns: "Scene and prefab edits in this repository's samples: how the kit's edit routes reach a sample here, the procedure for a declared direct-YAML edit, the verified ScreenEffect sample anchors and wiring, and what sample TextMesh Pro text and EventSystems need"
volatility: evolving
reviewed: 2026-10-25
---

# Unity YAML Guide

The `unity` stack pack owns how serialized assets change: the route order (live Editor, then a batch-mode Editor script, then direct YAML only as a declared exception), what direct edits never touch, and the checks after one. This page adds what is specific to this repository's samples.

## When

- changing a sample scene or prefab,
- adding TextMesh Pro text or an `EventSystem` to a sample scene or prefab,
- extending the URP ScreenEffect sample's panels, toggles, or slider events.

## Route Away When

- the route order and the post-edit checks: «Conventions» in [unity.md](../../kit/core/stacks/frameworks/unity.md),
- what a sample folder is and how samples import: [unity-upm.md](../../kit/core/stacks/frameworks/unity-upm.md),
- who owns samples: `sample-integrity-engineer` in `.ai/generated/catalog.md`.

## Reaching A Sample

- Samples live in `Mu3Library_*/Samples~/`, which Unity does not import. Each development project exposes them through a Git-ignored link, a junction on Windows and a symbolic link elsewhere: `UnityProject_BuiltIn/Assets/Mu3LibrarySamples`, `UnityProject_URP/Assets/Mu3LibrarySamplesURP`, `UnityProject_Game_WatermelonGame/Assets/Mu3LibrarySamplesWatermelonGame`.
- The Editor and batch routes therefore edit a sample in place through that path. There is no copy to bring back, and the `.meta` files Unity writes land in `Samples~`, where they are tracked.
- A fresh clone or worktree has no links until `commands.worktree_setup` (`mu3-cli unity link-samples`) creates them; run it before taking the Editor or batch route.
- The scene owns the ScreenEffect sample's wiring. Never replace scene wiring with runtime UI generation to avoid a scene edit.

## Sample Text And Input

No development project carries the TMP Essential Resources, and a consuming project may lack them or the Input System package, so a sample scene or prefab works without both. Verified 2026-10-25 on 6000.6.7f1 with uGUI 2.6.0. The rules below apply to samples drawn with uGUI and TextMesh Pro; the Multiplayer sample draws its buttons with IMGUI (`OnGUI`), which needs neither, so they do not apply to it.

- Every TextMesh Pro text references the sample's own font asset with that asset's material, and `Fonts/Sample Style Sheet.asset`. The font asset is `Fonts/Sample Sans SDF.asset`, a static Latin font asset, except the Template sample's Korean and Japanese text, which uses `Fonts/NotoSansKR-Bold SDF.asset`. Its source font is a subset that holds printable ASCII, the KS X 1001 characters other than Hanja, and 日本語, as the sample's `Third-Party Notices.txt` lists; text outside it, such as most kanji, renders as missing glyphs.
- At runtime TextMesh Pro reads `TMP Settings` for more than the default font, and in a player without them each read throws `NullReferenceException`: a text without a style sheet of its own reads the default style sheet, and so does a `<style>` or `<a>` tag whose style the text's sheet lacks; a character its font asset lacks reads the fallback fonts; a character from a fallback font asset reads the material preset setting, and any character drawn into a sub mesh, from a fallback font asset or from a second atlas texture of a dynamic font asset, reads the sub-object setting; an emoji reads the emoji fallbacks while **Emoji Fallback Support** is on; `<font>`, `<material>`, `<gradient>`, and `<sprite>` tags read the resource paths and the default sprite asset; Hangul or another Chinese, Japanese, or Korean character reads the line-breaking rules whenever the text wraps or its overflow is Truncate, Ellipsis, or Linked. Keep each text on one font asset that carries its characters, without those tags, and keep Chinese, Japanese, or Korean text on no wrapping with an overflow mode other than those three.
- A font asset material uses the sample's `Shaders/TMP_SDF-Mobile.shader`, a renamed copy of TextMesh Pro's mobile distance field shader; TextMesh Pro's own shaders come only with the TMP Essential Resources. A dynamic font asset keeps **Get Font Features** off, since adding a glyph at runtime reads `TMP Settings` while it is on.
- A text added in an Editor without `TMP Settings` keeps `m_fontSize: -99`, because TextMesh Pro skips its setup there, and in a player such a text reads `TMP Settings` for its defaults. Retarget texts from a batch script with `SerializedObject` (`m_fontAsset`, `m_sharedMaterial`, `m_StyleSheet`, and `m_fontSize` for a new text), which changes only the serialized values, and keep `m_fontSize: -99` out of sample scenes and prefabs.
- Without `TMP Settings`, TextMesh Pro reports a preferred width and height of 0 to the layout system, in a player too (`TMP_Text.GetPreferredWidth()` and `GetPreferredHeight()` return 0 then; `GetPreferredValues(width, height)` does not check). A `ContentSizeFitter`, or a layout group that controls its children's sizes, therefore treats such a text as empty. Give every text whose size a layout takes the sample's `SampleTextPreferredSize` (in the Template sample's `Global` scripts; copy it before another sample needs it), which reports `GetPreferredValues(width, height)` above TextMesh Pro's own layout priority in a player without `TMP Settings`. Leave it off a text whose size a `LayoutElement` already sets or whose own layout priority is above 0: the layout would then pick between them differently with and without `TMP Settings`.
- A batch save can store 0 for a size that a layout component drives, such as an `AspectRatioFitter` child of a layout group, since batch mode runs no layout pass before it saves; layout recomputes the size at runtime.
- An `EventSystem` holds a `StandaloneInputModule` and the sample's `SampleInputModuleSelector`, never a serialized `InputSystemUIInputModule`, which is a missing script without the Input System package.

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
