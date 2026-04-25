# Unity YAML Guide

This guide captures the verified workflow for direct Unity scene and prefab YAML edits in this repository.

## When To Use YAML Edits

- Prefer direct YAML edits when the owning architecture already expects serialized scene wiring, such as sample UI, serialized handler references, or persistent `UnityEvent` bindings.
- Do not add runtime-generation workarounds just to avoid scene edits if the scene is already the source of truth.
- Keep imported sample copies and package sample source copies aligned when both exist for the same feature.

## Safety Rules

- Only edit text-serialized Unity assets. If the asset is not stored as YAML text, stop and use Unity instead.
- Preserve `.meta` files for any add, move, or rename operation.
- Treat local `fileID` values as scene-local identifiers. They only need to be unique inside the asset being edited.
- Prefer cloning an existing serialized subtree over constructing new YAML blocks from scratch.
- Reuse verified anchors from the same scene before exploring broader surfaces.
- After a direct YAML edit, validate both structure and behavior: unique local IDs, expected serialized references, expected event methods, and compile or editor verification when available.

## Unity YAML Structure

Unity scene YAML is a list of serialized object blocks.

- Each block starts with `--- !u!<classID> &<localFileId>`.
- A `GameObject` block lists component references.
- `Transform` or `RectTransform` blocks define hierarchy through `m_Father` and `m_Children`.
- `MonoBehaviour` blocks store serialized field data, `m_Script` GUID references, and `UnityEvent` bindings.
- Internal references use `{fileID: <localFileId>}` and must be remapped when cloning within the same scene.

## Preferred Edit Pattern

For non-trivial scene edits, use this sequence:

1. Find the nearest working template subtree that already behaves like the target.
2. Clone the full subtree, not just the root object.
3. Allocate new local `fileID` values for every cloned block.
4. Remap internal `{fileID: ...}` references to the new IDs.
5. Keep external references unchanged unless the new subtree must point at a different existing object.
6. Retarget names, serialized fields, script GUIDs, and persistent event methods.
7. Update the parent `RectTransform.m_Children` list so the new subtree is actually reachable.
8. Reparse or re-scan the YAML to confirm there are no duplicate local IDs.

This is safer than hand-authoring isolated blocks because Unity UI objects usually depend on several linked components.

## Verified ScreenEffect Scene Anchors

These anchors are verified in the URP ScreenEffect sample scene and can be reused when extending that sample.

- `ScreenEffectCore` serialized handler owner: `MonoBehaviour` local `fileID 1345111599`
- Settings panel container: `RectTransform` local `fileID 1237334836`
- Toggle list container: `RectTransform` local `fileID 1605344097`
- Panel template root: `GameObject` local `fileID 1260321172` (`ToonPanel`)
- Top-level toggle template root: `GameObject` local `fileID 1223465003` (`ShakeToggle`)

Within the `ToonPanel` subtree, the reliable pattern is:

- panel root `GameObject`
- panel `RectTransform`
- handler `MonoBehaviour`
- title text `TextMeshProUGUI`
- `Options` container with slider row children
- `ActiveToggle` with a persistent `SetActive` event binding

Within the `ShakeToggle` subtree, the reliable pattern is:

- toggle root `GameObject`
- toggle `RectTransform`
- toggle `MonoBehaviour`
- label text `TextMeshProUGUI`
- persistent event binding targeting the panel root object

## Serialized Reference Rules

When adding a new sample handler that is exposed on `ScreenEffectCore`:

- add the serialized field in code first if it does not exist yet
- wire the scene field in the `ScreenEffectCore` block with the new handler component `fileID`
- keep the handler component inside the cloned panel subtree so panel-local events can target it directly

In the ScreenEffect sample, the serialized fields on `ScreenEffectCore` are the authoritative source for handler discovery. If the field is missing in scene YAML, the new panel may exist visually but will not be initialized by the sample flow.

## Script GUID And Editor Identifier Rules

For cloned handler components:

- update `m_Script` to the target handler script GUID from the corresponding `.cs.meta` file
- update `m_EditorClassIdentifier` so it matches the target runtime type
- do not change unrelated serialized fields unless the target handler actually needs different defaults

If a handler type changes but the old `m_Script` GUID remains, Unity may deserialize the wrong component or drop fields on import.

## Persistent UnityEvent Binding Rules

The ScreenEffect sample uses YAML-stored persistent calls for both toggles and sliders.

- Toggle rows retarget `m_Target` to the panel root object when opening or hiding a panel.
- Panel `ActiveToggle` retargets `m_Target` to the handler component and `m_MethodName` to `SetActive`.
- Slider rows retarget `m_Target` to the handler component and `m_MethodName` to the corresponding setter such as `SetValueWeight`.
- For sliders, update the serialized `m_Value` default when the new effect needs a different initial value.
- Keep `m_TargetAssemblyTypeName` synchronized with the target handler type and assembly name.

If `m_Target`, `m_TargetAssemblyTypeName`, and `m_MethodName` do not all agree, the Inspector may show a missing callback even if the YAML parses.

## Hierarchy Checklist

Whenever a UI subtree is cloned or shortened:

- update the parent `RectTransform.m_Children` list
- remove trimmed child rows from the cloned `Options` container if the new panel needs fewer controls than the template
- preserve the cloned root's parent reference when the template already points to the correct container
- resize containers like `Options` when the row count changes so the layout still fits the visible controls

## Validation Checklist

After editing a Unity YAML scene or prefab directly:

1. Confirm the asset still parses as YAML and has no duplicate local `fileID` values.
2. Confirm new serialized fields point at the intended component IDs.
3. Confirm new panel or toggle names are present.
4. Confirm new `m_Script` GUIDs and `m_EditorClassIdentifier` values match the intended scripts.
5. Confirm each persistent call uses the intended `m_Target`, `m_TargetAssemblyTypeName`, and `m_MethodName`.
6. Confirm imported sample and package sample copies stay in sync.
7. Run the narrowest available compile or editor verification.

## ScreenEffect-Specific Lesson

For the URP ScreenEffect sample, direct scene YAML editing is the correct path for adding handler wiring, panels, toggles, and slider events. Extending `ScreenEffectCore` with runtime UI generation to avoid scene edits is the wrong tradeoff for this repository because the sample architecture already treats the scene as the owner of that state.

## Maintenance Rule

When future work discovers a new reliable Unity YAML pattern or a new failure mode in this repository, update this guide in the same task so the next agent starts from the verified workflow instead of rediscovering it.