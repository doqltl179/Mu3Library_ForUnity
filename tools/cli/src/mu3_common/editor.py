from __future__ import annotations

import json
import os
import platform
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

from mu3_common.checkout import repo_root


TARGET_PATTERN = re.compile(r"^[a-z0-9][a-z0-9-]*$")
RESERVED_TARGETS = {"all", "changed"}


@dataclass(frozen=True)
class UnityTargetSpec:
    key: str
    package_path: Path
    project_path: Path
    # Where the project links to the package's Samples~ folder, relative to the project.
    samples_link: Path | None = None

    @property
    def samples_path(self) -> Path:
        return self.package_path / "Samples~"


def package_config_path(root: Path) -> Path:
    return root / "unity-cli-packages.tsv"


def _safe_relative_path(value: str, line_number: int) -> Path:
    path = Path(value)
    if path.is_absolute() or ".." in path.parts:
        raise ValueError(f"Mapping paths must remain inside the repository at line {line_number}: {value}")
    return path


def load_target_specs(root: Path | None = None) -> dict[str, UnityTargetSpec]:
    root = root or repo_root()
    config_path = package_config_path(root)
    if not config_path.is_file():
        raise ValueError(f"Unity package mapping not found: {config_path}")

    specs: dict[str, UnityTargetSpec] = {}
    for line_number, raw_line in enumerate(config_path.read_text(encoding="utf-8-sig").splitlines(), start=1):
        if not raw_line or raw_line.startswith("#"):
            continue

        # The fourth column, the samples link, is optional.
        columns = raw_line.split("\t")
        if len(columns) not in (3, 4) or not all(columns[:3]):
            raise ValueError(f"Invalid Unity package mapping at {config_path}:{line_number}")

        key, package_value, project_value = columns[:3]
        samples_value = columns[3] if len(columns) == 4 else ""
        if not TARGET_PATTERN.fullmatch(key) or key in RESERVED_TARGETS:
            raise ValueError(f"Invalid or reserved Unity target '{key}' at {config_path}:{line_number}")
        if key in specs:
            raise ValueError(f"Duplicate Unity target '{key}' at {config_path}:{line_number}")

        specs[key] = UnityTargetSpec(
            key=key,
            package_path=_safe_relative_path(package_value, line_number),
            project_path=_safe_relative_path(project_value, line_number),
            samples_link=_safe_relative_path(samples_value, line_number) if samples_value else None,
        )

    if not specs:
        raise ValueError(f"No Unity package mappings found in {config_path}")
    return specs


def project_editor_version(spec: UnityTargetSpec, root: Path) -> str | None:
    version_file = root / spec.project_path / "ProjectSettings" / "ProjectVersion.txt"
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
    Unity.app on macOS, as compile-unity.sh's editor_package_manager_dir looks for it.
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

    The rule is compile-unity.sh's editor_bundled_version, which retargets its --editor-version
    mirrors: the version the Editor's package manifest recommends, or else the version of the
    package's built-in copy. A package with neither is left out. A file of a shape other than
    the Editor writes raises ValueError naming it.
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
