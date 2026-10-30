---
id: fastapi
title: FastAPI
kind: framework
applies_to: []
related: [python, sql, docker]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://fastapi.tiangolo.com/", "https://fastapi.tiangolo.com/release-notes/", "https://docs.pydantic.dev/latest/", "https://docs.pydantic.dev/latest/migration/"]
---

# FastAPI

## Detect
- FastAPI, Starlette, and Pydantic versions in `pyproject.toml` or the lockfile (`uv.lock`, `poetry.lock`, `requirements*.txt`).
- Pydantic major from usage: `model_config`, `field_validator`, `model_dump()` mean v2; `class Config`, `@validator`, `.dict()` mean v1; `from pydantic.v1 import` means v1 models running under v2, which FastAPI 0.128+ rejects.
- App wiring: the `FastAPI()` instance, `APIRouter` modules, `lifespan`, middleware, and the server command (`uvicorn`, `fastapi run`, or gunicorn with uvicorn workers).
- Settings through `pydantic-settings` `BaseSettings`; database layer (SQLAlchemy sync or async session, SQLModel) and its driver.
- Tests: pytest with `TestClient`, or `httpx.AsyncClient` with `ASGITransport` plus `pytest-asyncio` or `anyio`.

## Conventions
- Declare path, query, header, and body inputs with type hints and Pydantic models; use `Annotated[..., Depends(...)]` and `Annotated[..., Query(...)]` when the codebase does.
- Keep separate input and output schemas (for example `UserCreate`, `UserRead`); set `response_model` or a return annotation on every endpoint so output is filtered, validated, and documented.
- Provide database sessions, auth, settings, and shared parameters through `Depends`; use `yield` dependencies for setup and teardown, with one session per request.
- Use `async def` only when the endpoint awaits async I/O; use plain `def` for blocking code, which FastAPI runs in a threadpool.
- Group routes with one `APIRouter` per domain, with `prefix` and `tags`; set `status_code` on the decorator (for example 201 on create).
- Raise `HTTPException` for HTTP errors; register exception handlers for domain errors instead of catching them in each route.
- Run startup and shutdown logic in a `lifespan` context manager.
- Load settings once (cached dependency or module singleton) and inject them; do not read `os.environ` inside routes.
- Use `BackgroundTasks` only for short post-response work; send long jobs to a task queue.

## Verify
- `commands.test` (pytest); swap dependencies in tests with `app.dependency_overrides` and clear them after each test.
- `commands.lint` and `commands.typecheck` when set.
- After schema changes, check that `/openapi.json` or `/docs` shows the intended request and response models.

## Pitfalls
- Blocking calls (`requests`, sync database drivers, `time.sleep`) inside `async def` stall every request on the worker; switch to `def`, an async client such as `httpx.AsyncClient`, or `anyio.to_thread.run_sync`.
- Returning ORM objects without an output schema can leak fields such as password hashes; return through a response model (`from_attributes=True` in v2).
- Mixing Pydantic v1 and v2 APIs (`.dict()` with `.model_dump()`, `orm_mode` with `from_attributes`); use the detected major's API throughout.
- `TestClient` runs `lifespan` only inside `with TestClient(app) as client:`; use the context manager when startup state is needed.
- Declaring `/users/{user_id}` before `/users/me` routes `me` to the parameterized path; declare fixed paths first.
- Lazy-loaded relationships on an async SQLAlchemy session raise `MissingGreenlet`; load them eagerly (for example `selectinload`).

## Version Notes
- FastAPI supports Pydantic v2 since 0.100.0; `Annotated` dependencies are the documented style; `lifespan` replaces the deprecated `@app.on_event`; the `fastapi dev`/`fastapi run` CLI ships with the `fastapi[standard]` extra (as of 2026-10, per fastapi.tiangolo.com/release-notes).
- Pin FastAPI and read the release notes on upgrade, since minor releases have shipped breaking changes. FastAPI 0.126+ requires Pydantic 2.7+ (Pydantic v1 dropped), 0.128+ also drops `pydantic.v1` models, and 0.129+ requires Python 3.10+ (as of 2026-10, per fastapi.tiangolo.com/release-notes).
