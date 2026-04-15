from __future__ import annotations

import re
from pathlib import Path

import typer


app = typer.Typer(
    add_completion=False,
    no_args_is_help=True,
    help="Auxiliary repository CLI for Mu3Library tooling and agent-framework workflows.",
)
repo_app = typer.Typer(no_args_is_help=True, help="Repository discovery commands.")
agents_app = typer.Typer(no_args_is_help=True, help="Agent framework discovery commands.")
app.add_typer(repo_app, name="repo")
app.add_typer(agents_app, name="agents")


def _repo_root() -> Path:
    current = Path(__file__).resolve()

    for candidate in current.parents:
        if (candidate / "AGENTS.md").exists() and (candidate / ".github").exists():
            return candidate

    raise RuntimeError("Could not locate the Mu3Library repository root.")


def _agent_paths() -> list[Path]:
    return sorted((_repo_root() / ".github" / "agents").glob("*.agent.md"))


def _parse_agent_name(file_path: Path) -> str:
    pattern = re.compile(r'^name:\s*"?(.*?)"?$')

    for line in file_path.read_text(encoding="utf-8-sig").splitlines():
        match = pattern.match(line.strip())
        if match:
            return match.group(1)

    return file_path.stem


def _read_handoff_template() -> str:
    contract_path = _repo_root() / "docs" / "ai-agents" / "handoff-contract.md"
    contract_text = contract_path.read_text(encoding="utf-8-sig")
    marker = "## Required Handoff Packet"
    marker_index = contract_text.find(marker)

    if marker_index == -1:
        raise RuntimeError("Could not locate the Required Handoff Packet section in docs/ai-agents/handoff-contract.md.")

    after_marker = contract_text[marker_index + len(marker) :]
    code_fence_index = after_marker.find("```md")

    if code_fence_index == -1:
        raise RuntimeError("Could not locate the handoff packet code fence in docs/ai-agents/handoff-contract.md.")

    after_fence = after_marker[code_fence_index + len("```md") :]
    fence_end_index = after_fence.find("```")

    if fence_end_index == -1:
        raise RuntimeError("Could not locate the handoff packet template in docs/ai-agents/handoff-contract.md.")

    return after_fence[:fence_end_index].strip()


@repo_app.command("info")
def repo_info() -> None:
    """Print key repository roots and framework document locations."""
    root = _repo_root()

    typer.echo(f"Repository root: {root}")
    typer.echo(f"Base package: {root / 'Mu3Library_Base'}")
    typer.echo(f"URP package: {root / 'Mu3Library_URP'}")
    typer.echo(f"Agent docs: {root / 'docs' / 'ai-agents'}")
    typer.echo(f"CLI tooling: {root / 'tools' / 'mu3_cli'}")


@agents_app.command("list")
def agents_list() -> None:
    """List registered agent documents under .github/agents."""
    agent_files = _agent_paths()

    if not agent_files:
        typer.echo("No agent documents found.")
        raise typer.Exit(code=1)

    for agent_file in agent_files:
        agent_name = _parse_agent_name(agent_file)
        relative_path = agent_file.relative_to(_repo_root())
        typer.echo(f"- {agent_name}: {relative_path}")


@agents_app.command("check")
def agents_check() -> None:
    """Validate that the primary agent discovery entrypoints exist."""
    root = _repo_root()
    missing: list[str] = []

    if not (root / "AGENTS.md").exists():
        missing.append("AGENTS.md")
    if not (root / ".github" / "copilot-instructions.md").exists():
        missing.append(".github/copilot-instructions.md")
    if not (root / ".github" / "agents").exists():
        missing.append(".github/agents/")

    if missing or not _agent_paths():
        typer.echo("Missing agent discovery entrypoints:")
        for relative_path in missing:
            typer.echo(f"- {relative_path}")
        if not _agent_paths():
            typer.echo("- .github/agents/*.agent.md")
        raise typer.Exit(code=1)

    typer.echo("Primary agent discovery entrypoints are present.")


@agents_app.command("handoff-template")
def handoff_template() -> None:
    """Print the repository handoff packet template."""
    typer.echo(_read_handoff_template())