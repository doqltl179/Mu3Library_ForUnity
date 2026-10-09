---
owns: "How the kit adapts to changing model capability: scaffold rules, tier-to-model mapping, and the calibration procedure"
volatility: evolving
reviewed: 2026-10-09
---

# Model Calibration

Rules written for today's models can become noise for tomorrow's: a rule that compensates for a weakness costs context and can distort behavior once the weakness is gone. New capabilities can also make a different structure possible. Calibration keeps the kit matched to the models actually in use.

## When

- a new model generation or family is adopted by the projects using the kit,
- agents repeatedly fail in the same way despite a rule,
- a tool gains a capability that changes how work could be structured (longer context, native subagents, new loading mechanisms).

## Route Away When

- filing the trigger as a change: [change-intake.md](change-intake.md),
- per-tool file formats: [tool-adapters.md](../integration/tool-adapters.md).

## Scaffold Rules

- A rule that exists only because current models need it is a scaffold rule (see «Rule Types» in [ssot.md](../principles/ssot.md)). End it with `[scaffold]` and give the reason in the same sentence.
- Find them with `rg -n "\[scaffold\]" core .ai/project`.
- Invariants and policies are never tagged; they hold regardless of model strength.

## Tier Mapping

- Agent cards declare a tier (`deep`, `standard`, `light`), never a model name.
- Each project maps tiers to models per tool in `[models.<tool>]` of its profile. A model release therefore changes one profile line per tool, not the kit.
- When no mapping is set, tools use their default model.

## Calibration Procedure

1. List scaffold rules and the rules named by recent failures or lessons.
2. Pick three to five representative recent tasks per affected area.
3. For each rule, compare outcomes with and without it (or with the rule rephrased), using the same model and task.
4. Remove a rule the model no longer needs; keep a rule that still prevents failures; rewrite a rule that fails as written.
5. Review whether new tool capabilities should change an adapter or the operating model; file intake records for those.
6. Record what changed and why in the changelog, so projects know which behaviors moved.

## Signals Worth Recording

- The same correction appearing in lessons across projects.
- Agents ignoring or over-applying a rule.
- Reports that stay correct after a rule was removed in a trial.
