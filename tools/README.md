# Tools

Repository-local support tooling lives here. These tools can help agents inspect or verify the repository, but they do not own Unity package behavior.

| Path | Owns |
|---|---|
| `mu3_cli/` | Python CLI for agent-framework and C# Dev Kit support workflows |
| `csdevkit_tests/` | Narrow xUnit metadata tests for C# Dev Kit integration checks |

Generated files under `bin/`, `obj/`, `.venv/`, and `*.egg-info/` are local artifacts and should not be tracked.
