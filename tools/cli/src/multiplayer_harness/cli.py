"""The multiplayer harness: two Windows players of one generated project play one session on this computer.

A host and a guest go through a player-hosted session of Base's Multiplayer on the machine-wide
local network, as the Multiplayer sample's unattended run does, without the sample: the host
creates a public lobby with lobby data, the guest finds it, joins it by its lobby ID, and sets
participant data, both get ready, the host starts the game, they exchange a message, the host
deletes the lobby data, the guest deletes its participant data, and the host kicks the guest. The
template under tools/multiplayer-harness/ holds the player's script, which drives the manager
without a DI core, and the build script. This module generates a project from the minimal config
in its work folder, adds the template, builds a Windows player, runs it twice at once, and reads
the MPHARNESS lines each player logs.
"""

from __future__ import annotations

import os
import platform
import re
import secrets
import shutil
import subprocess
import time
from contextlib import contextmanager
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path, PurePosixPath
from typing import Callable, Iterator, Sequence

import typer

from common.checkout import echo_repository_root, repo_root
from common.editor import EDITOR_VERSION_PATTERN, is_hub_editor_for, resolve_editor_path
from common.errors import describe_error
from common.process import run_with_timeout, stop_process_tree
from common.unity_projects import (
    CONFIGS_FOLDER,
    ConfigError,
    GenerateError,
    generate_project,
    load_configs,
    project_folder,
    project_path_problem,
    sync_template,
)
from common.work_dir import FolderNotClaimedError, claim_folder


# The config the project is generated from. The harness needs Base alone, which every config holds,
# and minimal holds no optional SDK, so it imports fastest; its DI package stays unused, since the
# player drives the manager without a core.
CONFIG = "minimal"
TEMPLATE = Path("tools", "multiplayer-harness")
# Where the template goes in the project. The harness changes and deletes files there and nowhere
# else in the project, which the config's files fill.
PROJECT_FOLDER = PurePosixPath("Assets", "MultiplayerHarness")
# What the build script saves in that folder, which the template does not hold.
GENERATED = ("Harness.unity",)
PLAYER_NAME = "MultiplayerHarness"
ROLES = ("host", "guest")
# Exit codes beside 0 (both players passed every check) and typer's 2 (a usage error). 1 means only
# that the players ran and a check failed or a player did not report; anything that stops the
# harness before or around the players, from a refused --work-dir to a failed build, is 3.
EXIT_CHECKS_FAILED = 1
EXIT_SETUP_FAILED = 3
# How long the harness waits past --player-timeout for a player to report and quit before it stops
# the player: each player fails its own run at the timeout and then tears down.
PLAYER_EXIT_GRACE = 30
# The file that marks a --work-dir as the harness's own. The harness writes it into a new or empty
# folder and changes or deletes files only in a folder that has it.
WORK_DIR_MARKER = ".mu3-multiplayer-harness"
WORK_DIR_MARKER_TEXT = "mu3-multiplayer-harness made this folder and may change or delete any file in it.\n"

LOG_PREFIX = "MPHARNESS "
PLAN_PATTERN = re.compile(r"^MPHARNESS PLAN (\S+) ?(.*)$")
CHECK_PATTERN = re.compile(r"^MPHARNESS CHECK (\S+) (PASS|FAIL) ?(.*)$")
RESULT_PATTERN = re.compile(r"^MPHARNESS RESULT (PASS|FAIL) role=\S+ checks=(\d+) failures=(\d+) errorLogs=(\d+)")
NOTE_PREFIXES = ("MPHARNESS START", "MPHARNESS INFO", "MPHARNESS ERRORLOG")
BUILD_PREFIXES = ("MPHARNESS SETTINGS", "MPHARNESS BUILD", "MPHARNESS STEP-", "MPHARNESS bad")


class HarnessSetupError(RuntimeError):
    """The work folder or the project could not be prepared or cleaned, or the players could not be built or started."""


@contextmanager
def setup_step(action: str) -> Iterator[None]:
    """Turn a file, data, or project error raised while doing `action` into a HarnessSetupError.

    None of them says anything about the checks, so none may end the command as a failed check.
    """
    try:
        yield
    except (OSError, ValueError, KeyError, GenerateError) as error:
        raise HarnessSetupError(f"{action} failed: {describe_error(error)}") from error


def default_work_dir(root: Path) -> Path:
    return root / "log" / "multiplayer-harness"


def run_label(editor_version: str | None) -> str:
    """The name of one Editor's player folder and logs: harness, or harness-<version>."""
    return "harness" if editor_version is None else f"harness-{editor_version}"


def new_lobby_name() -> str:
    """A lobby name of this run alone, so that a guest never finds a lobby an earlier run left."""
    return f"mpharness-{datetime.now():%Y%m%d-%H%M%S}-{secrets.token_hex(3)}"


def claim_work_dir(work_dir: Path) -> bool:
    """Make sure the harness may change and delete files in work_dir; returns whether this call marked it."""
    try:
        return claim_folder(work_dir, WORK_DIR_MARKER, WORK_DIR_MARKER_TEXT)
    except FolderNotClaimedError as error:
        raise HarnessSetupError(
            f"{error}, so the harness will not change or delete files in it; pass a new or empty --work-dir"
        ) from error


def build_command(editor_path: Path, project: Path, player_path: Path, log_path: Path) -> list[str]:
    return [
        str(editor_path),
        "-batchmode",
        "-buildTarget",
        "Win64",
        "-projectPath",
        str(project),
        "-executeMethod",
        "MultiplayerHarnessBuild.BuildPlayer",
        "-mu3-out",
        str(player_path),
        "-logFile",
        str(log_path),
    ]


def player_command(player_path: Path, log_path: Path, role: str, lobby: str, timeout: float) -> list[str]:
    return [
        str(player_path),
        "-batchmode",
        "-nographics",
        "-logFile",
        str(log_path),
        "-mpharness-role",
        role,
        "-mpharness-lobby",
        lobby,
        "-mpharness-timeout",
        f"{timeout:g}",
    ]


@dataclass(frozen=True)
class HarnessCheck:
    name: str
    passed: bool
    detail: str


@dataclass
class PlayerRun:
    """What one player logged: the checks its plan names, the checks it ran, and its result line."""

    plan: list[str] | None = None
    checks: list[HarnessCheck] = field(default_factory=list)
    # The checks and the other lines worth showing (START, INFO, ERRORLOG) in the order the player
    # logged them, as the command prints them.
    transcript: list[str] = field(default_factory=list)
    result: str | None = None
    declared_checks: int | None = None
    error_logs: int | None = None

    def unreached(self) -> list[str]:
        """The checks of the plan that the player never logged, in plan order."""
        logged = {check.name for check in self.checks}
        return [name for name in self.plan or () if name not in logged]


def parse_player_log(text: str) -> PlayerRun:
    run = PlayerRun()
    for raw_line in text.splitlines():
        line = raw_line.strip()
        if not line.startswith(LOG_PREFIX):
            continue
        plan = PLAN_PATTERN.match(line)
        if plan:
            run.plan = plan.group(2).split()
            continue
        check = CHECK_PATTERN.match(line)
        if check:
            run.checks.append(HarnessCheck(check.group(1), check.group(2) == "PASS", check.group(3).strip()))
            detail = check.group(3).strip()
            run.transcript.append(f"{check.group(2)} {check.group(1)}" + (f" ({detail})" if detail else ""))
            continue
        result = RESULT_PATTERN.match(line)
        if result:
            run.result = result.group(1)
            run.declared_checks = int(result.group(2))
            run.error_logs = int(result.group(4))
            continue
        if line.startswith(NOTE_PREFIXES):
            run.transcript.append(line.removeprefix(LOG_PREFIX))
    return run


def run_problems(run: PlayerRun, exit_code: int | None) -> list[str]:
    """Why a player's run does not pass; empty when it ran every planned check, passed them, and said so."""
    problems: list[str] = []
    if exit_code is None:
        problems.append("did not exit before the timeout and was stopped")
    elif exit_code != 0:
        problems.append(f"exited with {exit_code}")
    if run.plan is None:
        problems.append("logged no MPHARNESS PLAN line")
    if run.result is None:
        problems.append("logged no MPHARNESS RESULT line")
    else:
        if run.result != "PASS":
            problems.append(f"reported {run.result}")
        if run.declared_checks != len(run.checks):
            problems.append(f"counted {run.declared_checks} checks but logged {len(run.checks)}")
        if run.error_logs:
            problems.append(f"logged {run.error_logs} errors or exceptions")
    failed = [check.name for check in run.checks if not check.passed]
    if failed:
        problems.append(f"{len(failed)} checks failed: {', '.join(failed)}")
    unreached = run.unreached()
    if unreached:
        problems.append(f"{len(unreached)} planned checks were not reached: {', '.join(unreached)}")
    if not run.checks:
        problems.append("logged no MPHARNESS CHECK line")
    return problems


# Runs a command with a timeout in seconds and returns its exit code, or None when it was stopped there.
Runner = Callable[[list[str], float], "int | None"]
# Runs several commands at once with one timeout for all of them and returns each one's exit code,
# or None for each that was still running at the timeout and was stopped.
PlayersRunner = Callable[[Sequence[list[str]], float], "list[int | None]"]


def default_runner(command: list[str], timeout: float) -> int | None:
    return run_with_timeout(command, timeout)


def default_players_runner(commands: Sequence[list[str]], timeout: float) -> list[int | None]:
    """Start every command in order, wait for all of them until the timeout, and stop each still running then."""
    processes: list[subprocess.Popen] = []
    try:
        for command in commands:
            processes.append(subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL))
        deadline = time.monotonic() + timeout
        codes: list[int | None] = []
        for process in processes:
            try:
                codes.append(process.wait(timeout=max(0.0, deadline - time.monotonic())))
            except subprocess.TimeoutExpired:
                stop_process_tree(process)
                codes.append(None)
        return codes
    except BaseException:
        for process in processes:
            if process.poll() is None:
                stop_process_tree(process)
        raise


@dataclass(frozen=True)
class HarnessOptions:
    work_dir: Path | None = None
    # Another installed Editor to build with, such as 6000.3.17f1; None takes the minimal config's.
    editor_version: str | None = None
    build_timeout: float = 3600
    # Seconds each player's run may take; each player fails its run then and quits.
    player_timeout: float = 120


def prepare_project(root: Path, project: Path, editor_path: Path, editor_version: str | None, echo: Callable[[str], None]) -> list[str]:
    """Generate or refresh the minimal project and add the template; returns what changed in the template's folder."""
    try:
        config = load_configs(root)[CONFIG]
    except (ConfigError, KeyError) as error:
        raise HarnessSetupError(f"reading {(CONFIGS_FOLDER / CONFIG).as_posix()} failed: {describe_error(error)}") from error

    with setup_step(f"generating the project in {project}"):
        result = generate_project(root, config, project, editor_version, editor_path if editor_version else None)
    changes = f"{len(result.written)} file(s) written, {len(result.removed)} removed" if result.written or result.removed else "already up to date"
    echo(f"{'Generated' if result.created else 'Refreshed'} {project} from {(CONFIGS_FOLDER / CONFIG).as_posix()}: {changes}")
    for warning in result.warnings:
        echo(f"warning: {warning}")
    if editor_version is not None:
        echo(
            f"Retargeted the project to Unity {editor_version}: ProjectVersion.txt names it, and "
            + ("these Unity packages follow it:" if result.retargeted else "every Unity package already has the version it bundles.")
        )
        for change in result.retargeted:
            echo(f"  - {change}")

    with setup_step(f"copying {TEMPLATE.as_posix()} into the project"):
        synced = sync_template(root / TEMPLATE, project.joinpath(*PROJECT_FOLDER.parts), GENERATED)
    return [f"{PROJECT_FOLDER}/{path}" for path in synced]


def run_harness(
    root: Path,
    options: HarnessOptions,
    runner: Runner = default_runner,
    players_runner: PlayersRunner = default_players_runner,
    echo: Callable[[str], None] = typer.echo,
) -> int:
    """Build the player, run the host and the guest, print each check, and return the command's exit code.

    Raises HarnessSetupError, which the command ends with exit 3, when the work folder, the project,
    or the files around the players cannot be prepared, read, or cleaned, or a player cannot start.
    """
    if platform.system() != "Windows":
        raise HarnessSetupError("the harness builds a Windows x64 player, so it runs only on Windows")

    reference = (CONFIGS_FOLDER / CONFIG).as_posix()
    try:
        config_version = load_configs(root)[CONFIG].editor_version
    except (ConfigError, KeyError) as error:
        raise HarnessSetupError(f"reading {reference} failed: {describe_error(error)}") from error
    version = options.editor_version or config_version
    editor_path, overridden = resolve_editor_path(version)
    if not editor_path.is_file():
        requester = " for --editor-version" if options.editor_version else ""
        raise HarnessSetupError(f"the Unity Editor {version}{requester} is not installed: {editor_path}")

    work_dir = Path(os.path.abspath(options.work_dir or default_work_dir(root)))
    project = project_folder(work_dir, CONFIG, options.editor_version)
    problem = project_path_problem(project)
    if problem:
        raise HarnessSetupError(problem)
    label = run_label(options.editor_version)
    player_path = work_dir / "players" / label / f"{PLAYER_NAME}.exe"
    build_log = work_dir / f"{label}-build.log"
    player_logs = {role: work_dir / f"{label}-{role}.log" for role in ROLES}

    echo(f"Unity: {editor_path}" + (" (UNITY_EDITOR override)" if overridden else ""))
    if overridden and not is_hub_editor_for(editor_path, version):
        requester = "--editor-version" if options.editor_version else reference
        echo(f"warning: {requester} requires Unity {version} but the explicit override will be used: {editor_path}")
    echo(f"Project: {project}")
    with setup_step(f"preparing the work folder {work_dir}"):
        if claim_work_dir(work_dir):
            echo(f"Marked {work_dir} as the harness's work folder ({WORK_DIR_MARKER})")
    changed = prepare_project(root, project, editor_path, options.editor_version, echo)
    echo(f"Template changes: {', '.join(changed) if changed else 'none'}")

    with setup_step(f"removing the previous {label} player and logs"):
        if player_path.parent.exists():
            shutil.rmtree(player_path.parent)
        for log in (build_log, *player_logs.values()):
            log.unlink(missing_ok=True)

    echo(f"Building {label}; log: {build_log}")
    started = time.monotonic()
    with setup_step("running the Unity build"):
        status = runner(build_command(editor_path, project, player_path, build_log), options.build_timeout)
        build_text = build_log.read_text(encoding="utf-8", errors="replace") if build_log.is_file() else ""
    build_seconds = time.monotonic() - started
    for line in build_text.splitlines():
        if line.startswith(BUILD_PREFIXES):
            echo(line)
    if status != 0 or not player_path.is_file():
        reason = "timed out and was stopped" if status is None else f"exited with {status}"
        echo(f"error: the build {reason} after {build_seconds:.1f} s; see {build_log}")
        return EXIT_SETUP_FAILED
    echo(f"Built {player_path} in {build_seconds:.1f} s")

    lobby = new_lobby_name()
    commands = [player_command(player_path, player_logs[role], role, lobby, options.player_timeout) for role in ROLES]
    echo(f"Running the host and the guest on the lobby {lobby}; logs: {player_logs['host']}, {player_logs['guest']}")
    started = time.monotonic()
    with setup_step(f"starting {player_path}"):
        exit_codes = players_runner(commands, options.player_timeout + PLAYER_EXIT_GRACE)
    players_seconds = time.monotonic() - started
    runs: dict[str, PlayerRun] = {}
    for role in ROLES:
        with setup_step(f"reading {player_logs[role]}"):
            log_text = player_logs[role].read_text(encoding="utf-8", errors="replace") if player_logs[role].is_file() else ""
        runs[role] = parse_player_log(log_text)

    # From here on the checks are known, so nothing may turn them into exit 3.
    failures: list[str] = []
    for role, exit_code in zip(ROLES, exit_codes):
        run = runs[role]
        for line in run.transcript:
            echo(f"{role}: {line}")
        for name in run.unreached():
            echo(f"{role}: NOT REACHED {name}")
        passed = sum(1 for check in run.checks if check.passed)
        echo(
            f"{role}: {len(run.checks)} checks, {passed} passed, {len(run.checks) - passed} failed, "
            f"{len(run.unreached())} not reached; exit {'none (stopped)' if exit_code is None else exit_code}"
        )
        failures += [f"{role}: {problem}" for problem in run_problems(run, exit_code)]
    echo(f"The players ran for {players_seconds:.1f} s")

    if failures:
        for failure in failures:
            echo(f"FAIL: {failure}")
        return EXIT_CHECKS_FAILED
    echo(f"PASS: the host and the guest passed every check ({label})")
    return 0


app = typer.Typer(add_completion=False)


@app.command()
def multiplayer_harness(
    work_dir: Path | None = typer.Option(
        None,
        "--work-dir",
        help="Folder for the generated project, the player, and the logs (default log/multiplayer-harness); "
        "it must be new, empty, or one the harness made.",
    ),
    editor_version: str | None = typer.Option(
        None,
        "--editor-version",
        help="Build with this installed Editor, such as 6000.3.17f1, in a project of its own whose Unity packages "
        "take the versions that Editor bundles.",
    ),
    build_timeout: int = typer.Option(3600, "--build-timeout", help="Seconds the Editor may take to import and build."),
    player_timeout: int = typer.Option(
        120,
        "--player-timeout",
        help=f"Seconds each player's run may take; a player that reaches it fails its run, and one still running "
        f"{PLAYER_EXIT_GRACE} seconds later is stopped.",
    ),
) -> None:
    """Build the multiplayer harness as a Windows x64 player, run a host and a guest, and report each check.

    Exits 0 when both players passed every check, 1 when a check failed or a player did not report,
    and 3 when the work folder or the project could not be prepared or cleaned, the build failed, or
    a player could not be started.
    """
    if build_timeout <= 0 or player_timeout <= 0:
        raise typer.BadParameter("--build-timeout and --player-timeout expect a positive number of seconds.")
    if editor_version is not None and not EDITOR_VERSION_PATTERN.fullmatch(editor_version):
        raise typer.BadParameter(f"--editor-version expects a full Unity version such as 6000.3.17f1: {editor_version}")

    options = HarnessOptions(
        work_dir=work_dir, editor_version=editor_version, build_timeout=build_timeout, player_timeout=player_timeout
    )
    root = repo_root()
    echo_repository_root(root)
    try:
        exit_code = run_harness(root, options)
    except HarnessSetupError as error:
        typer.echo(f"error: {error}", err=True)
        raise typer.Exit(code=EXIT_SETUP_FAILED) from error
    if exit_code:
        raise typer.Exit(code=exit_code)
