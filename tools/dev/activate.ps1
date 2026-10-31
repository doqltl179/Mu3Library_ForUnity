# Enters the repository's development environment in this PowerShell session:
#   - creates .venv at the repository root on first use and installs mu3-cli into it,
#   - activates that virtual environment,
#   - sets the variables in .env.local (see tools/dev/env.local.example),
#   - on Windows, puts Git Bash first on PATH so `bash ./test-unity.sh` runs.
#
# Dot-source it, from any folder:   . .\tools\dev\activate.ps1
# Leave it again with:              deactivate
#
# It runs inside the caller's session, so it sets neither Set-StrictMode nor
# $ErrorActionPreference: both would stay in effect at the prompt after it returns.

if ($MyInvocation.InvocationName -ne '.') {
    Write-Warning "Dot-source this script so it can change the session: . $($MyInvocation.MyCommand.Path)"
    return
}

if ($global:Mu3DevState) {
    deactivate
}

$Mu3DevRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Mu3DevOnWindows = $env:OS -eq 'Windows_NT'
$Mu3DevVenv = Join-Path $Mu3DevRoot '.venv'
$Mu3DevVenvScripts = if ($Mu3DevOnWindows) { Join-Path $Mu3DevVenv 'Scripts' } else { Join-Path $Mu3DevVenv 'bin' }

# --- Virtual environment -------------------------------------------------------------------
if (-not (Test-Path (Join-Path $Mu3DevVenvScripts 'Activate.ps1'))) {
    Write-Host "[mu3] Creating $Mu3DevVenv and installing mu3-cli..."
    $Mu3DevCli = Join-Path $Mu3DevRoot 'tools\cli'
    if (Get-Command uv -ErrorAction SilentlyContinue) {
        uv venv $Mu3DevVenv --prompt mu3 --quiet
        if ($LASTEXITCODE -eq 0) { uv pip install --python $Mu3DevVenv --quiet -e $Mu3DevCli }
    }
    else {
        python -m venv $Mu3DevVenv --prompt mu3
        if ($LASTEXITCODE -eq 0) { & (Join-Path $Mu3DevVenvScripts 'python') -m pip install --quiet -e $Mu3DevCli }
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Error "[mu3] Setting up $Mu3DevVenv failed; nothing was activated. Delete the folder and try again."
        return
    }
}

# --- What deactivate restores --------------------------------------------------------------
$Mu3DevSaved = @{ 'MU3_DEV_ROOT' = $env:MU3_DEV_ROOT }
$Mu3DevPath = $env:PATH

. (Join-Path $Mu3DevVenvScripts 'Activate.ps1')
$Mu3DevVenvDeactivate = (Get-Command -Name deactivate -CommandType Function).ScriptBlock

# --- .env.local ----------------------------------------------------------------------------
$Mu3DevLoaded = @()
$Mu3DevEnvFile = Join-Path $Mu3DevRoot '.env.local'
if (Test-Path -LiteralPath $Mu3DevEnvFile) {
    $Mu3DevLineNumber = 0
    foreach ($Mu3DevLine in Get-Content -LiteralPath $Mu3DevEnvFile -Encoding UTF8) {
        $Mu3DevLineNumber++
        $Mu3DevText = $Mu3DevLine.Trim()
        if (-not $Mu3DevText -or $Mu3DevText.StartsWith('#')) {
            continue
        }

        if ($Mu3DevText -notmatch '^(?:export\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$') {
            Write-Warning "[mu3] .env.local line ${Mu3DevLineNumber} is not KEY=VALUE; skipped."
            continue
        }

        $Mu3DevKey = $Matches[1]
        $Mu3DevValue = $Matches[2].Trim()
        if ($Mu3DevValue.Length -ge 2 -and $Mu3DevValue[0] -eq $Mu3DevValue[-1] -and ($Mu3DevValue[0] -eq '"' -or $Mu3DevValue[0] -eq "'")) {
            $Mu3DevValue = $Mu3DevValue.Substring(1, $Mu3DevValue.Length - 2)
        }

        if (-not $Mu3DevSaved.ContainsKey($Mu3DevKey)) {
            $Mu3DevSaved[$Mu3DevKey] = [Environment]::GetEnvironmentVariable($Mu3DevKey, 'Process')
        }

        [Environment]::SetEnvironmentVariable($Mu3DevKey, $Mu3DevValue, 'Process')
        $Mu3DevLoaded += $Mu3DevKey
    }
}

# --- Git Bash ------------------------------------------------------------------------------
# PowerShell on Windows has no bash on PATH, or finds WSL's, which cannot run the repository's
# scripts; Git for Windows ships the bash they are written for. These are the places mu3-cli
# looks too (tools/cli/README.md, "Bash on Windows").
$Mu3DevBash = $null
if ($Mu3DevOnWindows) {
    $Mu3DevGit = Get-Command git -ErrorAction SilentlyContinue
    $Mu3DevGitBin = @(
        $(if ($Mu3DevGit) { Join-Path (Split-Path (Split-Path $Mu3DevGit.Source)) 'bin' }),
        $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'Git\bin' }),
        $(if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} 'Git\bin' }),
        $(if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'Programs\Git\bin' })
    ) | Where-Object { $_ -and (Test-Path (Join-Path $_ 'bash.exe')) } | Select-Object -First 1

    if ($Mu3DevGitBin) {
        $env:PATH = "$Mu3DevGitBin;$env:PATH"
        $Mu3DevBash = Join-Path $Mu3DevGitBin 'bash.exe'
    }
    else {
        Write-Warning "[mu3] Git Bash was not found; install Git for Windows to run the .sh scripts."
    }
}
else {
    $Mu3DevBash = (Get-Command bash -ErrorAction SilentlyContinue).Source
}

$env:MU3_DEV_ROOT = $Mu3DevRoot

$global:Mu3DevState = @{
    Path = $Mu3DevPath
    Saved = $Mu3DevSaved
    VenvDeactivate = $Mu3DevVenvDeactivate
}

function global:deactivate {
    param([switch]$NonDestructive)

    $state = $global:Mu3DevState
    if ($state) {
        & $state.VenvDeactivate
        foreach ($entry in $state.Saved.GetEnumerator()) {
            [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
        }

        $env:PATH = $state.Path
        Remove-Variable -Name Mu3DevState -Scope Global
    }

    if (-not $NonDestructive) {
        Remove-Item -Path function:global:deactivate -ErrorAction SilentlyContinue
    }
}

Write-Host "[mu3] Development environment for $Mu3DevRoot"
Write-Host "[mu3]   python : $((Get-Command python).Source)"
Write-Host "[mu3]   bash   : $(if ($Mu3DevBash) { $Mu3DevBash } else { 'not found' })"
Write-Host "[mu3]   .env.local: $(if ($Mu3DevLoaded.Count) { $Mu3DevLoaded -join ', ' } else { 'no variables' })"
Write-Host "[mu3] Leave with: deactivate"

Remove-Variable -Name Mu3DevRoot, Mu3DevOnWindows, Mu3DevVenv, Mu3DevVenvScripts, Mu3DevCli, Mu3DevSaved, Mu3DevPath, Mu3DevVenvDeactivate, Mu3DevLoaded, Mu3DevEnvFile, Mu3DevLineNumber, Mu3DevLine, Mu3DevText, Mu3DevKey, Mu3DevValue, Mu3DevGit, Mu3DevGitBin, Mu3DevBash -ErrorAction SilentlyContinue
