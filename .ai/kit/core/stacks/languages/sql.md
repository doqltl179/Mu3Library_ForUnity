---
id: sql
title: SQL
kind: language
applies_to: ["**/*.sql"]
related: [dbt]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://www.postgresql.org/docs/current/", "https://www.postgresql.org/docs/release/", "https://dev.mysql.com/doc/refman/9.7/en/", "https://dev.mysql.com/doc/refman/8.4/en/", "https://www.sqlite.org/docs.html", "https://www.sqlite.org/changes.html", "https://learn.microsoft.com/en-us/sql/t-sql/language-reference"]
---

# SQL

## Detect
- Dialect before writing anything: database driver and connection URL, ORM or migration config, container services, or `dialect` in `.sqlfluff`. PostgreSQL, MySQL/MariaDB, SQLite, SQL Server, and data warehouses differ in syntax and semantics.
- Server version from the same sources; features vary by version.
- Migration tool and folder, for example Flyway (`V<n>__*.sql`), Liquibase changelogs, Alembic `versions/`, golang-migrate or dbmate (`*.up.sql`/`*.down.sql`), EF Core `Migrations/`, Prisma `migrations/`. A `dbt_project.yml` means dbt: follow the `dbt` pack.
- Naming and casing conventions from the existing schema and queries.

## Conventions
- Pass every value as a bind parameter through the driver; never concatenate input into SQL. Take dynamic identifiers only from an allowlist and quote them in the dialect's style.
- Change schema only through a new migration; never edit a migration that has run in any shared environment.
- List columns explicitly in `SELECT` and `INSERT`; `SELECT *` over-fetches and breaks when columns change.
- Use explicit `JOIN ... ON` with table aliases; qualify every column in multi-table queries.
- Add `ORDER BY` whenever order matters, including with row limits; paginate large tables by key (`WHERE id > ?`), not by large offsets.
- Run statements that must succeed together in one transaction. Default isolation: PostgreSQL and SQL Server `READ COMMITTED`, MySQL InnoDB `REPEATABLE READ`, SQLite serializable (as of 2026-10, per each vendor's documentation).
- Back new filters, joins, and foreign keys with indexes; confirm each with `EXPLAIN`.
- Use the dialect's precise types (for example `timestamptz` in PostgreSQL, `DECIMAL` for money).

## Verify
- Apply new migrations to a disposable database, then roll them back when the tool supports down migrations.
- Inspect plans with `EXPLAIN` for queries on large tables.
- Lint: `commands.lint` (for example `sqlfluff lint`); tests: `commands.test`.

## Pitfalls
- `x = NULL` is never true; use `IS NULL`, or the dialect's null-safe comparison: `IS NOT DISTINCT FROM` (PostgreSQL, SQL Server, SQLite; see «Version Notes») or MySQL `<=>`.
- `NOT IN (subquery)` returns no rows when the subquery yields a NULL; use `NOT EXISTS`. `COUNT(col)` skips NULLs; `COUNT(*)` does not.
- `UPDATE`/`DELETE` without `WHERE`, or with a wrong join, changes every row; run the matching `SELECT` first.
- `EXPLAIN ANALYZE` executes the statement; wrap data-modifying statements in a transaction and roll back.
- Functions or casts on an indexed column in `WHERE` (`lower(email) = ?`) bypass the index; index the expression or rewrite the predicate.
- Building indexes or adding constrained columns on large tables can block writes; use online options such as PostgreSQL `CREATE INDEX CONCURRENTLY` (not allowed inside a transaction).
- Dialect-only syntax: row limits (`LIMIT`, `TOP`, `FETCH FIRST`), upserts (`ON CONFLICT`, `ON DUPLICATE KEY UPDATE`, `MERGE`), identifier quoting (`"x"`, `` `x` ``, `[x]`), concatenation (`||`, `CONCAT`, `+`).
- PostgreSQL folds unquoted identifiers to lowercase; avoid quoted mixed-case names.
- Integer division truncates in PostgreSQL, SQL Server, and SQLite (`1/2 = 0`); cast to a decimal type first.

## Version Notes
- Check the server version before using: PostgreSQL `MERGE` (15+) and `MERGE ... RETURNING` (17+); MySQL CTEs and window functions (8.0+); SQLite `RETURNING` (3.35+) and `STRICT` tables (3.37+) (as of 2026-10, per postgresql.org, dev.mysql.com, and sqlite.org).
- `IS [NOT] DISTINCT FROM` needs SQL Server 2022+ and SQLite 3.39+; older SQLite accepts the equivalent `IS`/`IS NOT`; MySQL (through 9.7) documents only `<=>` (as of 2026-10, per learn.microsoft.com, sqlite.org, and dev.mysql.com).
- MySQL deprecates `VALUES(col)` inside `ON DUPLICATE KEY UPDATE` since 8.0.20; reference the new row through a row alias instead (8.0.19+), for example `INSERT INTO t (a, b) VALUES (?, ?) AS new ON DUPLICATE KEY UPDATE b = new.b` (as of 2026-10, per dev.mysql.com).
