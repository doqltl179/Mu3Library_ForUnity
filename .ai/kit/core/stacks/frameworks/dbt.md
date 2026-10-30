---
id: dbt
title: dbt
kind: framework
applies_to: ["**/models/**/*.sql", "**/models/**/*.yml", "**/macros/**/*.sql", "**/dbt_project.yml"]
related: [sql, python]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://docs.getdbt.com/docs/introduction", "https://docs.getdbt.com/reference/node-selection/syntax", "https://docs.getdbt.com/docs/build/incremental-models", "https://docs.getdbt.com/docs/build/data-tests", "https://docs.getdbt.com/docs/dbt-versions/core-upgrade"]
---

# dbt

## Detect
- `dbt_project.yml`: `name`, `profile`, `require-dbt-version`, paths (`model-paths`, `macro-paths`, `test-paths`, `snapshot-paths`, `seed-paths`), `vars`, and folder-level configs (`+materialized`, `+schema`).
- Engine and adapter: `dbt --version`; `dbt-core` plus `dbt-<adapter>` in Python dependencies, or the dbt Fusion engine, or the dbt Cloud CLI. Supported flags and features differ between them.
- Packages: `packages.yml` or `dependencies.yml`, pinned in `package-lock.yml`; install with `dbt deps`.
- Profiles: `profiles.yml` in `~/.dbt/`, the project directory, or `DBT_PROFILES_DIR`; it holds credentials and is not committed. Note the available targets (for example `dev`, `ci`, `prod`).
- Layers and naming the project uses, for example `staging/` (`stg_`), `intermediate/` (`int_`), `marts/` (`fct_`, `dim_`).
- SQL lint config, for example `.sqlfluff`.

## Conventions
- Reference models with `{{ ref('model') }}` and raw tables with `{{ source('source', 'table') }}` declared in YAML; never hard-code database or schema names, which breaks lineage, environments, and selection.
- One model per file; the file name is the model name and is unique across the project.
- Staging models map 1:1 to sources and only rename, cast, and lightly clean; joins and business logic go downstream.
- Choose materialization per model: `view` (default), `table`, `incremental`, `ephemeral`, or `materialized_view` where the adapter supports it; use snapshots to keep slowly changing history.
- Incremental models: filter new rows inside `{% if is_incremental() %}` against `{{ this }}`; set `unique_key` and an `incremental_strategy` the adapter supports; set `on_schema_change`; add a lookback window when data arrives late.
- Test each model's primary key with `unique` and `not_null`; add `relationships` and `accepted_values` where they encode real rules. Singular tests are SQL files in `tests/` that return failing rows.
- Document models and columns with `description` in YAML next to the model; reuse long text with `{% docs %}` blocks.
- List columns explicitly in marts instead of `select *`.
- Reuse macros from installed packages (for example `dbt_utils`) and dbt's cross-database macros before writing adapter-specific SQL.

## Verify
- Prefer `commands.build` and `commands.test`.
- `dbt parse` catches project and YAML errors; `dbt compile -s <model>` writes rendered SQL to `target/compiled/` for debugging Jinja.
- `dbt build -s <model>+` runs and tests the model and everything downstream; preview a selection with `dbt ls -s <selector>`.
- `dbt build -s <model> --empty` validates SQL against the warehouse without reading data.
- CI-style runs: `dbt build -s state:modified+ --defer --state <prod-artifacts-dir>`.
- Run against the development target. Get explicit confirmation before `--target prod` or `--full-refresh` on an incremental model; both rewrite production data or rebuild expensive tables.

## Pitfalls
- `dbt run` skips tests; use `dbt build` to run and test in DAG order.
- An incremental model without a correct `unique_key` duplicates rows or fails merges.
- Changing an incremental model's columns or logic without `on_schema_change` or `--full-refresh` leaves stale or missing columns.
- `is_incremental()` is false on the first run and on full refresh; the model must build correctly without the filter.
- Editing `target/`, `dbt_packages/`, or `logs/`: they are generated and not committed.
- Macros that query the warehouse (`run_query`) get no results at parse time; guard them with `{% if execute %}`.
- Committing credentials in `profiles.yml`; read secrets with `{{ env_var('NAME') }}`.

## Version Notes
- Generic tests are declared under `data_tests:` (the older `tests:` key is still accepted), and YAML `unit_tests:` exist from dbt Core 1.8 (as of 2026-10, per dbt upgrade guides).
- The `--empty` flag exists from dbt Core 1.8 (as of 2026-10, per dbt docs).
- The dbt Fusion engine is a separate engine from dbt Core; check which one the project runs before relying on Core-only behavior or flags (as of 2026-10, per docs.getdbt.com).
