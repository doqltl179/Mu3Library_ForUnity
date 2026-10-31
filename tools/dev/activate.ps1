# Enters the repository's development environment in this PowerShell session:
#   - creates .venv at the repository root on first use and installs mu3-cli into it,
#   - sets the variables in .env.local (see tools/dev/env.local.example),
#   - activates that virtual environment,
#   - on Windows, puts Git Bash first on PATH so `bash ./test-unity.sh` runs.
#
# From the repository root:   . .\tools\dev\activate.ps1
# (from elsewhere, give the script's path; running it without the leading dot works too)
# Leave it again with:        deactivate
#
# What it keeps is process environment variables, the global deactivate function, and
# $global:Mu3DevState. Everything else runs in a script block of its own, so the caller's
# session gets no leftover variables, and its StrictMode and $ErrorActionPreference stay as
# they are.

& {
    param([string]$ScriptRoot)

    if (Test-Path -Path variable:global:Mu3DevState) {
        deactivate
    }

    # Activating over another virtual environment would leave that one's folder on the PATH
    # that deactivate restores.
    if ($env:VIRTUAL_ENV -and (Get-Command -Name deactivate -CommandType Function -ErrorAction SilentlyContinue)) {
        Write-Host "[mu3] Leaving the active virtual environment $env:VIRTUAL_ENV"
        deactivate
    }

    $root = (Resolve-Path -Path (Join-Path $ScriptRoot '..\..')).Path
    $onWindows = $env:OS -eq 'Windows_NT'
    $venv = Join-Path $root '.venv'
    $scripts = if ($onWindows) { Join-Path $venv 'Scripts' } else { Join-Path $venv 'bin' }

    # CPython names the script Activate.ps1 and uv names it activate.ps1, which differ on a
    # case-sensitive file system.
    $findActivate = {
        'Activate.ps1', 'activate.ps1' |
            ForEach-Object { Join-Path $scripts $_ } |
            Where-Object { Test-Path -LiteralPath $_ } |
            Select-Object -First 1
    }

    # --- Virtual environment ---------------------------------------------------------------
    $activate = & $findActivate
    if (-not $activate) {
        Write-Host "[mu3] Creating $venv and installing mu3-cli..."
        $cli = Join-Path $root 'tools\cli'
        $python = Get-Command -Name python, python3 -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1

        if (Get-Command -Name uv -CommandType Application -ErrorAction SilentlyContinue) {
            uv venv $venv --prompt mu3 --quiet
            if ($LASTEXITCODE -eq 0) { uv pip install --python $venv --quiet -e $cli }
        }
        elseif ($python) {
            & $python.Source -m venv $venv --prompt mu3
            $venvPython = if ($onWindows) { Join-Path $scripts 'python.exe' } else { Join-Path $scripts 'python' }
            if ($LASTEXITCODE -eq 0) { & $venvPython -m pip install --quiet -e $cli }
        }
        else {
            Write-Error '[mu3] Neither uv nor Python was found; install one of them and try again.'
            return
        }

        $activate = & $findActivate
        if ($LASTEXITCODE -ne 0 -or -not $activate) {
            Write-Error "[mu3] Setting up $venv failed; nothing was activated. Delete the folder and try again."
            return
        }
    }

    # --- What deactivate restores ----------------------------------------------------------
    # VIRTUAL_ENV and VIRTUAL_ENV_PROMPT can come from a parent process, such as an editor's
    # terminal, with no deactivate to call; the virtual environment's activation removes them.
    $savedPath = $env:PATH
    $saved = @{}
    foreach ($name in 'MU3_DEV_ROOT', 'VIRTUAL_ENV', 'VIRTUAL_ENV_PROMPT') {
        $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }

    # --- .env.local ------------------------------------------------------------------------
    # Loaded before the virtual environment, so a variable that changes how it activates, such
    # as VIRTUAL_ENV_DISABLE_PROMPT, takes effect.
    $loaded = New-Object -TypeName System.Collections.Generic.List[string]
    $envFile = Join-Path $root '.env.local'
    if (Test-Path -LiteralPath $envFile) {
        $number = 0
        foreach ($line in Get-Content -LiteralPath $envFile -Encoding UTF8) {
            $number++
            $text = $line.Trim()
            if (-not $text -or $text.StartsWith('#')) {
                continue
            }

            # Case-sensitive, as bash matches it.
            if ($text -cnotmatch '^(?:export\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$') {
                Write-Warning "[mu3] .env.local line ${number} is not KEY=VALUE; skipped."
                continue
            }

            $key = $Matches[1]
            $value = $Matches[2].Trim()
            if ($value.Length -ge 2 -and $value[0] -eq $value[-1] -and ($value[0] -eq '"' -or $value[0] -eq "'")) {
                $value = $value.Substring(1, $value.Length - 2)
            }

            if (-not $saved.ContainsKey($key)) {
                $saved[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
            }

            [Environment]::SetEnvironmentVariable($key, $value, 'Process')
            $loaded.Add($key)
        }
    }

    # Called rather than dot-sourced, so its own variables stay out of the caller's session;
    # what it keeps, the environment, the prompt, and deactivate, is global.
    & $activate
    $venvDeactivate = (Get-Command -Name deactivate -CommandType Function).ScriptBlock

    # --- Git Bash --------------------------------------------------------------------------
    # PowerShell on Windows has no bash on PATH, or finds WSL's, which cannot run the
    # repository's scripts. mu3-cli's own lookup (tools/cli/README.md, "Bash on Windows")
    # chooses the bash, so both run the same one. Exit code 3 means mu3-cli is missing.
    $bash = $null
    if ($onWindows) {
        $bash = & python -I -c "import contextlib, sys`ntry: import mu3_cli.unity as u`nexcept ImportError: sys.exit(3)`nwith contextlib.suppress(Exception): print(u.find_windows_bash())"
        if ($LASTEXITCODE -eq 3) {
            Write-Warning "[mu3] mu3-cli is not installed in $venv, so Git Bash was not looked up; delete the folder and enter again."
        }
        elseif (-not $bash) {
            Write-Warning '[mu3] Git Bash was not found; install Git for Windows to run the .sh scripts.'
        }
        else {
            # A bash that PATH already finds stays where it is: moving its folder, such as
            # Git\usr\bin, to the front would put Git's Unix tools ahead of Windows' own.
            $bashCommand = Get-Command -Name bash -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $bashCommand -or $bashCommand.Source -ne $bash) {
                $env:PATH = (Split-Path -Path $bash -Parent) + [IO.Path]::PathSeparator + $env:PATH
            }
        }
    }
    else {
        $bashCommand = Get-Command -Name bash -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($bashCommand) { $bash = $bashCommand.Source }
    }

    $env:MU3_DEV_ROOT = $root

    $global:Mu3DevState = @{
        Path = $savedPath
        Saved = $saved
        VenvDeactivate = $venvDeactivate
    }

    function global:deactivate {
        param([switch]$NonDestructive)

        $state = Get-Variable -Name Mu3DevState -Scope Global -ValueOnly -ErrorAction SilentlyContinue
        if ($state) {
            # The virtual environment's deactivate removes this function unless it is told to
            # be non-destructive as well.
            & $state.VenvDeactivate -NonDestructive:$NonDestructive
            foreach ($entry in $state.Saved.GetEnumerator()) {
                [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
            }

            $env:PATH = $state.Path
            Remove-Variable -Name Mu3DevState -Scope Global
        }

        # A function: path takes no scope qualifier (function:global:deactivate removes nothing);
        # from here, function:deactivate is this global function.
        if (-not $NonDestructive) {
            Remove-Item -Path function:deactivate -ErrorAction SilentlyContinue
        }
    }

    $pythonCommand = Get-Command -Name python -CommandType Application | Select-Object -First 1
    Write-Host "[mu3] Development environment for $root"
    Write-Host "[mu3]   python : $($pythonCommand.Source)"
    Write-Host "[mu3]   bash   : $(if ($bash) { $bash } else { 'not found' })"
    Write-Host "[mu3]   .env.local: $(if ($loaded.Count) { $loaded -join ', ' } else { 'no variables' })"
    Write-Host "[mu3] Leave with: deactivate"
} $PSScriptRoot
