"""The Unity project configs in tools/unity-projects/ and the projects the tools generate from them.

A config is a folder that holds Packages/manifest.json, ProjectSettings/, and the template files its
project needs. In the manifest, a dependency whose value is "checkout" is the package of that name in
this checkout, found by the name in its package.json, so a package can move without a config edit;
the generated project references it by the absolute path of its folder. The manifest is not
copied as it is.

A project is generated in a work folder outside the checkout, by default
${MU3_WORK_ROOT}/Mu3Library_ForUnity/<unit>/<config>, and refreshed on every run: each file the
config holds is written when it differs, each file that an earlier run wrote and the config no longer
holds is removed, and everything else, Library/ and Unity's other folders among them, stays for the
next run. A directory link that the marker lists, which runs made into a package's Samples~ before
Base dropped its last sample, is removed without following it. A project folder is the tool's only
once its marker file says so, and it belongs to the checkout the marker names, so that two checkouts
whose branches share a unit's folder name never refresh each other's projects.

compile-unity.sh and test-unity.sh run this file by its path, as `python -I <path> describe|generate`,
so it uses the standard library and the common modules that need nothing else.
"""

from __future__ import annotations

import os
import sys

if __name__ == "__main__" and not __package__:
    # Run by its path, the file finds the other common modules beside it; -I leaves PYTHONPATH out.
    sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import argparse
import json
import platform
import re
import shutil
import stat
import subprocess
from dataclasses import dataclass, field
from pathlib import Path, PurePosixPath
from typing import Callable, Mapping, Sequence

from common.editor import editor_package_manager_dir, folder_editor_version, retarget_dependencies
from common.project_lock import file_is_held_open, project_lock_state
from common.work_dir import FolderNotClaimedError, claim_folder


CONFIGS_FOLDER = Path("tools", "unity-projects")
# The manifest value that names a package of this checkout.
CHECKOUT_PACKAGE = "checkout"
MANIFEST = "Packages/manifest.json"
LOCK_FILE = "Packages/packages-lock.json"
PROJECT_VERSION = "ProjectSettings/ProjectVersion.txt"
CONFIG_NAME = re.compile(r"[a-z0-9][a-z0-9-]*")
# Names that compile-unity.sh and test-unity.sh take as selections, not as configs.
RESERVED_NAMES = {"all", "changed"}
WORK_ROOT_VARIABLE = "MU3_WORK_ROOT"
# The project's folder under the work root, which holds one folder per unit.
WORK_FOLDER = "Mu3Library_ForUnity"
# Unity cannot read every package file from a project folder of 98 characters or more on Windows,
# so the unit's folder takes no more than this much of the branch name (.ai/project/wiki/overview.md).
UNIT_LENGTH = 32
# That length: Unity's asset import cannot read every package file from a project folder of this many
# characters or more on Windows (tools/README.md, Fresh Clone Setup).
PROJECT_PATH_LIMIT = 98
PROJECT_MARKER = ".mu3-unity-project"
PROJECT_MARKER_TEXT = (
    "common.unity_projects generated this Unity project from tools/unity-projects for the checkout it "
    "names below and may change or delete the files and links it lists below.\n"
)
_WINDOWS_REPARSE_TAG_MOUNT_POINT = 0xA0000003


class ConfigError(ValueError):
    """A config, or the checkout packages it names, cannot be read as the tools need them."""


class WorkDirError(ValueError):
    """No work folder: MU3_WORK_ROOT is not set, or HEAD names no branch, and no --work-dir was given."""


class GenerateError(RuntimeError):
    """A project could not be generated or refreshed in its folder."""


@dataclass(frozen=True)
class ProjectConfig:
    name: str
    folder: Path
    manifest: dict
    # The checkout packages the manifest names, by package name, each with its folder relative to the root.
    packages: dict[str, PurePosixPath]
    editor_version: str
    has_testables: bool


def _git(root: Path, *arguments: str) -> subprocess.CompletedProcess[bytes]:
    return subprocess.run(["git", "-C", str(root), *arguments], capture_output=True, check=False)


def checkout_packages(root: Path) -> dict[str, PurePosixPath]:
    """Each package of the checkout by the name in its package.json, with its folder relative to the root.

    A package is any package.json that Git tracks or would track, so a package that is not committed
    yet counts too, and one in a Git-ignored folder, such as Unity's Library/, does not.
    """
    listed = _git(root, "ls-files", "-z", "--cached", "--others", "--exclude-standard", "--", "*package.json")
    if listed.returncode != 0:
        raise ConfigError(f"git ls-files failed in {root}: {listed.stderr.decode(errors='replace').strip()}")
    packages: dict[str, PurePosixPath] = {}
    for entry in sorted(set(listed.stdout.decode("utf-8").split("\0"))):
        path = PurePosixPath(entry)
        if path.name != "package.json" or not (root / path).is_file():
            continue
        try:
            data = json.loads((root / path).read_text(encoding="utf-8-sig"))
        except (OSError, ValueError) as error:
            raise ConfigError(f"{path} cannot be read as JSON: {error}") from error
        name = data.get("name") if isinstance(data, dict) else None
        if not isinstance(name, str) or not name:
            continue
        if name in packages:
            raise ConfigError(f"two packages are named {name}: {packages[name]} and {path.parent}")
        packages[name] = path.parent
    return packages


def _read_json_file(path: Path, label: str) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except OSError as error:
        raise ConfigError(f"{label} cannot be read: {error}") from error
    except ValueError as error:
        raise ConfigError(f"{label} is not valid JSON: {error}") from error


def read_config(folder: Path, packages: Mapping[str, PurePosixPath]) -> ProjectConfig:
    """Read and check one config folder; a config the tools cannot generate from raises ConfigError."""
    name = folder.name
    label = (CONFIGS_FOLDER / name).as_posix()
    if not CONFIG_NAME.fullmatch(name) or name in RESERVED_NAMES:
        raise ConfigError(f"{label}: a config name takes lowercase letters, digits, and dashes, and is neither all nor changed")

    manifest = _read_json_file(folder / MANIFEST, f"{label}/{MANIFEST}")
    dependencies = manifest.get("dependencies") if isinstance(manifest, dict) else None
    if not isinstance(dependencies, dict) or not all(isinstance(value, str) for value in dependencies.values()):
        raise ConfigError(f"{label}/{MANIFEST} is not a package manifest: a JSON object whose dependencies map names to strings")
    named: dict[str, PurePosixPath] = {}
    for package, value in dependencies.items():
        if value != CHECKOUT_PACKAGE:
            continue
        if package not in packages:
            raise ConfigError(f'{label}/{MANIFEST} names {package} as "{CHECKOUT_PACKAGE}", but no package.json in the checkout has that name')
        named[package] = packages[package]

    editor_version = folder_editor_version(folder)
    if not editor_version:
        raise ConfigError(f"{label}/{PROJECT_VERSION} is missing or names no m_EditorVersion")

    return ProjectConfig(
        name=name,
        folder=folder,
        manifest=manifest,
        packages=named,
        editor_version=editor_version,
        has_testables=bool(manifest.get("testables")),
    )


def load_configs(root: Path) -> dict[str, ProjectConfig]:
    """Every config in tools/unity-projects/, by name in sorted order."""
    folder = root / CONFIGS_FOLDER
    if not folder.is_dir():
        raise ConfigError(f"{CONFIGS_FOLDER.as_posix()} is missing")
    packages = checkout_packages(root)
    configs = {
        entry.name: read_config(entry, packages)
        for entry in sorted(folder.iterdir(), key=lambda entry: entry.name)
        if entry.is_dir()
    }
    if not configs:
        raise ConfigError(f"{CONFIGS_FOLDER.as_posix()} holds no config")
    return configs


def unit_name(branch: str) -> str:
    """The unit's folder name: the first 32 characters of the branch, slashes as dashes, no trailing dots."""
    return branch[:UNIT_LENGTH].replace("/", "-").rstrip(".")


def current_branch(root: Path) -> str | None:
    completed = _git(root, "symbolic-ref", "--quiet", "--short", "HEAD")
    branch = completed.stdout.decode("utf-8").strip() if completed.returncode == 0 else ""
    return branch or None


def default_work_dir(root: Path, environ: Mapping[str, str] = os.environ) -> Path:
    """${MU3_WORK_ROOT}/Mu3Library_ForUnity/<unit>, or WorkDirError saying what to set."""
    work_root = environ.get(WORK_ROOT_VARIABLE, "").strip()
    if not work_root:
        raise WorkDirError(
            f"no work folder for the generated Unity projects: set the user environment variable {WORK_ROOT_VARIABLE} "
            "to a short folder outside the checkout, such as D:/w, or pass --work-dir <folder>"
        )
    branch = current_branch(root)
    if branch is None:
        raise WorkDirError(
            f"HEAD of {root} names no branch, so the work folder under {WORK_ROOT_VARIABLE} has no unit to be named "
            "after; check out a branch or pass --work-dir <folder>"
        )
    unit = unit_name(branch)
    if not unit:
        raise WorkDirError(f"the branch {branch} leaves no folder name; pass --work-dir <folder>")
    # «Work Folders» in the kit's git-workflow.md: the root is absolute or starts with ~ for the home folder.
    folder = Path(work_root).expanduser()
    if not folder.is_absolute():
        raise WorkDirError(
            f"{WORK_ROOT_VARIABLE} is {work_root}, which is not an absolute folder; set it to one outside the checkout, "
            "such as D:/w, or pass --work-dir <folder>"
        )
    return folder / WORK_FOLDER / unit


def resolve_work_dir(root: Path, work_dir: Path | None, environ: Mapping[str, str] = os.environ) -> Path:
    return Path(os.path.abspath(work_dir)) if work_dir is not None else default_work_dir(root, environ)


def project_folder(work_dir: Path, config: str, editor_version: str | None = None) -> Path:
    """The folder of a config's project: <config>, or <config>-<version> for another Editor, which keeps its own Library/."""
    return work_dir / (config if editor_version is None else f"{config}-{editor_version}")


def is_directory_link(path: Path) -> bool:
    """Whether path is a symbolic link or, on Windows, a directory junction, even a dangling one."""
    if path.is_symlink():
        return True
    try:
        status = os.lstat(path)
    except OSError:
        return False
    return getattr(status, "st_reparse_tag", 0) == _WINDOWS_REPARSE_TAG_MOUNT_POINT


def create_directory_link(link_path: Path, target: Path) -> None:
    """Link link_path to the target folder: a junction on Windows, which needs neither administrator
    rights nor Developer Mode, and a symbolic link elsewhere. Both take the target's absolute path."""
    link_path.parent.mkdir(parents=True, exist_ok=True)
    if platform.system() == "Windows":
        completed = subprocess.run(
            ["cmd", "/d", "/c", "mklink", "/J", str(link_path), str(target)],
            capture_output=True,
            text=True,
            errors="replace",
            check=False,
        )
        if completed.returncode != 0:
            raise OSError(f"mklink /J failed: {(completed.stderr or completed.stdout).strip()}")
        return
    os.symlink(target, link_path, target_is_directory=True)


def remove_directory_link(link_path: Path) -> None:
    """Remove the link itself; neither call follows it into the folder it points at."""
    if platform.system() == "Windows":
        os.rmdir(link_path)
    else:
        os.unlink(link_path)


@dataclass
class ProjectRecord:
    """What the last run wrote into a project folder, as its marker file lists it, and the checkout it wrote them for."""

    files: set[str] = field(default_factory=set)
    links: set[str] = field(default_factory=set)
    checkout: str | None = None


def read_record(project: Path) -> ProjectRecord:
    record = ProjectRecord()
    marker = project / PROJECT_MARKER
    if not marker.is_file():
        return record
    for line in marker.read_text(encoding="utf-8").splitlines():
        kind, _, path = line.partition(" ")
        if kind == "file" and path:
            record.files.add(path)
        elif kind == "link" and path:
            record.links.add(path)
        elif kind == "checkout" and path:
            record.checkout = path
    return record


def write_record(project: Path, record: ProjectRecord) -> None:
    lines = [f"checkout {record.checkout}"] if record.checkout else []
    lines += [f"file {path}" for path in sorted(record.files)] + [f"link {path}" for path in sorted(record.links)]
    (project / PROJECT_MARKER).write_text(PROJECT_MARKER_TEXT + "".join(f"{line}\n" for line in lines), encoding="utf-8")


def checkout_identity(root: Path) -> str:
    """The checkout a project is generated for, spelled the same way through a subst drive or a link."""
    return Path(root).resolve().as_posix()


def _inside_project(project: Path, relative: str) -> Path:
    """The path of a recorded entry, refused when it would leave the project or pass through a link."""
    path = PurePosixPath(relative)
    if path.is_absolute() or ".." in path.parts or not path.parts:
        raise GenerateError(f"{project / PROJECT_MARKER} lists {relative}, which is not a path inside the project")
    current = project
    for part in path.parts[:-1]:
        current = current / part
        if is_directory_link(current):
            raise GenerateError(f"{project / PROJECT_MARKER} lists {relative}, which lies behind the link {current}")
    return project.joinpath(*path.parts)


def write_if_changed(path: Path, data: bytes) -> bool:
    if path.is_file() and path.read_bytes() == data:
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
    return True


def sync_template(template: Path, target: Path, generated: Sequence[str] = ()) -> list[str]:
    """Copy a tool's template folder into a folder of a Unity project and remove the files it no longer has.

    A file whose content is unchanged is left alone, so Unity reimports only what changed, and the
    .meta files Unity wrote are kept. `generated` names the paths, relative to the folder, that the
    run itself creates there, such as a plugin DLL or a scene, which stay too.
    Returns the paths, relative to the folder, that were written or removed.
    """
    changed: list[str] = []
    expected: set[str] = set(generated)
    for source in sorted(path for path in template.rglob("*") if path.is_file()):
        relative = source.relative_to(template).as_posix()
        expected.add(relative)
        if write_if_changed(target / relative, source.read_bytes()):
            changed.append(relative)

    if target.is_dir():
        for existing in sorted(path for path in target.rglob("*") if path.is_file()):
            relative = existing.relative_to(target).as_posix()
            if relative.endswith(".meta"):
                # Unity removes the .meta of a missing asset on its next import.
                continue
            if relative not in expected:
                existing.unlink()
                changed.append(relative)
    return changed


def project_path_problem(project: Path, system: str | None = None, also: str = "") -> str | None:
    """Why a project folder is too deep for Unity on Windows, or None when it is not.

    `also` names what else fails from such a folder, such as ", and a content build fails from deeper ones".
    """
    if (system or platform.system()) != "Windows" or len(str(project)) < PROJECT_PATH_LIMIT:
        return None
    return (
        f"the project folder {project} has {len(str(project))} characters; on Windows Unity "
        f"cannot read every package file from a project folder of {PROJECT_PATH_LIMIT} characters or more{also} "
        "(Fresh Clone Setup in tools/README.md); pass a shorter "
        "--work-dir, such as a folder in the unit's work folder under MU3_WORK_ROOT"
    )


@dataclass
class GenerateResult:
    created: bool = False
    written: list[str] = field(default_factory=list)
    removed: list[str] = field(default_factory=list)
    retargeted: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)


def generated_manifest(root: Path, config: ProjectConfig) -> dict:
    """The config's manifest with each checkout package at the absolute path of its folder."""
    manifest = json.loads(json.dumps(config.manifest))
    for package, folder in config.packages.items():
        manifest["dependencies"][package] = "file:" + (root / folder).as_posix()
    return manifest


def expected_files(
    root: Path, config: ProjectConfig, project: Path, editor_version: str | None, editor_path: Path | None, result: GenerateResult
) -> dict[str, bytes]:
    """The content of each file the project gets from its config, by its path in the project."""
    files: dict[str, bytes] = {}
    for source in sorted(path for path in config.folder.rglob("*") if path.is_file()):
        relative = source.relative_to(config.folder).as_posix()
        if relative == MANIFEST:
            continue
        files[relative] = source.read_bytes()

    manifest = generated_manifest(root, config)
    if editor_version is not None:
        package_manager_dir = editor_package_manager_dir(editor_path) if editor_path is not None else None
        if package_manager_dir is None:
            raise GenerateError(f"cannot find the packages that Unity {editor_version} bundles next to {editor_path}")
        try:
            manifest["dependencies"], result.retargeted, result.warnings = retarget_dependencies(
                manifest["dependencies"], package_manager_dir, editor_version
            )
        except ValueError as error:
            raise GenerateError(str(error)) from error
        # Unity resolves the packages again for that Editor, and adds its revision line to
        # ProjectVersion.txt as it opens the project, so a file that already names the version stays.
        files.pop(LOCK_FILE, None)
        named = f"m_EditorVersion: {editor_version}"
        current = project / PROJECT_VERSION
        lines = current.read_text(encoding="utf-8-sig").splitlines() if current.is_file() else []
        files[PROJECT_VERSION] = current.read_bytes() if named in lines else f"{named}\n".encode("utf-8")
    files[MANIFEST] = (json.dumps(manifest, indent=2) + "\n").encode("utf-8")
    return files


def generate_project(
    root: Path,
    config: ProjectConfig,
    project: Path,
    editor_version: str | None = None,
    editor_path: Path | None = None,
    probe: Callable[[Path], bool | None] = file_is_held_open,
) -> GenerateResult:
    """Create the config's project in its folder, or refresh one an earlier run made there.

    With editor_version, the project names that Editor and takes the Unity package versions it
    bundles, which editor_path, the Editor's executable, gives. Raises GenerateError when the folder
    is not the tool's, an Editor has the project open, or a file or link cannot be written or removed.
    """
    result = GenerateResult()
    try:
        result.created = claim_folder(project, PROJECT_MARKER, PROJECT_MARKER_TEXT)
    except FolderNotClaimedError as error:
        raise GenerateError(f"{error}, so the tools will not generate a project in it; pass another --work-dir or empty it") from error
    except OSError as error:
        raise GenerateError(f"{project} cannot be prepared: {error}") from error

    owner = checkout_identity(root)
    try:
        previous = read_record(project)
    except OSError as error:
        raise GenerateError(f"{project / PROJECT_MARKER} cannot be read: {error}") from error
    # Branches whose first 32 characters agree share a unit's folder; a marker without the line comes
    # from before the line existed, from this branch, and takes this checkout as its owner.
    if previous.checkout is not None and os.path.normcase(previous.checkout) != os.path.normcase(owner):
        raise GenerateError(
            f"{project} was generated for the checkout at {previous.checkout}, not for {owner}, since their branches "
            "share the unit's folder name; pass --work-dir <folder> to give this checkout's projects a folder of their own"
        )

    state = project_lock_state(project, probe)
    if state == "open":
        raise GenerateError(f"{project} is open in Unity; close it, since a refresh would change its files under the Editor")
    if state == "unknown":
        raise GenerateError(f"{project}/Temp/UnityLockfile exists and could not be checked; delete it if no Editor has the project open")

    try:
        files = expected_files(root, config, project, editor_version, editor_path, result)

        # A link that an earlier run made, into a package's Samples~ before Base dropped its last
        # sample (#556), goes first, without following it.
        for relative in sorted(previous.links):
            link = _inside_project(project, relative)
            if is_directory_link(link):
                remove_directory_link(link)

        for relative, data in files.items():
            if write_if_changed(_inside_project(project, relative), data):
                result.written.append(relative)

        for relative in sorted(previous.files - set(files)):
            path = _inside_project(project, relative)
            if path.is_file() or path.is_symlink():
                path.unlink()
                result.removed.append(relative)
            # The folders the file leaves empty go too, up to the project.
            parent = path.parent
            while parent != project and parent.is_dir() and not is_directory_link(parent) and not any(parent.iterdir()):
                parent.rmdir()
                parent = parent.parent

        # Git keeps no empty folder, so a config without assets, such as minimal, holds no Assets/,
        # which a Unity project needs.
        (project / "Assets").mkdir(exist_ok=True)
        write_record(project, ProjectRecord(files=set(files), checkout=owner))
    except OSError as error:
        raise GenerateError(f"refreshing {project} failed: {error}") from error
    return result


def remove_generated_project(project: Path) -> None:
    """Delete a generated project folder, Library/ included, after removing its links without following them."""
    if not project.exists():
        return
    if not (project / PROJECT_MARKER).is_file():
        raise GenerateError(f"{project} has no {PROJECT_MARKER} marker, so the tools will not delete it")
    for relative in read_record(project).links:
        link = _inside_project(project, relative)
        if is_directory_link(link):
            remove_directory_link(link)

    def retry_writable(function: Callable, failed: str, *_: object) -> None:
        # Unity's Library can hold read-only files.
        os.chmod(failed, stat.S_IWRITE)
        function(failed)

    if sys.version_info >= (3, 12):
        shutil.rmtree(project, onexc=retry_writable)
    else:
        shutil.rmtree(project, onerror=retry_writable)


def describe_lines(root: Path, work_dir: Path | None, editor_version: str | None) -> list[str]:
    """What compile-unity.sh reads: one tab-separated line per package and config, and the work folder.

    package <folder> <name>
    config <name> <Editor version> <1 when the manifest lists testables, else 0> <project folder or empty> <package folders>
    work-dir <folder>, or work-dir-error <why there is none>
    """
    configs = load_configs(root)
    lines = [f"package\t{folder}\t{name}" for name, folder in sorted(checkout_packages(root).items(), key=lambda item: str(item[1]))]
    try:
        work = resolve_work_dir(root, work_dir)
    except WorkDirError as error:
        work, work_line = None, f"work-dir-error\t{error}"
    else:
        work_line = f"work-dir\t{work.as_posix()}"
    for config in configs.values():
        project = project_folder(work, config.name, editor_version).as_posix() if work is not None else ""
        folders = " ".join(sorted(str(folder) for folder in config.packages.values()))
        lines.append(f"config\t{config.name}\t{config.editor_version}\t{int(config.has_testables)}\t{project}\t{folders}")
    lines.append(work_line)
    return lines


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="unity_projects.py", description=__doc__.splitlines()[0])
    commands = parser.add_subparsers(dest="command", required=True)
    describe = commands.add_parser("describe", help="Print the configs, the packages they name, and their project folders.")
    describe.add_argument("--root", type=Path, required=True)
    describe.add_argument("--work-dir", type=Path)
    describe.add_argument("--editor-version")
    generate = commands.add_parser("generate", help="Create or refresh one config's project.")
    generate.add_argument("--root", type=Path, required=True)
    generate.add_argument("--config", required=True)
    generate.add_argument("--project", type=Path, required=True)
    generate.add_argument("--editor-version")
    generate.add_argument("--editor", type=Path)
    arguments = parser.parse_args(argv)

    try:
        if arguments.command == "describe":
            for line in describe_lines(arguments.root, arguments.work_dir, arguments.editor_version):
                print(line)
            return 0

        config = load_configs(arguments.root).get(arguments.config)
        if config is None:
            raise ConfigError(f"no config named {arguments.config} in {CONFIGS_FOLDER.as_posix()}")
        project = Path(os.path.abspath(arguments.project))
        result = generate_project(arguments.root, config, project, arguments.editor_version, arguments.editor)
    except (ConfigError, GenerateError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1

    action = "Generated" if result.created else "Refreshed"
    changes = f"{len(result.written)} file(s) written, {len(result.removed)} removed" if result.written or result.removed else "already up to date"
    print(f"{action} {project.as_posix()} from {(CONFIGS_FOLDER / config.name).as_posix()}: {changes}")
    for warning in result.warnings:
        print(f"warning: {warning}", file=sys.stderr)
    if arguments.editor_version is not None:
        print(
            f"Retargeted the project to Unity {arguments.editor_version}: ProjectVersion.txt names it, "
            "packages-lock.json is left out"
            + (", and these Unity packages follow it:" if result.retargeted else ", and every Unity package already has the version it bundles.")
        )
        for change in result.retargeted:
            print(f"  - {change}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
