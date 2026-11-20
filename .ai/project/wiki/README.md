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
| [rules/package-architecture.md](rules/package-architecture.md) | Mu3Library package rules: surfaces per package, assembly dependency direction, the optional-integration symbols and split files, Unity package and built-in module declarations in package.json, DI and CoreBase, project C# conventions, and the review focus for package changes |
| [glossary.md](glossary.md) | Approved rendering of every project term in each language, terms kept as-is, and per-locale style choices for translated text |
| [overview.md](overview.md) | What this repository is: the package model, the package-first rule, the project configs and where their Unity projects are generated, which checkout a Claude app session's unit uses, where units running at the same time put their worktrees, where each machine sets the work-folder root and how short `<unit>` is, removing a worktree, directory map, commit scopes, human-facing doc layout, file encoding, and local-only verification |
| [package-release.md](package-release.md) | How the Mu3Library packages are released beyond the profile's [[release.packages]]: the root changelog headers, install URLs, the dependency versions checked at a package's first release, and GitHub Release titles and notes |
| [package-split.md](package-split.md) | What a unit that splits a feature out of Base into a package of its own changes: the new package's files, its dependency versions, moving the code, its editor tools, and its tests with what each assembly keeps or gets, the Base internals it used, a tool that keeps reading it, and dropping its SDK gate and the references only it needed, the configs, tools, and profile, and the docs up to the package's first release |
| [runtime-design.md](runtime-design.md) | Design rules the package runtime has settled on: Base managers' callbacks, initialization, disposal, object ownership, and shared command guards; superseded async operations; the Watermelon board command contracts and list; and the Watermelon board rules |
| [unity-verification.md](unity-verification.md) | How package changes are verified in this repository: the Unity compile entry point and its targets, what each kind of package change must show, where the tests live, and what the report states |
| [unity-yaml-guide.md](unity-yaml-guide.md) | Scene and prefab edits in this repository's samples: how the kit's edit routes reach a sample here and the procedure for a declared direct-YAML edit |
<!-- agentkit:end index -->
