---
id: unity
title: Unity
kind: framework
applies_to: ["**/Assets/**/*.cs", "**/Packages/**/*.cs", "**/*.asmdef", "**/*.asmref", "**/*.unity", "**/*.prefab", "**/*.asset"]
related: [csharp, unity-upm]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://docs.unity3d.com/Manual/index.html", "https://docs.unity3d.com/ScriptReference/index.html", "https://docs.unity3d.com/Manual/EditorCommandLineArguments.html", "https://docs.unity3d.com/Manual/test-framework/reference-command-line.html", "https://unity.com/releases/editor/archive", "https://docs.unity3d.com/6000.4/Documentation/Manual/UpgradeGuideUnity64.html", "https://docs.unity3d.com/Manual/UnityYAML.html", "https://docs.unity3d.com/Manual/FormatDescription.html", "https://docs.unity3d.com/Manual/yaml-prefab-serialization.html", "https://docs.unity3d.com/Manual/default-directories.html", "https://docs.unity3d.com/ScriptReference/AssetDatabase.ForceReserializeAssets.html", "https://docs.unity3d.com/ScriptReference/PrefabUtility.LoadPrefabContents.html", "https://discussions.unity.com/t/unity-6-2-catching-and-fixing-malformed-asset-prefab-or-unity-files-before-they-break-your-project/1684262", "https://github.com/Unity-Technologies/skills/blob/main/skills/unity-cli/SKILL.md", "https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli", "https://docs.unity.com/en-us/unity-cli/release-notes"]
---

# Unity

## Detect
- Editor version: `m_EditorVersion` in `ProjectSettings/ProjectVersion.txt`. Build and test only with that exact Editor; another version reimports and upgrades the project.
- Render pipeline: `com.unity.render-pipelines.universal` (URP) or `com.unity.render-pipelines.high-definition` (HDRP) in `Packages/manifest.json`, plus the pipeline asset assigned in Graphics settings; neither means Built-in. Shaders and materials do not port between pipelines.
- Packages: `Packages/manifest.json` (requested) and `Packages/packages-lock.json` (resolved); embedded packages live in `Packages/<name>/`.
- Packages outside `Assets/` and `Packages/` (for example at the repository root): their C# files are not matched by the default globs. Bind the package folder to both `unity` and `unity-upm` with `stack_paths` in the profile and activate both.
- Assemblies: `.asmdef`/`.asmref` files. Scripts outside any asmdef compile into `Assembly-CSharp`, or `Assembly-CSharp-Editor` when under an `Editor` folder.
- Player settings in `ProjectSettings/ProjectSettings.asset`: scripting backend (Mono or IL2CPP), API compatibility level, active input handling (Input Manager, Input System package, or both).
- Asset serialization mode must be Force Text (Project Settings > Editor) for scene, prefab, and asset diffs to be readable.
- Editor availability, before changing serialized assets: an Editor has the project open, an installed Editor can run batch mode, or neither. It selects the route in «Conventions».

## Conventions
- Keep `UnityEditor` code out of player builds: put it in an `Editor` folder or an asmdef whose `includePlatforms` is `["Editor"]`; guard Editor calls in runtime scripts with `#if UNITY_EDITOR`. A runtime assembly that references `UnityEditor` fails the player build.
- Declare a new asmdef's references explicitly; asmdefs cannot reference `Assembly-CSharp`, and references must not form cycles.
- Commit every `.meta` file and move or rename it with its asset (prefer the Editor or `AssetDatabase.MoveAsset`). The `.meta` holds the GUID that all references use; deleting or regenerating it breaks references silently.
- Name each `MonoBehaviour`/`ScriptableObject` file exactly after its class.
- Expose inspector fields with `[SerializeField] private`, not public fields. When renaming a serialized field, add `[FormerlySerializedAs("oldName")]` (`UnityEngine.Serialization`); without it the stored value is dropped.
- Change scenes, prefabs, and other serialized assets by the first route that applies. Unity advises against editing its YAML directly: `fileID`/GUID links and prefab overrides break silently.
  1. A live Editor is reachable (a live-Editor bridge the project already uses, or a user who runs your menu item): change the asset through it with an Editor script or menu item (`SerializedObject`, `PrefabUtility.LoadPrefabContents` → `SaveAsPrefabAsset` → `UnloadPrefabContents`, `EditorSceneManager`, `AssetDatabase`). Never hand-edit then: the running Editor misses the change until reimport.
  2. No Editor has the project open and one is installed: run a static Editor method with `<Unity> -batchmode -quit -projectPath <path> -executeMethod <Class.Method> -logFile <file>`; the method saves its changes and throws or calls `EditorApplication.Exit(1)` on failure. A batch run cannot observe its result (a reference saved as null still reports success), so confirm it in the log, an EditMode test, or the next Editor load.
  3. Neither is possible: direct YAML editing is a declared exception. Edit only text files (Force Text; first line `%YAML 1.1`), copy the shape of an existing block of the same class, and give each new object an `&<id>` anchor unused in the file. Never touch `PrefabInstance` documents, `m_Modifications`, or `stripped` objects; stop and report instead. State "edited Unity YAML directly; no Editor available" in the report.
  4. After any direct YAML edit, verify before finishing: each `--- !u!<class> &<id>` anchor is unique in the file; each non-zero local `{fileID: N}` matches an anchor in the file; each `guid` in the edited blocks matches a `.meta` in the project or a package; `m_Father` ↔ `m_Children` and `m_GameObject` ↔ `m_Component` agree both ways. At the next opportunity, load the file in the Editor or batch mode and read the console or log for malformed-YAML errors.
- Lifecycle: `Awake` → `OnEnable` → `Start` → `FixedUpdate` → `Update` → `LateUpdate`; teardown `OnDisable` → `OnDestroy`. Initialize self in `Awake`, reach other objects in `Start`; order across objects is undefined unless set with `[DefaultExecutionOrder]` or Script Execution Order.
- Physics in `FixedUpdate`, camera follow in `LateUpdate`; scale per-frame motion by `Time.deltaTime`.
- In `Update`, `FixedUpdate`, and `LateUpdate`: use references cached in `Awake`/`Start`; never call `GetComponent`, `FindObjectsByType`, or `GameObject.Find`; avoid allocations (LINQ, closures, string concatenation, new collections, boxing); use `CompareTag` and non-allocating physics queries.
- Call Unity APIs only from the main thread.

## Verify
- Prefer `commands.build` and `commands.test`.
- Compile check: `<Unity> -batchmode -quit -projectPath <path> -logFile <file>`; a non-zero exit code or `error CS` in the log is a failure. Batch mode cannot open a project that an Editor already has open.
- Tests (Unity Test Framework): `<Unity> -batchmode -projectPath <path> -runTests -testPlatform EditMode|PlayMode -testResults <file.xml> -logFile <file>`; narrow with `-testFilter` or `-testCategory`. Omit `-quit`; it is not supported while tests run, and the runner exits when done.
- Test asmdefs reference the test runner assemblies; EditMode test asmdefs include only the `Editor` platform.

## Pitfalls
- `UnityEngine.Object` overrides `==`: a destroyed object equals `null`, but `?.`, `??`, and `is null` bypass that check. Use `== null` or the implicit bool for Unity objects.
- Constructors on `MonoBehaviour` may run outside Play Mode and off the main thread; initialize in `Awake`. Create components with `AddComponent` and scriptable objects with `ScriptableObject.CreateInstance`, never `new`.
- Changing a field's default in code does not update instances already serialized in scenes and prefabs; they keep their stored values.
- IL2CPP code stripping removes members reached only through reflection; keep them with `[Preserve]` or `link.xml`.
- Editing `Library/`, `Temp/`, `Logs/`, or `obj/`: they are generated caches and are not committed.
- Assuming a fresh git worktree is ready: it has no `Library/`. Each worktree is its own project folder with its own `Library/`; never share, link, or commit it. The first Editor or batch run there performs a full import that costs time and disk. Untracked links or junctions (for example ones exposing `Samples~` to a host project) are missing too; recreate them or work in the checkout that has them.
- Mixing mass reserialization (`AssetDatabase.ForceReserializeAssets`) into a feature change: it rewrites many files and buries the real diff. Run it only when requested, as its own change.
- Changing a `ScriptableObject` asset in Play Mode in the Editor persists after exiting Play Mode; copy it with `Instantiate` for runtime state.

## Version Notes
- Unity 6 Editor versions are numbered `6000.x` (as of 2026-10, per unity.com/releases).
- `Awaitable` (async/await support) exists from Unity 2023.1 and Unity 6; check the project's version before replacing coroutines (as of 2026-10, per Unity Scripting API).
- `Object.FindObjectOfType`/`FindObjectsOfType` are obsolete in Unity 6; use `FindFirstObjectByType`, `FindAnyObjectByType`, or `FindObjectsByType`. From Unity 6.4, `FindObjectsSortMode` and the overloads taking it are obsolete too; call the overloads without it (as of 2026-10, per Unity Scripting API).
- URP custom render passes differ when Render Graph is enabled (the default for new URP projects in Unity 6). Unity 6.4 removes URP Compatibility Mode, so custom passes there must use the Render Graph API; on 6.0–6.3 check the Compatibility Mode setting before writing `ScriptableRenderPass` code (as of 2026-10, per the Unity 6.4 upgrade guide).
- From Unity 6.2, Unity logs explicit errors for some malformed `.unity`, `.prefab`, and `.asset` files: unknown types, scripts on types that cannot host them, `!u!` header and type mismatches, missing headers, and invalid IDs. The checks are not exhaustive; earlier versions discard malformed data without reporting it (as of 2026-10, per Unity's technical article on Unity Discussions, 2025-09-19).
- Example live-Editor bridge, never a requirement: the Unity CLI (`unity`, 1.0.0-beta.13 on 2026-10-07; no stable release yet) drives a running Unity 6.0+ Editor through the `com.unity.pipeline` package (`unity status`, `unity command`). Its `unity mcp` replaces the deprecated MCP server of the in-Editor AI assistant package (as of 2026-10, per docs.unity.com Unity CLI release notes and "Replace MCP server with Unity CLI").
