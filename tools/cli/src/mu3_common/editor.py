from __future__ import annotations

import json
import os
import platform
import re
from pathlib import Path
from typing import Iterable


def folder_editor_version(project_folder: Path) -> str | None:
    """The Editor version that a project folder's ProjectSettings/ProjectVersion.txt names, or None.

    compile-unity.sh and test-unity.sh take each config's version from it, through unity_projects.py describe.
    """
    version_file = project_folder / "ProjectSettings" / "ProjectVersion.txt"
    if not version_file.is_file():
        return None

    for line in version_file.read_text(encoding="utf-8-sig").splitlines():
        if line.startswith("m_EditorVersion: "):
            return line.removeprefix("m_EditorVersion: ").strip()
    return None


def _override_editor_path() -> Path | None:
    value = os.environ.get("UNITY_EDITOR") or os.environ.get("UNITY")
    if not value:
        return None

    path = Path(value).expanduser()
    if path.suffix == ".app":
        return path / "Contents" / "MacOS" / "Unity"
    return path


def default_editor_path(version: str) -> Path:
    editor_root = os.environ.get("UNITY_HUB_EDITOR_ROOT")
    if editor_root:
        root = Path(editor_root).expanduser() / version
    elif platform.system() == "Darwin":
        root = Path("/Applications/Unity/Hub/Editor") / version
    elif platform.system() == "Windows":
        root = Path(os.environ.get("PROGRAMFILES", "C:/Program Files")) / "Unity" / "Hub" / "Editor" / version
    else:
        root = Path.home() / "Unity" / "Hub" / "Editor" / version

    if platform.system() == "Darwin":
        return root / "Unity.app" / "Contents" / "MacOS" / "Unity"
    return root / "Editor" / "Unity.exe" if platform.system() == "Windows" else root / "Editor" / "Unity"


def resolve_editor_path(version: str) -> tuple[Path, bool]:
    override = _override_editor_path()
    if override is not None:
        return override, True
    return default_editor_path(version), False


def is_hub_editor_for(editor_path: Path, version: str) -> bool:
    """Whether a Unity executable sits where Unity Hub installs that Editor version.

    compile-unity.sh's hub_editor_suffix makes the same test to warn when UNITY_EDITOR or UNITY
    overrides the Editor a project or --editor-version asks for with another one.
    """
    if platform.system() == "Darwin":
        suffix: tuple[str, ...] = ("Editor", version, "Unity.app", "Contents", "MacOS", "Unity")
    else:
        suffix = ("Editor", version, "Editor", "Unity.exe" if platform.system() == "Windows" else "Unity")
    tail = editor_path.parts[-len(suffix):]
    return [os.path.normcase(part) for part in tail] == [os.path.normcase(part) for part in suffix]


def editor_package_manager_dir(editor_path: Path) -> Path | None:
    """The folder in which an Editor keeps the packages it bundles, or None when it has none.

    It lies beside the executable in Data/ on Windows and Linux, and in Contents/Resources of
    Unity.app on macOS.
    """
    for candidate in (
        editor_path.parent / "Data" / "Resources" / "PackageManager",
        editor_path.parent.parent / "Resources" / "PackageManager",
    ):
        if (candidate / "BuiltInPackages").is_dir():
            return candidate
    return None


def _json_version(value: object) -> str | None:
    return value if isinstance(value, str) and value else None


def _read_json_object(path: Path) -> dict:
    """A JSON file that holds an object; anything else raises ValueError naming the file."""
    try:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as error:
        raise ValueError(f"{path} is not valid JSON: {error}") from error
    if not isinstance(data, dict):
        raise ValueError(f"{path} does not hold a JSON object")
    return data


def editor_bundled_versions(package_manager_dir: Path, package_names: Iterable[str]) -> dict[str, str]:
    """The version an Editor bundles of each named package it bundles.

    The version the Editor's package manifest recommends, or else the version of the package's
    built-in copy. A package with neither is left out. A file of a shape other than the Editor
    writes raises ValueError naming it.
    """
    manifest_file = package_manager_dir / "Editor" / "manifest.json"
    recommended: dict = {}
    if manifest_file.is_file():
        recommended = _read_json_object(manifest_file).get("packages", {})
        if not isinstance(recommended, dict):
            raise ValueError(f"{manifest_file} is malformed: packages is not a JSON object")

    versions: dict[str, str] = {}
    for name in package_names:
        entry = recommended.get(name)
        version = _json_version(entry.get("version")) if isinstance(entry, dict) else None
        built_in_copy = package_manager_dir / "BuiltInPackages" / name / "package.json"
        if version is None and built_in_copy.is_file():
            version = _json_version(_read_json_object(built_in_copy).get("version"))
        if version is not None:
            versions[name] = version
    return versions


def retarget_dependencies(
    dependencies: dict[str, str], package_manager_dir: Path, editor_version: str
) -> tuple[dict[str, str], list[str], list[str]]:
    """Point a manifest's dependencies at the Unity packages another Editor bundles.

    Each com.unity.* package pinned to a version takes the version the Editor bundles, a built-in
    module the Editor does not ship is dropped, since Unity cannot resolve it, and any other package
    it does not bundle keeps its version. Returns the dependencies, the changes, and the warnings to
    print. The generated projects of compile-unity.sh --editor-version and the DI stripping
    harness's mirror both follow it.
    """
    pinned = [
        name for name, version in dependencies.items() if name.startswith("com.unity.") and re.match(r"[0-9]", version)
    ]
    bundled = editor_bundled_versions(package_manager_dir, pinned)
    retargeted: dict[str, str] = {}
    changes: list[str] = []
    warnings: list[str] = []
    for name, version in dependencies.items():
        if name not in pinned:
            retargeted[name] = version
        elif name in bundled:
            retargeted[name] = bundled[name]
            if bundled[name] != version:
                changes.append(f"{name} {version} -> {bundled[name]}")
        elif name.startswith("com.unity.modules."):
            changes.append(f"{name} removed, Unity {editor_version} does not ship it")
        else:
            retargeted[name] = version
            warnings.append(f"Unity {editor_version} does not bundle {name}, so the project keeps {version}")
    return retargeted, changes, warnings
