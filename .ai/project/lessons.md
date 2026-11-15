# Lessons

Project-specific lessons learned from corrections, newest last. One line each: the rule, then why.
Promote each lesson, and delete it here, when «Promotion» in [memory-policy.md](../kit/core/wiki/operating-model/memory-policy.md) says.

- Test each feature's intent, its role, and its common usages; do not add tests, measurements, or tooling hardening that hunt for situations no real use has shown, and leave those to what real runs or external users' issues bring — on 2026-11-15 the user judged that edge cases of the test tooling had overtaken the package work, and stressed that this means not testing excessively, not skipping tests (#514).
- Record a reviewer's hypothetical edge case as a known limit, or drop it, instead of registering it as a follow-up issue; register only a defect seen in a real run or in the work itself — follow-ups chained from reviews of tooling fixes (#474 → #492–#494, #483 → #501) filled most of one batch (#514).
- Before a long or repeated Unity run that holds the PC, such as a measurement that starts `test-unity.sh` or players dozens of times, say how long it takes and ask the user when to run it — #501's forced-kill measurement kept Unity starting until the user could not use the PC (#514).
