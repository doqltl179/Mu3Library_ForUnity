# Base Samples

Use this page only when editing importable sample scenes or sample assets under `Mu3Library_Base/Samples~`.

## Rules

- Preserve `.meta` files, scene references, sample structure, and importable package sample integrity.
- Prefer the smallest sample change that demonstrates the package behavior; do not refactor unrelated sample content.
- If a sample change implies runtime or editor API changes, treat the package code as the primary owner and keep the sample update secondary.

## Route Away When

- the work actually belongs to runtime code or editor tooling: go back to [base-package-routing.md](base-package-routing.md).
- the work is no longer inside the base package: go back to [README.md](README.md).
