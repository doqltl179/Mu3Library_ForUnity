from __future__ import annotations

import os
from pathlib import Path

import typer


# The folders, relative to a checkout's root, that its .gitignore keeps worktrees in.
WORKTREE_FOLDERS = (Path(".claude", "worktrees"), Path(".worktrees"))


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


def _checkout_holding(root: Path, working_directory: Path) -> tuple[bool, Path | None]:
    """Return whether root holds the working directory with no other checkout in between, and a folder.

    When root holds it, the folder is the one in root's worktree folders that holds it, or None when
    none does. Otherwise it is the nearest other checkout that holds it, or None when no checkout does.
    """
    # resolve() follows a subst drive or a link to the folder it names, so both spellings meet there.
    root_folder = root.resolve()
    # A worktree folder, or `.claude` above one, may itself be a link, which each candidate resolves through.
    worktree_folders = {(root_folder / folder).resolve() for folder in WORKTREE_FOLDERS}
    in_worktree_folder: Path | None = None
    below: Path | None = None
    for candidate in (working_directory, *working_directory.parents):
        resolved = candidate.resolve()
        if resolved == root_folder:
            return True, in_worktree_folder
        if _is_checkout_root(candidate):
            return False, candidate
        if below is not None and resolved in worktree_folders:
            in_worktree_folder = below
        below = candidate
    return False, None


def outside_checkout_refusal(root: Path, package: str, command: str) -> str | None:
    """Return why the tool `package`'s `command`, such as mu3_samples' `link`, must not change the
    checkout at root from the working directory, or None when the working directory is inside that
    checkout. The message names the tool by its command, such as `mu3-samples`, and by its module.

    An installed tool acts on the checkout it was installed from wherever it runs, so run from
    another checkout it would change files there instead. The working directory is inside root when
    root, or a subst drive or link that names it, holds it with no other checkout in between, so a
    worktree nested in root's folder, such as one under `.claude/worktrees/`, is outside it. A folder
    in root's worktree folders that is no checkout of its own is refused too, such as one that a failed
    `git worktree remove` left there without the `AGENTS.md` and `.github` that mark a checkout: run
    there, the command is meant for that folder's own links, not root's (#436).
    """
    program = f"{package.replace('_', '-')} {command}"
    try:
        working_directory = Path.cwd()
    except OSError as error:
        return (
            f"{program} would change the checkout at {root}, but the working directory cannot be read "
            f"({error}), so nothing was changed. Run it again from inside that checkout."
        )
    try:
        inside, folder = _checkout_holding(root, working_directory)
    except OSError:
        # A folder on the way up that cannot be read leaves the answer unknown, which counts as outside.
        inside, folder = False, None
    if inside and folder is None:
        return None
    refusal = f"{program} would change the checkout at {root}, but the working directory {working_directory} "
    if inside:
        return refusal + (
            f"is in {folder}, which is in that checkout's worktree folder {folder.parent} but is no checkout of "
            "its own, such as a folder that a failed git worktree remove left behind, so nothing was changed. "
            'To remove that folder\'s links, see "Worktree Removal" in tools/README.md. To change that checkout, '
            "run it from inside it, outside its worktree folders."
        )
    if folder is None:
        return refusal + "is outside it and in no checkout, so nothing was changed. Run it from inside that checkout."
    return refusal + (
        f"is outside it, in the checkout at {folder}, so nothing was changed. Run it from inside {root}, "
        f"or, to change the checkout at {folder}, run PYTHONPATH=tools/cli/src python -m {package} {command} "
        "from that checkout's root."
    )
