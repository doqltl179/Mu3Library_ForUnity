from __future__ import annotations

import re
import subprocess
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


def repo_root() -> Path:
    current = Path(__file__).resolve()

    for candidate in current.parents:
        if (candidate / "AGENTS.md").exists() and (candidate / ".github").exists():
            return candidate

    raise RuntimeError("Could not locate the Mu3Library repository root.")


def is_generated_tool_artifact(relative_path: str) -> bool:
    """Return whether a tracked tools path belongs to a generated output directory."""
    parts = Path(relative_path).parts
    return any(part in {"bin", "obj"} or part.endswith(".egg-info") for part in parts)


def tracked_tool_paths(root: Path) -> list[str]:
    """List tracked files under tools without relying on shell-specific pipelines."""
    result = subprocess.run(
        ["git", "ls-files", "--", "tools"],
        cwd=root,
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    return [line for line in result.stdout.splitlines() if line]


def repository_text_paths(root: Path) -> list[str]:
    """List tracked and untracked, non-ignored repository text candidates."""
    result = subprocess.run(
        [
            "git",
            "ls-files",
            "--cached",
            "--others",
            "--exclude-standard",
            "--",
            ".github",
            "docs",
            "tasks",
            "tools",
        ],
        cwd=root,
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    return [line for line in result.stdout.splitlines() if Path(line).suffix in REPOSITORY_TEXT_SUFFIXES]


def repository_hygiene_issues(
    root: Path | None = None,
    tracked_paths: Iterable[str] | None = None,
) -> list[str]:
    """Collect repository layout, link, routing-reference, and tooling hygiene issues."""
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
        tool_paths = list(tracked_paths) if tracked_paths is not None else tracked_tool_paths(root)
    except (OSError, subprocess.CalledProcessError) as error:
        issues.append(f"could not inspect tracked tooling artifacts: {error}")
    else:
        for relative_path in tool_paths:
            if is_generated_tool_artifact(relative_path):
                issues.append(f"generated tooling artifact is tracked: {relative_path}")

    return issues


def agent_paths() -> list[Path]:
    return sorted((repo_root() / ".github" / "agents").glob("*.agent.md"))


def parse_agent_name(file_path: Path) -> str:
    pattern = re.compile(r'^name:\s*"?(.*?)"?$')

    for line in file_path.read_text(encoding="utf-8-sig").splitlines():
        match = pattern.match(line.strip())
        if match:
            return match.group(1)

    return file_path.stem


def read_handoff_template() -> str:
    contract_path = repo_root() / "docs" / "ai-agents" / "contracts" / "handoff-contract.md"
    contract_text = contract_path.read_text(encoding="utf-8-sig")
    marker = "## Required Handoff Packet"
    marker_index = contract_text.find(marker)

    if marker_index == -1:
        raise RuntimeError(
            "Could not locate the Required Handoff Packet section in docs/ai-agents/contracts/handoff-contract.md."
        )

    after_marker = contract_text[marker_index + len(marker) :]
    code_fence_index = after_marker.find("```md")

    if code_fence_index == -1:
        raise RuntimeError(
            "Could not locate the handoff packet code fence in docs/ai-agents/contracts/handoff-contract.md."
        )

    after_fence = after_marker[code_fence_index + len("```md") :]
    fence_end_index = after_fence.find("```")

    if fence_end_index == -1:
        raise RuntimeError(
            "Could not locate the handoff packet template in docs/ai-agents/contracts/handoff-contract.md."
        )

    return after_fence[:fence_end_index].strip()
