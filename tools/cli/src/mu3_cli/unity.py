from __future__ import annotations

import json
import os
import platform
import re
import shutil
import subprocess
from dataclasses import asdict, dataclass
from pathlib import Path

import typer

from mu3_cli.repository import repo_root


unity_app = typer.Typer(no_args_is_help=True, help="Unity Editor diagnostics and batchmode orchestration.")

TARGET_PATTERN = re.compile(r"^[a-z0-9][a-z0-9-]*$")
RESERVED_TARGETS = {"all", "changed"}


@dataclass(frozen=True)
class UnityTargetSpec:
    key: str
    package_path: Path
    project_path: Path


@dataclass(frozen=True)
class UnityDiagnostic:
    status: str
    target: str
    title: str
    detail: str
    suggestion: str | None = None


def package_config_path(root: Path) -> Path:
    return root / "unity-cli-packages.tsv"


def compile_script_path(root: Path) -> Path:
    return root / "compile-unity.sh"


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

        columns = raw_line.split("\t")
        if len(columns) != 3 or not all(columns):
            raise ValueError(f"Invalid Unity package mapping at {config_path}:{line_number}")

        key, package_value, project_value = columns
        if not TARGET_PATTERN.fullmatch(key) or key in RESERVED_TARGETS:
            raise ValueError(f"Invalid or reserved Unity target '{key}' at {config_path}:{line_number}")
        if key in specs:
            raise ValueError(f"Duplicate Unity target '{key}' at {config_path}:{line_number}")

        specs[key] = UnityTargetSpec(
            key=key,
            package_path=_safe_relative_path(package_value, line_number),
            project_path=_safe_relative_path(project_value, line_number),
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


def installed_playback_engines(editor_path: Path) -> list[str]:
    candidates: list[Path] = []
    if platform.system() == "Darwin" and len(editor_path.parents) >= 4:
        candidates.extend(
            [
                editor_path.parents[1] / "PlaybackEngines",
                editor_path.parents[3] / "PlaybackEngines",
            ]
        )
    else:
        candidates.extend(
            [
                editor_path.parent / "Data" / "PlaybackEngines",
                editor_path.parent.parent / "PlaybackEngines",
            ]
        )

    engines: set[str] = set()
    for candidate in candidates:
        if candidate.is_dir():
            engines.update(path.name for path in candidate.iterdir() if path.is_dir())
    return sorted(engines)


def _diagnostic(
    status: str,
    target: str,
    title: str,
    detail: str,
    suggestion: str | None = None,
) -> UnityDiagnostic:
    return UnityDiagnostic(status=status, target=target, title=title, detail=detail, suggestion=suggestion)


def _project_manifest_references_package(spec: UnityTargetSpec, root: Path) -> tuple[bool, str]:
    manifest_path = root / spec.project_path / "Packages" / "manifest.json"
    if not manifest_path.is_file():
        return False, f"Missing {manifest_path.relative_to(root)}."

    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as error:
        return False, f"Invalid JSON in {manifest_path.relative_to(root)}: {error}"

    dependencies = manifest.get("dependencies", {})
    expected_reference = f"file:../../{spec.package_path.as_posix()}"
    if isinstance(dependencies, dict) and expected_reference in dependencies.values():
        return True, f"References {expected_reference}."
    return False, f"Does not reference {expected_reference}."


def run_unity_diagnostics(
    specs: dict[str, UnityTargetSpec],
    root: Path,
) -> list[UnityDiagnostic]:
    diagnostics: list[UnityDiagnostic] = []
    script_path = compile_script_path(root)

    if script_path.is_file() and os.access(script_path, os.X_OK):
        diagnostics.append(_diagnostic("PASS", "global", "Compile entrypoint", "compile-unity.sh is executable."))
    else:
        diagnostics.append(
            _diagnostic(
                "FAIL",
                "global",
                "Compile entrypoint",
                "compile-unity.sh is missing or not executable.",
                "Restore the root compile entrypoint and its executable bit.",
            )
        )

    official_cli = shutil.which("unity")
    if official_cli:
        diagnostics.append(_diagnostic("PASS", "global", "Official Unity CLI", f"Found {official_cli}."))
    else:
        diagnostics.append(
            _diagnostic(
                "WARN",
                "global",
                "Official Unity CLI",
                "The experimental Unity CLI binary is not on PATH; direct Editor execution remains available.",
            )
        )

    if os.access(root, os.W_OK):
        diagnostics.append(
            _diagnostic("PASS", "global", "Log root", "Repository is writable for future log/mu3_cli run records.")
        )
    else:
        diagnostics.append(
            _diagnostic("FAIL", "global", "Log root", "Repository is not writable for future log/mu3_cli run records.")
        )

    for spec in specs.values():
        package_root = root / spec.package_path
        project_root = root / spec.project_path
        package_manifest = package_root / "package.json"

        if package_root.is_dir() and package_manifest.is_file():
            diagnostics.append(
                _diagnostic("PASS", spec.key, "Package root", f"Found {spec.package_path}/package.json.")
            )
        else:
            diagnostics.append(
                _diagnostic(
                    "FAIL",
                    spec.key,
                    "Package root",
                    f"Missing package root or manifest at {spec.package_path}.",
                )
            )

        if project_root.is_dir():
            diagnostics.append(_diagnostic("PASS", spec.key, "Unity project", f"Found {spec.project_path}."))
        else:
            diagnostics.append(
                _diagnostic("FAIL", spec.key, "Unity project", f"Missing Unity project {spec.project_path}.")
            )

        references_package, reference_detail = _project_manifest_references_package(spec, root)
        diagnostics.append(
            _diagnostic(
                "PASS" if references_package else "FAIL",
                spec.key,
                "Local package reference",
                reference_detail,
            )
        )

        version = project_editor_version(spec, root)
        if version is None:
            diagnostics.append(
                _diagnostic(
                    "FAIL",
                    spec.key,
                    "Editor version",
                    "ProjectVersion.txt is missing or has no m_EditorVersion.",
                )
            )
            continue

        diagnostics.append(_diagnostic("PASS", spec.key, "Editor version", f"Requires Unity {version}."))
        editor_path, is_override = resolve_editor_path(version)
        if editor_path.is_file() and os.access(editor_path, os.X_OK):
            detail = f"Found executable {editor_path}."
            if is_override:
                detail += " Explicit UNITY_EDITOR/UNITY override is active."
            diagnostics.append(_diagnostic("PASS", spec.key, "Editor executable", detail))
            engines = installed_playback_engines(editor_path)
            diagnostics.append(
                _diagnostic(
                    "PASS",
                    spec.key,
                    "Platform modules",
                    f"Detected {len(engines)} playback engine(s): {', '.join(engines) if engines else 'none'}.",
                )
            )
        else:
            diagnostics.append(
                _diagnostic(
                    "FAIL",
                    spec.key,
                    "Editor executable",
                    f"Required Editor is not executable: {editor_path}",
                    f"Install Unity {version} or set UNITY_EDITOR to an explicit executable.",
                )
            )

        lock_file = project_root / "Temp" / "UnityLockfile"
        if lock_file.exists():
            diagnostics.append(
                _diagnostic(
                    "WARN",
                    spec.key,
                    "Project lock",
                    f"{spec.project_path} is open; compile-unity.sh will use an isolated mirror.",
                )
            )
        else:
            diagnostics.append(_diagnostic("PASS", spec.key, "Project lock", "Project is not locked."))

    return diagnostics


def diagnostic_summary(diagnostics: list[UnityDiagnostic]) -> dict[str, int]:
    return {
        "pass": sum(item.status == "PASS" for item in diagnostics),
        "warn": sum(item.status == "WARN" for item in diagnostics),
        "fail": sum(item.status == "FAIL" for item in diagnostics),
    }


def render_diagnostics(diagnostics: list[UnityDiagnostic], output_format: str) -> dict[str, int]:
    summary = diagnostic_summary(diagnostics)
    if output_format == "json":
        typer.echo(json.dumps({"diagnostics": [asdict(item) for item in diagnostics], "summary": summary}, indent=2))
        return summary
    if output_format != "text":
        raise typer.BadParameter("--format must be either 'text' or 'json'.")

    for item in diagnostics:
        typer.echo(f"[{item.status}] [{item.target}] {item.title}: {item.detail}")
        if item.suggestion:
            typer.echo(f"  Suggestion: {item.suggestion}")
    typer.echo(f"Summary: {summary['pass']} pass, {summary['warn']} warn, {summary['fail']} fail")
    return summary


def compile_command_arguments(
    target: str,
    base: str | None,
    dry_run: bool,
    isolated: bool,
    in_place: bool,
    keep_staging: bool,
) -> list[str]:
    if isolated and in_place:
        raise ValueError("--isolated and --in-place cannot be used together.")
    if base and target != "changed":
        raise ValueError("--base can only be used with the changed target.")

    arguments = [target]
    if base:
        arguments.extend(["--base", base])
    if dry_run:
        arguments.append("--dry-run")
    if isolated:
        arguments.append("--isolated")
    if in_place:
        arguments.append("--in-place")
    if keep_staging:
        arguments.append("--keep-staging")
    return arguments


def run_compile_script(arguments: list[str], root: Path) -> int:
    script_path = compile_script_path(root)
    if not script_path.is_file() or not os.access(script_path, os.X_OK):
        raise ValueError(f"Unity compile entrypoint is missing or not executable: {script_path}")

    command = [str(script_path), *arguments]
    typer.echo(f"Running: {' '.join(command)}")
    return subprocess.run(command, cwd=root, check=False).returncode


@unity_app.command("doctor")
def unity_doctor(
    target: str | None = typer.Option(None, "--target", "-t", help="Optional configured target to inspect."),
    output_format: str = typer.Option("text", "--format", help="Output format: text or json."),
) -> None:
    """Diagnose Unity package mappings, projects, Editors, modules, locks, and log readiness."""
    root = repo_root()
    try:
        specs = load_target_specs(root)
    except ValueError as error:
        raise typer.BadParameter(str(error)) from error

    if target is not None:
        if target not in specs:
            raise typer.BadParameter(f"Unknown Unity target '{target}'. Choose one of: {', '.join(specs)}.")
        specs = {target: specs[target]}

    summary = render_diagnostics(run_unity_diagnostics(specs, root), output_format)
    if summary["fail"]:
        raise typer.Exit(code=1)


@unity_app.command("changes")
def unity_changes(
    base: str | None = typer.Option(None, "--base", help="Optional Git ref for <ref>...HEAD selection."),
) -> None:
    """Explain which package projects are selected from Git changes."""
    root = repo_root()
    arguments = compile_command_arguments("changed", base, True, False, False, False)
    return_code = run_compile_script(arguments, root)
    if return_code:
        raise typer.Exit(code=return_code)


@unity_app.command("compile")
def unity_compile(
    target: str = typer.Argument("changed", help="changed, all, or a target from unity-cli-packages.tsv."),
    base: str | None = typer.Option(None, "--base", help="Optional Git ref for <ref>...HEAD selection."),
    dry_run: bool = typer.Option(False, "--dry-run", help="Print selection without launching Unity."),
    isolated: bool = typer.Option(False, "--isolated", help="Always compile an isolated source mirror."),
    in_place: bool = typer.Option(False, "--in-place", help="Compile directly and fail when the project is open."),
    keep_staging: bool = typer.Option(False, "--keep-staging", help="Keep an explicitly isolated mirror."),
) -> None:
    """Run the repository's change-aware Unity batchmode compiler."""
    root = repo_root()
    try:
        arguments = compile_command_arguments(target, base, dry_run, isolated, in_place, keep_staging)
        return_code = run_compile_script(arguments, root)
    except ValueError as error:
        raise typer.BadParameter(str(error)) from error

    if return_code:
        raise typer.Exit(code=return_code)
