---
owns: "How outside material (vendor docs, articles, other prompts, community practice) is evaluated and adopted into the kit: source priority, adoption rules, and keep-or-reject criteria"
volatility: evolving
reviewed: 2026-10-09
---

# External Guidance

## When

- a task depends on current versions, vendor behavior, or other time-sensitive facts,
- adapting an outside prompt, article, policy, or workflow into kit guidance.

## Route Away When

- the adaptation procedure: the `kit-external-adapt` skill,
- recording the resulting change: [change-intake.md](change-intake.md).

## Source Priority

1. Evidence in the repository itself: files, configs, lockfiles, history.
2. Official vendor documentation, release notes, and changelogs.
3. Standards bodies and specifications.
4. Reputable secondary sources, to corroborate only.
5. Community posts, never as the sole basis for a rule.

Search for current information before answering or editing when the answer depends on dates, versions, or vendor behavior. Use exact dates, not "recently".

## Adoption Rules

- Verify that a local analog exists before adopting a rule about it; a mention is not proof.
- Separate durable patterns (source priority, verification habits, structure) from source-local details (product names, identity text, proprietary tags, tool schemas, paths, feature flags).
- Paraphrase in original words. Quote only when exact wording matters, with the source attached.
- Attach a source and an "as of" date to every external fact, and mark inferences as inferences.
- Map each adopted pattern to its owning page or skill; extend an existing owner before adding a new one.

## Keep Or Reject

| Keep | Reject by default |
|---|---|
| Source and freshness discipline | Vendor identity or persona text |
| Verification and honesty habits | Product catalogs, model names, pricing in rule pages |
| Structural patterns with a clear owner | Another platform's tool schemas or tags |
| Tool-loading mechanics the kit renders for (into the adapter table) | Hard-coded paths or feature flags from another environment |
