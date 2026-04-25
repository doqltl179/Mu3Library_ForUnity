# Mu3 CLI

`mu3-cli` is an auxiliary Python command-line tool for repository automation.

It is intentionally scoped to tooling-safe roots and should not modify Unity runtime or editor package surfaces unless that is explicitly requested.

## Initial Command Surface

- `repo info`: print key repository roots and framework document locations.
- `agents list`: list registered agent documents under `.github/agents`.
- `agents check`: validate that the primary agent discovery entrypoints exist.
- `agents handoff-template`: print the current handoff packet template from `docs/ai-agents/handoff-contract.md`.

## Environment Bootstrap

Preferred flow with `uv`:

```powershell
cd tools/mu3_cli
uv venv .venv
.\.venv\Scripts\Activate.ps1
uv pip install -e .
mu3-cli --help
```

Portable fallback with standard `venv`:

```powershell
cd tools/mu3_cli
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -e .
mu3-cli --help
```

## Notes

- This package lives under `tools/` so it stays outside Unity package delivery surfaces.
- The command tree is intentionally small. Add new groups only when a workflow becomes stable and reusable.
- The CLI is a read-only convenience surface for discovery and preflight checks. Governance policy still lives in the framework docs and instructions.