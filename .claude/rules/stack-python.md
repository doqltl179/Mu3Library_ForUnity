---
paths:
  - "**/*.py"
  - "**/*.pyi"
---
<!-- agentkit:generated from .ai/kit/core/stacks/languages/python.md, .ai/project/profile.toml. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Python

## Detect
- Environment manager from lockfile and `pyproject.toml`: `uv.lock` → uv; `poetry.lock` or `[tool.poetry]` → Poetry; `pdm.lock` → PDM; `hatch.toml` or `[tool.hatch]` → Hatch; `Pipfile.lock` → Pipenv; `requirements*.txt` (with `*.in` sources for pip-tools) → pip. Use only that tool.
- `pylock.toml` is the standard lockfile (PEP 751); its `created-by` key names the tool that owns it.
- Python version from `requires-python`, `.python-version`, or the CI matrix. Run code in the project's virtual environment (the manager's, or `.venv/`), never the system interpreter.
- Tool config in `pyproject.toml` `[tool.*]` tables or dedicated files: ruff (`ruff.toml`), mypy (`mypy.ini`), pyright (`pyrightconfig.json`), pytest (`pytest.ini`, `conftest.py`), tox/nox (`tox.ini`, `noxfile.py`).
- Package layout (`src/` or flat) and build backend in `[build-system]`.

## Conventions
- Change dependencies through the manager (`uv add`, `poetry add`, `pdm add`) so `pyproject.toml` and the lockfile change together; with pip-tools, edit the `.in` file and recompile.
- Annotate every public function; keep the type checker's configured strictness. Use dataclasses, `TypedDict`, or `Protocol` instead of untyped dicts across module boundaries.
- Validate external data at the boundary with the project's schema library before trusting its type.
- Use `with` for files, locks, and connections; `pathlib.Path` for paths.
- Catch specific exceptions; chain with `raise NewError(...) from err` to keep the cause.
- Use `logging` in library code, not `print`; pass arguments lazily (`log.info("id=%s", item_id)`).
- Use timezone-aware datetimes (`datetime.now(timezone.utc)`).
- Never call blocking I/O inside `async def`; use an async library or `asyncio.to_thread`.

## Verify
- Run tools through the manager (for example `uv run pytest`, `poetry run mypy`) so the project interpreter and pinned versions are used.
- Lint and format: `commands.lint`, `commands.format`; with ruff, `ruff check` and `ruff format --check`.
- Type check: `commands.typecheck`, or the configured mypy/pyright.
- Tests: `commands.test`, narrowed with `pytest path/test_x.py::test_name` or `-k <expr>`.

## Pitfalls
- Mutable default arguments (`def f(items=[])`) are shared across calls; default to `None` and create inside.
- Bare `except:` also catches `KeyboardInterrupt` and `SystemExit`; catch `Exception` or narrower.
- A local module named like a stdlib or installed package (`json.py`, `logging.py`) shadows it on import.
- Closures created in a loop see the loop variable's final value; bind it with a default argument or `functools.partial`.
- `pip install` without updating the lockfile leaves CI on different versions.
- `is` compares identity; use `==` for values and keep `is` for `None`.
- `float` for money; use `decimal.Decimal`.

## Version Notes
- Check `requires-python` before using: `X | Y` unions and `match` (3.10); `tomllib` and `except*` (3.11); `type` statements and PEP 695 generics (3.12) (as of 2026-10, per docs.python.org whatsnew).
- Python 3.14 evaluates annotations lazily (PEP 649), so unquoted forward references no longer fail at definition time there; keep quotes or `from __future__ import annotations` while older versions are supported (as of 2026-10, per docs.python.org).
- The free-threaded (no-GIL) interpreter is officially supported from 3.14 but remains a separate optional build; do not assume it (as of 2026-10, per docs.python.org).
- Each minor version gets about five years of support; check end-of-life dates before targeting one (as of 2026-10, per devguide.python.org/versions).
