---
applyTo: "Mu3Library_*/**/*.cs,UnityProject_*/Assets/**/*.cs,**/*.asmdef,**/*.asmref,**/*.unity,**/*.prefab,**/*.asset"
---
<!-- agentkit:generated from .ai/kit/core/stacks/frameworks/unity.md, .ai/project/profile.toml. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Unity

## Detect
- Editor version: `m_EditorVersion` in `ProjectSettings/ProjectVersion.txt`. Build and test only with that exact Editor; another version reimports and upgrades the project.
- Render pipeline: `com.unity.render-pipelines.universal` (URP) or `com.unity.render-pipelines.high-definition` (HDRP) in `Packages/manifest.json`, plus the pipeline asset assigned in Graphics settings; neither means Built-in. Shaders and materials do not port between pipelines.
- Packages: `Packages/manifest.json` (requested) and `Packages/packages-lock.json` (resolved); embedded packages live in `Packages/<name>/`.
- Assemblies: `.asmdef`/`.asmref` files. Scripts outside any asmdef compile into `Assembly-CSharp`, or `Assembly-CSharp-Editor` when under an `Editor` folder.
- Player settings in `ProjectSettings/ProjectSettings.asset`: scripting backend (Mono or IL2CPP), API compatibility level, active input handling (Input Manager, Input System package, or both).
- Asset serialization mode must be Force Text (Project Settings > Editor) for scene, prefab, and asset diffs to be readable.

## Conventions
- Keep `UnityEditor` code out of player builds: put it in an `Editor` folder or an asmdef whose `includePlatforms` is `["Editor"]`; guard Editor calls in runtime scripts with `#if UNITY_EDITOR`. A runtime assembly that references `UnityEditor` fails the player build.
- Declare a new asmdef's references explicitly; asmdefs cannot reference `Assembly-CSharp`, and references must not form cycles.
- Commit every `.meta` file and move or rename it with its asset (prefer the Editor or `AssetDatabase.MoveAsset`). The `.meta` holds the GUID that all references use; deleting or regenerating it breaks references silently.
- Name each `MonoBehaviour`/`ScriptableObject` file exactly after its class.
- Expose inspector fields with `[SerializeField] private`, not public fields. When renaming a serialized field, add `[FormerlySerializedAs("oldName")]` (`UnityEngine.Serialization`); without it the stored value is dropped.
- Change scenes, prefabs, and assets through Editor APIs (`PrefabUtility`, `EditorSceneManager`, `AssetDatabase`, `SerializedObject`) or an Editor script, not by editing YAML; hand edits break `fileID`/GUID links and prefab overrides.
- Lifecycle: `Awake` → `OnEnable` → `Start` → `FixedUpdate` → `Update` → `LateUpdate`; teardown `OnDisable` → `OnDestroy`. Initialize self in `Awake`, reach other objects in `Start`; order across objects is undefined unless set with `[DefaultExecutionOrder]` or Script Execution Order.
- Physics in `FixedUpdate`, camera follow in `LateUpdate`; scale per-frame motion by `Time.deltaTime`.
- In `Update`, `FixedUpdate`, and `LateUpdate`: use references cached in `Awake`/`Start`; never call `GetComponent`, `FindObjectsByType`, or `GameObject.Find`; avoid allocations (LINQ, closures, string concatenation, new collections, boxing); use `CompareTag` and non-allocating physics queries.
- Call Unity APIs only from the main thread.

## Verify
- Prefer `commands.build` and `commands.test`.
- Compile check: `<Unity> -batchmode -quit -projectPath <path> -logFile <file>`; a non-zero exit code or `error CS` in the log is a failure. Batchmode fails while another Editor has the project open.
- Tests (Unity Test Framework): `<Unity> -batchmode -projectPath <path> -runTests -testPlatform EditMode|PlayMode -testResults <file.xml> -logFile <file>`; narrow with `-testFilter` or `-testCategory`. Omit `-quit`; it is not supported while tests run, and the runner exits when done.
- Test asmdefs reference the test runner assemblies; EditMode test asmdefs include only the `Editor` platform.

## Pitfalls
- `UnityEngine.Object` overrides `==`: a destroyed object equals `null`, but `?.`, `??`, and `is null` bypass that check. Use `== null` or the implicit bool for Unity objects.
- Constructors on `MonoBehaviour` may run outside Play Mode and off the main thread; initialize in `Awake`. Create components with `AddComponent` and scriptable objects with `ScriptableObject.CreateInstance`, never `new`.
- Changing a field's default in code does not update instances already serialized in scenes and prefabs; they keep their stored values.
- IL2CPP code stripping removes members reached only through reflection; keep them with `[Preserve]` or `link.xml`.
- Editing `Library/`, `Temp/`, `Logs/`, or `obj/`: they are generated caches and are not committed.
- Changing a `ScriptableObject` asset in Play Mode in the Editor persists after exiting Play Mode; copy it with `Instantiate` for runtime state.

## Version Notes
- Unity 6 Editor versions are numbered `6000.x` (as of 2026-10, per unity.com/releases).
- `Awaitable` (async/await support) exists from Unity 2023.1 and Unity 6; check the project's version before replacing coroutines (as of 2026-10, per Unity Scripting API).
- `Object.FindObjectOfType`/`FindObjectsOfType` are obsolete in Unity 6; use `FindFirstObjectByType`, `FindAnyObjectByType`, or `FindObjectsByType`. From Unity 6.4, `FindObjectsSortMode` and the overloads taking it are obsolete too; call the overloads without it (as of 2026-10, per Unity Scripting API).
- URP custom render passes differ when Render Graph is enabled (the default for new URP projects in Unity 6). Unity 6.4 removes URP Compatibility Mode, so custom passes there must use the Render Graph API; on 6.0–6.3 check the Compatibility Mode setting before writing `ScriptableRenderPass` code (as of 2026-10, per the Unity 6.4 upgrade guide).
