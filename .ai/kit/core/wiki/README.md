---
owns: "Wiki root: which section owns a question when the startup route map does not name a page"
volatility: stable
reviewed: 2026-10-09
---

# Agentkit Wiki

Open this page only when [START.md](../START.md) did not name the page you need. Pick the section whose question matches yours, open its index, and stop at the first page that owns the question.

Navigation rule: entry point → section index → owning page. If a question needs a fourth hop, the wiki is missing an owner; report it to `role-governor`.

<!-- agentkit:begin index -->
| Page | Owns |
|---|---|
| [authoring/README.md](authoring/README.md) | **authoring/** — How to create or change kit assets: wiki pages, agent cards, skills, and stack packs |
| [evolution/README.md](evolution/README.md) | **evolution/** — How the kit stays current: freshness review, external change intake, the tech radar, model calibration, versioning, and adopting outside guidance |
| [integration/README.md](integration/README.md) | **integration/** — How the kit is installed into a project, configured through the project overlay, and rendered for each AI tool |
| [operating-model/README.md](operating-model/README.md) | **operating-model/** — How the agent organization runs: its structure, the order of a request, owner selection, delegation, handoffs, context use, and memory |
| [principles/README.md](principles/README.md) | **principles/** — Which invariant principle governs where knowledge lives, how agent-facing docs are written, and how agents stay honest |
| [workflows/README.md](workflows/README.md) | **workflows/** — Which repeatable workflow policy applies: planning, Git, issues and pull requests, code-change safety, verification, review, documentation, and release |
<!-- agentkit:end index -->

Agent cards, skills, and stack packs are listed in `.ai/generated/catalog.md` (active in this project) and in the kit catalogs: [agents](../agents/CATALOG.md), [skills](../skills/CATALOG.md), [stacks](../stacks/CATALOG.md).
