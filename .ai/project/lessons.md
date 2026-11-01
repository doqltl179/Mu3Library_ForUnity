# Lessons

Project-specific lessons learned from corrections, newest last. One line each: the rule, then why.
Promote a standing rule to `.ai/project/wiki/`, and a lesson that would help every project upstream with the `kit-upstream-propose` skill; then delete it here.

- After writing a `\uXXXX` escape in any file, confirm the file still holds the escape, for example with `LC_ALL=C.UTF-8 grep -nP '[^\x00-\x7F]' <file>`, because the file editing tool has saved such escapes as the characters themselves: an invisible `'\uE000'` and a test table's accented letters in PR #287, which compiled and passed the tests until review caught them, and the `'\uE000'` of a wiki line while #292 was recorded.
