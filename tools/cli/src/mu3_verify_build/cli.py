"""The verification build: a player of a copy of UnityProject_BuiltIn that keeps its Addressables output in the checkout.

The tracked Addressables profile, GitHub_Windows, builds the bundles and the remote catalog into an
absolute folder outside the checkout, the one the real bundle deployment uses, and by the user's
decision (#481) neither that profile nor the deployment changes. This tool makes a copy of the
project and Base from HEAD's tracked files, changes the copy alone so that a player build writes
nothing outside the checkout and its work folder, and builds a Windows x64 player of it, with the
Addressables content or without it.
"""

from __future__ import annotations

import os
import platform
import shutil
import stat
import subprocess
import sys
import tarfile
import time
from contextlib import contextmanager
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Callable, Iterator, Sequence

import typer

from mu3_common.checkout import echo_repository_root, repo_root
from mu3_common.editor import UnityTargetSpec, is_hub_editor_for, load_target_specs, project_editor_version, resolve_editor_path
from mu3_common.work_dir import FolderNotClaimedError, claim_folder


# The only development project with Addressables settings.
TARGET = "built-in"
SETTINGS_ASSET = Path("Assets", "AddressableAssetsData", "AddressableAssetSettings.asset")
PROJECT_SETTINGS_ASSET = Path("ProjectSettings", "ProjectSettings.asset")
PROFILE_NAME = "GitHub_Windows"
BUILD_PATH_VARIABLES = ("Remote.BuildPath", "Local.BuildPath")
# The values of AddressableAssetSettings.PlayerBuildOption that each --content sets:
# BuildWithPlayer and DoNotBuildWithPlayer, in the order of the enum in Addressables 2.11.2.
CONTENT_MODES = {"build": 1, "none": 2}
# The player keys its persistentDataPath, where Addressables keeps a downloaded catalog, and its
# PlayerPrefs on these names, so the copy's player reads and writes none of the real player's data.
COMPANY_NAME = "Mu3LibraryVerify"
PRODUCT_NAME = "VerifyBuild"
BUILD_SCRIPT = Path("tools", "verify-build", "Editor", "Mu3VerifyBuild.cs")
COPY_BUILD_SCRIPT = Path("Assets", "Mu3VerifyBuild", "Editor", "Mu3VerifyBuild.cs")
# What a content build leaves in a project and a player build without content copies into the player.
EARLIER_CONTENT = Path("Library", "com.unity.addressables", "aa")
# The file that marks the work folder and the bundles folder as this tool's. It writes it into a new
# or empty folder and changes or deletes files only in a folder that has it. The two folders share it,
# so work_dir_problem keeps them apart.
WORK_DIR_MARKER = ".mu3-verify-build"
WORK_DIR_MARKER_TEXT = "mu3-verify-build made this folder and may change or delete any file in it.\n"
COPY_COMMIT_FILE = "copy-commit.txt"
# Unity's asset import cannot read every package file from a project folder of this many characters
# or more on Windows, and a content build fails from deeper folders (tools/README.md, Fresh Clone Setup).
PROJECT_PATH_LIMIT = 98
# Addressables reads [name] and {name} in a profile value as variables to evaluate.
PROFILE_VARIABLE_CHARACTERS = "[]{}"
LOG_PREFIX = "MU3VERIFY "
CONTENT_BUILT_LINE = "Addressable content successfully built"
# Exit codes beside 0 (the copy is ready, and with a build the player built as asked) and typer's 2
# (a usage error). 1 means Unity ran and the build failed, refused the settings, or built the content
# when it should not have or did not when it should; 3 means the run stopped before Unity started.
EXIT_BUILD_FAILED = 1
EXIT_SETUP_FAILED = 3


class VerifySetupError(RuntimeError):
    """The work folder, the copy, or its settings could not be prepared, or Unity could not be started."""


class SettingsFormatError(ValueError):
    """A settings file of the copy does not have the shape the tool changes."""


@contextmanager
def setup_step(action: str) -> Iterator[None]:
    """Turn a file, data, or process error raised while doing `action` into a VerifySetupError."""
    try:
        yield
    except (OSError, ValueError, subprocess.SubprocessError, tarfile.TarError) as error:
        message = " ".join(str(error).split())
        raise VerifySetupError(f"{action} failed: {type(error).__name__}: {message}") from error


def default_work_dir(root: Path) -> Path:
    return root / "log" / "verify-build"


def bundles_dir(root: Path) -> Path:
    """Where a content build puts the bundles and the remote catalog: inside the checkout, in Git-ignored log/."""
    return root / "log" / "verify-build-bundles"


def yaml_single_quoted(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def profile_path_problem(path: Path) -> str | None:
    """Why a folder cannot be written as a profile value, or None when it can."""
    text = str(path)
    if not path.is_absolute():
        return f"{text} is not an absolute path"
    if any(character in text for character in PROFILE_VARIABLE_CHARACTERS):
        return f"{text} holds one of {PROFILE_VARIABLE_CHARACTERS}, which Addressables reads as a profile variable"
    if any(ord(character) < 32 for character in text):
        return f"{text!r} holds a control character"
    return None


def project_path_problem(copy_project: Path, system: str | None = None) -> str | None:
    """Why the copy's project folder is too deep for Unity on Windows, or None when it is not."""
    if (system or platform.system()) != "Windows" or len(str(copy_project)) < PROJECT_PATH_LIMIT:
        return None
    return (
        f"the copy's project folder {copy_project} has {len(str(copy_project))} characters; on Windows Unity "
        f"cannot read every package file from a project folder of {PROJECT_PATH_LIMIT} characters or more, and "
        "a content build fails from deeper ones (Fresh Clone Setup in tools/README.md); pass a shorter "
        "--work-dir, such as the unit's folder under MU3_WORK_ROOT"
    )


def work_dir_problem(work_dir: Path, bundles: Path) -> str | None:
    """Why --work-dir cannot hold the copy beside the bundles folder, or None when it can.

    A content build empties the bundles folder, so a work folder that is it, lies inside it, or holds
    it would lose the copy. Both carry the same marker, so the marker alone cannot tell them apart.
    """
    # resolve() follows a subst drive or a link to the folder it names, so both spellings meet there.
    work, folder = work_dir.resolve(), bundles.resolve()
    if work == folder:
        relation = "is"
    elif folder in work.parents:
        relation = "is inside"
    elif work in folder.parents:
        relation = "holds"
    else:
        return None
    return (
        f"--work-dir {work_dir} {relation} the bundles folder {bundles}, which a content build empties; "
        "pass a --work-dir apart from it"
    )


def content_mode_problem(copy_project: Path, content: str) -> str | None:
    """Why the copy cannot build a player in this --content mode, or None when it can."""
    if content == "none" and (copy_project / EARLIER_CONTENT).exists():
        return (
            f"{copy_project / EARLIER_CONTENT} holds the content of an earlier build, which a player built with "
            "--content none would carry; pass --fresh to build from a new copy, or another --work-dir"
        )
    return None


def scene_problem(scene: str) -> str | None:
    """Why --scene does not name a scene asset of the copy, or None when it does."""
    path = PurePosixPath(scene)
    if "\\" in scene or path.is_absolute() or ".." in path.parts or path.parts[:1] not in (("Assets",), ("Packages",)):
        return f"--scene expects a scene asset path below Assets/ or Packages/ with forward slashes: {scene}"
    if path.suffix != ".unity":
        return f"--scene expects a .unity scene: {scene}"
    return None


def _indent(line: str) -> int:
    return len(line) - len(line.lstrip(" "))


def _sequence_items(lines: Sequence[str], key_index: int) -> list[tuple[int, int]]:
    """The line ranges of the items of the block sequence that the key on lines[key_index] holds.

    Unity writes a sequence's items at the indentation of its key, each starting with "- ".
    """
    indent = _indent(lines[key_index])
    item_start = " " * indent + "- "
    items: list[tuple[int, int]] = []
    index = key_index + 1
    while index < len(lines):
        line = lines[index]
        if line.startswith(item_start):
            items.append((index, index + 1))
        elif items and line.strip() and _indent(line) > indent:
            items[-1] = (items[-1][0], index + 1)
        else:
            break
        index += 1
    return items


def _item_fields(lines: Sequence[str], start: int, end: int) -> dict[str, int]:
    """The line index of each key of one sequence item, the key after its "- " included."""
    item_indent = _indent(lines[start]) + 2
    fields: dict[str, int] = {}
    for index in range(start, end):
        line = lines[index]
        if index == start:
            line = line.replace("- ", "  ", 1)
        if _indent(line) == item_indent and ":" in line:
            fields.setdefault(line.strip().split(":", 1)[0], index)
    return fields


def _value(line: str) -> str:
    return line.split(":", 1)[1].strip()


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


def override_addressables_settings(text: str, bundles: str, content: str) -> str:
    """The copy's AddressableAssetSettings.asset with the decided changes (#481).

    In the active profile, which must be GitHub_Windows, Remote.BuildPath and Local.BuildPath name
    the bundles folder, and m_BuildAddressablesWithPlayerBuild takes the value of the --content mode.
    Every other line stays as it was, Remote.LoadPath included. A file of another shape raises
    SettingsFormatError, so the tool never guesses which line to change.
    """
    label = SETTINGS_ASSET.as_posix()
    lines = text.splitlines(keepends=True)
    keys = [line.rstrip("\r\n") for line in lines]

    active = _value(keys[_single_key_line(keys, "m_ActiveProfileId", 2, label)])
    names: dict[str, str] = {}
    for start, end in _sequence_items(keys, _single_key_line(keys, "m_ProfileEntryNames", 4, label)):
        fields = _item_fields(keys, start, end)
        if "m_Id" in fields and "m_Name" in fields:
            names[_value(keys[fields["m_Name"]])] = _value(keys[fields["m_Id"]])
    missing = [name for name in BUILD_PATH_VARIABLES if name not in names]
    if missing:
        raise SettingsFormatError(f"{label} names no profile variable {', '.join(missing)}")

    profile: tuple[int, int] | None = None
    for start, end in _sequence_items(keys, _single_key_line(keys, "m_Profiles", 4, label)):
        fields = _item_fields(keys, start, end)
        if "m_Id" in fields and _value(keys[fields["m_Id"]]) == active:
            profile_name = _value(keys[fields["m_ProfileName"]]) if "m_ProfileName" in fields else None
            if profile_name != PROFILE_NAME:
                raise SettingsFormatError(f"the active profile of {label} is {profile_name}, not {PROFILE_NAME}")
            profile = (start, end)
    if profile is None:
        raise SettingsFormatError(f"{label} has no profile with the active profile's id {active}")

    profile_fields = _item_fields(keys, *profile)
    if "m_Values" not in profile_fields:
        raise SettingsFormatError(f"the profile {PROFILE_NAME} of {label} has no m_Values")
    values: dict[str, tuple[int, int]] = {}
    for start, end in _sequence_items(keys, profile_fields["m_Values"]):
        fields = _item_fields(keys, start, end)
        if "m_Id" in fields and "m_Value" in fields:
            values[_value(keys[fields["m_Id"]])] = (fields["m_Value"], end)

    replacements: dict[int, tuple[str, int]] = {}
    for name in BUILD_PATH_VARIABLES:
        if names[name] not in values:
            raise SettingsFormatError(f"the profile {PROFILE_NAME} of {label} has no value for {name}")
        value_index, item_end = values[names[name]]
        # A plain scalar that Unity wrapped continues on the deeper lines below its key.
        continued = value_index + 1
        while continued < item_end and _indent(keys[continued]) > _indent(keys[value_index]):
            continued += 1
        replacements[value_index] = (" " * _indent(keys[value_index]) + "m_Value: " + yaml_single_quoted(bundles), continued)

    option_index = _single_key_line(keys, "m_BuildAddressablesWithPlayerBuild", 2, label)
    replacements[option_index] = (f"  m_BuildAddressablesWithPlayerBuild: {CONTENT_MODES[content]}", option_index + 1)

    for index in sorted(replacements, reverse=True):
        replacement, end = replacements[index]
        _replace_line(lines, index, replacement)
        del lines[index + 1:end]
    return "".join(lines)


def override_project_names(text: str) -> str:
    """The copy's ProjectSettings.asset with the company and product names of the verification player."""
    label = PROJECT_SETTINGS_ASSET.as_posix()
    lines = text.splitlines(keepends=True)
    keys = [line.rstrip("\r\n") for line in lines]
    for key, value in (("companyName", COMPANY_NAME), ("productName", PRODUCT_NAME)):
        _replace_line(lines, _single_key_line(keys, key, 2, label), f"  {key}: {value}")
    return "".join(lines)


def _safe_member_path(name: str, prefixes: Sequence[PurePosixPath]) -> PurePosixPath:
    path = PurePosixPath(name)
    if path.is_absolute() or ".." in path.parts or not any(path.parts[: len(prefix.parts)] == prefix.parts for prefix in prefixes):
        raise VerifySetupError(f"git archive returned a path outside the copied folders: {name}")
    return path


def copy_from_archive(root: Path, commit: str, spec: UnityTargetSpec, work_dir: Path) -> int:
    """Write the tracked files of the project and its package at commit into work_dir.

    The package's Samples~ folder is written a second time, as a real folder at the project's samples
    link, where the development project has a junction or a symbolic link instead. Returns the number
    of files written.
    """
    project = PurePosixPath(spec.project_path.as_posix())
    package = PurePosixPath(spec.package_path.as_posix())
    samples = package / "Samples~"
    samples_link = project / spec.samples_link.as_posix() if spec.samples_link is not None else None
    command = ["git", "-C", str(root), "archive", "--format=tar", commit, "--", project.as_posix(), package.as_posix()]
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    written = 0
    unreadable: tarfile.TarError | None = None
    try:
        with tarfile.open(fileobj=process.stdout, mode="r|") as archive:
            for member in archive:
                if member.isdir():
                    continue
                path = _safe_member_path(member.name, (project, package))
                if not member.isfile():
                    raise VerifySetupError(f"{member.name} is not a regular file at {commit}, so it cannot be copied")
                source = archive.extractfile(member)
                data = source.read() if source is not None else b""
                destinations = [path]
                if samples_link is not None and path.parts[: len(samples.parts)] == samples.parts:
                    destinations.append(samples_link.joinpath(*path.parts[len(samples.parts):]))
                for destination in destinations:
                    target = work_dir.joinpath(*destination.parts)
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(data)
                    written += 1
    except tarfile.TarError as error:
        # A git archive that failed ends its stream early; its own message names the cause.
        unreadable = error
    finally:
        _, git_error = process.communicate()
    if process.returncode != 0:
        raise VerifySetupError(f"git archive exited with {process.returncode}: {git_error.decode(errors='replace').strip()}")
    if unreadable is not None:
        raise VerifySetupError(f"the archive of {commit} could not be read: {unreadable}")
    return written


def git_output(root: Path, *arguments: str) -> str:
    return subprocess.run(
        ["git", "-C", str(root), *arguments], check=True, capture_output=True, text=True, encoding="utf-8"
    ).stdout


def remove_tree(path: Path) -> None:
    """Delete a folder, read-only files included, which Unity's Library can hold."""

    def retry_writable(function: Callable, failed: str, *_: object) -> None:
        os.chmod(failed, stat.S_IWRITE)
        function(failed)

    if sys.version_info >= (3, 12):
        shutil.rmtree(path, onexc=retry_writable)
    else:
        shutil.rmtree(path, onerror=retry_writable)


def build_command(
    editor_path: Path,
    copy_project: Path,
    content: str,
    bundles: Path,
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
        str(copy_project),
        "-executeMethod",
        "Mu3VerifyBuild.Build",
        "-mu3-content",
        content,
        "-mu3-bundles",
        str(bundles),
        "-mu3-out",
        str(player_path),
    ]
    for scene in scenes:
        command += ["-mu3-scene", scene]
    return command + ["-logFile", str(log_path)]


def files_below(folder: Path, skip: str | None = None) -> list[Path]:
    if not folder.is_dir():
        return []
    return sorted(path for path in folder.rglob("*") if path.is_file() and path.name != skip)


Runner = Callable[[list[str]], int]


def default_runner(command: list[str]) -> int:
    return subprocess.run(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False).returncode


@dataclass(frozen=True)
class VerifyOptions:
    content: str
    work_dir: Path | None = None
    fresh: bool = False
    build: bool = True
    scenes: tuple[str, ...] = ()


def prepare_copy(
    root: Path, options: VerifyOptions, spec: UnityTargetSpec, work_dir: Path, bundles: Path, echo: Callable[[str], None]
) -> Path:
    """Make or reuse the copy in work_dir and point it at the bundles folder; returns the copy's project folder."""
    copy_project = work_dir / spec.project_path
    copy_package = work_dir / spec.package_path
    commit_file = work_dir / COPY_COMMIT_FILE

    with setup_step(f"preparing the work folder {work_dir}"):
        try:
            if claim_folder(work_dir, WORK_DIR_MARKER, WORK_DIR_MARKER_TEXT):
                echo(f"Marked {work_dir} as the verification build's work folder ({WORK_DIR_MARKER})")
        except FolderNotClaimedError as error:
            raise VerifySetupError(
                f"{error}, so mu3-verify-build will not change or delete files in it; pass a new or empty --work-dir"
            ) from error
    with setup_step("reading HEAD"):
        commit = git_output(root, "rev-parse", "HEAD").strip()

    exists = copy_project.exists() or copy_package.exists()
    if options.fresh and exists:
        echo(f"Removing the copy in {work_dir} (--fresh)")
        with setup_step(f"removing the copy in {work_dir}"):
            for folder in (copy_project, copy_package, work_dir / "players"):
                if folder.exists():
                    remove_tree(folder)
            commit_file.unlink(missing_ok=True)
        exists = False
    if exists:
        with setup_step(f"reading {commit_file}"):
            recorded = commit_file.read_text(encoding="utf-8").strip() if commit_file.is_file() else None
        if recorded != commit:
            held = f"a copy of {recorded}" if recorded else "a copy whose commit is unknown, such as one an interrupted run left"
            raise VerifySetupError(f"{work_dir} holds {held}, not of HEAD {commit}; pass --fresh to make a new copy")
        echo(f"Reusing the copy of {commit} in {work_dir}, with its Library/")
    else:
        echo(f"Copying the tracked files of {spec.project_path.as_posix()} and {spec.package_path.as_posix()} at HEAD {commit}")
        with setup_step(f"copying HEAD into {work_dir}"):
            written = copy_from_archive(root, commit, spec, work_dir)
            commit_file.write_text(commit + "\n", encoding="utf-8")
        echo(f"Wrote {written} files; the samples are a real folder in the copy, not a link")

    with setup_step("reading the checkout's uncommitted changes"):
        changed = git_output(root, "status", "--porcelain", "--", spec.project_path.as_posix(), spec.package_path.as_posix())
    if changed.strip():
        echo(f"warning: the copy holds HEAD {commit}, so these uncommitted changes are not in it:")
        for line in changed.splitlines():
            echo(f"  {line}")

    problem = content_mode_problem(copy_project, options.content)
    if problem:
        raise VerifySetupError(problem)

    with setup_step(f"changing the copy's {SETTINGS_ASSET.as_posix()}"):
        settings = copy_project / SETTINGS_ASSET
        settings.write_bytes(
            override_addressables_settings(settings.read_bytes().decode("utf-8"), str(bundles), options.content).encode("utf-8")
        )
    with setup_step(f"changing the copy's {PROJECT_SETTINGS_ASSET.as_posix()}"):
        project_settings = copy_project / PROJECT_SETTINGS_ASSET
        project_settings.write_bytes(override_project_names(project_settings.read_bytes().decode("utf-8")).encode("utf-8"))
    with setup_step(f"copying {BUILD_SCRIPT.as_posix()} into the copy"):
        script = copy_project / COPY_BUILD_SCRIPT
        script.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(root / BUILD_SCRIPT, script)
    echo(
        f"Changed in the copy only: {PROFILE_NAME}'s {' and '.join(BUILD_PATH_VARIABLES)} -> {bundles}, "
        f"m_BuildAddressablesWithPlayerBuild -> {CONTENT_MODES[options.content]}, "
        f"companyName/productName -> {COMPANY_NAME}/{PRODUCT_NAME}, and {COPY_BUILD_SCRIPT.as_posix()} added; "
        "Remote.LoadPath is left as it is"
    )
    return copy_project


def run_verify_build(root: Path, options: VerifyOptions, runner: Runner = default_runner, echo: Callable[[str], None] = typer.echo) -> int:
    """Prepare the copy and, unless options.build is off, build its player; returns the command's exit code.

    Raises VerifySetupError, which the command ends with exit 3, when anything before Unity fails.
    """
    with setup_step("reading the development projects"):
        spec = load_target_specs(root)[TARGET]
    work_dir = Path(os.path.abspath(options.work_dir or default_work_dir(root)))
    bundles = Path(os.path.abspath(bundles_dir(root)))
    for problem in (
        work_dir_problem(work_dir, bundles),
        project_path_problem(work_dir / spec.project_path),
        profile_path_problem(bundles),
    ):
        if problem:
            raise VerifySetupError(problem)

    copy_project = prepare_copy(root, options, spec, work_dir, bundles, echo)
    if not options.build:
        echo(f"The copy is ready: open {copy_project} in Unity {project_editor_version(spec, work_dir) or ''}".rstrip())
        return 0

    version = project_editor_version(spec, work_dir)
    if version is None:
        raise VerifySetupError(f"no Editor version in {copy_project / 'ProjectSettings' / 'ProjectVersion.txt'}")
    editor_path, overridden = resolve_editor_path(version)
    if not editor_path.is_file():
        raise VerifySetupError(f"the Unity Editor {version} is not installed: {editor_path}")
    echo(f"Unity: {editor_path}" + (" (UNITY_EDITOR override)" if overridden else ""))
    if overridden and not is_hub_editor_for(editor_path, version):
        echo(f"warning: {spec.project_path.as_posix()} requires Unity {version} but the explicit override will be used: {editor_path}")

    label = f"content-{options.content}"
    player_path = work_dir / "players" / label / f"{PRODUCT_NAME}.exe"
    log_path = work_dir / f"{label}-build.log"
    with setup_step(f"removing the previous {label} player and log"):
        if player_path.parent.exists():
            remove_tree(player_path.parent)
        log_path.unlink(missing_ok=True)
    if options.content == "build":
        with setup_step(f"emptying the bundles folder {bundles}"):
            try:
                claim_folder(bundles, WORK_DIR_MARKER, WORK_DIR_MARKER_TEXT)
            except FolderNotClaimedError as error:
                raise VerifySetupError(f"{error}, so mu3-verify-build will not build content into it") from error
            for child in bundles.iterdir():
                if child.name == WORK_DIR_MARKER:
                    continue
                if child.is_dir():
                    remove_tree(child)
                else:
                    child.unlink()

    echo(f"Building {label}; log: {log_path}")
    started = time.monotonic()
    with setup_step("starting Unity"):
        status = runner(build_command(editor_path, copy_project, options.content, bundles, player_path, options.scenes, log_path))
    duration = time.monotonic() - started
    with setup_step(f"reading {log_path}"):
        log_text = log_path.read_text(encoding="utf-8", errors="replace") if log_path.is_file() else ""
    for line in log_text.splitlines():
        if line.startswith(LOG_PREFIX) or CONTENT_BUILT_LINE in line:
            echo(line)

    content_built = (copy_project / EARLIER_CONTENT).is_dir() or CONTENT_BUILT_LINE in log_text
    built_files = files_below(bundles, WORK_DIR_MARKER) if options.content == "build" else []
    echo(f"Unity exited with {status} after {duration:.1f} s")
    echo(f"Player: {player_path}" if player_path.is_file() else "Player: none")
    if options.content == "build":
        echo(f"Content: {'built' if content_built else 'not built'}, {len(built_files)} files in {bundles}")
    else:
        echo(f"Content: {'built' if content_built else 'not built'}")

    problems: list[str] = []
    if status != 0:
        problems.append(f"Unity exited with {status}; see {log_path}")
    if not player_path.is_file():
        problems.append("no player was built")
    if options.content == "build" and not (content_built and built_files):
        problems.append(f"no content was built into {bundles}")
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
        help="build: build the Addressables content with the player, into log/verify-build-bundles in the checkout; "
        "none: build the player without content, from a copy that never built any.",
    ),
    work_dir: Path | None = typer.Option(
        None,
        "--work-dir",
        help="Folder for the copy, the players, and the build logs (default log/verify-build); "
        "it must be new, empty, or one this command made.",
    ),
    fresh: bool = typer.Option(False, "--fresh", help="Delete the copy in --work-dir and make a new one of HEAD."),
    build: bool = typer.Option(True, "--build/--no-build", help="Build the player, or only make and change the copy."),
    scene: list[str] | None = typer.Option(
        None, "--scene", help="Scene asset of the copy to build, such as Assets/Mu3LibrarySamples/...; repeat for more. Default: one empty scene."
    ),
) -> None:
    """Build a Windows x64 player of a copy of UnityProject_BuiltIn that writes no Addressables output outside the checkout.

    Exits 0 when the copy is ready and, with a build, the player built as --content asks; 1 when
    Unity ran and the build failed or did not build the content as asked; and 3 when the run stopped
    before Unity started.
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

    options = VerifyOptions(content=content, work_dir=work_dir, fresh=fresh, build=build, scenes=scenes)
    root = repo_root()
    echo_repository_root(root)
    try:
        exit_code = run_verify_build(root, options)
    except VerifySetupError as error:
        typer.echo(f"error: {error}", err=True)
        raise typer.Exit(code=EXIT_SETUP_FAILED) from error
    if exit_code:
        raise typer.Exit(code=exit_code)
