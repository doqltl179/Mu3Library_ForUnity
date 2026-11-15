"""Keeps the repository tools in the development environment's .venv in step with tools/cli.

tools/dev/activate.sh and tools/dev/activate.ps1 run this with the .venv's own Python every time
they enter the environment, so both shells check and repair alike. It compares what that Python
has installed with tools/cli/pyproject.toml:

- the distribution the file names, installed in editable mode from this checkout's tools/cli,
  with the file's version, dependencies, and commands, and a launcher for each command;
- no distribution the tools were once installed as (FORMER_DISTRIBUTIONS).

When something differs, it removes the former distributions and installs tools/cli again, with
uv when it is on PATH and the .venv's pip otherwise, then checks once more.

Only the standard library, since it runs before the tools are installed. Exit status: 0 when the
install matches, already or after installing again; 1 when it still differs or could not be
installed again; 2 when tools/cli/pyproject.toml cannot be read.
"""

from __future__ import annotations

import importlib
import importlib.metadata
import importlib.util
import json
import os
import re
import shutil
import subprocess
import sys
import sysconfig
from collections.abc import Callable, Iterable, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from urllib.parse import urlparse
from urllib.request import url2pathname

# Names the tools were installed under before; an old .venv keeps their launchers, which fail.
FORMER_DISTRIBUTIONS = ("mu3-cli",)

PREFIX = "[mu3]"


@dataclass(frozen=True)
class Project:
    name: str
    version: str
    dependencies: tuple[str, ...]
    scripts: dict[str, str]


@dataclass
class Check:
    problems: list[str] = field(default_factory=list)
    former: list[str] = field(default_factory=list)
    reinstall: bool = False


def normalize_name(name: str) -> str:
    """The comparable form of a distribution name (PEP 503)."""
    return re.sub(r"[-_.]+", "-", name).lower()


# The canonical form of a PEP 440 public version, the form setuptools writes into the metadata.
# packaging writes 01 as 1 and leaves out an epoch of 0.
_NUMBER = r"(?:0|[1-9][0-9]*)"
_CANONICAL_VERSION = re.compile(
    rf"(?:[1-9][0-9]*!)?{_NUMBER}(?:\.{_NUMBER})*(?:(?:a|b|rc){_NUMBER})?(?:\.post{_NUMBER})?(?:\.dev{_NUMBER})?"
)

_REQUIREMENT = re.compile(r"\s*([A-Za-z0-9][A-Za-z0-9._-]*)\s*(\[[^\]]*\])?\s*(.*)", re.DOTALL)


def normalize_requirement(text: str) -> str:
    """A requirement in a form that pyproject.toml and the installed Requires-Dist share.

    setuptools writes "typer>=0.12,<1.0" as "typer<1.0,>=0.12", so specifiers are sorted, and
    spaces and quotes of a marker are evened out.
    """
    requirement, _, marker = text.partition(";")
    match = _REQUIREMENT.fullmatch(requirement)
    if not match:
        return re.sub(r"\s+", "", text)

    name, extras, specifiers = match.groups()
    result = normalize_name(name)
    if extras:
        result += "[" + ",".join(sorted(normalize_name(e.strip()) for e in extras[1:-1].split(",") if e.strip())) + "]"
    specifiers = specifiers.strip()
    if specifiers.startswith("(") and specifiers.endswith(")"):
        specifiers = specifiers[1:-1]
    result += ",".join(sorted(s.replace(" ", "") for s in specifiers.split(",") if s.strip()))
    if marker.strip():
        result += ";" + re.sub(r"\s+", "", marker).replace("'", '"')
    return result


_EXTRA_MARKER = re.compile(r";.*\bextra\s*==", re.DOTALL)

_KEY_LINE = re.compile(r"\s*([A-Za-z0-9_-]+|\"[^\"]+\")\s*=\s*(.*)")
# A basic string, whose escapes _unescape replaces, or a literal string, which has none.
_STRING = re.compile(r'"((?:[^"\\]|\\.)*)"|\'([^\']*)\'')


def _unescape(text: str) -> str:
    return re.sub(r"\\(.)", r"\1", text)


def _string_values(text: str) -> list[str]:
    return [_unescape(basic) if basic or not literal else literal for basic, literal in _STRING.findall(text)]


def _strip_comment(line: str) -> str:
    """The line without its comment: a # outside a basic or a literal string starts one."""
    quote = None
    i = 0
    while i < len(line):
        c = line[i]
        if quote:
            if c == "\\" and quote == '"':
                i += 1
            elif c == quote:
                quote = None
        elif c in "\"'":
            quote = c
        elif c == "#":
            return line[:i]
        i += 1
    return line


def read_minimal_toml(text: str) -> dict:
    """Reads the part of pyproject.toml this script needs, for Python 3.10, which has no tomllib.

    It takes the strings and the arrays of strings of [project], one key per line, an array over
    several lines included, and the strings of [project.scripts]; a string is a basic or a literal
    string on one line, and a # outside one starts a comment. A key is bare or in double quotes.
    Other values, such as an inline table or a multi-line string, are left out.
    """
    project: dict = {}
    scripts: dict[str, str] = {}
    table = None
    lines = iter(text.splitlines())
    for line in lines:
        line = _strip_comment(line)
        stripped = line.strip()
        if not stripped:
            continue
        if stripped.startswith("["):
            table = stripped.strip("[] \t")
            continue
        if table not in ("project", "project.scripts"):
            continue

        match = _KEY_LINE.fullmatch(line)
        if not match:
            continue
        key, value = match.group(1).strip('"'), match.group(2).strip()
        if value.startswith("["):
            while not _closes_array(value):
                try:
                    value += "\n" + _strip_comment(next(lines))
                except StopIteration:
                    raise ValueError(f"the array of {key} does not end") from None
            parsed = _string_values(value)
        else:
            if not _STRING.match(value):
                continue
            parsed = _string_values(value)[0]

        (project if table == "project" else scripts)[key] = parsed

    if scripts:
        project["scripts"] = scripts
    return {"project": project}


def _closes_array(value: str) -> bool:
    return _STRING.sub("", value).rstrip().endswith("]")


def read_project(path: Path) -> Project:
    text = path.read_text(encoding="utf-8")
    try:
        import tomllib
    except ModuleNotFoundError:
        data = read_minimal_toml(text)
    else:
        data = tomllib.loads(text)

    project = data["project"]
    return Project(
        name=str(project["name"]),
        version=str(project["version"]),
        dependencies=tuple(str(d) for d in project.get("dependencies", ())),
        scripts={str(k): str(v) for k, v in project.get("scripts", {}).items()},
    )


def same_path(left: Path, right: Path) -> bool:
    return os.path.normcase(os.path.realpath(left)) == os.path.normcase(os.path.realpath(right))


def editable_source(distribution: importlib.metadata.Distribution) -> Path | None:
    """The folder a distribution is installed from in editable mode (PEP 610), or None."""
    try:
        text = distribution.read_text("direct_url.json")
        data = json.loads(text) if text else {}
    except (OSError, ValueError):
        return None
    if not isinstance(data, dict) or not (data.get("dir_info") or {}).get("editable"):
        return None

    url = urlparse(str(data.get("url", "")))
    if url.scheme != "file":
        return None
    path = f"//{url.netloc}{url.path}" if url.netloc else url.path
    return Path(url2pathname(path))


def has_launcher(name: str, folder: Path) -> bool:
    """Whether the folder holds a launcher for the command, such as mu3-repo.exe on Windows."""
    return shutil.which(name, path=str(folder)) is not None


def check(
    project: Project,
    tools_dir: Path,
    scripts_dir: Path,
    distributions: Iterable[importlib.metadata.Distribution],
    launcher: Callable[[str, Path], bool] = has_launcher,
) -> Check:
    by_name: dict[str, importlib.metadata.Distribution] = {}
    for distribution in distributions:
        name = distribution.metadata["Name"]
        if name:
            by_name.setdefault(normalize_name(name), distribution)

    result = Check()
    for former in FORMER_DISTRIBUTIONS:
        if normalize_name(former) in by_name and normalize_name(former) != normalize_name(project.name):
            result.former.append(former)
            result.problems.append(f"the former distribution {former} is still installed")

    installed = by_name.get(normalize_name(project.name))
    if installed is None:
        result.problems.append(f"{project.name} is not installed")
        result.reinstall = True
        return result

    def differs(problem: str) -> None:
        result.problems.append(problem)
        result.reinstall = True

    source = editable_source(installed)
    if source is None:
        differs(f"{project.name} is not installed in editable mode from {tools_dir}")
    elif not same_path(source, tools_dir):
        differs(f"{project.name} is installed from {source}, not from {tools_dir}")

    # The metadata holds the version in its canonical form, such as 0.2.0b0 for 0.2.0-beta, which
    # this script does not compute, so a version written otherwise is not compared.
    version = installed.metadata["Version"]
    if _CANONICAL_VERSION.fullmatch(project.version) and version != project.version:
        differs(f"{project.name} {version} is installed, and tools/cli/pyproject.toml has {project.version}")

    # Requires-Dist also lists the optional dependencies, each with a marker on its extra.
    wanted = sorted(normalize_requirement(d) for d in project.dependencies)
    have = sorted(normalize_requirement(d) for d in (installed.requires or ()) if not _EXTRA_MARKER.search(d))
    if wanted != have:
        differs(f"its dependencies are {', '.join(have) or 'none'}, and tools/cli/pyproject.toml lists {', '.join(wanted) or 'none'}")

    commands = {e.name: e.value for e in installed.entry_points if e.group == "console_scripts"}
    missing = sorted(set(project.scripts) - set(commands))
    extra = sorted(set(commands) - set(project.scripts))
    moved = sorted(n for n in set(project.scripts) & set(commands) if project.scripts[n] != commands[n])
    if missing:
        differs(f"it lacks the command(s) {', '.join(missing)}")
    if extra:
        differs(f"it has the command(s) {', '.join(extra)}, which tools/cli/pyproject.toml no longer lists")
    if moved:
        differs(f"the command(s) {', '.join(moved)} run another function than tools/cli/pyproject.toml gives")
    without = sorted(n for n in set(project.scripts) & set(commands) if not launcher(n, scripts_dir))
    if without:
        differs(f"the command(s) {', '.join(without)} have no launcher in {scripts_dir}")

    return result


def installed_distributions() -> Iterable[importlib.metadata.Distribution]:
    """The distributions installed in this Python's site-packages.

    Only there: an editable install puts the source folder on sys.path, and setuptools may leave a
    *.egg-info in it, such as the src/mu3_cli.egg-info of the former distribution, which would
    still read as installed after it was removed.
    """
    folders = dict.fromkeys(sysconfig.get_path(name) for name in ("purelib", "platlib"))
    return importlib.metadata.distributions(path=[f for f in folders if f])


Runner = Callable[[Sequence[str]], int]


def _run(command: Sequence[str]) -> int:
    return subprocess.run(list(command), check=False).returncode


def installer_commands(
    python: str,
    tools_dir: Path,
    former: Sequence[str],
    reinstall: bool,
    uv: str | None,
    has_pip: bool,
) -> list[list[str]] | None:
    """The commands that repair the install, or None when neither uv nor pip can run them."""
    if uv:
        uninstall = [uv, "pip", "uninstall", "--python", python, "--quiet", *former]
        install = [uv, "pip", "install", "--python", python, "--quiet", "-e", str(tools_dir)]
    elif has_pip:
        uninstall = [python, "-I", "-m", "pip", "uninstall", "--yes", "--quiet", "--disable-pip-version-check", *former]
        install = [python, "-I", "-m", "pip", "install", "--quiet", "--disable-pip-version-check", "-e", str(tools_dir)]
    else:
        return None

    commands = []
    if former:
        commands.append(uninstall)
    if reinstall:
        commands.append(install)
    return commands


def main(
    argv: Sequence[str] | None = None,
    *,
    distributions: Callable[[], Iterable[importlib.metadata.Distribution]] = installed_distributions,
    runner: Runner = _run,
    launcher: Callable[[str, Path], bool] = has_launcher,
    find_uv: Callable[[], str | None] = lambda: shutil.which("uv"),
    find_pip: Callable[[], bool] = lambda: importlib.util.find_spec("pip") is not None,
) -> int:
    del argv  # no options
    tools_dir = Path(__file__).resolve().parent.parent / "cli"
    pyproject = tools_dir / "pyproject.toml"
    try:
        project = read_project(pyproject)
    except (OSError, KeyError, TypeError, ValueError) as error:
        print(f"{PREFIX} Could not read {pyproject}, so the repository tools were not checked: {error}", file=sys.stderr)
        return 2

    scripts_dir = Path(sysconfig.get_path("scripts"))
    result = check(project, tools_dir, scripts_dir, distributions(), launcher)
    if not result.problems:
        return 0

    print(f"{PREFIX} The repository tools in {sys.prefix} differ from tools/cli/pyproject.toml:")
    for problem in result.problems:
        print(f"{PREFIX}   {problem}")

    uv = find_uv()
    commands = installer_commands(sys.executable, tools_dir, result.former, result.reinstall, uv, find_pip())
    if commands is None:
        print(
            f"{PREFIX} Neither uv nor this .venv's pip is there to install them again; delete {sys.prefix} and enter again.",
            file=sys.stderr,
        )
        return 1

    print(f"{PREFIX} Installing them again with {'uv' if uv else 'pip'}...")
    failed = [c for c in commands if runner(c) != 0]
    importlib.invalidate_caches()
    remaining = check(project, tools_dir, scripts_dir, distributions(), launcher)
    if failed or remaining.problems:
        for problem in remaining.problems:
            print(f"{PREFIX}   still: {problem}", file=sys.stderr)
        print(
            f"{PREFIX} Installing the repository tools again failed; delete {sys.prefix} and enter again.",
            file=sys.stderr,
        )
        return 1

    print(f"{PREFIX} The repository tools match tools/cli/pyproject.toml again.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
