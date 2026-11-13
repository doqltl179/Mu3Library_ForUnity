from __future__ import annotations

import os
import subprocess
import sys
import unicodedata
from pathlib import Path
from typing import Iterable


REQUIRED_DOC_PATHS = (
    "README.md",
    "CHANGELOG.md",
    "docs/readme/README.ko.md",
    "docs/readme/README.ja.md",
    "docs/changelog/CHANGELOG.ko.md",
    "docs/changelog/CHANGELOG.ja.md",
)
FORBIDDEN_ROOT_DOC_PATHS = (
    "README.ko.md",
    "README.ja.md",
    "CHANGELOG.ko.md",
    "CHANGELOG.ja.md",
)
REQUIRED_README_LINKS = (
    "docs/readme/README.ko.md",
    "docs/readme/README.ja.md",
    "CHANGELOG.md",
)
STALE_AGENT_REFERENCE_PARTS = (
    "agent-" + "catalog",
    "agent-spec-" + "contract",
    "contracts/" + "README",
    "control-plane-" + "routing",
    "unity-specialist-" + "routing",
    "base-package-" + "routing",
    "urp-package-" + "routing",
)
REPOSITORY_TEXT_SUFFIXES = {".md", ".py", ".yml"}
PACKAGE_CODE_PATHSPEC = "Mu3Library_*"
PACKAGE_CODE_SUFFIXES = {".cs"}
# Characters a diff or an editor shows as nothing, or as something else: controls other than tab and
# carriage return, format characters such as zero-width spaces, bidirectional controls, and a byte
# order mark past the start of a file, private-use characters, and the line and paragraph separators.
# Package code writes them as \u escapes; an editing tool once saved such escapes as the characters.
HIDDEN_CHARACTER_CATEGORIES = {"Cc", "Cf", "Co", "Zl", "Zp"}
ALLOWED_CONTROL_CHARACTERS = {"\t", "\r"}
# The replacement character, which a lossy conversion leaves where it could not carry a character,
# such as an unpaired surrogate that an editing tool saved in place of its \u escape. A test meant
# to hold that surrogate then checks nothing, so package code writes U+FFFD as an escape too.
REPLACEMENT_CHARACTER = chr(0xFFFD)


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
    resolved. An installed `mu3-cli` sits in the checkout it was installed from, so a working
    directory inside that same checkout supplies its spelling, such as the subst drive it was run from.
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


def _holds_without_another_checkout(root: Path, working_directory: Path) -> bool:
    # resolve() follows a subst drive or a link to the folder it names, so both spellings meet there.
    root_folder = root.resolve()
    for candidate in (working_directory, *working_directory.parents):
        if candidate.resolve() == root_folder:
            return True
        if _is_checkout_root(candidate):
            return False
    return False


def outside_checkout_refusal(root: Path, command: str) -> str | None:
    """Return why the mu3-cli `command`, such as `unity link-samples`, must not change the checkout at
    root from the working directory, or None when the working directory is inside that checkout.

    An installed `mu3-cli` acts on the checkout it was installed from wherever it runs, so run from
    another checkout it would change files there instead. The working directory is inside root when
    root, or a subst drive or link that names it, holds it with no other checkout in between, so a
    worktree nested in root's folder, such as one under `.claude/worktrees/`, is outside it.
    """
    try:
        working_directory = Path.cwd()
    except OSError as error:
        return (
            f"mu3-cli {command} would change the checkout at {root}, but the working directory cannot be read "
            f"({error}), so nothing was changed. Run it again from inside that checkout."
        )
    try:
        if _holds_without_another_checkout(root, working_directory):
            return None
    except OSError:
        # A folder on the way up that cannot be read leaves the answer unknown, which counts as outside.
        pass
    return (
        f"mu3-cli {command} would change the checkout at {root}, but the working directory {working_directory} "
        "is outside it, so nothing was changed. Run it from inside that checkout, or, for the checkout you are in, "
        f"run PYTHONPATH=tools/cli/src python -m mu3_cli {command} from that checkout's root."
    )


def is_generated_tool_artifact(relative_path: str) -> bool:
    """Return whether a tracked tools path belongs to a generated output directory."""
    parts = Path(relative_path).parts
    return any(part in {"bin", "obj"} or part.endswith(".egg-info") for part in parts)


def _listed_paths(root: Path, *arguments: str) -> list[str]:
    """Return the paths `git ls-files` lists with the arguments, each spelled as its file is named.

    Without -z, Git quotes a path that holds a character outside ASCII and escapes that character's
    bytes, as core.quotepath does by default, so the path ends in a quote and its suffix no longer
    matches. With -z it prints every path as it is and ends each with NUL.
    """
    result = subprocess.run(
        ["git", "ls-files", "-z", *arguments],
        cwd=root,
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    return [path for path in result.stdout.split("\0") if path]


def tracked_tool_paths(root: Path) -> list[str]:
    """List tracked files under tools without relying on shell-specific pipelines."""
    return _listed_paths(root, "--", "tools")


def repository_text_paths(root: Path) -> list[str]:
    """List tracked and untracked, non-ignored repository text candidates."""
    paths = _listed_paths(root, "--cached", "--others", "--exclude-standard", "--", ".github", "docs", "tools")
    return [path for path in paths if Path(path).suffix in REPOSITORY_TEXT_SUFFIXES]


def package_code_paths(root: Path) -> list[str]:
    """List tracked and untracked, non-ignored package code files."""
    paths = _listed_paths(root, "--cached", "--others", "--exclude-standard", "--", PACKAGE_CODE_PATHSPEC)
    return [path for path in paths if Path(path).suffix in PACKAGE_CODE_SUFFIXES]


def hidden_characters(line: str) -> list[str]:
    """Return the code points, as U+XXXX, of the characters in a line that a reader cannot see."""
    return [
        f"U+{ord(character):04X}"
        for character in line
        if character not in ALLOWED_CONTROL_CHARACTERS
        and unicodedata.category(character) in HIDDEN_CHARACTER_CATEGORIES
    ]


def _undecodable_bytes_hint(error: UnicodeDecodeError) -> str:
    """Say what the bytes UTF-8 could not decode may be, and how package code writes them instead."""
    data, start = error.object, error.start
    # An unpaired surrogate written out as bytes, ED A0 80 to ED BF BF, starts with ED and a byte
    # from A0 to BF, which UTF-8 never encodes.
    if data[start] == 0xED and start + 1 < len(data) and 0xA0 <= data[start + 1] <= 0xBF:
        return (
            "and its bytes may be an unpaired surrogate (ED A0 80 to ED BF BF), which package code writes "
            "as a \\u escape"
        )
    return "as text saved in another encoding, such as Korean saved as CP949, is not; save the file as UTF-8"


def package_code_issues(root: Path, relative_paths: Iterable[str]) -> list[str]:
    """Report each line of package code that holds an invisible or private-use character or the
    replacement character, and each file that is not UTF-8."""
    issues: list[str] = []
    for relative_path_text in relative_paths:
        relative_path = Path(relative_path_text)
        candidate = root / relative_path
        if not candidate.is_file():
            continue
        try:
            # utf-8-sig drops only a byte order mark at the start; one anywhere else is reported.
            text = candidate.read_bytes().decode("utf-8-sig")
        except OSError as error:
            issues.append(f"could not inspect package code file: {relative_path} ({error})")
            continue
        except UnicodeDecodeError as error:
            line_number = error.object[: error.start].count(b"\n") + 1
            issues.append(
                f"could not inspect package code file: {relative_path} ({error}); line {line_number} is not UTF-8, "
                + _undecodable_bytes_hint(error)
            )
            continue
        # str.splitlines() would also split at the separators this check reports.
        for line_number, line in enumerate(text.split("\n"), start=1):
            code_points = hidden_characters(line)
            if code_points:
                issues.append(
                    f"invisible or private-use character {', '.join(code_points)} in package code, "
                    f"write it as a \\u escape: {relative_path}:{line_number}"
                )
            if REPLACEMENT_CHARACTER in line:
                issues.append(
                    "replacement character U+FFFD in package code, which a lossy conversion leaves where it "
                    f"could not carry a character, write the character meant as a \\u escape: {relative_path}:{line_number}"
                )
    return issues


def repository_hygiene_issues(
    root: Path | None = None,
    tracked_paths: Iterable[str] | None = None,
) -> list[str]:
    """Collect repository layout, link, routing-reference, package-code character, and tooling hygiene issues."""
    root = root or repo_root()
    issues: list[str] = []

    for relative_path in REQUIRED_DOC_PATHS:
        if not (root / relative_path).is_file():
            issues.append(f"required document is missing: {relative_path}")

    for relative_path in FORBIDDEN_ROOT_DOC_PATHS:
        if (root / relative_path).exists():
            issues.append(f"localized document must not be stored at repository root: {relative_path}")

    readme_path = root / "README.md"
    if readme_path.is_file():
        readme_text = readme_path.read_text(encoding="utf-8-sig")
        for required_link in REQUIRED_README_LINKS:
            if required_link not in readme_text:
                issues.append(f"README.md is missing required link: {required_link}")

    try:
        text_paths = repository_text_paths(root)
    except (OSError, subprocess.CalledProcessError) as error:
        issues.append(f"could not list repository text files: {error}")
    else:
        for relative_path_text in text_paths:
            relative_path = Path(relative_path_text)
            candidate = root / relative_path
            if not candidate.is_file():
                continue
            try:
                lines = candidate.read_text(encoding="utf-8-sig").splitlines()
            except (OSError, UnicodeDecodeError) as error:
                issues.append(f"could not inspect repository text file: {relative_path} ({error})")
                continue
            for line_number, line in enumerate(lines, start=1):
                if any(reference in line for reference in STALE_AGENT_REFERENCE_PARTS):
                    issues.append(f"stale AI-agent routing reference: {relative_path}:{line_number}")

    try:
        code_paths = package_code_paths(root)
    except (OSError, subprocess.CalledProcessError) as error:
        issues.append(f"could not list package code files: {error}")
    else:
        issues.extend(package_code_issues(root, code_paths))

    try:
        tool_paths = list(tracked_paths) if tracked_paths is not None else tracked_tool_paths(root)
    except (OSError, subprocess.CalledProcessError) as error:
        issues.append(f"could not inspect tracked tooling artifacts: {error}")
    else:
        for relative_path in tool_paths:
            if is_generated_tool_artifact(relative_path):
                issues.append(f"generated tooling artifact is tracked: {relative_path}")

    return issues


def agentkit_cli_path(root: Path) -> Path:
    """Return the installed agentkit CLI, which owns validation of the agent docs."""
    return root / ".ai" / "kit" / "tools" / "agentkit.py"


def run_agentkit_check(root: Path | None = None) -> int:
    """Run `agentkit.py check` from the repository root and return its exit code."""
    root = root or repo_root()
    return subprocess.run([sys.executable, str(agentkit_cli_path(root)), "check"], cwd=root, check=False).returncode
