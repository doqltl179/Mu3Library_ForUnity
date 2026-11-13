# Lessons

Project-specific lessons learned from corrections, newest last. One line each: the rule, then why.
Promote each lesson, and delete it here, when «Promotion» in [memory-policy.md](../kit/core/wiki/operating-model/memory-policy.md) says.

- Check each `\uXXXX` escape you write by its bytes: build the escape text and each search needle in a script from `chr(92)`, confirm the needle's hit count equals the escapes written, confirm changed lines where only ASCII belongs hold only bytes 32 to 126 plus tab, LF, and CR, and run `mu3-cli repo check` for package code. The tool-input transport can turn typed `\uXXXX` text into the character in file edits and shell commands alike, and a typed `grep -nF` needle then matches only the real character, never the escape text; the file editing tool saved escapes as characters in PR #287 and in a wiki line during #292, and byte checks kept them intact in PRs #401 and #406. Proposed upstream as [ai-agents#82](https://github.com/doqltl179/ai-agents/issues/82); keep this lesson until a kit release ships it.
