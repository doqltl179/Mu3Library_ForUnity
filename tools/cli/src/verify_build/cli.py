"""The verification build: a Windows x64 player of a project generated from the full config.

The full config keeps only the Addressables settings that edit-mode Addressables initializes from:
the Default profile and a Default Local Group that builds into the project's own Library (ADR 0001
D17), so a player build writes no Addressables output outside the work folder. This tool generates
the project in its work folder, sets in that project alone whether the player build builds the
content, gives the player a company and product name of its own, adds the build script, and builds
the player, with the Addressables content or without it.
"""

from __future__ import annotations

import os
import platform
import shutil
import subprocess
import time
from contextlib import contextmanager
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Callable, Iterator, Sequence

import typer

from common.checkout import echo_repository_root, repo_root
from common.editor import folder_editor_version, is_hub_editor_for, resolve_editor_path
from common.process import run_with_timeout
from common.unity_projects import (
    CONFIGS_FOLDER,
    ConfigError,
    GenerateError,
    generate_project,
    load_configs,
    remove_generated_project,
)
from common.work_dir import FolderNotClaimedError, claim_folder


# The config whose project the player is built from, the only one with Addressables settings.
CONFIG = "full"
SETTINGS_ASSET = Path("Assets", "AddressableAssetsData", "AddressableAssetSettings.asset")
PROJECT_SETTINGS_ASSET = Path("ProjectSettings", "ProjectSettings.asset")
# The values of AddressableAssetSettings.PlayerBuildOption that each --content sets:
# BuildWithPlayer and DoNotBuildWithPlayer, in the order of the enum in Addressables 2.11.2.
CONTENT_MODES = {"build": 1, "none": 2}
# The player keys its persistentDataPath, where Addressables keeps a downloaded catalog, and its
# PlayerPrefs on these names, so the player reads and writes nothing that a player under the
# config's names, Mu3Library of DefaultCompany, left on the PC.
COMPANY_NAME = "Mu3LibraryVerify"
PRODUCT_NAME = "VerifyBuild"
BUILD_SCRIPT = Path("tools", "verify-build", "Editor", "Mu3VerifyBuild.cs")
PROJECT_BUILD_SCRIPT = Path("Assets", "Mu3VerifyBuild", "Editor", "Mu3VerifyBuild.cs")
# What a content build leaves in a project and a player build without content copies into the player.
EARLIER_CONTENT = Path("Library", "com.unity.addressables", "aa")
# What a player built with the content carries: the content build's settings, beside its catalog.
PLAYER_CONTENT = Path(f"{PRODUCT_NAME}_Data", "StreamingAssets", "aa", "settings.json")
# The file that marks the work folder as this tool's. It writes it into a new or empty folder and
# changes or deletes files only in a folder that has it.
WORK_DIR_MARKER = ".mu3-verify-build"
WORK_DIR_MARKER_TEXT = "mu3-verify-build made this folder and may change or delete any file in it.\n"
# Unity's asset import cannot read every package file from a project folder of this many characters
# or more on Windows, and a content build fails from deeper folders (tools/README.md, Fresh Clone Setup).
PROJECT_PATH_LIMIT = 98
LOG_PREFIX = "MU3VERIFY "
CONTENT_BUILT_LINE = "Addressable content successfully built"
# Exit codes beside 0 (the project is ready, and with a build the player built as asked) and typer's
# 2 (a usage error). 1 means Unity ran and the build failed, refused the settings, passed the build
# timeout, or built the content when it should not have or did not when it should; 3 means the
# project could not be prepared, Unity could not be started, or its log could not be read.
EXIT_BUILD_FAILED = 1
EXIT_SETUP_FAILED = 3


class VerifySetupError(RuntimeError):
    """The work folder, the project, or its settings could not be prepared, Unity could not be started, or its log read."""


class SettingsFormatError(ValueError):
    """A settings file of the project does not have the shape the tool changes."""


@contextmanager
def setup_step(action: str) -> Iterator[None]:
    """Turn a file, data, or process error raised while doing `action` into a VerifySetupError."""
    try:
        yield
    except (OSError, ValueError, subprocess.SubprocessError, GenerateError) as error:
        message = " ".join(str(error).split())
        raise VerifySetupError(f"{action} failed: {type(error).__name__}: {message}") from error


def default_work_dir(root: Path) -> Path:
    return root / "log" / "verify-build"


def project_path_problem(project: Path, system: str | None = None) -> str | None:
    """Why the project folder is too deep for Unity on Windows, or None when it is not."""
    if (system or platform.system()) != "Windows" or len(str(project)) < PROJECT_PATH_LIMIT:
        return None
    return (
        f"the project folder {project} has {len(str(project))} characters; on Windows Unity "
        f"cannot read every package file from a project folder of {PROJECT_PATH_LIMIT} characters or more, and "
        "a content build fails from deeper ones (Fresh Clone Setup in tools/README.md); pass a shorter "
        "--work-dir, such as a folder in the unit's work folder under MU3_WORK_ROOT"
    )


def content_mode_problem(project: Path, content: str) -> str | None:
    """Why the project cannot build a player in this --content mode, or None when it can."""
    if content == "none" and (project / EARLIER_CONTENT).exists():
        return (
            f"{project / EARLIER_CONTENT} holds the content of an earlier build, which a player built with "
            "--content none would carry; pass --fresh to build from a new project, or another --work-dir"
        )
    return None


def scene_problem(scene: str) -> str | None:
    """Why --scene does not name a scene asset of the project, or None when it does."""
    path = PurePosixPath(scene)
    if "\\" in scene or path.is_absolute() or ".." in path.parts or path.parts[:1] not in (("Assets",), ("Packages",)):
        return f"--scene expects a scene asset path below Assets/ or Packages/ with forward slashes: {scene}"
    if path.suffix != ".unity":
        return f"--scene expects a .unity scene: {scene}"
    return None


def _single_key_line(lines: Sequence[str], key: str, indent: int, label: str) -> int:
    prefix = " " * indent + key + ":"
    found = [index for index, line in enumerate(lines) if line.startswith(prefix)]
    if len(found) != 1:
        raise SettingsFormatError(f"{label} has {len(found)} {key} lines, not one")
    return found[0]


def _replace_line(lines: list[str], index: int, text: str) -> None:
    line = lines[index]
    ending = line[len(line.rstrip("\r\n")):]
    lines[index] = text + ending


def _replace_keys(text: str, values: Sequence[tuple[str, str]], label: str) -> str:
    """The text with the one line of each top-level key of a Unity asset set to its value."""
    lines = text.splitlines(keepends=True)
    keys = [line.rstrip("\r\n") for line in lines]
    for key, value in values:
        _replace_line(lines, _single_key_line(keys, key, 2, label), f"  {key}: {value}")
    return "".join(lines)


def override_player_build_option(text: str, content: str) -> str:
    """The project's AddressableAssetSettings.asset with m_BuildAddressablesWithPlayerBuild set for --content.

    Every other line stays as it is. A file without exactly one such line raises SettingsFormatError,
    so the tool never guesses which line to change.
    """
    return _replace_keys(text, [("m_BuildAddressablesWithPlayerBuild", str(CONTENT_MODES[content]))], SETTINGS_ASSET.as_posix())


def override_project_names(text: str) -> str:
    """The project's ProjectSettings.asset with the company and product names of the verification player."""
    return _replace_keys(text, [("companyName", COMPANY_NAME), ("productName", PRODUCT_NAME)], PROJECT_SETTINGS_ASSET.as_posix())


def build_command(
    editor_path: Path,
    project: Path,
    content: str,
    player_path: Path,
    scenes: Sequence[str],
    log_path: Path,
) -> list[str]:
    command = [
        str(editor_path),
        "-batchmode",
        "-buildTarget",
        "Win64",
        "-projectPath",
        str(project),
        "-executeMethod",
        "Mu3VerifyBuild.Build",
        "-mu3-content",
        content,
        "-mu3-out",
        str(player_path),
    ]
    for scene in scenes:
        command += ["-mu3-scene", scene]
    return command + ["-logFile", str(log_path)]


# Runs a command with a timeout in seconds and returns its exit code, or None when it was stopped there.
Runner = Callable[[list[str], float], "int | None"]


def default_runner(command: list[str], timeout: float) -> int | None:
    return run_with_timeout(command, timeout)


@dataclass(frozen=True)
class VerifyOptions:
    content: str
    work_dir: Path | None = None
    fresh: bool = False
    build: bool = True
    scenes: tuple[str, ...] = ()
    # Seconds Unity may take to open the project and build; then it is stopped with what it started.
    build_timeout: float = 3600


def prepare_project(root: Path, options: VerifyOptions, work_dir: Path, echo: Callable[[str], None]) -> Path:
    """Generate or refresh the full project in work_dir and change it for the build; returns its folder."""
    project = work_dir / CONFIG
    players = work_dir / "players"

    with setup_step(f"preparing the work folder {work_dir}"):
        try:
            if claim_folder(work_dir, WORK_DIR_MARKER, WORK_DIR_MARKER_TEXT):
                echo(f"Marked {work_dir} as the verification build's work folder ({WORK_DIR_MARKER})")
        except FolderNotClaimedError as error:
            raise VerifySetupError(
                f"{error}, so mu3-verify-build will not change or delete files in it; pass a new or empty --work-dir"
            ) from error

    if options.fresh and (project.exists() or players.exists()):
        echo(f"Removing the project and the players in {work_dir} (--fresh)")
        with setup_step(f"removing the project in {work_dir}"):
            remove_generated_project(project)
            if players.exists():
                shutil.rmtree(players)

    try:
        config = load_configs(root)[CONFIG]
    except (ConfigError, KeyError) as error:
        raise VerifySetupError(f"reading {(CONFIGS_FOLDER / CONFIG).as_posix()} failed: {error}") from error
    with setup_step(f"generating the project in {project}"):
        result = generate_project(root, config, project)
    changes = f"{len(result.written)} file(s) written, {len(result.removed)} removed" if result.written or result.removed else "already up to date"
    echo(f"{'Generated' if result.created else 'Refreshed'} {project} from {(CONFIGS_FOLDER / CONFIG).as_posix()}: {changes}")

    problem = content_mode_problem(project, options.content)
    if problem:
        raise VerifySetupError(problem)

    with setup_step(f"changing the project's {SETTINGS_ASSET.as_posix()}"):
        settings = project / SETTINGS_ASSET
        settings.write_bytes(override_player_build_option(settings.read_bytes().decode("utf-8"), options.content).encode("utf-8"))
    with setup_step(f"changing the project's {PROJECT_SETTINGS_ASSET.as_posix()}"):
        project_settings = project / PROJECT_SETTINGS_ASSET
        project_settings.write_bytes(override_project_names(project_settings.read_bytes().decode("utf-8")).encode("utf-8"))
    with setup_step(f"copying {BUILD_SCRIPT.as_posix()} into the project"):
        script = project / PROJECT_BUILD_SCRIPT
        script.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(root / BUILD_SCRIPT, script)
    echo(
        f"Changed in this project only: m_BuildAddressablesWithPlayerBuild -> {CONTENT_MODES[options.content]}, "
        f"companyName/productName -> {COMPANY_NAME}/{PRODUCT_NAME}, and {PROJECT_BUILD_SCRIPT.as_posix()} added"
    )
    return project


def run_verify_build(root: Path, options: VerifyOptions, runner: Runner = default_runner, echo: Callable[[str], None] = typer.echo) -> int:
    """Prepare the project and, unless options.build is off, build its player; returns the command's exit code.

    Raises VerifySetupError, which the command ends with exit 3, when the project cannot be prepared,
    Unity cannot be started, or its log cannot be read.
    """
    work_dir = Path(os.path.abspath(options.work_dir or default_work_dir(root)))
    problem = project_path_problem(work_dir / CONFIG)
    if problem:
        raise VerifySetupError(problem)

    project = prepare_project(root, options, work_dir, echo)
    version = folder_editor_version(project)
    if not options.build:
        echo(f"The project is ready: open {project} in Unity {version or ''}".rstrip())
        return 0

    if version is None:
        raise VerifySetupError(f"no Editor version in {project / 'ProjectSettings' / 'ProjectVersion.txt'}")
    editor_path, overridden = resolve_editor_path(version)
    if not editor_path.is_file():
        raise VerifySetupError(f"the Unity Editor {version} is not installed: {editor_path}")
    echo(f"Unity: {editor_path}" + (" (UNITY_EDITOR override)" if overridden else ""))
    if overridden and not is_hub_editor_for(editor_path, version):
        echo(f"warning: {(CONFIGS_FOLDER / CONFIG).as_posix()} requires Unity {version} but the explicit override will be used: {editor_path}")

    label = f"content-{options.content}"
    player_path = work_dir / "players" / label / f"{PRODUCT_NAME}.exe"
    log_path = work_dir / f"{label}-build.log"
    with setup_step(f"removing the previous {label} player and log"):
        if player_path.parent.exists():
            shutil.rmtree(player_path.parent)
        log_path.unlink(missing_ok=True)

    echo(f"Building {label}; log: {log_path}")
    started = time.monotonic()
    with setup_step("starting Unity"):
        status = runner(build_command(editor_path, project, options.content, player_path, options.scenes, log_path), options.build_timeout)
    duration = time.monotonic() - started
    with setup_step(f"reading {log_path}"):
        log_text = log_path.read_text(encoding="utf-8", errors="replace") if log_path.is_file() else ""
    for line in log_text.splitlines():
        if line.startswith(LOG_PREFIX) or CONTENT_BUILT_LINE in line:
            echo(line)

    content_built = (project / EARLIER_CONTENT).is_dir() or CONTENT_BUILT_LINE in log_text
    carried = (player_path.parent / PLAYER_CONTENT).is_file()
    if status is None:
        echo(f"Unity was stopped after {duration:.1f} s, at the build timeout of {options.build_timeout:g} s")
    else:
        echo(f"Unity exited with {status} after {duration:.1f} s")
    echo(f"Player: {player_path}" if player_path.is_file() else "Player: none")
    echo(f"Content: {'built' if content_built else 'not built'}, {'carried' if carried else 'not carried'} in the player's StreamingAssets/aa")

    problems: list[str] = []
    if status is None:
        problems.append(f"Unity did not finish within --build-timeout and was stopped with the processes it started; see {log_path}")
    elif status != 0:
        problems.append(f"Unity exited with {status}; see {log_path}")
    if not player_path.is_file():
        problems.append("no player was built")
    elif options.content == "build" and not (content_built and carried):
        problems.append(f"the player carries no Addressables content: {player_path.parent / PLAYER_CONTENT} is missing")
    if options.content == "none" and content_built:
        problems.append("content was built although --content none turned it off")
    if problems:
        for problem in problems:
            echo(f"FAIL: {problem}")
        return EXIT_BUILD_FAILED
    echo(f"PASS: the player built {'with' if options.content == 'build' else 'without'} Addressables content")
    return 0


app = typer.Typer(add_completion=False)


@app.command()
def verify_build(
    content: str = typer.Option(
        ...,
        "--content",
        help="build: build the Addressables content with the player, which carries it in StreamingAssets/aa; "
        "none: build the player without content, from a project that never built any.",
    ),
    work_dir: Path | None = typer.Option(
        None,
        "--work-dir",
        help="Folder for the generated project, the players, and the build logs (default log/verify-build); "
        "it must be new, empty, or one this command made.",
    ),
    fresh: bool = typer.Option(False, "--fresh", help="Delete the project and the players in --work-dir and generate a new project."),
    build: bool = typer.Option(True, "--build/--no-build", help="Build the player, or only generate and change the project."),
    scene: list[str] | None = typer.Option(
        None, "--scene", help="Scene asset of the project to build, such as Assets/Mu3LibrarySamples/...; repeat for more. Default: one empty scene."
    ),
    build_timeout: int = typer.Option(
        3600, "--build-timeout", help="Seconds Unity may take to open the project and build, after which it is stopped."
    ),
) -> None:
    """Build a Windows x64 player of a project generated from tools/unity-projects/full.

    Exits 0 when the project is ready and, with a build, the player built as --content asks; 1 when
    Unity ran and the build failed, passed --build-timeout, or did not build the content as asked;
    and 3 when the project could not be prepared, Unity could not be started, or its log could not be read.
    """
    if content not in CONTENT_MODES:
        raise typer.BadParameter(f"--content expects one of: {', '.join(CONTENT_MODES)}.")
    scenes = tuple(scene or ())
    for name in scenes:
        problem = scene_problem(name)
        if problem:
            raise typer.BadParameter(problem)
    if scenes and not build:
        raise typer.BadParameter("--scene names what to build, so it cannot be combined with --no-build.")
    if build_timeout <= 0:
        raise typer.BadParameter("--build-timeout expects a positive number of seconds.")

    options = VerifyOptions(
        content=content, work_dir=work_dir, fresh=fresh, build=build, scenes=scenes, build_timeout=build_timeout
    )
    root = repo_root()
    echo_repository_root(root)
    try:
        exit_code = run_verify_build(root, options)
    except VerifySetupError as error:
        typer.echo(f"error: {error}", err=True)
        raise typer.Exit(code=EXIT_SETUP_FAILED) from error
    if exit_code:
        raise typer.Exit(code=exit_code)
