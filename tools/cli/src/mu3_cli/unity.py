from __future__ import annotations

import json
import os
import platform
import re
import shutil
import subprocess
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Callable, Mapping

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
    # Where the project links to the package's Samples~ folder, relative to the project.
    samples_link: Path | None = None

    @property
    def samples_path(self) -> Path:
        return self.package_path / "Samples~"


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


class BashNotFoundError(ValueError):
    """No bash that can run the repository's shell entry points was found."""

    def __init__(self, detail: str, suggestion: str) -> None:
        super().__init__(f"{detail} {suggestion}")
        self.detail = detail
        self.suggestion = suggestion


# The Windows system directories hold bash.exe only as the launcher of WSL, which runs the
# script inside a Linux distribution that cannot reach the Windows Unity Editor.
_WINDOWS_SYSTEM_DIRECTORIES = {"system32", "syswow64", "sysnative"}
# Folders inside a Git for Windows installation that hold a git.exe.
_GIT_FOR_WINDOWS_TOOL_DIRECTORIES = {"mingw64", "mingw32", "clangarm64", "usr"}


def _is_wsl_launcher(path: Path, environ: Mapping[str, str]) -> bool:
    if "windowsapps" in (part.lower() for part in path.parts):
        return True
    system_root = environ.get("SystemRoot") or environ.get("SYSTEMROOT") or environ.get("windir") or r"C:\Windows"
    return path.parent.name.lower() in _WINDOWS_SYSTEM_DIRECTORIES and os.path.normcase(
        str(path.parent.parent)
    ) == os.path.normcase(str(Path(system_root)))


def git_for_windows_bash(git_path: Path) -> Path:
    """Return where Git for Windows keeps bash.exe, given the git.exe it put on PATH.

    The installer puts <Git>\\cmd (and sometimes <Git>\\mingw64\\bin) on PATH but not <Git>\\bin.
    <Git>\\bin\\bash.exe also adds Git's Unix tools to PATH before it starts, which the scripts need.
    """
    tool_directory = git_path.parent
    if tool_directory.name.lower() == "bin" and tool_directory.parent.name.lower() in _GIT_FOR_WINDOWS_TOOL_DIRECTORIES:
        install_root = tool_directory.parent.parent
    else:
        install_root = tool_directory.parent
    return install_root / "bin" / "bash.exe"


def _default_git_for_windows_roots(environ: Mapping[str, str]) -> list[Path]:
    roots = [
        Path(environ[name]) / "Git"
        for name in ("ProgramW6432", "ProgramFiles", "ProgramFiles(x86)")
        if environ.get(name)
    ]
    if environ.get("LOCALAPPDATA"):
        roots.append(Path(environ["LOCALAPPDATA"]) / "Programs" / "Git")
    return roots


def find_windows_bash(
    which: Callable[[str], str | None] = shutil.which,
    environ: Mapping[str, str] = os.environ,
) -> Path:
    """Find the bash of Git for Windows: on PATH, next to git, or in a default install location."""
    skipped_launcher: Path | None = None
    on_path = which("bash")
    if on_path:
        candidate = Path(on_path)
        if not _is_wsl_launcher(candidate, environ):
            return candidate
        skipped_launcher = candidate

    git_path = which("git")
    if git_path:
        candidate = git_for_windows_bash(Path(git_path))
        if candidate.is_file():
            return candidate

    for install_root in _default_git_for_windows_roots(environ):
        candidate = install_root / "bin" / "bash.exe"
        if candidate.is_file():
            return candidate

    detail = ["Git for Windows bash was not found; the Unity entry points need it on Windows."]
    if skipped_launcher is not None:
        detail.append(f"The bash on PATH ({skipped_launcher}) starts WSL, not Git Bash, so it was skipped.")
    if git_path:
        detail.append(f"No bash.exe was found next to {git_path} or in the default install locations.")
    else:
        detail.append("git is not on PATH either, and no default install location holds bash.exe.")
    raise BashNotFoundError(
        " ".join(detail),
        "Install Git for Windows (https://git-scm.com/download/win), or add the folder that holds its "
        "bash.exe, usually C:\\Program Files\\Git\\bin, to PATH.",
    )


def find_bash(
    which: Callable[[str], str | None] = shutil.which,
    environ: Mapping[str, str] = os.environ,
    system: str | None = None,
) -> Path:
    """Find the bash that runs compile-unity.sh and test-unity.sh on this host."""
    if (system or platform.system()) == "Windows":
        return find_windows_bash(which, environ)

    on_path = which("bash")
    if not on_path:
        raise BashNotFoundError("bash was not found on PATH.", "Install bash and put it on PATH.")
    return Path(on_path)


def bash_script_command(script_path: Path, arguments: list[str]) -> list[str]:
    # Windows cannot execute a shell script directly, so bash has to carry it.
    if platform.system() == "Windows":
        return [str(find_windows_bash()), str(script_path), *arguments]

    return [str(script_path), *arguments]


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
        return _diagnostic(
            "WARN",
            spec.key,
            "Project lock",
            f"{lock_path} exists and whether an Editor holds it could not be checked; "
            "compile-unity.sh will use an isolated mirror.",
            "Install lsof so the lock holder can be checked, or close the Editor and delete the file.",
        )
    return _diagnostic("PASS", spec.key, "Project lock", "Project is not locked.")


_WINDOWS_REPARSE_TAG_MOUNT_POINT = 0xA0000003
LINK_SAMPLES_COMMAND = "PYTHONPATH=tools/cli/src python -m mu3_cli unity link-samples"


def is_directory_link(path: Path) -> bool:
    """Whether path is a symbolic link or, on Windows, a directory junction, even a dangling one."""
    if path.is_symlink():
        return True
    try:
        status = os.lstat(path)
    except OSError:
        return False
    return getattr(status, "st_reparse_tag", 0) == _WINDOWS_REPARSE_TAG_MOUNT_POINT


def _link_destination(path: Path) -> str:
    try:
        destination = os.readlink(path)
    except OSError:
        return "an unreadable destination"
    return destination.removeprefix("\\\\?\\")


@dataclass(frozen=True)
class SampleLinkState:
    """linked, missing, misdirected (a link elsewhere or to nothing), blocked (a real folder or file), or no-samples."""

    state: str
    detail: str


def sample_link_state(spec: UnityTargetSpec, root: Path) -> SampleLinkState | None:
    """Unity skips folders whose name ends in ~, so a project compiles a package's samples only
    through a link from its Assets folder to Samples~. Returns None for a target without one."""
    if spec.samples_link is None:
        return None

    link_label = (spec.project_path / spec.samples_link).as_posix()
    samples_label = spec.samples_path.as_posix()
    link_path = root / spec.project_path / spec.samples_link
    samples_path = root / spec.samples_path

    if not samples_path.is_dir():
        return SampleLinkState("no-samples", f"{samples_label} does not exist.")
    if is_directory_link(link_path):
        if link_path.exists() and os.path.samefile(link_path, samples_path):
            return SampleLinkState("linked", f"{link_label} links to {samples_label}.")
        return SampleLinkState(
            "misdirected", f"{link_label} links to {_link_destination(link_path)} instead of {samples_label}."
        )
    if link_path.exists():
        kind = "folder" if link_path.is_dir() else "file"
        return SampleLinkState("blocked", f"{link_label} is a real {kind}, not a link to {samples_label}.")
    return SampleLinkState(
        "missing", f"{link_label} is missing, so the project does not compile the samples in {samples_label}."
    )


def create_directory_link(link_path: Path, target: Path) -> None:
    """Link link_path to the target folder.

    Windows gets a junction, which needs neither administrator rights nor Developer Mode and
    takes an absolute target. Elsewhere a relative symbolic link keeps working after the
    checkout moves.
    """
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

    os.symlink(os.path.relpath(target, link_path.parent), link_path, target_is_directory=True)


def remove_directory_link(link_path: Path) -> None:
    """Remove the link itself; neither call follows it into the folder it points at."""
    if platform.system() == "Windows":
        os.rmdir(link_path)
    else:
        os.unlink(link_path)


@dataclass(frozen=True)
class SampleLinkResult:
    target: str
    ok: bool
    message: str


def link_samples(specs: dict[str, UnityTargetSpec], root: Path) -> list[SampleLinkResult]:
    """Create each target's samples link, leave a correct one alone, and replace a link that
    points elsewhere. A real folder or file in the link's place is never touched."""
    results: list[SampleLinkResult] = []
    for spec in specs.values():
        current = sample_link_state(spec, root)
        if current is None:
            continue

        link_path = root / spec.project_path / spec.samples_link
        # A junction stores its target as given, so it must not keep a subst drive or a relative path.
        samples_path = (root / spec.samples_path).resolve()
        link_label = (spec.project_path / spec.samples_link).as_posix()
        samples_label = spec.samples_path.as_posix()

        if current.state == "linked":
            results.append(SampleLinkResult(spec.key, True, f"Already linked: {link_label} -> {samples_label}."))
            continue
        if current.state in ("blocked", "no-samples"):
            suggestion = (
                " Move it out of the way, then run this command again; it was left untouched."
                if current.state == "blocked"
                else ""
            )
            results.append(SampleLinkResult(spec.key, False, f"error: {current.detail}{suggestion}"))
            continue
        if not link_path.parent.is_dir():
            results.append(
                SampleLinkResult(spec.key, False, f"error: {link_path.parent.relative_to(root).as_posix()} is missing.")
            )
            continue

        try:
            if current.state == "misdirected":
                remove_directory_link(link_path)
            create_directory_link(link_path, samples_path)
        except OSError as error:
            results.append(SampleLinkResult(spec.key, False, f"error: could not link {link_label}: {error}"))
            continue

        verb = "Relinked" if current.state == "misdirected" else "Linked"
        results.append(SampleLinkResult(spec.key, True, f"{verb} {link_label} -> {samples_label}."))
    return results


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
    without_samples: bool = False,
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
    if without_samples:
        arguments.append("--without-samples")
    return arguments


def run_compile_script(arguments: list[str], root: Path) -> int:
    script_path = compile_script_path(root)
    if not script_path.is_file():
        raise ValueError(f"Unity compile entrypoint is missing: {script_path}")
    if platform.system() != "Windows" and not os.access(script_path, os.X_OK):
        raise ValueError(f"Unity compile entrypoint is not executable: {script_path}")

    command = bash_script_command(script_path, arguments)
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
    try:
        arguments = compile_command_arguments("changed", base, True, False, False, False)
        return_code = run_compile_script(arguments, root)
    except ValueError as error:
        raise typer.BadParameter(str(error)) from error

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
    without_samples: bool = typer.Option(
        False, "--without-samples", help="Compile even when a samples link is missing, leaving the samples out."
    ),
) -> None:
    """Run the repository's change-aware Unity batchmode compiler."""
    root = repo_root()
    try:
        arguments = compile_command_arguments(
            target, base, dry_run, isolated, in_place, keep_staging, without_samples
        )
        return_code = run_compile_script(arguments, root)
    except ValueError as error:
        raise typer.BadParameter(str(error)) from error

    if return_code:
        raise typer.Exit(code=return_code)


@unity_app.command("link-samples")
def unity_link_samples(
    target: str | None = typer.Option(None, "--target", "-t", help="Optional configured target to link."),
) -> None:
    """Link each development project's Assets folder to its package's Samples~ folder.

    A correct link is left alone, a link that points elsewhere is replaced, and a real folder
    in the link's place stops the command without being touched.
    """
    root = repo_root()
    try:
        specs = load_target_specs(root)
    except ValueError as error:
        raise typer.BadParameter(str(error)) from error

    if target is not None:
        if target not in specs:
            raise typer.BadParameter(f"Unknown Unity target '{target}'. Choose one of: {', '.join(specs)}.")
        specs = {target: specs[target]}

    results = link_samples(specs, root)
    for result in results:
        typer.echo(f"[{result.target}] {result.message}", err=not result.ok)
    if not results:
        typer.echo("No configured target has a samples link.")
    if not all(result.ok for result in results):
        raise typer.Exit(code=1)
