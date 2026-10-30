# Lessons

Project-specific lessons learned from corrections, newest last. One line each: the rule, then why.
Promote a standing rule to `.ai/project/wiki/`, and a lesson that would help every project upstream with the `kit-upstream-propose` skill; then delete it here.

- Unity's `Vector2.Lerp` accepts one scalar interpolation value; retain a small component-wise helper when X and Y use separate normalized interpolation values.
- A tiled `SpriteRenderer` draws over `SpriteRenderer.size` and not over the sprite bounds, and assigning a sprite at runtime does not resize it; fit such a renderer through its size and keep its transform scale the same on both axes, which then only decides how big one repeated segment is drawn.
- `LocalizationSettings.Instance` never answers null: it creates and returns a default settings object, logging a warning, when the project carries no asset. `HasSettings` is the only real presence check, and `LocaleIdentifier.CultureInfo` stays null for a custom locale code the culture table does not carry, so neither may be read straight through.
- Read line endings from `git ls-files --eol` or `file`, never from `grep -c $'\r$'` in Git Bash: one shell counted every line of an LF file and of a CRLF file alike, which made Unity's LF output look like CRLF, and another counted none of a CRLF file's lines without `-U`. A tracked file whose line endings alone changed shows as modified in `git status` while `git diff` stays empty, because Git takes a changed size for a change.
