---
name: performance-investigate
description: "Improve performance by measurement: define the metric, workload, and target, reproduce with a benchmark or profile, find the dominant cost, change one thing at a time, re-measure, and report before and after numbers, stopping when the target is met. Use when something is too slow, uses too much memory or other resources, or exceeds a performance budget."
---
<!-- agentkit:generated from .ai/kit/core/skills/performance-investigate/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Investigate Performance

## Use When
- An operation is too slow, or uses too much memory, CPU, network, or bundle size.
- A performance budget or service-level target is exceeded.

## Do Not Use When
- The output is wrong → `bug-diagnose`.
- Restructuring with no performance target → `refactor-safely`.
- The fix is a newer runtime or library version → `dependency-upgrade`, then measure here.

## Inputs
- The metric (for example: p95 latency, peak memory, throughput), the workload, and the target value.
- The environment where the metric matters, and a way to run the code there or in a close equivalent.
- `commands.build`, `commands.run`, and `commands.test`.

## Steps
1. Define the metric, workload, and target. If no target is given, pause and ask per «Stop, Pause, or Proceed» in [integrity.md](.ai/kit/core/wiki/principles/integrity.md), proposing one.
2. Build a repeatable measurement: fixed input, production build settings, warm-up, and at least five runs. Record the command, environment, median, and spread.
3. Record the baseline. If it already meets the target, stop and report.
4. Profile to find the dominant cost with a tool that fits the metric (CPU profiler, allocation profiler, query log, network trace). Rank costs by their share of the metric. Leave code outside the dominant cost untouched.
5. Form one hypothesis and change one thing.
6. Re-measure with the same command and environment. Keep the change only if the gain exceeds the run-to-run spread; otherwise revert it and record the rejected hypothesis.
7. Run `commands.test` and the checks per «Verification By Change Type» in [verification.md](.ai/kit/core/wiki/workflows/verification.md). A performance change must not change behavior.
8. Commit each kept change with `git-commit`, with before and after numbers in the commit body.
9. Repeat steps 4–8 until the target is met. Stop and report early when the remaining dominant cost lies outside the code (infrastructure, an external service) or needs a design change the user must approve.

## Output
- The metric, workload, environment, and measurement command.
- Baseline and after numbers for each kept change, and whether the target was met.
- Rejected hypotheses, the remaining dominant costs, and options that need a decision.
