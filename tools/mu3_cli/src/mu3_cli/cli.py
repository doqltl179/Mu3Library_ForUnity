from __future__ import annotations

import typer

from mu3_cli.csdevkit import csdevkit_app
from mu3_cli.repository import agent_paths, parse_agent_name, read_handoff_template, repo_root


app = typer.Typer(
    add_completion=False,
    no_args_is_help=True,
    help="Auxiliary repository CLI for Mu3Library tooling and agent-framework workflows.",
)
repo_app = typer.Typer(no_args_is_help=True, help="Repository discovery commands.")
agents_app = typer.Typer(no_args_is_help=True, help="Agent framework discovery commands.")
app.add_typer(repo_app, name="repo")
app.add_typer(agents_app, name="agents")
app.add_typer(csdevkit_app, name="csdevkit")


@repo_app.command("info")
def repo_info() -> None:
    """Print key repository roots and framework document locations."""
    root = repo_root()

    typer.echo(f"Repository root: {root}")
    typer.echo(f"Base package: {root / 'Mu3Library_Base'}")
    typer.echo(f"URP package: {root / 'Mu3Library_URP'}")
    typer.echo(f"Agent docs: {root / 'docs' / 'ai-agents'}")
    typer.echo(f"CLI tooling: {root / 'tools' / 'mu3_cli'}")


@agents_app.command("list")
def agents_list() -> None:
    """List registered agent documents under .github/agents."""
    agent_files = agent_paths()

    if not agent_files:
        typer.echo("No agent documents found.")
        raise typer.Exit(code=1)

    for agent_file in agent_files:
        agent_name = parse_agent_name(agent_file)
        relative_path = agent_file.relative_to(repo_root())
        typer.echo(f"- {agent_name}: {relative_path}")


@agents_app.command("check")
def agents_check() -> None:
    """Validate that the primary agent discovery entrypoints exist."""
    root = repo_root()
    missing: list[str] = []

    if not (root / "AGENTS.md").exists():
        missing.append("AGENTS.md")
    if not (root / ".github" / "copilot-instructions.md").exists():
        missing.append(".github/copilot-instructions.md")
    if not (root / ".github" / "agents").exists():
        missing.append(".github/agents/")

    if missing or not agent_paths():
        typer.echo("Missing agent discovery entrypoints:")
        for relative_path in missing:
            typer.echo(f"- {relative_path}")
        if not agent_paths():
            typer.echo("- .github/agents/*.agent.md")
        raise typer.Exit(code=1)

    typer.echo("Primary agent discovery entrypoints are present.")


@agents_app.command("handoff-template")
def handoff_template() -> None:
    """Print the repository handoff packet template."""
    typer.echo(read_handoff_template())