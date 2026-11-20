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

from common.bash import BashNotFoundError, bash_script_command, find_bash
from common.checkout import echo_repository_root, repo_root
from common.editor import resolve_editor_path
from common.project_lock import file_is_held_open, project_lock_state
from common.unity_projects import (
    CONFIGS_FOLDER,
    ConfigError,
    ProjectConfig,
    WorkDirError,
    load_configs,
    project_folder,
    resolve_work_dir,
)


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


def project_lock_diagnostic(
    config: str,
    project: Path,
    probe: Callable[[Path], bool | None] = file_is_held_open,
    system: str | None = None,
) -> UnityDiagnostic:
    """The lock of a config's generated project, which compile-unity.sh refreshes only while no Editor has it open."""
    lock_path = (project / "Temp" / "UnityLockfile").as_posix()
    if not project.exists():
        return _diagnostic("PASS", config, "Project lock", f"{project.as_posix()} is not generated yet.")
    state = project_lock_state(project, probe)

    if state == "open":
        return _diagnostic(
            "WARN",
            config,
            "Project lock",
            f"{project.as_posix()} is open in Unity; compile-unity.sh and test-unity.sh refuse to refresh it.",
            "Close the Editor that has it open.",
        )
    if state == "stale":
        return _diagnostic(
            "WARN",
            config,
            "Project lock",
            f"{lock_path} is stale: no running process holds it, so compile-unity.sh runs the project anyway.",
            f"Delete {lock_path} unless an Editor should have the project open.",
        )
    if state == "unknown":
        # On Windows the check opens the file and failed for a reason other than another
        # holder, such as denied access; elsewhere it needs lsof, which is missing.
        if (system or platform.system()) == "Windows":
            cause = "could not be opened to check whether an Editor holds it"
            suggestion = f"If no Editor has {project.as_posix()} open, delete {lock_path}; otherwise check that it can be read."
        else:
            cause = "exists, and lsof, which checks whether an Editor holds it, is not installed"
            suggestion = f"Install lsof, or delete {lock_path} if no Editor has {project.as_posix()} open."
        return _diagnostic(
            "WARN",
            config,
            "Project lock",
            f"{lock_path} {cause}; compile-unity.sh and test-unity.sh refuse to run the project.",
            suggestion,
        )
    return _diagnostic("PASS", config, "Project lock", "Project is not locked.")


def _diagnostic(
    status: str,
    target: str,
    title: str,
    detail: str,
    suggestion: str | None = None,
) -> UnityDiagnostic:
    return UnityDiagnostic(status=status, target=target, title=title, detail=detail, suggestion=suggestion)


def work_dir_diagnostic(root: Path, work_dir: Path | None = None) -> tuple[UnityDiagnostic, Path | None]:
    """Where the projects are generated, which compile-unity.sh and test-unity.sh need, and that folder."""
    try:
        folder = resolve_work_dir(root, work_dir)
    except WorkDirError as error:
        return _diagnostic("FAIL", "global", "Work folder", f"{error}."), None
    return _diagnostic("PASS", "global", "Work folder", f"Projects are generated in {folder.as_posix()}."), folder


def bash_diagnostic(finder: Callable[[], Path] = find_bash) -> UnityDiagnostic:
    """The compile and test commands run through bash, so a host without one cannot run them."""
    try:
        bash_path = finder()
    except BashNotFoundError as error:
        return _diagnostic("FAIL", "global", "Bash", error.detail, error.suggestion)
    return _diagnostic("PASS", "global", "Bash", f"Found {bash_path}.")


def run_unity_diagnostics(
    configs: dict[str, ProjectConfig],
    root: Path,
    work_dir: Path | None = None,
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
            _diagnostic("PASS", "global", "Log root", "Repository is writable for the run records under log/.")
        )
    else:
        diagnostics.append(
            _diagnostic("FAIL", "global", "Log root", "Repository is not writable for the run records under log/.")
        )

    work_diagnostic, folder = work_dir_diagnostic(root, work_dir)
    diagnostics.append(work_diagnostic)

    for config in configs.values():
        # load_configs already refused a config whose checkout packages do not resolve.
        packages = ", ".join(f"{name} ({path})" for name, path in config.packages.items()) or "none"
        diagnostics.append(
            _diagnostic("PASS", config.name, "Checkout packages", f"{(CONFIGS_FOLDER / config.name).as_posix()} names {packages}.")
        )

        version = config.editor_version
        diagnostics.append(_diagnostic("PASS", config.name, "Editor version", f"Requires Unity {version}."))
        editor_path, is_override = resolve_editor_path(version)
        if editor_path.is_file() and os.access(editor_path, os.X_OK):
            detail = f"Found executable {editor_path}."
            if is_override:
                detail += " Explicit UNITY_EDITOR/UNITY override is active."
            diagnostics.append(_diagnostic("PASS", config.name, "Editor executable", detail))
            engines = installed_playback_engines(editor_path)
            diagnostics.append(
                _diagnostic(
                    "PASS",
                    config.name,
                    "Platform modules",
                    f"Detected {len(engines)} playback engine(s): {', '.join(engines) if engines else 'none'}.",
                )
            )
        else:
            diagnostics.append(
                _diagnostic(
                    "FAIL",
                    config.name,
                    "Editor executable",
                    f"Required Editor is not executable: {editor_path}",
                    f"Install Unity {version} through Unity Hub, set UNITY_HUB_EDITOR_ROOT when the Hub installs "
                    "Editors outside its default location, or set UNITY_EDITOR to an explicit executable.",
                )
            )

        if folder is not None:
            diagnostics.append(project_lock_diagnostic(config.name, project_folder(folder, config.name)))

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
    work_dir: Path | None = None,
    editor_version: str | None = None,
) -> list[str]:
    if base and target != "changed":
        raise ValueError("--base can only be used with the changed target.")

    arguments = [target]
    if base:
        arguments.extend(["--base", base])
    if dry_run:
        arguments.append("--dry-run")
    if work_dir is not None:
        arguments.extend(["--work-dir", str(work_dir)])
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
    target: str | None = typer.Option(None, "--target", "-t", help="Optional config in tools/unity-projects to inspect."),
    work_dir: Path | None = typer.Option(None, "--work-dir", help="The work folder the shell entrypoints are given, if any."),
    output_format: str = typer.Option("text", "--format", help="Output format: text or json."),
) -> None:
    """Diagnose bash, the project configs, the work folder, Editors, modules, project locks, and log readiness."""
    root = repo_root()
    # The JSON report carries the root as a field, so that its output stays one JSON object.
    if output_format != "json":
        echo_repository_root(root)
    try:
        configs = load_configs(root)
    except ConfigError as error:
        raise typer.BadParameter(str(error)) from error

    if target is not None:
        if target not in configs:
            raise typer.BadParameter(f"Unknown Unity target '{target}'. Choose one of: {', '.join(configs)}.")
        configs = {target: configs[target]}

    summary = render_diagnostics(run_unity_diagnostics(configs, root, work_dir), output_format, root)
    if summary["fail"]:
        raise typer.Exit(code=1)


@app.command("changes")
def unity_changes(
    base: str | None = typer.Option(None, "--base", help="Optional Git ref for <ref>...HEAD selection."),
) -> None:
    """Explain which project configs the Git changes select."""
    root = repo_root()
    echo_repository_root(root)
    try:
        arguments = entry_point_arguments("changed", base, True)
        return_code = run_entry_point(compile_script_path(root), arguments, root)
    except ValueError as error:
        raise typer.BadParameter(str(error)) from error

    if return_code:
        raise typer.Exit(code=return_code)


@app.command("compile")
def unity_compile(
    target: str = typer.Argument("changed", help="changed, all, or a config in tools/unity-projects."),
    base: str | None = typer.Option(None, "--base", help="Optional Git ref for <ref>...HEAD selection."),
    dry_run: bool = typer.Option(False, "--dry-run", help="Print the selection and the project folders without launching Unity."),
    work_dir: Path | None = typer.Option(
        None, "--work-dir", help="Generate the projects here instead of in ${MU3_WORK_ROOT}/Mu3Library_ForUnity/<unit>."
    ),
    editor_version: str | None = typer.Option(
        None,
        "--editor-version",
        help="Compile with this installed Editor version, such as 6000.3.17f1, in a project retargeted to it.",
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
        arguments = entry_point_arguments(target, base, dry_run, work_dir, editor_version) + stall_timeout_arguments(
            stall_timeout
        )
        return_code = run_entry_point(compile_script_path(root), arguments, root)
    except ValueError as error:
        raise typer.BadParameter(str(error)) from error

    if return_code:
        raise typer.Exit(code=return_code)


@app.command("test")
def unity_test(
    target: str = typer.Argument("changed", help="changed, all, or a config in tools/unity-projects."),
    base: str | None = typer.Option(None, "--base", help="Optional Git ref for <ref>...HEAD selection."),
    dry_run: bool = typer.Option(False, "--dry-run", help="Print the selection and the project folders without launching Unity."),
    work_dir: Path | None = typer.Option(
        None, "--work-dir", help="Generate the projects here instead of in ${MU3_WORK_ROOT}/Mu3Library_ForUnity/<unit>."
    ),
    editor_version: str | None = typer.Option(
        None,
        "--editor-version",
        help="Test with this installed Editor version, such as 6000.3.17f1, in a project retargeted to it.",
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
            entry_point_arguments(target, base, dry_run, work_dir, editor_version)
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
