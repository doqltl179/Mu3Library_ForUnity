from __future__ import annotations

import re

import typer

from mu3_cli.csdevkit import csdevkit_app
from mu3_cli.repository import agent_paths, parse_agent_name, read_handoff_template, repo_root
from mu3_cli.unity import unity_app


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
app.add_typer(unity_app, name="unity")


AGENT_DOC_FILE_LIMIT = 24
AGENT_ROLE_CARD_LINE_LIMIT = 30
INSTRUCTION_FILE_LINE_LIMIT = 65
PROMPT_FILE_LINE_LIMIT = 30
SKILL_FILE_LINE_LIMIT = 60
STARTUP_LINE_BUDGETS = {
    ".github/copilot-instructions.md": 45,
    ".github/instructions/agent-framework.instructions.md": 65,
    "docs/ai-agents/routing/README.md": 70,
}
BROAD_APPLY_TO_PATTERN = re.compile(r"applyTo:\s*['\"]?\*\*['\"]?\s*$")


@repo_app.command("info")
def repo_info() -> None:
    """Print key repository roots and framework document locations."""
    root = repo_root()

    typer.echo(f"Repository root: {root}")
    typer.echo(f"Base package: {root / 'Mu3Library_Base'}")
    typer.echo(f"URP package: {root / 'Mu3Library_URP'}")
    typer.echo(f"Agent docs: {root / 'docs' / 'ai-agents'}")
    typer.echo(f"CLI tooling: {root / 'tools' / 'cli'}")


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
    invalid: list[str] = []

    if not (root / "AGENTS.md").exists():
        missing.append("AGENTS.md")
    if not (root / ".github" / "copilot-instructions.md").exists():
        missing.append(".github/copilot-instructions.md")
    if not (root / ".github" / "agents").exists():
        missing.append(".github/agents/")
    if not (root / "docs" / "ai-agents" / "routing" / "README.md").exists():
        missing.append("docs/ai-agents/routing/README.md")
    if not (root / "docs" / "ai-agents" / "contracts" / "handoff-contract.md").exists():
        missing.append("docs/ai-agents/contracts/handoff-contract.md")

    obsolete_paths = [
        "docs/ai-agents/routing/" + "agent-" + "catalog.md",
        "docs/ai-agents/routing/" + "control-plane-" + "routing.md",
        "docs/ai-agents/routing/" + "unity-specialist-" + "routing.md",
        "docs/ai-agents/contracts/" + "README.md",
        "docs/ai-agents/contracts/" + "agent-spec-" + "contract.md",
        "docs/ai-agents/packages/" + "base-package-" + "routing.md",
        "docs/ai-agents/packages/" + "urp-package-" + "routing.md",
    ]
    for relative_path in obsolete_paths:
        if (root / relative_path).exists():
            invalid.append(f"obsolete file still exists: {relative_path}")

    if missing or not agent_paths():
        typer.echo("Missing agent discovery entrypoints:")
        for relative_path in missing:
            typer.echo(f"- {relative_path}")
        if not agent_paths():
            typer.echo("- .github/agents/*.agent.md")
        raise typer.Exit(code=1)

    for relative_path, max_lines in STARTUP_LINE_BUDGETS.items():
        full_path = root / relative_path
        line_count = len(full_path.read_text(encoding="utf-8-sig").splitlines())
        if line_count > max_lines:
            invalid.append(f"context entry exceeds {max_lines} lines: {relative_path} ({line_count})")

    agent_doc_count = len(list((root / "docs" / "ai-agents").rglob("*.md")))
    if agent_doc_count > AGENT_DOC_FILE_LIMIT:
        invalid.append(f"AI-agent docs exceed {AGENT_DOC_FILE_LIMIT} files: docs/ai-agents ({agent_doc_count})")

    for instruction_file in sorted((root / ".github" / "instructions").glob("*.instructions.md")):
        relative_path = instruction_file.relative_to(root)
        lines = instruction_file.read_text(encoding="utf-8-sig").splitlines()
        frontmatter = lines[:10]
        if len(lines) > INSTRUCTION_FILE_LINE_LIMIT:
            invalid.append(
                f"instruction file exceeds {INSTRUCTION_FILE_LINE_LIMIT} lines: "
                f"{relative_path} ({len(lines)})"
            )
        if not any(line.startswith("description:") for line in frontmatter):
            invalid.append(f"instruction file is missing frontmatter description: {relative_path}")
        broad_apply_to = next((line for line in frontmatter if BROAD_APPLY_TO_PATTERN.fullmatch(line)), None)
        if broad_apply_to:
            invalid.append(f"instruction file uses broad applyTo: {relative_path} ({broad_apply_to})")

    for agent_file in agent_paths():
        line_count = len(agent_file.read_text(encoding="utf-8-sig").splitlines())
        if line_count > AGENT_ROLE_CARD_LINE_LIMIT:
            invalid.append(
                f"agent role card exceeds {AGENT_ROLE_CARD_LINE_LIMIT} lines: "
                f"{agent_file.relative_to(root)} ({line_count})"
            )

    for prompt_file in sorted((root / ".github" / "prompts").glob("*.prompt.md")):
        relative_path = prompt_file.relative_to(root)
        lines = prompt_file.read_text(encoding="utf-8-sig").splitlines()
        frontmatter = lines[:10]
        if len(lines) > PROMPT_FILE_LINE_LIMIT:
            invalid.append(f"prompt file exceeds {PROMPT_FILE_LINE_LIMIT} lines: {relative_path} ({len(lines)})")
        if not any(line.startswith("name:") for line in frontmatter):
            invalid.append(f"prompt file is missing frontmatter name: {relative_path}")
        if not any(line.startswith("description:") for line in frontmatter):
            invalid.append(f"prompt file is missing frontmatter description: {relative_path}")

    for skill_file in sorted((root / ".github" / "skills").glob("*/SKILL.md")):
        relative_path = skill_file.relative_to(root)
        lines = skill_file.read_text(encoding="utf-8-sig").splitlines()
        frontmatter = lines[:10]
        if len(lines) > SKILL_FILE_LINE_LIMIT:
            invalid.append(f"skill file exceeds {SKILL_FILE_LINE_LIMIT} lines: {relative_path} ({len(lines)})")
        if not any(line.startswith("name:") for line in frontmatter):
            invalid.append(f"skill file is missing frontmatter name: {relative_path}")
        if not any(line.startswith("description:") for line in frontmatter):
            invalid.append(f"skill file is missing frontmatter description: {relative_path}")

    if invalid:
        typer.echo("Invalid agent framework shape:")
        for issue in invalid:
            typer.echo(f"- {issue}")
        raise typer.Exit(code=1)

    typer.echo("Agent framework entrypoints, context budgets, instruction scope, prompts, skills, and role-card shape are valid.")


@agents_app.command("handoff-template")
def handoff_template() -> None:
    """Print the repository handoff packet template."""
    typer.echo(read_handoff_template())
