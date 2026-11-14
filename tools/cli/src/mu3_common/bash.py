from __future__ import annotations

import os
import platform
import shutil
from pathlib import Path
from typing import Callable, Mapping


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
