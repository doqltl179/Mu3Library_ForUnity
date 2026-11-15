"""The DI stripping harness: build a Windows player of a small mirror project, run it, report its checks.

Managed code stripping removes code that only reflection reaches, and the DI container reaches the
constructors and [Inject] members it fills only through reflection. The Editor and the EditMode and
PlayMode tests never strip, so only a player shows such a regression. The harness template under
tools/di-strip-harness/ registers services that nothing creates with `new`; this module copies it
into a mirror project that references the checkout's Base and DI packages, builds a player at the
chosen backend and Managed Stripping Level, runs it, and reads the HARNESS lines the player logs.
"""

from __future__ import annotations

import json
import os
import platform
import re
import shutil
from contextlib import contextmanager
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Iterator, Sequence

import typer

from common.checkout import echo_repository_root, repo_root
from common.editor import (
    editor_package_manager_dir,
    folder_editor_version,
    is_hub_editor_for,
    resolve_editor_path,
    retarget_dependencies,
)
from common.process import run_with_timeout
from common.unity_projects import CONFIGS_FOLDER, checkout_packages
from common.work_dir import FolderNotClaimedError, claim_folder


BACKENDS = ("mono", "il2cpp")
STRIPPING_LEVELS = ("Disabled", "Minimal", "Low", "Medium", "High")
BASE_PACKAGE = "com.github.doqltl179.mu3library.base"
DI_PACKAGE = "com.github.doqltl179.mu3library.di"
# The checkout packages the mirror references: the container and the link.xml processor live in DI,
# which depends on Base.
MIRROR_PACKAGES = (BASE_PACKAGE, DI_PACKAGE)
UNITASK_PACKAGE = "com.cysharp.unitask"
# The config whose Editor version and Unity modules the mirror takes: like the mirror, it holds Base
# and DI without any optional SDK, so the mirror runs the coroutine path unless --with-unitask adds
# UniTask.
REFERENCE_CONFIG = "minimal"
# The config whose manifest and lock file pin the UniTask that --with-unitask adds.
UNITASK_CONFIG = "full"
PLAYER_NAME = "Harness"
# Exit codes beside 0 (every check passed) and typer's 2 (a usage error). 1 means only that the
# player ran and a check failed or it did not report; anything that stops the harness before or
# around the player, from a refused --work-dir to a locked file or a failed build, is 3.
EXIT_CHECKS_FAILED = 1
EXIT_BUILD_FAILED = 3
# A project path longer than this risks the Windows path limit inside Library/ and the IL2CPP
# build folders, as compile-unity.sh warns for its projects.
LONG_PROJECT_PATH = 100
# The file that marks a --work-dir as the harness's own. The harness writes it into a new or empty
# folder and changes or deletes files only in a folder that has it.
WORK_DIR_MARKER = ".mu3-di-strip-harness"
WORK_DIR_MARKER_TEXT = "mu3-di-strip-harness made this folder and may change or delete any file in it.\n"
# A full Unity release, as compile-unity.sh --editor-version accepts it; it names a Unity Hub folder.
EDITOR_VERSION_PATTERN = re.compile(r"[0-9]+\.[0-9]+\.[0-9]+[a-z][0-9]+([a-z][0-9]+)?")

CHECK_PATTERN = re.compile(r"^HARNESS CHECK (\S+) (PASS|FAIL) ?(.*)$")
RESULT_PATTERN = re.compile(r"^HARNESS RESULT (PASS|FAIL) checks=(\d+) failures=(\d+) errorLogs=(\d+)")


class HarnessSetupError(RuntimeError):
    """The work folder or the mirror could not be prepared or cleaned, or the player could not be built or started."""


def describe_error(error: BaseException) -> str:
    """One line naming an exception and its message, such as PermissionError: [WinError 5] ..."""
    message = " ".join(str(error).split())
    return f"{type(error).__name__}: {message}" if message else type(error).__name__


@contextmanager
def setup_step(action: str) -> Iterator[None]:
    """Turn a file, permission, or data error raised while doing `action` into a HarnessSetupError.

    A locked or unwritable file raises OSError, a malformed manifest ValueError (JSONDecodeError)
    or KeyError, and an unreadable checkout package ConfigError, a ValueError; none of them says
    anything about the checks, so none may end the command as a failed check.
    """
    try:
        yield
    except (OSError, KeyError, ValueError) as error:
        raise HarnessSetupError(f"{action} failed: {describe_error(error)}") from error


def template_dir(root: Path) -> Path:
    return root / "tools" / "di-strip-harness"


def default_work_dir(root: Path) -> Path:
    return root / "log" / "di-strip-harness"


def mirror_project_dir(work_dir: Path, editor_version: str | None) -> Path:
    """The mirror for the Editor the run uses: each --editor-version gets its own, with its own Library/."""
    return work_dir / ("project" if editor_version is None else f"project-{editor_version}")


def run_label(backend: str, stripping: str, with_unitask: bool, editor_version: str | None = None) -> str:
    """The name of one configuration's player folder and logs, such as mono-high-unitask or mono-high-6000.3.17f1."""
    label = f"{backend}-{stripping.lower()}" + ("-unitask" if with_unitask else "")
    return label if editor_version is None else f"{label}-{editor_version}"


def claim_work_dir(work_dir: Path) -> bool:
    """Make sure the harness may change and delete files in work_dir.

    A folder that does not exist or is empty becomes the harness's own: it gets the marker file.
    A folder with the marker is one the harness made. Any other folder is refused, so a wrong
    --work-dir cannot cost a project its assets; a work folder made before the marker existed is
    refused too, and the user deletes it or passes another. Returns whether this call wrote the marker.
    """
    try:
        return claim_folder(work_dir, WORK_DIR_MARKER, WORK_DIR_MARKER_TEXT)
    except FolderNotClaimedError as error:
        raise HarnessSetupError(
            f"{error}, so the harness will not change or delete files in it; pass a new or empty --work-dir, "
            "or delete the folder yourself if an earlier version of the harness made it"
        ) from error


def read_dependencies(path: Path, entry_type: type) -> dict:
    """The dependencies object of a package manifest or lock file, each entry checked to be entry_type.

    A manifest maps each package to a version string, a lock file to an object. A file that is not
    JSON, or whose shape is not that, stops the run with its path rather than a traceback.
    """
    try:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as error:
        raise HarnessSetupError(f"{path} is not valid JSON: {describe_error(error)}") from error
    dependencies = data.get("dependencies", {}) if isinstance(data, dict) else None
    if not isinstance(dependencies, dict):
        raise HarnessSetupError(f"{path} is not a package manifest: a JSON object with a dependencies object")
    for name, entry in dependencies.items():
        if not isinstance(entry, entry_type):
            expected = "a string" if entry_type is str else "a JSON object"
            raise HarnessSetupError(f"{path} is malformed: the dependency {name} is not {expected}")
    return dependencies


def config_folder(root: Path, name: str) -> Path:
    return root / CONFIGS_FOLDER / name


def unitask_source(root: Path) -> str:
    """The UniTask Git URL of the full config, pinned to the commit its lock file names."""
    project = config_folder(root, UNITASK_CONFIG) / "Packages"
    url = read_dependencies(project / "manifest.json", str).get(UNITASK_PACKAGE)
    if url is None:
        raise HarnessSetupError(f"{project / 'manifest.json'} has no {UNITASK_PACKAGE} dependency for --with-unitask to pin")
    if "#" in url:
        return url
    commit = read_dependencies(project / "packages-lock.json", dict).get(UNITASK_PACKAGE, {}).get("hash")
    return f"{url}#{commit}" if isinstance(commit, str) and commit else url


def mirror_manifest(reference_manifest: dict, package_paths: dict[str, Path], unitask: str | None) -> dict:
    """The mirror's Packages/manifest.json.

    It keeps the reference config's built-in modules and uGUI, which Base's runtime needs, adds each
    package of package_paths, by name, from its folder in this checkout, and adds UniTask only when
    asked: no render pipeline, IDE, or test package, and no testables, so the player holds little
    besides Base, DI, and the harness.
    """
    dependencies = {
        name: version
        for name, version in reference_manifest.get("dependencies", {}).items()
        if name.startswith("com.unity.modules.") or name == "com.unity.ugui"
    }
    for name, path in package_paths.items():
        dependencies[name] = "file:" + path.resolve().as_posix()
    if unitask:
        dependencies[UNITASK_PACKAGE] = unitask
    return {"dependencies": dict(sorted(dependencies.items()))}


def write_if_changed(path: Path, data: bytes) -> bool:
    if path.is_file() and path.read_bytes() == data:
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
    return True


def sync_template(template_assets: Path, project_assets: Path, generated: Sequence[str] = ()) -> list[str]:
    """Copy the template's assets into the mirror and remove the files it no longer has.

    A file whose content is unchanged is left alone, so Unity reimports only what changed, and the
    .meta files Unity wrote are kept. `generated` names the asset paths the run itself creates, such
    as the plugin DLL and the scene.
    Returns the asset paths that were written or removed.
    """
    changed: list[str] = []
    expected: set[str] = set(generated)
    for source in sorted(path for path in template_assets.rglob("*") if path.is_file()):
        relative = source.relative_to(template_assets).as_posix()
        expected.add(relative)
        if write_if_changed(project_assets / relative, source.read_bytes()):
            changed.append(relative)

    if project_assets.is_dir():
        for existing in sorted(path for path in project_assets.rglob("*") if path.is_file()):
            relative = existing.relative_to(project_assets).as_posix()
            if relative.endswith(".meta"):
                # Unity removes the .meta of a missing asset on its next import.
                continue
            if relative not in expected:
                existing.unlink()
                changed.append(relative)
    return changed


def editor_mono(editor_path: Path) -> tuple[Path, Path]:
    """The Mono runtime and the mcs compiler that the Editor ships beside its executable."""
    if platform.system() == "Darwin":
        mono_root = editor_path.parents[1] / "MonoBleedingEdge"
    else:
        mono_root = editor_path.parent / "Data" / "MonoBleedingEdge"
    mono = mono_root / "bin" / ("mono.exe" if platform.system() == "Windows" else "mono")
    return mono, mono_root / "lib" / "mono" / "4.5" / "mcs.exe"


def plugin_compile_command(editor_path: Path, source: Path, output: Path) -> list[str]:
    mono, mcs = editor_mono(editor_path)
    return [str(mono), str(mcs), "-target:library", "-sdk:4.5", "-nologo", f"-out:{output}", str(source)]


def build_command(
    editor_path: Path, project_dir: Path, backend: str, stripping: str, player_path: Path, log_path: Path
) -> list[str]:
    return [
        str(editor_path),
        "-batchmode",
        "-buildTarget",
        "Win64",
        "-projectPath",
        str(project_dir),
        "-executeMethod",
        "StripHarnessBuild.BuildPlayer",
        "-mu3-backend",
        backend,
        "-mu3-stripping",
        stripping,
        "-mu3-out",
        str(player_path),
        "-logFile",
        str(log_path),
    ]


def player_command(player_path: Path, log_path: Path) -> list[str]:
    return [str(player_path), "-batchmode", "-nographics", "-logFile", str(log_path)]


@dataclass(frozen=True)
class HarnessCheck:
    name: str
    passed: bool
    detail: str


@dataclass
class HarnessRun:
    """What one player run logged: its checks and the result line the boot writes last."""

    checks: list[HarnessCheck] = field(default_factory=list)
    # The other HARNESS lines worth showing: START, INFO, UNUSED, ERRORLOG, EXCEPTION.
    notes: list[str] = field(default_factory=list)
    result: str | None = None
    declared_checks: int | None = None
    error_logs: int | None = None


def parse_player_log(text: str) -> HarnessRun:
    run = HarnessRun()
    for raw_line in text.splitlines():
        line = raw_line.strip()
        if not line.startswith("HARNESS "):
            continue
        check = CHECK_PATTERN.match(line)
        if check:
            run.checks.append(HarnessCheck(check.group(1), check.group(2) == "PASS", check.group(3).strip()))
            continue
        result = RESULT_PATTERN.match(line)
        if result:
            run.result = result.group(1)
            run.declared_checks = int(result.group(2))
            run.error_logs = int(result.group(4))
            continue
        if line.startswith(("HARNESS START", "HARNESS INFO", "HARNESS UNUSED", "HARNESS ERRORLOG", "HARNESS EXCEPTION")):
            run.notes.append(line.removeprefix("HARNESS "))
    return run


def run_problems(run: HarnessRun, exit_code: int | None) -> list[str]:
    """Why a player run does not pass; empty when every check passed and the player said so."""
    problems: list[str] = []
    if exit_code is None:
        problems.append("the player did not exit before the timeout and was stopped")
    elif exit_code != 0:
        problems.append(f"the player exited with {exit_code}")
    if run.result is None:
        problems.append("the player logged no HARNESS RESULT line")
    else:
        if run.result != "PASS":
            problems.append(f"the player reported {run.result}")
        if run.declared_checks != len(run.checks):
            problems.append(f"the player counted {run.declared_checks} checks but logged {len(run.checks)}")
        if run.error_logs:
            problems.append(f"the player logged {run.error_logs} errors or exceptions")
    failed = [check.name for check in run.checks if not check.passed]
    if failed:
        problems.append(f"{len(failed)} checks failed: {', '.join(failed)}")
    if not run.checks:
        problems.append("the player logged no HARNESS CHECK line")
    return problems


def player_sizes(player_path: Path) -> dict[str, int]:
    """Byte sizes of what managed stripping changes: each managed DLL (Mono) and GameAssembly.dll (IL2CPP)."""
    sizes: dict[str, int] = {}
    managed = player_path.parent / f"{player_path.stem}_Data" / "Managed"
    if managed.is_dir():
        for dll in sorted(managed.glob("*.dll")):
            sizes[f"Managed/{dll.name}"] = dll.stat().st_size
    game_assembly = player_path.parent / "GameAssembly.dll"
    if game_assembly.is_file():
        sizes["GameAssembly.dll"] = game_assembly.stat().st_size
    return sizes


Runner = Callable[[list[str], float], "int | None"]


@dataclass(frozen=True)
class HarnessOptions:
    backend: str = "mono"
    stripping: str = "High"
    with_unitask: bool = False
    work_dir: Path | None = None
    build_timeout: float = 3600
    player_timeout: float = 120
    # Another installed Editor to build with, such as 6000.3.17f1; None takes the minimal config's.
    editor_version: str | None = None


def prepare_mirror(
    root: Path,
    project_dir: Path,
    editor_path: Path,
    with_unitask: bool,
    runner: Runner,
    editor_version: str | None = None,
    echo: Callable[[str], None] = lambda line: None,
) -> list[str]:
    """Create or refresh the mirror project and return what changed in it.

    With editor_version the mirror names that Editor and takes the Unity package versions it
    bundles; without it, the mirror takes the minimal config's ProjectVersion.txt.
    """
    reference = config_folder(root, REFERENCE_CONFIG)
    template = template_dir(root)
    changed: list[str] = []

    plugin_asset = "Plugins/HarnessPlugin.dll"
    # The plugin DLL and the scene are made in the mirror, by this module and by the build script.
    generated = [plugin_asset, "Harness/Harness.unity"]
    synced = sync_template(template / "Assets", project_dir / "Assets", generated)
    changed += [f"Assets/{path}" for path in synced]

    packages = checkout_packages(root)
    for name in MIRROR_PACKAGES:
        if name not in packages:
            raise HarnessSetupError(f"no package.json in the checkout names {name}")
    manifest = mirror_manifest(
        {"dependencies": read_dependencies(reference / "Packages" / "manifest.json", str)},
        {name: root / packages[name] for name in MIRROR_PACKAGES},
        unitask_source(root) if with_unitask else None,
    )
    version_file = project_dir / "ProjectSettings" / "ProjectVersion.txt"
    if editor_version is None:
        version = (reference / "ProjectSettings" / "ProjectVersion.txt").read_bytes()
    else:
        package_manager_dir = editor_package_manager_dir(editor_path)
        if package_manager_dir is None:
            raise HarnessSetupError(f"cannot find the packages that Unity {editor_version} bundles next to {editor_path}")
        manifest["dependencies"], retargeted, warnings = retarget_dependencies(
            manifest["dependencies"], package_manager_dir, editor_version
        )
        # Unity adds its revision line when it opens the mirror, so a file that already names the
        # version stays as it is.
        named = f"m_EditorVersion: {editor_version}"
        current = version_file.read_text(encoding="utf-8-sig").splitlines() if version_file.is_file() else []
        version = version_file.read_bytes() if named in current else f"{named}\n".encode("utf-8")
        for warning in warnings:
            echo(f"warning: {warning}")
        echo(
            f"Retargeted the mirror to Unity {editor_version}: ProjectVersion.txt names it, and "
            + ("these Unity packages follow it:" if retargeted else "every Unity package already has the version it bundles.")
        )
        for change in retargeted:
            echo(f"  - {change}")
    if write_if_changed(project_dir / "Packages" / "manifest.json", (json.dumps(manifest, indent=2) + "\n").encode("utf-8")):
        changed.append("Packages/manifest.json")
    if write_if_changed(version_file, version):
        changed.append("ProjectSettings/ProjectVersion.txt")

    source = template / "Plugin" / "HarnessPlugin.cs"
    dll = project_dir / "Assets" / plugin_asset
    if not dll.is_file() or dll.stat().st_mtime < source.stat().st_mtime:
        dll.parent.mkdir(parents=True, exist_ok=True)
        command = plugin_compile_command(editor_path, source, dll)
        status = runner(command, 300)
        if status != 0 or not dll.is_file():
            raise HarnessSetupError(f"the plugin DLL did not compile (exit {status}): {' '.join(command)}")
        changed.append(f"Assets/{plugin_asset}")
    return changed


def default_runner(command: list[str], timeout: float) -> int | None:
    return run_with_timeout(command, timeout)


def run_harness(root: Path, options: HarnessOptions, runner: Runner = default_runner, echo: Callable[[str], None] = typer.echo) -> int:
    """Build and run one configuration, print each check, and return the command's exit code.

    Raises HarnessSetupError, which the command ends with exit 3, when the work folder, the mirror,
    or the files around the player cannot be prepared, read, or cleaned.
    """
    if platform.system() != "Windows":
        raise HarnessSetupError("the harness builds a Windows x64 player, so it runs only on Windows")

    reference = (CONFIGS_FOLDER / REFERENCE_CONFIG).as_posix()
    version = options.editor_version or folder_editor_version(config_folder(root, REFERENCE_CONFIG))
    if version is None:
        raise HarnessSetupError(f"no Editor version in {reference}/ProjectSettings/ProjectVersion.txt")
    editor_path, overridden = resolve_editor_path(version)
    if not editor_path.is_file():
        requester = " for --editor-version" if options.editor_version else ""
        raise HarnessSetupError(f"the Unity Editor {version}{requester} is not installed: {editor_path}")

    work_dir = Path(os.path.abspath(options.work_dir or default_work_dir(root)))
    project_dir = mirror_project_dir(work_dir, options.editor_version)
    label = run_label(options.backend, options.stripping, options.with_unitask, options.editor_version)
    player_path = work_dir / "players" / label / f"{PLAYER_NAME}.exe"
    build_log = work_dir / f"{label}-build.log"
    player_log = work_dir / f"{label}-player.log"

    echo(f"Unity: {editor_path}" + (" (UNITY_EDITOR override)" if overridden else ""))
    if overridden and not is_hub_editor_for(editor_path, version):
        # As compile-unity.sh warns: the mirror and the labels name the version, the override decides.
        requester = "--editor-version" if options.editor_version else reference
        echo(f"warning: {requester} requires Unity {version} but the explicit override will be used: {editor_path}")
    echo(f"Mirror project: {project_dir}")
    if len(str(project_dir)) > LONG_PROJECT_PATH:
        echo(
            f"warning: the mirror path is {len(str(project_dir))} characters, so Unity's Library and the IL2CPP "
            "build may pass the Windows path limit; pass a shorter --work-dir"
        )
    with setup_step(f"preparing the work folder {work_dir}"):
        if claim_work_dir(work_dir):
            echo(f"Marked {work_dir} as the harness's work folder ({WORK_DIR_MARKER})")
    with setup_step(f"preparing the mirror project {project_dir}"):
        changed = prepare_mirror(
            root, project_dir, editor_path, options.with_unitask, runner, options.editor_version, echo
        )
    echo(f"Mirror changes: {', '.join(changed) if changed else 'none'}")

    with setup_step(f"removing the previous {label} player and logs"):
        if player_path.parent.exists():
            shutil.rmtree(player_path.parent)
        for log in (build_log, player_log):
            log.unlink(missing_ok=True)

    echo(f"Building {label} (backend {options.backend}, Managed Stripping Level {options.stripping}); log: {build_log}")
    with setup_step("running the Unity build"):
        status = runner(
            build_command(editor_path, project_dir, options.backend, options.stripping, player_path, build_log),
            options.build_timeout,
        )
        build_text = build_log.read_text(encoding="utf-8", errors="replace") if build_log.is_file() else ""
    for line in build_text.splitlines():
        if line.startswith(("HARNESS BUILD", "HARNESS settings", "HARNESS STEP-", "HARNESS bad")):
            echo(line)
    if status != 0 or not player_path.is_file():
        reason = "timed out and was stopped" if status is None else f"exited with {status}"
        echo(f"error: the build {reason}; see {build_log}")
        return EXIT_BUILD_FAILED

    echo(f"Running {player_path}; log: {player_log}")
    with setup_step(f"starting {player_path}"):
        exit_code = runner(player_command(player_path, player_log), options.player_timeout)
    with setup_step(f"reading {player_log}"):
        log_text = player_log.read_text(encoding="utf-8", errors="replace") if player_log.is_file() else ""
    run = parse_player_log(log_text)

    # From here on the checks are known, so nothing may turn them into exit 3.
    for note in run.notes:
        echo(note)
    for check in run.checks:
        echo(f"{'PASS' if check.passed else 'FAIL'} {check.name}" + (f" ({check.detail})" if check.detail else ""))

    try:
        sizes = player_sizes(player_path)
    except OSError as error:
        echo(f"warning: could not measure the player's DLLs: {describe_error(error)}")
        sizes = {}
    managed_total = sum(size for name, size in sizes.items() if name.startswith("Managed/"))
    if managed_total:
        echo(f"Managed DLLs: {managed_total} bytes in {sum(1 for name in sizes if name.startswith('Managed/'))} files")
    if "GameAssembly.dll" in sizes:
        echo(f"GameAssembly.dll: {sizes['GameAssembly.dll']} bytes")

    passed = sum(1 for check in run.checks if check.passed)
    problems = run_problems(run, exit_code)
    echo(f"{label}: {len(run.checks)} checks, {passed} passed, {len(run.checks) - passed} failed")
    if problems:
        for problem in problems:
            echo(f"FAIL: {problem}")
        return EXIT_CHECKS_FAILED
    echo(f"PASS: every check passed ({label})")
    return 0


app = typer.Typer(add_completion=False)


@app.command()
def di_strip_harness(
    stripping: str = typer.Option("High", "--stripping", help="Managed Stripping Level: " + ", ".join(STRIPPING_LEVELS) + "."),
    backend: str = typer.Option("mono", "--backend", help="Scripting backend: mono or il2cpp."),
    with_unitask: bool = typer.Option(
        False, "--with-unitask", help="Add UniTask, pinned as the full config pins it, so cores prepare through UniTask."
    ),
    work_dir: Path | None = typer.Option(
        None,
        "--work-dir",
        help="Folder for the mirror project, players, and logs (default log/di-strip-harness); "
        "it must be new, empty, or one the harness made.",
    ),
    editor_version: str | None = typer.Option(
        None,
        "--editor-version",
        help="Build with this installed Editor, such as 6000.3.17f1, in its own mirror whose Unity packages "
        "take the versions that Editor bundles.",
    ),
    build_timeout: int = typer.Option(3600, "--build-timeout", help="Seconds the Editor may take to import and build."),
    player_timeout: int = typer.Option(120, "--player-timeout", help="Seconds the player may run."),
) -> None:
    """Build the DI stripping harness as a Windows x64 player, run it, and report each check.

    Exits 0 when every check passed, 1 when a check failed or the player did not report, and 3
    when the work folder or the mirror could not be prepared or cleaned, the build failed, or the
    player could not be started.
    """
    if stripping not in STRIPPING_LEVELS:
        raise typer.BadParameter(f"--stripping expects one of: {', '.join(STRIPPING_LEVELS)}.")
    if backend not in BACKENDS:
        raise typer.BadParameter(f"--backend expects one of: {', '.join(BACKENDS)}.")
    if build_timeout <= 0 or player_timeout <= 0:
        raise typer.BadParameter("--build-timeout and --player-timeout expect a positive number of seconds.")
    if editor_version is not None and not EDITOR_VERSION_PATTERN.fullmatch(editor_version):
        raise typer.BadParameter(f"--editor-version expects a full Unity version such as 6000.3.17f1: {editor_version}")

    options = HarnessOptions(
        backend=backend,
        stripping=stripping,
        with_unitask=with_unitask,
        work_dir=work_dir,
        build_timeout=build_timeout,
        player_timeout=player_timeout,
        editor_version=editor_version,
    )
    root = repo_root()
    echo_repository_root(root)
    try:
        exit_code = run_harness(root, options)
    except HarnessSetupError as error:
        typer.echo(f"error: {error}", err=True)
        raise typer.Exit(code=EXIT_BUILD_FAILED) from error
    if exit_code:
        raise typer.Exit(code=exit_code)
