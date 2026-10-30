---
id: django
title: Django
kind: framework
applies_to: ["**/models.py", "**/views.py", "**/serializers.py", "**/urls.py", "**/migrations/*.py", "**/settings*.py", "**/settings/*.py"]
related: [python, sql]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://docs.djangoproject.com/en/stable/", "https://docs.djangoproject.com/en/stable/releases/", "https://www.djangoproject.com/download/", "https://docs.djangoproject.com/en/6.1/releases/6.0/", "https://docs.djangoproject.com/en/6.1/releases/6.1/", "https://www.django-rest-framework.org/"]
---

# Django

## Detect
- Django version in `pyproject.toml` or the lockfile; `manage.py` location and `DJANGO_SETTINGS_MODULE` (set in `manage.py`, `wsgi.py`/`asgi.py`, and the pytest config).
- Settings layout: one `settings.py` or a package (`settings/base.py`, `dev.py`, `prod.py`), and how environment values are loaded.
- `INSTALLED_APPS` for project apps and integrations such as Django REST Framework (`rest_framework`), Celery, or django-filter.
- `AUTH_USER_MODEL` for a custom user model; the `DATABASES` engine.
- Test runner: `manage.py test` or pytest-django (`pytest.ini`, `conftest.py`); factories or fixtures in use.

## Conventions
- Keep each domain in its own app with its models, views, urls, admin, and tests; follow the existing app layout.
- Reference users through `settings.AUTH_USER_MODEL` in relations and `get_user_model()` in code, never `django.contrib.auth.models.User` directly.
- Put query logic in model managers and custom QuerySets; keep views thin.
- Load related rows explicitly: `select_related` for foreign keys and one-to-one, `prefetch_related` for reverse and many-to-many relations, including relations read by templates and serializers.
- Use set-based operations: `exists()` for presence, `bulk_create`/`update` for batches, `F()` expressions for race-free increments.
- Wrap multi-step writes in `transaction.atomic()`; send emails and enqueue tasks from `transaction.on_commit`.
- After changing models, run `makemigrations`, review the generated file, and commit it with the model change. Write data migrations with `RunPython` using `apps.get_model()`, not direct model imports.
- Validate input with Forms/ModelForms or DRF serializers; never use `request.POST` or `request.data` values unvalidated.
- DRF: set `permission_classes` explicitly per view or as a deliberate default; tune each view's `queryset` for the fields its serializer reads.
- Keep `SecurityMiddleware`, `CsrfViewMiddleware`, and `XFrameOptionsMiddleware` enabled; production settings need `DEBUG = False`, explicit `ALLOWED_HOSTS`, and secrets from the environment.
- Use `timezone.now()` when `USE_TZ = True`; use callables for mutable field defaults (`default=dict`).

## Verify
- `commands.test` (`python manage.py test` or `pytest`).
- `python manage.py makemigrations --check --dry-run` fails when a model change lacks a migration.
- `python manage.py check`, and `check --deploy` against production settings.

## Pitfalls
- Editing or deleting a migration already applied in a shared environment breaks other databases; add a new migration instead.
- Two migrations with the same parent conflict; inspect both, then run `makemigrations --merge`.
- Queries inside loops (N+1) multiply round trips; fix with `select_related`/`prefetch_related` and lock the count with `assertNumQueries` or `django_assert_num_queries`.
- Calling the sync ORM from an async view raises `SynchronousOnlyOperation`; use async QuerySet methods (`aget`, `acount`, `async for`) or `sync_to_async`.
- `mark_safe`, `|safe`, or string-formatted SQL in `raw()`/`extra()` with user input enables XSS or SQL injection; keep auto-escaping and pass query parameters.
- `null=True` on `CharField`/`TextField` creates two empty values; use `blank=True` with an empty-string default.
- Business logic hidden in signals runs implicitly and is hard to trace; call services explicitly.

## Version Notes
- Django 6.1 is the latest release and 5.2 is the current LTS (extended support until April 2028); 6.0 gets security fixes until April 2027. 6.2 LTS is planned for April 2027; later feature releases use calendar versions (Django 2028 in January 2028), each with three years of support. Check the supported versions table before upgrading (as of 2026-10, per djangoproject.com/download).
- Django 6.0 requires Python 3.12+ (5.2 is the last series for 3.10 and 3.11). `DEFAULT_AUTO_FIELD` now defaults to `BigAutoField`; a project that relied on the old default must set `DEFAULT_AUTO_FIELD = "django.db.models.AutoField"` to keep it (as of 2026-10, per docs.djangoproject.com 6.0 release notes).
- Django 6.0 adds `django.tasks` (`@task`, `.enqueue()`, the `TASKS` setting) without a worker, and its built-in backends are for development and testing; keep the project's existing task queue unless it already uses `django.tasks`. It also adds CSP (`ContentSecurityPolicyMiddleware`, `SECURE_CSP`) and template partials (`{% partialdef %}`, `{% partial %}`) (as of 2026-10, per docs.djangoproject.com 6.0 release notes).
- Django 6.1 adds `QuerySet.fetch_mode()`: `FETCH_PEERS` loads a missing field for all instances from the same QuerySet, `FETCH_RAISE` raises `FieldFetchBlocked`. `on_delete=DB_CASCADE`, `DB_SET_NULL`, or `DB_SET_DEFAULT` act in SQL (`ON DELETE`), and `DB_CASCADE` does not trigger `pre_delete`/`post_delete` signals. The `MAILERS` setting deprecates `EMAIL_BACKEND`, the other `EMAIL_*` settings, and the `connection` and `fail_silently` mail arguments. Minimums: PostgreSQL 15, MySQL 8.4, MariaDB 10.11, SQLite 3.37 (as of 2026-10, per docs.djangoproject.com 6.1 release notes).
- The `STORAGES` setting replaces `DEFAULT_FILE_STORAGE` and `STATICFILES_STORAGE` (deprecated in 4.2, removed in 5.1); the async QuerySet API exists since 4.1 (as of 2026-10, per docs.djangoproject.com release notes).
