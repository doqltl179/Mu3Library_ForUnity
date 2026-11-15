from __future__ import annotations

import os
from pathlib import Path

import typer


def _is_checkout_root(path: Path) -> bool:
    return (path / "AGENTS.md").exists() and (path / ".github").exists()


def _enclosing_root(path: Path) -> Path | None:
    for candidate in (path, *path.parents):
        if _is_checkout_root(candidate):
            return candidate
    return None


def repo_root() -> Path:
    """Return the repository root, spelled the way the command reached it.

    On Windows `Path.resolve()` turns a `subst` drive back into the long path it maps, and the
    Unity entry points open the project under whatever root they are given, so the root is never
    resolved. An installed tool, such as `mu3-unity`, sits in the checkout it was installed from, so a
    working directory inside that same checkout supplies its spelling, such as the subst drive it was run from.
    """
    module_root = _enclosing_root(Path(os.path.abspath(__file__)).parent)
    if module_root is None:
        raise RuntimeError("Could not locate the Mu3Library repository root.")

    try:
        working_root = _enclosing_root(Path.cwd())
    except OSError:
        # A working directory that was deleted leaves only the module's own spelling.
        return module_root
    # resolve() follows a subst drive or a link to the folder it names, so both spellings meet there.
    if working_root is not None and working_root.resolve() == module_root.resolve():
        return working_root
    return module_root


def echo_repository_root(root: Path) -> None:
    """Name the checkout a command acts on, which for an installed tool is the one it was installed
    from, wherever the command runs."""
    typer.echo(f"Repository root: {root}")
