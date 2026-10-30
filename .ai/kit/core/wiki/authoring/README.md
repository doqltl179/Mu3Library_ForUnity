---
owns: "How to create or change kit assets: wiki pages, agent cards, skills, and stack packs"
volatility: stable
reviewed: 2026-10-09
---

# Authoring

Open this section before adding or changing any agent-facing file in `core/` or `.ai/project/`. Each page owns one asset type's schema and placement; the matching `kit-*` skill walks the procedure. Scaffold new files with `agentkit.py new`; `check` warns while a template placeholder is left unfilled.

<!-- agentkit:begin index -->
| Page | Owns |
|---|---|
| [agent-spec.md](agent-spec.md) | Agent card schema, the granularity model (role card × stack pack × project binding), departments, tiers, access levels, and when to add a new agent |
| [page-spec.md](page-spec.md) | Wiki page schema: placement by question shape, frontmatter, section shape, section indexes, and project wiki pages |
| [skill-spec.md](skill-spec.md) | Skill schema and categories, the boundary between a skill, an agent, and a wiki page, and how a skill references policy |
| [stack-spec.md](stack-spec.md) | Stack pack schema and kinds: how language, framework, and infrastructure knowledge is written, bound to paths, and kept current |
<!-- agentkit:end index -->
