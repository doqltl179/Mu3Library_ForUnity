---
name: test-add
description: "Add focused tests for a change: follow the nearest existing test pattern, choose the smallest test level that observes the behavior, cover the behavior and its edge cases, and prove each new test fails without the change. Use when a fix, feature, or refactor needs tests, or a behavior has no test."
---
<!-- agentkit:generated from .ai/kit/core/skills/test-add/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Add Tests

## Use When
- A fix, feature, or refactor needs tests.
- A behavior has no test, or a reported bug needs a regression test.

## Do Not Use When
- `commands.test` is empty and the project has no test setup → report the gap; adding a test framework follows «Dependencies» in [code-changes.md](.ai/kit/core/wiki/workflows/code-changes.md).
- Measuring speed or memory → `performance-investigate`.

## Inputs
- The behavior to cover, stated as observable inputs and outputs.
- The change under test (a diff or commit), if one exists.
- `commands.test` and `commands.e2e` from the profile.

## Steps
1. List the cases: the main behavior, then each edge case that applies (boundaries, empty and missing input, invalid input, error paths, ordering and concurrency).
2. Find the nearest existing test pattern: tests for the same module first, then for its siblings (`rg -l <module name>` in the test directories). Copy their framework, file location, naming, fixtures, and helpers. A test library the project does not use yet follows «Dependencies» in code-changes.md.
3. Choose the smallest level that observes the behavior:

   | The behavior lives in | Level |
   |---|---|
   | One function or class with no I/O | Unit |
   | Interaction across modules, or with a database, filesystem, or network boundary | Integration, with the project's existing fixtures |
   | A user-visible flow through the running app that lower levels cannot observe | End-to-end via `commands.e2e` |

4. Write one behavior per test, asserting observable results, not internal calls or private state. Keep tests deterministic: control time, randomness, and network the way the nearest pattern does. Snapshot files follow «Generated Code» in code-changes.md.
5. Prove each test can fail. For a change: run the test with the change absent (write the test first, or set the change aside with `git stash push -- <changed paths>` and restore it with `git stash pop`) and confirm it fails for the expected reason. For existing behavior, including characterization tests for `refactor-safely`: confirm the test passes now and fails when the code under test is temporarily broken, then undo that edit.
6. Run the new tests with the change present, then the full `commands.test`. Run a test that depends on time or concurrency several times to expose flakiness; judge the results per «Flaky Tests And Disabled Checks» in [verification.md](.ai/kit/core/wiki/workflows/verification.md).
7. Commit with `git-commit`, together with the change or separately per «Commit Convention» in [git-workflow.md](.ai/kit/core/wiki/workflows/git-workflow.md).

## Output
- The test files and test names added, and the level chosen for each.
- Evidence that each test fails without the change and passes with it, per «Evidence Format» in [verification.md](.ai/kit/core/wiki/workflows/verification.md).
