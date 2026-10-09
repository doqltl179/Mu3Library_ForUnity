---
paths:
  - "**/*.sh"
  - "**/*.bash"
  - "**/*.ps1"
  - "**/*.psm1"
---
<!-- agentkit:generated from .ai/kit/core/stacks/languages/shell.md, .ai/project/profile.toml. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Shell (Bash, sh, PowerShell)

## Detect
- Interpreter from the shebang (`#!/usr/bin/env bash`, `#!/bin/sh`) or extension. A `sh` script must stay POSIX: no arrays, `[[ ]]`, `<<<`, or `function` keyword.
- Target OS and runner: CI image, container base image (`/bin/sh` is often dash or BusyBox, not bash), macOS (BSD tools, bash 3.2), Windows.
- PowerShell edition: `#Requires -Version`/`-PSEdition`, and whether callers run `pwsh` (PowerShell 7) or `powershell` (Windows PowerShell 5.1).
- Lint and format config: `.shellcheckrc`, shfmt settings in `.editorconfig`, `PSScriptAnalyzerSettings.psd1`; Pester tests (`*.Tests.ps1`).

## Conventions
- Bash: start with `set -euo pipefail`. POSIX `sh`: `set -eu`, and add `pipefail` only when every target shell supports it (see «Version Notes»).
- Quote every expansion: `"$var"`, `"$@"`, `"$(cmd)"`. Build argument lists as bash arrays, not strings.
- Use `$(...)` not backticks; `[[ ]]` in bash and `[ ]` in `sh`; `printf` instead of `echo` for arbitrary data; `read -r`; `command -v` instead of `which`.
- Declare function variables `local` in bash; handle `cd` failure (`cd "$dir" || exit 1`).
- Create temp files with `mktemp` and remove them in `trap '...' EXIT`.
- Validate paths before deleting: `rm -rf -- "${target:?}"` aborts when the variable is unset or empty. Never `rm -rf` a variable or glob that was not validated.
- PowerShell: start scripts with `Set-StrictMode -Version Latest` and `$ErrorActionPreference = 'Stop'`; check `$LASTEXITCODE` after native commands.
- PowerShell: name functions `Verb-Noun` with approved verbs (`Get-Verb`); use `[CmdletBinding()]` and typed `param()` with validation attributes; support `-WhatIf` via `SupportsShouldProcess` in destructive functions; write full cmdlet and parameter names, not aliases.

## Verify
- `commands.lint`, or `shellcheck <file>` (add `-s sh` or `-s bash` when there is no shebang); `bash -n <file>` for syntax; `shfmt -d <file>` when the project uses shfmt.
- PowerShell: `Invoke-ScriptAnalyzer -Path <file>`; `Invoke-Pester` when tests exist.
- Run portable scripts on every target OS and shell, for example through the CI matrix.

## Pitfalls
- Unquoted expansions word-split and glob; `for f in $(ls)` breaks on spaces. Iterate globs directly, or use `find ... -print0` with `while IFS= read -r -d ''`.
- `set -e` does not fire inside `if`/`while` conditions, `&&`/`||` lists, or functions called from them; check critical commands explicitly.
- GNU-only forms fail on BSD/macOS: `sed -i` without a suffix argument, `date -d`, `stat -c`. Use portable alternatives or branch on `uname`.
- CRLF line endings break bash scripts (`$'\r': command not found`); keep LF.
- PowerShell functions return every uncaptured value; assign unwanted results to `$null` or cast to `[void]`.
- PowerShell: put `$null` on the left (`$null -eq $x`); with a collection on the left, the comparison filters elements instead.

## Version Notes
- macOS `/bin/bash` is bash 3.2: associative arrays, `mapfile`/`readarray`, and `${var,,}` need bash 4.0+ (as of 2026-10, per Apple's open-source bash distribution and the bash NEWS file).
- POSIX.1-2024 standardizes `set -o pipefail`, but older `/bin/sh` implementations reject it, for example dash before 0.5.13; confirm the target image's `sh` before using it in `sh` scripts (as of 2026-10, per pubs.opengroup.org and the dash repository).
- Pipeline chains (`&&`, `||`), the ternary `? :`, and `??` require PowerShell 7.0+; Windows PowerShell 5.1 rejects them (as of 2026-10, per learn.microsoft.com What's New in PowerShell 7.0).
- PowerShell 7 writes `utf8NoBOM` by default. Windows PowerShell 5.1 writes UTF-16LE from `Out-File` and `>`, the ANSI code page from `Set-Content`, and always adds a BOM with `-Encoding UTF8`; pass `-Encoding` explicitly whenever 5.1 writes files (as of 2026-10, per learn.microsoft.com about_Character_Encoding).
- Windows PowerShell 5.1 reads BOM-less script files as ANSI; save a `.ps1` that contains non-ASCII characters as UTF-8 with BOM when 5.1 must run it (as of 2026-10, per learn.microsoft.com about_Character_Encoding).
- PowerShell 7.4+ supports `$PSNativeCommandUseErrorActionPreference = $true` (default `$false`), which turns non-zero native exit codes into errors handled by `$ErrorActionPreference`; elsewhere check `$LASTEXITCODE` (as of 2026-10, per learn.microsoft.com about_Preference_Variables).
- PowerShell 7.6 is the current LTS (supported until 2028-11-14); 7.4 and 7.5 reach end of support on 2026-11-10, so target 7.6 in new CI images and installers (as of 2026-10, per learn.microsoft.com PowerShell Support Lifecycle).
