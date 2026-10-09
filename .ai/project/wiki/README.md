---
owns: "Project-specific knowledge the kit leaves open: architecture, domain terms, module map, local conventions, environments"
volatility: evolving
reviewed: 2026-10-09
---

# Project Wiki

Facts about this project only. Rules that hold for every project live in the kit; link to them instead of restating them. Add pages with `agentkit.py new page <name>`; the index below is generated.

<!-- agentkit:begin index -->
| Page | Owns |
|---|---|
| [glossary.md](glossary.md) | Approved rendering of every project term in each language, terms kept as-is, and per-locale style choices for translated text |
| [overview.md](overview.md) | What this repository is: the package model, the package-first rule, development projects, directory map, commit scopes, human-facing doc layout, file encoding, and local-only verification |
| [package-architecture.md](package-architecture.md) | Mu3Library package rules: surfaces per package, assembly boundaries and dependency direction, define-gated optional integrations, DI and CoreBase, project C# conventions, and the review focus for package changes |
| [package-release.md](package-release.md) | How the three Mu3Library packages are versioned and released: version files, tag formats, changelog headers, install URLs, and GitHub Release titles and notes |
| [unity-verification.md](unity-verification.md) | How package changes are verified in this repository: the Unity compile entry point and its targets, what each kind of package change must show, where the tests live, and what the report states |
| [unity-yaml-guide.md](unity-yaml-guide.md) | The verified procedure for editing text-serialized Unity scenes and prefabs directly in this repository, its ScreenEffect sample anchors, and the checks after each edit |
<!-- agentkit:end index -->
