# Development Idea Creator Evaluation

## Purpose

Use this document to regression-test the `development-idea-creator` workflow asset after changes to:

- `.github/skills/development-idea-creator/SKILL.md`
- `.github/prompts/development-idea-creator.prompt.md`
- `.github/copilot-instructions.md`

The goal is to verify two behaviors at the same time:

- broad or ambiguous ideation requests should widen into net-new package directions,
- explicit refinement requests should stay in safe incremental-refinement mode.

## Runner Setup

- Use repository evidence only for regression runs.
- Disable web research so the result reflects the workflow contract rather than external inspiration.
- Run each case independently with the same model and similar output budget.
- Score the actual output, not the stated intention.
- Keep the response concise enough that ranking and top-idea drift are easy to inspect.

## Fixed Prompt Set

### Case 1: Broad Direction

Prompt:

```text
Mu3Library의 다음 개발 방향이 막막하다. 이 패키지 프로젝트를 앞으로 어디로 확장하면 좋을까?
```

Pass signals:

- the top idea introduces a net-new package surface, workflow multiplier, or ecosystem bridge,
- the top 2 ideas are not both setup-hub, docs-only, or sample-pack polish,
- the output names a missing layer or whitespace area before converging.

### Case 2: Existing-Feature Pain Point

Prompt:

```text
Mu3Library의 DI/MVP 쪽은 가능성이 있는데 신규 사용자가 바로 써먹기 어렵다. 다음 개발 아이디어를 제안해줘.
```

Pass signals:

- the answer widens one hop beyond the named pain point,
- the top idea is not just documentation, helper polish, or another sample,
- at least 2 of the top 3 ideas extend into a new reusable surface or workflow.

### Case 3: Adoption Growth

Prompt:

```text
URP sample, optional integrations, utility surface는 있는데 패키지 채택을 더 키우고 싶다. 다음 방향 아이디어를 제안해줘.
```

Pass signals:

- the top idea creates a new adoption reason rather than a broader sample pack,
- existing optional-package patterns do not dominate the ranking by default,
- the answer identifies a package-family gap such as persistence, orchestration, or live-content workflow.

### Case 4: Explicit Incremental Control

Prompt:

```text
Mu3Library의 MVPHelper와 Template 쪽을 크게 바꾸지는 말고, 다음 버전에서 넣을 수 있는 안전한 점진 개선 아이디어를 정리해줘.
```

Pass signals:

- the answer explicitly stays in incremental-refinement mode,
- the top 2 ideas remain inside existing surfaces,
- no new package family or strategic bet appears in the top 2.

## Scoring Rubric

Score each case from 0 to 10.
Use 0, 1, or 2 points per category.

| Category | 0 | 1 | 2 |
|---|---|---|---|
| Mode fidelity | Ignores the request mode | Partly follows the mode but drifts | Fully matches broad-ideation or refinement intent |
| Repository grounding | Bare generic ideas with little repo evidence | Some repo anchors but shallow | Ideas are clearly shaped by real repository surfaces and gaps |
| Option diversity | Repeats one lane with cosmetic variation | Some diversity but buckets blur together | Distinct lanes with clearly different package or workflow bets |
| Novelty alignment | Top idea is mostly incremental when broad novelty is expected, or too ambitious when refinement is requested | Mixed alignment | Top idea matches the expected novelty level for the case |
| Constraint discipline | Breaks package boundaries or ignores obvious risks | Risks are noted but weakly integrated | Public API, asmdef, optional-gate, sample, and verification constraints are respected |

## Gate Conditions

Case 1 to Case 3 must all satisfy these gates:

- top idea is `net-new`, `adjacent-new`, or clearly equivalent,
- at least 2 of the top 3 ideas go beyond minor polish,
- no broad case ranks a docs-only, setup-hub, or sample-only idea first.

Case 4 must satisfy these gates:

- the output declares or clearly honors incremental-refinement mode,
- top 2 ideas stay additive within existing surfaces,
- no new package family appears in the top 2.

Overall result rules:

- Pass: every gate passes, no case scores below 8/10, and the average score is at least 8.5/10.
- Warning: one gate misses, or one case scores 7/10, or the average falls between 7.0 and 8.4.
- Fail: Case 4 breaks refinement intent, or 2 broad cases rank incremental polish first, or any case scores 6/10 or lower.

## Baseline Snapshot

Baseline run date: 2026-05-03

| Case | Observed top idea | Result |
|---|---|---|
| Case 1 | Mu3 AppFlow package | Pass |
| Case 2 | Feature-slice starter package for DI/MVP adoption | Pass |
| Case 3 | Versioned Save/Profile package | Pass |
| Case 4 | MVPHelper preview and validation refinements | Pass |

## Common Failure Patterns

- Broad cases collapse into setup hub, sample pack, or documentation polish.
- A named pain point is answered only inside the existing feature instead of widening one hop to adjacent workflows.
- Optional integration expansion dominates the ranking even when it is just another instance of an existing pattern.
- Explicit refinement asks still produce new package families or strategic bets.

## Suggested Evaluation Output Shape

Ask the evaluator to return these sections for each case:

1. Repository signals
2. Ranked ideas
3. Named top idea
4. Short comparison or verdict

That shape is compact enough to score quickly and still exposes drift in ranking behavior.