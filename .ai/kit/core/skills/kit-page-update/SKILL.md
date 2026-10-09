---
name: kit-page-update
description: "Change an existing wiki page: locate the single owning page, edit only that owner, classify the rule type, update links and dependent skills and cards, bump the reviewed date only after verification, then regenerate and validate. Invariant changes go through role-governor first. Use when a rule, fact, or route on an existing page is wrong, outdated, incomplete, or contradicted by another file."
category: kit
volatility: evolving
reviewed: 2026-10-09
---

# Update Wiki Page

## Use When
- A rule, fact, or route on an existing page is wrong, outdated, or incomplete.
- Two files disagree and the owner must win.
- A behavior change needs its owning docs updated in the same unit of work.

## Do Not Use When
- No page owns the question → `kit-page-add`.
- The page is only overdue for review, with no known defect → `kit-freshness-review`.
- The page is under `.ai/kit/` in an installed project → `kit-upstream-propose`.

## Inputs
- The intended change and its evidence: a source URL with date, a failing check, or a user decision.
- The layer of the owning page: core (kit repository only) or the project wiki.

## Steps
1. Locate the owner: `rg` the key terms and read the `owns` line of each candidate; the page whose `owns` claims the question is the owner. Edit only that page.
2. Classify the change per «Rule Types» in [ssot.md](../../wiki/principles/ssot.md):
   - invariant: hand the proposed edit to `role-governor` and apply it only after a `continue` verdict;
   - policy: confirm the decision came from the user or team, and record it on the page;
   - fact: cite the source with its date, and keep the page `volatile` with that URL in `sources`;
   - scaffold: tag the rule `[scaffold]`.
3. Edit per [page-spec.md](../../wiki/authoring/page-spec.md) and [agent-first-writing.md](../../wiki/principles/agent-first-writing.md). If the `owns` line changes, confirm no other page now claims the same question.
4. When the edit renames a file or a section heading, `rg` the old name and update every link and «…» citation in the same change.
5. `rg` for links to the changed section from skills, agent cards, and stack packs; update any whose steps or boundaries no longer match the rule.
6. Delete or replace with a link every other copy of the changed rule found in steps 1–5, per rule 4 of «Rules» in ssot.md.
7. Bump `reviewed` to today only if the whole page was checked per «What Reviewed Means» in [freshness-policy.md](../../wiki/evolution/freshness-policy.md); otherwise leave the date.
8. In the kit repository, add an Unreleased entry per «Changelog Format» in [versioning.md](../../wiki/evolution/versioning.md), with a migration note per «Migration Notes» when overlays must change.
9. Run `agentkit.py sync` to refresh indexes and rendered files, then `agentkit.py check`; fix every error before reporting.

## Output
- The owning page and a one-line summary of the change, with its rule type.
- Dependent files updated, any `role-governor` verdict, and the `check` result.
