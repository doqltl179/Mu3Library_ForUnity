---
id: godot
title: Godot
kind: framework
applies_to: ["**/*.gd", "**/*.tscn", "**/*.tres", "**/*.gdshader", "**/project.godot"]
related: [csharp]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://docs.godotengine.org/en/stable/", "https://docs.godotengine.org/en/stable/tutorials/editor/command_line_tutorial.html", "https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/import_process.html", "https://docs.godotengine.org/en/stable/tutorials/migrating/upgrading_to_godot_4.html", "https://godotengine.org/article/uid-changes-coming-to-godot-4-4/"]
---

# Godot

## Detect
- Major version from `project.godot`: `config_version=5` is Godot 4.x, `config_version=4` is Godot 3.x. In Godot 4, `config/features` under `[application]` lists the minor version (for example `"4.3"`).
- Language: `.gd` files are GDScript; a `.csproj` using `Godot.NET.Sdk` means C#, which needs the .NET build of the editor.
- Godot 4 renderer (`rendering/renderer/rendering_method`: Forward+, Mobile, or Compatibility) limits available rendering features.
- `[autoload]` (singletons), `[input]` (action names), and `[editor_plugins]` in `project.godot`; third-party addons in `addons/`.
- Test framework from `addons/`, for example GUT (`addons/gut`) or gdUnit4 (`addons/gdUnit4`).

## Conventions
- Write code for the detected major version only. Godot 3 and 4 APIs differ, for example: `export`/`onready` → `@export`/`@onready`; `yield` → `await`; `connect("sig", obj, "method")` → `sig.connect(callable)`; `instance()` → `instantiate()`; `KinematicBody2D` → `CharacterBody2D`; `Spatial` → `Node3D`.
- Type GDScript: annotate variables, parameters, and return types (`var speed: float = 200.0`, `func hit(amount: int) -> void`) or infer with `:=`. Types surface errors in the editor and enable faster typed instructions in Godot 4.
- Compose behavior from scenes; make each scene runnable alone, with dependencies passed in through `@export` properties.
- Call down, signal up: parents call methods on children; children emit signals instead of reaching up with `get_parent()` chains or absolute node paths.
- Cache nodes with `@onready var x := $Path` or scene-unique names (`%Name`); never call `get_node` every frame.
- Put physics and movement in `_physics_process(delta)` and visuals in `_process(delta)`; scale motion by `delta`.
- `_ready` runs on children before parents: a parent may use its children in `_ready`, not the reverse.
- Edit `.tscn`/`.tres` in the editor. When a text edit is unavoidable, keep `[ext_resource]`/`[sub_resource]` ids, `uid` values, and every `ExtResource(...)`/`SubResource(...)` reference consistent.
- Move and rename files in the editor's FileSystem dock so references update. Outside the editor, move each `.import` and `.uid` sidecar with its file.
- C# in Godot 4: classes deriving from Godot types are `partial` and named after their file; signals are `[Signal]` delegates whose names end in `EventHandler`.

## Verify
- Prefer `commands.test` and `commands.build`.
- Parse a script without running it: `godot --headless --path <project> --check-only --script <res://path.gd>`.
- Import resources on a fresh checkout before running tests: `godot --headless --path <project> --import` (when `godot --help` lists `--import`).
- Run tests headlessly through the detected addon, for example GUT: `godot --headless --path <project> -s addons/gut/gut_cmdln.gd -gexit`.
- C#: `dotnet build` the project's `.csproj` or `.sln`.

## Pitfalls
- Committing `.godot/` (Godot 4) or `.import/` (Godot 3): they are caches; ignore them. Do commit the `*.import` sidecars next to assets (they hold import settings) and `*.uid` files.
- Loaded resources are shared: changing a `.tres` resource at runtime changes it for every user; call `duplicate()` or enable `resource_local_to_scene` for per-instance state.
- Godot 3 snippets mismatch Godot 4 APIs; check code against the detected version's documentation.
- Renaming a signal, method, node, or exported property breaks connections and values stored in scenes; search `.tscn` files (`[connection ...]`, property lines) for the old name and update them.
- Calling `free()` on nodes inside the scene tree during callbacks; use `queue_free()`.
- Changing the scene tree from a thread; use `call_deferred`.

## Version Notes
- `--headless` is a Godot 4 flag; Godot 3 runs headless through the separate server/headless build (as of 2026-10, per Godot docs).
- From Godot 4.4, scripts and shaders get `.uid` sidecar files; commit them and move them with their file (as of 2026-10, per godotengine.org 4.4 UID article).
- C# projects target the .NET version the editor release requires; check `TargetFramework` in the `.csproj` before upgrading Godot (as of 2026-10, per Godot docs).
