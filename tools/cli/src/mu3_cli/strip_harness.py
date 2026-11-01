"""The DI stripping harness: build a Windows player of a small mirror project, run it, report its checks.

Managed code stripping removes code that only reflection reaches, and the DI container reaches the
constructors and [Inject] members it fills only through reflection. The Editor and the EditMode and
PlayMode tests never strip, so only a player shows such a regression. The harness template under
tools/di-strip-harness/ registers services that nothing creates with `new`; this module copies it
into a mirror project that references the checkout's Base package, builds a player at the chosen
backend and Managed Stripping Level, runs it, and reads the HARNESS lines the player logs.
"""

from __future__ import annotations

import json
import os
import platform
import re
import shutil
import subprocess
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Sequence

import typer

from mu3_cli.repository import repo_root
from mu3_cli.unity import load_target_specs, project_editor_version, resolve_editor_path


BACKENDS = ("mono", "il2cpp")
STRIPPING_LEVELS = ("Disabled", "Minimal", "Low", "Medium", "High")
BASE_PACKAGE = "com.github.doqltl179.mu3library.base"
BASE_PACKAGE_PATH = "Mu3Library_Base"
UNITASK_PACKAGE = "com.cysharp.unitask"
# The development project whose Editor version and Unity modules the mirror takes: it lacks
# UniTask, so the mirror runs the coroutine path unless --with-unitask adds UniTask.
REFERENCE_TARGET = "urp"
# The development project whose manifest and lock file pin the UniTask that --with-unitask adds.
UNITASK_TARGET = "built-in"
PLAYER_NAME = "Harness"
# Exit codes beside 0 (every check passed) and typer's 2 (a usage error).
EXIT_CHECKS_FAILED = 1
EXIT_BUILD_FAILED = 3
# A project path longer than this risks the Windows path limit inside Library/ and the IL2CPP
# build folders, as compile-unity.sh warns for its mirrors.
LONG_PROJECT_PATH = 100

CHECK_PATTERN = re.compile(r"^HARNESS CHECK (\S+) (PASS|FAIL) ?(.*)$")
RESULT_PATTERN = re.compile(r"^HARNESS RESULT (PASS|FAIL) checks=(\d+) failures=(\d+) errorLogs=(\d+)")


def template_dir(root: Path) -> Path:
    return root / "tools" / "di-strip-harness"


def default_work_dir(root: Path) -> Path:
    return root / "log" / "di-strip-harness"


def run_label(backend: str, stripping: str, with_unitask: bool) -> str:
    """The name of one configuration's player folder and logs, such as mono-high-unitask."""
    return f"{backend}-{stripping.lower()}" + ("-unitask" if with_unitask else "")


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def unitask_source(root: Path) -> str:
    """The UniTask Git URL of the built-in development project, pinned to the commit its lock file names."""
    specs = load_target_specs(root)
    project = root / specs[UNITASK_TARGET].project_path / "Packages"
    url = read_json(project / "manifest.json")["dependencies"][UNITASK_PACKAGE]
    if "#" in url:
        return url
    lock = read_json(project / "packages-lock.json").get("dependencies", {}).get(UNITASK_PACKAGE, {})
    commit = lock.get("hash")
    return f"{url}#{commit}" if commit else url


def mirror_manifest(reference_manifest: dict, base_path: Path, unitask: str | None) -> dict:
    """The mirror's Packages/manifest.json.

    It keeps the reference project's built-in modules and uGUI, which Base's runtime needs, adds Base
    from this checkout, and adds UniTask only when asked: no render pipeline, IDE, or test package,
    and no testables, so the player holds little besides Base and the harness.
    """
    dependencies = {
        name: version
        for name, version in reference_manifest.get("dependencies", {}).items()
        if name.startswith("com.unity.modules.") or name == "com.unity.ugui"
    }
    dependencies[BASE_PACKAGE] = "file:" + base_path.resolve().as_posix()
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


def stop_process_tree(process: subprocess.Popen) -> None:
    """Stop a process this module started and the processes it started, such as Unity's build workers."""
    if platform.system() == "Windows":
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(process.pid)], capture_output=True, check=False)
    process.kill()
    process.wait()


def run_with_timeout(command: list[str], timeout: float, cwd: Path | None = None) -> int | None:
    """Run a command and return its exit code, or None after stopping it at the timeout."""
    process = subprocess.Popen(command, cwd=cwd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        return process.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        stop_process_tree(process)
        return None
    except BaseException:
        stop_process_tree(process)
        raise


Runner = Callable[[list[str], float], "int | None"]


@dataclass(frozen=True)
class HarnessOptions:
    backend: str = "mono"
    stripping: str = "High"
    with_unitask: bool = False
    work_dir: Path | None = None
    build_timeout: float = 3600
    player_timeout: float = 120


class HarnessSetupError(RuntimeError):
    """The mirror could not be prepared or the player could not be built."""


def prepare_mirror(root: Path, project_dir: Path, editor_path: Path, with_unitask: bool, runner: Runner) -> list[str]:
    """Create or refresh the mirror project and return what changed in it."""
    specs = load_target_specs(root)
    reference = root / specs[REFERENCE_TARGET].project_path
    template = template_dir(root)
    changed: list[str] = []

    plugin_asset = "Plugins/HarnessPlugin.dll"
    # The plugin DLL and the scene are made in the mirror, by this module and by the build script.
    generated = [plugin_asset, "Harness/Harness.unity"]
    synced = sync_template(template / "Assets", project_dir / "Assets", generated)
    changed += [f"Assets/{path}" for path in synced]

    manifest = mirror_manifest(
        read_json(reference / "Packages" / "manifest.json"),
        root / BASE_PACKAGE_PATH,
        unitask_source(root) if with_unitask else None,
    )
    if write_if_changed(project_dir / "Packages" / "manifest.json", (json.dumps(manifest, indent=2) + "\n").encode("utf-8")):
        changed.append("Packages/manifest.json")
    version = (reference / "ProjectSettings" / "ProjectVersion.txt").read_bytes()
    if write_if_changed(project_dir / "ProjectSettings" / "ProjectVersion.txt", version):
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
    """Build and run one configuration, print each check, and return the command's exit code."""
    if platform.system() != "Windows":
        raise HarnessSetupError("the harness builds a Windows x64 player, so it runs only on Windows")

    specs = load_target_specs(root)
    version = project_editor_version(specs[REFERENCE_TARGET], root)
    if version is None:
        raise HarnessSetupError(f"no Editor version in {specs[REFERENCE_TARGET].project_path}/ProjectSettings/ProjectVersion.txt")
    editor_path, overridden = resolve_editor_path(version)
    if not editor_path.is_file():
        raise HarnessSetupError(f"the Unity Editor {version} is not installed: {editor_path}")

    work_dir = Path(os.path.abspath(options.work_dir or default_work_dir(root)))
    project_dir = work_dir / "project"
    label = run_label(options.backend, options.stripping, options.with_unitask)
    player_path = work_dir / "players" / label / f"{PLAYER_NAME}.exe"
    build_log = work_dir / f"{label}-build.log"
    player_log = work_dir / f"{label}-player.log"

    echo(f"Unity: {editor_path}" + (" (UNITY_EDITOR override)" if overridden else ""))
    echo(f"Mirror project: {project_dir}")
    if len(str(project_dir)) > LONG_PROJECT_PATH:
        echo(
            f"warning: the mirror path is {len(str(project_dir))} characters, so Unity's Library and the IL2CPP "
            "build may pass the Windows path limit; pass a shorter --work-dir"
        )
    changed = prepare_mirror(root, project_dir, editor_path, options.with_unitask, runner)
    echo(f"Mirror changes: {', '.join(changed) if changed else 'none'}")

    if player_path.parent.exists():
        shutil.rmtree(player_path.parent)
    for log in (build_log, player_log):
        log.unlink(missing_ok=True)

    echo(f"Building {label} (backend {options.backend}, Managed Stripping Level {options.stripping}); log: {build_log}")
    status = runner(build_command(editor_path, project_dir, options.backend, options.stripping, player_path, build_log), options.build_timeout)
    build_text = build_log.read_text(encoding="utf-8", errors="replace") if build_log.is_file() else ""
    for line in build_text.splitlines():
        if line.startswith(("HARNESS BUILD", "HARNESS settings", "HARNESS STEP-", "HARNESS bad")):
            echo(line)
    if status != 0 or not player_path.is_file():
        reason = "timed out and was stopped" if status is None else f"exited with {status}"
        echo(f"error: the build {reason}; see {build_log}")
        return EXIT_BUILD_FAILED

    echo(f"Running {player_path}; log: {player_log}")
    exit_code = runner(player_command(player_path, player_log), options.player_timeout)
    run = parse_player_log(player_log.read_text(encoding="utf-8", errors="replace") if player_log.is_file() else "")

    for note in run.notes:
        echo(note)
    for check in run.checks:
        echo(f"{'PASS' if check.passed else 'FAIL'} {check.name}" + (f" ({check.detail})" if check.detail else ""))

    sizes = player_sizes(player_path)
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


def unity_di_strip_harness(
    stripping: str = typer.Option("High", "--stripping", help="Managed Stripping Level: " + ", ".join(STRIPPING_LEVELS) + "."),
    backend: str = typer.Option("mono", "--backend", help="Scripting backend: mono or il2cpp."),
    with_unitask: bool = typer.Option(
        False, "--with-unitask", help="Add UniTask, pinned as the built-in project pins it, so cores prepare through UniTask."
    ),
    work_dir: Path | None = typer.Option(
        None, "--work-dir", help="Folder for the mirror project, players, and logs (default log/di-strip-harness)."
    ),
    build_timeout: int = typer.Option(3600, "--build-timeout", help="Seconds the Editor may take to import and build."),
    player_timeout: int = typer.Option(120, "--player-timeout", help="Seconds the player may run."),
) -> None:
    """Build the DI stripping harness as a Windows x64 player, run it, and report each check.

    Exits 0 when every check passed, 1 when a check failed or the player did not report,
    and 3 when the mirror could not be prepared or the build failed.
    """
    if stripping not in STRIPPING_LEVELS:
        raise typer.BadParameter(f"--stripping expects one of: {', '.join(STRIPPING_LEVELS)}.")
    if backend not in BACKENDS:
        raise typer.BadParameter(f"--backend expects one of: {', '.join(BACKENDS)}.")
    if build_timeout <= 0 or player_timeout <= 0:
        raise typer.BadParameter("--build-timeout and --player-timeout expect a positive number of seconds.")

    options = HarnessOptions(backend, stripping, with_unitask, work_dir, build_timeout, player_timeout)
    try:
        exit_code = run_harness(repo_root(), options)
    except HarnessSetupError as error:
        typer.echo(f"error: {error}", err=True)
        raise typer.Exit(code=EXIT_BUILD_FAILED) from error
    if exit_code:
        raise typer.Exit(code=exit_code)
