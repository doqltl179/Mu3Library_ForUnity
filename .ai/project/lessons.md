# Lessons

Project-specific lessons learned from corrections, newest last. One line each: the rule, then why.
Promote a standing rule to `.ai/project/wiki/`, and a lesson that would help every project upstream with the `kit-upstream-propose` skill; then delete it here.

- Check a `\uXXXX` escape you wrote by its bytes, never with a typed needle: write the escape text and any search needle from a script that builds the backslash with `chr(92)`, confirm that needle is found and that changed lines where only ASCII belongs hold no byte above 127, and run `mu3-cli repo check` for package code, because the tool-input transport can turn typed `\uXXXX` text into the character itself, in file edits and shell commands alike, so a typed `grep -nF '\uFFFD'` also matches the real character and proves nothing: it saved an invisible `'\uE000'` and a test table's accented letters in PR #287 and a wiki line's `'\uE000'` while #292 was recorded, and only byte checks kept the real U+FFFD and surrogate characters out of PRs #401 and #406.
