from __future__ import annotations

import re
from pathlib import Path


def repo_root() -> Path:
    current = Path(__file__).resolve()

    for candidate in current.parents:
        if (candidate / "AGENTS.md").exists() and (candidate / ".github").exists():
            return candidate

    raise RuntimeError("Could not locate the Mu3Library repository root.")


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
    contract_path = repo_root() / "docs" / "ai-agents" / "handoff-contract.md"
    contract_text = contract_path.read_text(encoding="utf-8-sig")
    marker = "## Required Handoff Packet"
    marker_index = contract_text.find(marker)

    if marker_index == -1:
        raise RuntimeError(
            "Could not locate the Required Handoff Packet section in docs/ai-agents/handoff-contract.md."
        )

    after_marker = contract_text[marker_index + len(marker) :]
    code_fence_index = after_marker.find("```md")

    if code_fence_index == -1:
        raise RuntimeError(
            "Could not locate the handoff packet code fence in docs/ai-agents/handoff-contract.md."
        )

    after_fence = after_marker[code_fence_index + len("```md") :]
    fence_end_index = after_fence.find("```")

    if fence_end_index == -1:
        raise RuntimeError(
            "Could not locate the handoff packet template in docs/ai-agents/handoff-contract.md."
        )

    return after_fence[:fence_end_index].strip()