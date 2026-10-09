---
owns: "How to write and phrase documentation whose primary reader is an AI agent, including the language of agent-facing docs"
volatility: stable
reviewed: 2026-10-09
---

# Agent-First Writing

The primary reader of every kit file is an AI agent: it starts each session without memory, has a limited context budget, follows text literally, and cannot ask the author what was meant. Humans read the same files second.

## When

- writing or revising any page, agent card, skill, or stack pack,
- a page is being misread or ignored by agents.

## Route Away When

- the required frontmatter and section shape: [page-spec.md](../authoring/page-spec.md), [agent-spec.md](../authoring/agent-spec.md), [skill-spec.md](../authoring/skill-spec.md), [stack-spec.md](../authoring/stack-spec.md),
- where the content should live at all: [ssot.md](ssot.md),
- line budgets: [context-budget.md](../operating-model/context-budget.md).

## Rules

1. Put the exit first. Open with what the page is for, then `When` and `Route Away When`, so a reader on the wrong page leaves after a few lines.
2. One page answers one question, stated in its `owns` field.
3. Write testable imperatives: "Do X when Y." Never "consider", "try to", "where possible", or "as appropriate" without the condition that decides it.
4. State conditions, thresholds, and the owner of each exception explicitly.
5. Use tables for selection (situation → choice), numbered lists for order, bullets for unordered sets.
6. Name things exactly. Paths, commands, agent names, skill names, and profile keys go in backticks and match the real identifier.
7. Give the reason in one line when a rule looks arbitrary. Agents generalize correctly from reasons and badly from bare commands.
8. Keep history out of rule pages. No "previously", "we used to", or change narratives; history belongs in `CHANGELOG.md` and Git.
9. Use at most one minimal example per concept and label it as an example. A rule must be stated outside its example.
10. Write agent-facing kit files in English: it is the most token-efficient language for current models and the common language of AI tooling. Human-facing output (reports to the user, commit messages, pull requests, issues) follows `project.language` in the profile.
11. Keep identifiers stable. Renaming a file, agent, or skill means updating every link in the same change; `agentkit.py check` finds broken links.
12. Prefer a positive instruction with a stated boundary over a long list of prohibitions.

## Phrases To Replace

| Instead of | Write |
|---|---|
| "should probably", "consider" | "Do X when Y; otherwise Z." |
| "etc.", "and so on" | The complete list, or "for example:" plus the rule that defines membership |
| "the relevant docs" | The exact link |
| "be careful with X" | The specific failure and the check that prevents it |
| "as needed" | The trigger that makes it needed |
