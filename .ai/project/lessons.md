# Lessons

Project-specific lessons learned from corrections, newest last. One line each: the rule, then why.
Promote a standing rule to `.ai/project/wiki/`, and a lesson that would help every project upstream with the `kit-upstream-propose` skill; then delete it here.

- Check each `\uXXXX` escape you write by its bytes: build the escape text and each search needle in a script from `chr(92)`, confirm the needle's hit count equals the escapes written, confirm changed lines where only ASCII belongs hold only bytes 32 to 126 plus tab, LF, and CR, and run `mu3-cli repo check` for package code. The tool-input transport can turn typed `\uXXXX` text into the character in file edits and shell commands alike, and a typed `grep -nF` needle then matches only the real character, never the escape text; the file editing tool saved escapes as characters in PR #287 and in a wiki line during #292, and byte checks kept them intact in PRs #401 and #406.
