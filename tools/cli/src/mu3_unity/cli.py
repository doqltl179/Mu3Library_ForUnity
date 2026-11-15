from __future__ import annotations

import json
import os
import platform
import shutil
import subprocess
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Callable

import typer

from mu3_common.bash import BashNotFoundError, bash_script_command, find_bash
from mu3_common.checkout import echo_repository_root, repo_root
from mu3_common.editor import UnityTargetSpec, load_target_specs, project_editor_version, resolve_editor_path
from mu3_common.sample_links import LINK_SAMPLES_COMMAND, sample_link_state


app = typer.Typer(
    add_completion=False,
    no_args_is_help=True,
    help="Unity Editor diagnostics and batchmode orchestration.",
)


@dataclass(frozen=True)
class UnityDiagnostic:
    status: str
    target: str
    title: str
    detail: str
    suggestion: str | None = None


def compile_script_path(root: Path) -> Path:
    return root / "compile-unity.sh"


def test_script_path(root: Path) -> Path:
    return root / "test-unity.sh"


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


_WINDOWS_GENERIC_READ = 0x80000000
_WINDOWS_OPEN_EXISTING = 3
_WINDOWS_FILE_ATTRIBUTE_NORMAL = 0x80
_WINDOWS_ERROR_FILE_NOT_FOUND = 2
_WINDOWS_ERROR_PATH_NOT_FOUND = 3
_WINDOWS_ERROR_SHARING_VIOLATION = 32


def _windows_file_is_held_open(path: Path) -> bool | None:
    import ctypes
    from ctypes import wintypes

    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    create_file = kernel32.CreateFileW
    create_file.argtypes = (
        wintypes.LPCWSTR,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.LPVOID,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.HANDLE,
    )
    create_file.restype = wintypes.HANDLE
    close_handle = kernel32.CloseHandle
    close_handle.argtypes = (wintypes.HANDLE,)
    close_handle.restype = wintypes.BOOL

    # Opening the file without sharing fails with a sharing violation for as long
    # as any other handle is open, whatever access that handle asked for.
    handle = create_file(
        str(path), _WINDOWS_GENERIC_READ, 0, None, _WINDOWS_OPEN_EXISTING, _WINDOWS_FILE_ATTRIBUTE_NORMAL, None
    )
    if handle is None or handle == ctypes.c_void_p(-1).value:
        error = ctypes.get_last_error()
        if error == _WINDOWS_ERROR_SHARING_VIOLATION:
            return True
        if error in (_WINDOWS_ERROR_FILE_NOT_FOUND, _WINDOWS_ERROR_PATH_NOT_FOUND):
            return False
        return None

    close_handle(handle)
    return False


def file_is_held_open(path: Path) -> bool | None:
    """Return whether a process holds the file open, or None when this host cannot tell.

    compile-unity.sh answers the same question with PowerShell on Windows and lsof elsewhere.
    """
    if platform.system() == "Windows":
        return _windows_file_is_held_open(path)

    lsof = shutil.which("lsof")
    if lsof is None:
        return None
    # lsof prints the holders' process IDs and exits 1 when there are none.
    completed = subprocess.run([lsof, "-t", "--", str(path)], capture_output=True, text=True, check=False)
    return bool(completed.stdout.strip())


def project_lock_state(project_root: Path, probe: Callable[[Path], bool | None] = file_is_held_open) -> str:
    """Return absent, open, stale, or unknown for the project's Temp/UnityLockfile.

    A running Editor keeps the file open, so a lock file that no process holds was left
    behind by an Editor that crashed or was killed and does not mean the project is open.
    """
    lock_file = project_root / "Temp" / "UnityLockfile"
    if not lock_file.exists():
        return "absent"

    held = probe(lock_file)
    if held is None:
        return "unknown"
    if held:
        return "open"
    return "stale" if lock_file.exists() else "absent"


def project_lock_diagnostic(
    spec: UnityTargetSpec,
    root: Path,
    probe: Callable[[Path], bool | None] = file_is_held_open,
    system: str | None = None,
) -> UnityDiagnostic:
    lock_path = (spec.project_path / "Temp" / "UnityLockfile").as_posix()
    state = project_lock_state(root / spec.project_path, probe)

    if state == "open":
        return _diagnostic(
            "WARN",
            spec.key,
            "Project lock",
            f"{spec.project_path} is open in Unity; compile-unity.sh will use an isolated mirror.",
        )
    if state == "stale":
        return _diagnostic(
            "WARN",
            spec.key,
            "Project lock",
            f"{lock_path} is stale: no running process holds it, so compile-unity.sh compiles in place.",
            f"Delete {lock_path} unless an Editor should have the project open.",
        )
    if state == "unknown":
        # On Windows the check opens the file and failed for a reason other than another
        # holder, such as denied access; elsewhere it needs lsof, which is missing.
        if (system or platform.system()) == "Windows":
            cause = "could not be opened to check whether an Editor holds it"
            suggestion = (
                f"If no Editor has {spec.project_path} open, delete {lock_path}; otherwise check that it can be read."
            )
        else:
            cause = "exists, and lsof, which checks whether an Editor holds it, is not installed"
            suggestion = f"Install lsof, or delete {lock_path} if no Editor has {spec.project_path} open."
        return _diagnostic(
            "WARN",
            spec.key,
            "Project lock",
            f"{lock_path} {cause}; compile-unity.sh will use an isolated mirror and refuses --in-place.",
            suggestion,
        )
    return _diagnostic("PASS", spec.key, "Project lock", "Project is not locked.")


def sample_link_diagnostic(spec: UnityTargetSpec, root: Path) -> UnityDiagnostic | None:
    current = sample_link_state(spec, root)
    if current is None:
        return None
    if current.state == "linked":
        return _diagnostic("PASS", spec.key, "Sample link", current.detail)

    if current.state == "blocked":
        suggestion = f"Move it out of the way, then run {LINK_SAMPLES_COMMAND}."
    elif current.state == "no-samples":
        suggestion = "Restore the package's Samples~ folder or remove the samples link from unity-cli-packages.tsv."
    else:
        suggestion = f"Run {LINK_SAMPLES_COMMAND}."
    return _diagnostic(
        "FAIL",
        spec.key,
        "Sample link",
        f"{current.detail} compile-unity.sh refuses to run without it unless --without-samples is given.",
        suggestion,
    )


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


def bash_diagnostic(finder: Callable[[], Path] = find_bash) -> UnityDiagnostic:
    """The compile and test commands run through bash, so a host without one cannot run them."""
    try:
        bash_path = finder()
    except BashNotFoundError as error:
        return _diagnostic("FAIL", "global", "Bash", error.detail, error.suggestion)
    return _diagnostic("PASS", "global", "Bash", f"Found {bash_path}.")


def run_unity_diagnostics(
    specs: dict[str, UnityTargetSpec],
    root: Path,
) -> list[UnityDiagnostic]:
    diagnostics: list[UnityDiagnostic] = []

    for title, script_path in (("Compile entrypoint", compile_script_path(root)), ("Test entrypoint", test_script_path(root))):
        if script_path.is_file() and os.access(script_path, os.X_OK):
            diagnostics.append(_diagnostic("PASS", "global", title, f"{script_path.name} is executable."))
        else:
            diagnostics.append(
                _diagnostic(
                    "FAIL",
                    "global",
                    title,
                    f"{script_path.name} is missing or not executable.",
                    f"Restore the root {script_path.name} and its executable bit.",
                )
            )

    diagnostics.append(bash_diagnostic())

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
            _diagnostic("PASS", "global", "Log root", "Repository is writable for future log/mu3_unity run records.")
        )
    else:
        diagnostics.append(
            _diagnostic("FAIL", "global", "Log root", "Repository is not writable for future log/mu3_unity run records.")
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

        samples = sample_link_diagnostic(spec, root)
        if samples is not None:
            diagnostics.append(samples)

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
                    f"Install Unity {version} through Unity Hub, set UNITY_HUB_EDITOR_ROOT when the Hub installs "
                    "Editors outside its default location, or set UNITY_EDITOR to an explicit executable.",
                )
            )

        diagnostics.append(project_lock_diagnostic(spec, root))

    return diagnostics


def diagnostic_summary(diagnostics: list[UnityDiagnostic]) -> dict[str, int]:
    return {
        "pass": sum(item.status == "PASS" for item in diagnostics),
        "warn": sum(item.status == "WARN" for item in diagnostics),
        "fail": sum(item.status == "FAIL" for item in diagnostics),
    }


def render_diagnostics(diagnostics: list[UnityDiagnostic], output_format: str, root: Path) -> dict[str, int]:
    summary = diagnostic_summary(diagnostics)
    if output_format == "json":
        document = {
            "repository_root": str(root),
            "diagnostics": [asdict(item) for item in diagnostics],
            "summary": summary,
        }
        typer.echo(json.dumps(document, indent=2))
        return summary
    if output_format != "text":
        raise typer.BadParameter("--format must be either 'text' or 'json'.")

    for item in diagnostics:
        typer.echo(f"[{item.status}] [{item.target}] {item.title}: {item.detail}")
        if item.suggestion:
            typer.echo(f"  Suggestion: {item.suggestion}")
    typer.echo(f"Summary: {summary['pass']} pass, {summary['warn']} warn, {summary['fail']} fail")
    return summary


def entry_point_arguments(
    target: str,
    base: str | None,
    dry_run: bool,
    isolated: bool,
    in_place: bool,
    keep_staging: bool,
    without_samples: bool = False,
    editor_version: str | None = None,
) -> list[str]:
    if isolated and in_place:
        raise ValueError("--isolated and --in-place cannot be used together.")
    if base and target != "changed":
        raise ValueError("--base can only be used with the changed target.")
    if editor_version and in_place:
        raise ValueError("--editor-version runs in an isolated mirror, so it cannot be used with --in-place.")

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
    if without_samples:
        arguments.append("--without-samples")
    if editor_version:
        arguments.extend(["--editor-version", editor_version])
    return arguments


TEST_PLATFORMS = ("editmode", "playmode")


def platform_arguments(platform_name: str) -> list[str]:
    """The --platform option that test-unity.sh takes on top of the shared selection options."""
    if platform_name not in TEST_PLATFORMS:
        raise ValueError(f"--platform expects one of: {', '.join(TEST_PLATFORMS)}.")
    return ["--platform", platform_name]


def filter_arguments(test_filter: str | None) -> list[str]:
    """The --filter option that test-unity.sh passes to Unity's -testFilter, or nothing."""
    if test_filter is None:
        return []
    if not test_filter.strip():
        raise ValueError("--filter requires a test name or pattern.")
    if test_filter.startswith("-"):
        raise ValueError(f"--filter expects a test name or pattern, not an option: {test_filter}")
    if test_filter.startswith("/"):
        raise ValueError(f"--filter cannot start with /, which Git Bash rewrites as a file path: {test_filter}")
    return ["--filter", test_filter]


def stall_timeout_arguments(stall_timeout: int | None) -> list[str]:
    """The --stall-timeout option of compile-unity.sh and test-unity.sh, or nothing for the script's default."""
    if stall_timeout is None:
        return []
    if stall_timeout < 0:
        raise ValueError("--stall-timeout expects a whole number of seconds.")
    return ["--stall-timeout", str(stall_timeout)]


def load_arguments(load_spinners: int | None, repeat: int | None) -> list[str]:
    """The --load-spinners and --repeat options of test-unity.sh, each only when given."""
    arguments: list[str] = []
    if load_spinners is not None:
        if load_spinners < 0:
            raise ValueError("--load-spinners expects a whole number of spinners.")
        arguments.extend(["--load-spinners", str(load_spinners)])
    if repeat is not None:
        if repeat < 1:
            raise ValueError("--repeat expects a whole number of runs, 1 or more.")
        arguments.extend(["--repeat", str(repeat)])
    return arguments


def run_entry_point(script_path: Path, arguments: list[str], root: Path) -> int:
    """Run compile-unity.sh or test-unity.sh, which take the same selection options."""
    if not script_path.is_file():
        raise ValueError(f"Unity entrypoint is missing: {script_path}")
    if platform.system() != "Windows" and not os.access(script_path, os.X_OK):
        raise ValueError(f"Unity entrypoint is not executable: {script_path}")

    command = bash_script_command(script_path, arguments)
    typer.echo(f"Running: {' '.join(command)}")
    return subprocess.run(command, cwd=root, check=False).returncode


@app.command("doctor")
def unity_doctor(
    target: str | None = typer.Option(None, "--target", "-t", help="Optional configured target to inspect."),
    output_format: str = typer.Option("text", "--format", help="Output format: text or json."),
) -> None:
    """Diagnose Unity package mappings, projects, Editors, modules, locks, and log readiness."""
    root = repo_root()
    # The JSON report carries the root as a field, so that its output stays one JSON object.
    if output_format != "json":
        echo_repository_root(root)
    try:
        specs = load_target_specs(root)
    except ValueError as error:
        raise typer.BadParameter(str(error)) from error

    if target is not None:
        if target not in specs:
            raise typer.BadParameter(f"Unknown Unity target '{target}'. Choose one of: {', '.join(specs)}.")
        specs = {target: specs[target]}

    summary = render_diagnostics(run_unity_diagnostics(specs, root), output_format, root)
    if summary["fail"]:
        raise typer.Exit(code=1)


@app.command("changes")
def unity_changes(
    base: str | None = typer.Option(None, "--base", help="Optional Git ref for <ref>...HEAD selection."),
) -> None:
    """Explain which package projects are selected from Git changes."""
    root = repo_root()
    echo_repository_root(root)
    try:
        arguments = entry_point_arguments("changed", base, True, False, False, False)
        return_code = run_entry_point(compile_script_path(root), arguments, root)
    except ValueError as error:
        raise typer.BadParameter(str(error)) from error

    if return_code:
        raise typer.Exit(code=return_code)


@app.command("compile")
def unity_compile(
    target: str = typer.Argument("changed", help="changed, all, or a target from unity-cli-packages.tsv."),
    base: str | None = typer.Option(None, "--base", help="Optional Git ref for <ref>...HEAD selection."),
    dry_run: bool = typer.Option(False, "--dry-run", help="Print selection without launching Unity."),
    isolated: bool = typer.Option(False, "--isolated", help="Always compile an isolated source mirror."),
    in_place: bool = typer.Option(False, "--in-place", help="Compile directly and fail when the project is open."),
    keep_staging: bool = typer.Option(False, "--keep-staging", help="Keep an explicitly isolated mirror."),
    without_samples: bool = typer.Option(
        False, "--without-samples", help="Compile even when a samples link is missing, leaving the samples out."
    ),
    editor_version: str | None = typer.Option(
        None,
        "--editor-version",
        help="Compile with this installed Editor version, such as 6000.3.17f1, in a mirror retargeted to it.",
    ),
    stall_timeout: int | None = typer.Option(
        None,
        "--stall-timeout",
        help="Stop Unity and fail the target once its log has not grown for this many seconds (default 300; 0 never).",
    ),
) -> None:
    """Run the repository's change-aware Unity batchmode compiler."""
    root = repo_root()
    echo_repository_root(root)
    try:
        arguments = entry_point_arguments(
            target, base, dry_run, isolated, in_place, keep_staging, without_samples, editor_version
        ) + stall_timeout_arguments(stall_timeout)
        return_code = run_entry_point(compile_script_path(root), arguments, root)
    except ValueError as error:
        raise typer.BadParameter(str(error)) from error

    if return_code:
        raise typer.Exit(code=return_code)



@app.command("test")
def unity_test(
    target: str = typer.Argument("changed", help="changed, all, or a target from unity-cli-packages.tsv."),
    base: str | None = typer.Option(None, "--base", help="Optional Git ref for <ref>...HEAD selection."),
    dry_run: bool = typer.Option(False, "--dry-run", help="Print selection without launching Unity."),
    isolated: bool = typer.Option(False, "--isolated", help="Always test an isolated source mirror."),
    in_place: bool = typer.Option(False, "--in-place", help="Test directly and fail when the project is open."),
    keep_staging: bool = typer.Option(False, "--keep-staging", help="Keep an explicitly isolated mirror."),
    without_samples: bool = typer.Option(
        False, "--without-samples", help="Test even when a samples link is missing, leaving the samples out."
    ),
    editor_version: str | None = typer.Option(
        None,
        "--editor-version",
        help="Test with this installed Editor version, such as 6000.3.17f1, in a mirror retargeted to it.",
    ),
    test_platform: str = typer.Option(
        "editmode", "--platform", help="editmode or playmode: the Unity Test Framework platform to run."
    ),
    test_filter: str | None = typer.Option(
        None,
        "--filter",
        help="Run only the tests whose full name matches this Unity -testFilter pattern, [Explicit] ones included.",
    ),
    stall_timeout: int | None = typer.Option(
        None,
        "--stall-timeout",
        help="Stop Unity and fail the target once its log has not grown for this many seconds (default 600; 0 never).",
    ),
    load_spinners: int | None = typer.Option(
        None,
        "--load-spinners",
        help="Spin this many CPU cores, one Python process each, from the first test of each run to its last.",
    ),
    repeat: int | None = typer.Option(
        None, "--repeat", help="Run each target this many times, starting Unity anew, and total the runs' results."
    ),
) -> None:
    """Run the EditMode or PlayMode tests of the selected projects through test-unity.sh."""
    root = repo_root()
    echo_repository_root(root)
    try:
        arguments = (
            entry_point_arguments(target, base, dry_run, isolated, in_place, keep_staging, without_samples, editor_version)
            + platform_arguments(test_platform)
            + filter_arguments(test_filter)
            + stall_timeout_arguments(stall_timeout)
            + load_arguments(load_spinners, repeat)
        )
        return_code = run_entry_point(test_script_path(root), arguments, root)
    except ValueError as error:
        raise typer.BadParameter(str(error)) from error

    if return_code:
        raise typer.Exit(code=return_code)
