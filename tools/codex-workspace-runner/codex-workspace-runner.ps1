<#
.SYNOPSIS
Runs a file-backed Codex workspace task and records exact usage from JSONL events.

.DESCRIPTION
Reads the task prompt from `codex-runner-input.md` next to this script, runs
`codex exec --json` from the current workspace, and stores input/output copies
plus JSONL and usage summary files under `temp/codex-runner-cache/<date>/`.
User-level Codex config is ignored by default so globally configured MCP
servers are not loaded. By default, Codex starts from an isolated temp
directory so repository AGENTS guidance is not auto-loaded for simple tasks.

.EXAMPLE
.\tools\codex-workspace-runner\codex-workspace-runner.ps1

.EXAMPLE
.\tools\codex-workspace-runner\codex-workspace-runner.ps1 `
  -Sandbox workspace-write

.EXAMPLE
.\tools\codex-workspace-runner\codex-workspace-runner.ps1 `
  -Model gpt-5.4-mini `
  -ReasoningEffort low

.EXAMPLE
.\tools\codex-workspace-runner\codex-workspace-runner.ps1 `
  -LoadRepositoryInstructions
#>

[CmdletBinding()]
param(
    [ValidateSet("read-only", "workspace-write", "danger-full-access")]
    [string] $Sandbox = "read-only",

    [string] $Model = "gpt-5.5",

    [ValidateSet("default", "low", "medium", "high", "xhigh")]
    [string] $ReasoningEffort = "medium",

    [string] $CodexCommand = "codex",

    [string] $CacheRoot = "temp/codex-runner-cache",

    [switch] $LoadRepositoryInstructions,

    [string[]] $CodexArgs = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[Console]::InputEncoding = $utf8NoBom
[Console]::OutputEncoding = $utf8NoBom
$OutputEncoding = $utf8NoBom

$root = (Get-Location).ProviderPath
$scriptDir = Split-Path -Parent $PSCommandPath

function Resolve-WorkspacePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $root $Path))
}

function New-SafeTimestamp {
    param(
        [Parameter(Mandatory = $true)]
        [datetime] $Date
    )

    return $Date.ToString("HHmmss-fff")
}

function New-UsageTotals {
    [ordered]@{
        input_tokens            = 0
        cached_input_tokens     = 0
        output_tokens           = 0
        reasoning_output_tokens = 0
    }
}

function Add-UsageValue {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary] $Totals,

        [Parameter(Mandatory = $true)]
        [object] $Usage,

        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    if ($null -ne $Usage.PSObject.Properties[$Name] -and $null -ne $Usage.$Name) {
        $Totals[$Name] += [int64] $Usage.$Name
    }
}

function Format-UsageLine {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary] $Usage
    )

    $uncachedInput = [Math]::Max(0, [int64] $Usage.input_tokens - [int64] $Usage.cached_input_tokens)
    return "input=$($Usage.input_tokens), cached_input=$($Usage.cached_input_tokens), uncached_input=$uncachedInput, output=$($Usage.output_tokens), reasoning_output=$($Usage.reasoning_output_tokens)"
}

function Resolve-CodexCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $CommandName
    )

    if ([System.IO.Path]::IsPathRooted($CommandName) -and (Test-Path -LiteralPath $CommandName -PathType Leaf)) {
        return [System.IO.Path]::GetFullPath($CommandName)
    }

    $commandInfo = Get-Command $CommandName -ErrorAction SilentlyContinue
    if ($null -ne $commandInfo) {
        if (-not [string]::IsNullOrWhiteSpace($commandInfo.Source)) {
            return $commandInfo.Source
        }
        if ($commandInfo.PSObject.Properties["Path"] -and -not [string]::IsNullOrWhiteSpace($commandInfo.Path)) {
            return $commandInfo.Path
        }
        return $CommandName
    }

    if ($CommandName -ne "codex") {
        return $null
    }

    $candidates = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_CLI_PATH)) {
        $candidates.Add($env:CODEX_CLI_PATH)
    }

    $codexConfigPath = Join-Path $HOME ".codex\config.toml"
    if (Test-Path -LiteralPath $codexConfigPath -PathType Leaf) {
        $configLine = Select-String -LiteralPath $codexConfigPath -Pattern "^\s*CODEX_CLI_PATH\s*=" | Select-Object -First 1
        if ($null -ne $configLine -and $configLine.Line -match "CODEX_CLI_PATH\s*=\s*['""](?<path>[^'""]+)['""]") {
            $candidates.Add($Matches.path)
        }
    }

    $extensionRoot = Join-Path $HOME ".vscode\extensions"
    if (Test-Path -LiteralPath $extensionRoot -PathType Container) {
        Get-ChildItem -LiteralPath $extensionRoot -Directory -Filter "openai.chatgpt-*" -ErrorAction SilentlyContinue |
            ForEach-Object {
                $candidates.Add((Join-Path $_.FullName "bin\windows-x86_64\codex.exe"))
            }
    }

    $localCodexBinRoot = Join-Path $env:LOCALAPPDATA "OpenAI\Codex\bin"
    if (Test-Path -LiteralPath $localCodexBinRoot -PathType Container) {
        Get-ChildItem -LiteralPath $localCodexBinRoot -Directory -ErrorAction SilentlyContinue |
            ForEach-Object {
                $candidates.Add((Join-Path $_.FullName "codex.exe"))
            }
    }

    $windowsAppsCodex = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\codex.exe"
    $candidates.Add($windowsAppsCodex)

    foreach ($candidate in ($candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    return $null
}

$resolvedCodexCommand = Resolve-CodexCommand -CommandName $CodexCommand
if ([string]::IsNullOrWhiteSpace($resolvedCodexCommand)) {
    throw "Codex command not found. Install Codex or pass -CodexCommand with the full path to codex.exe."
}

$resolvedInputFile = [System.IO.Path]::GetFullPath((Join-Path $scriptDir "codex-runner-input.md"))
if (-not (Test-Path -LiteralPath $resolvedInputFile -PathType Leaf)) {
    throw "Input file not found: $resolvedInputFile"
}

$prompt = Get-Content -LiteralPath $resolvedInputFile -Raw -Encoding UTF8
if ([string]::IsNullOrWhiteSpace($prompt)) {
    throw "Input file is empty: $resolvedInputFile"
}

$startedAt = Get-Date
$dateFolderName = $startedAt.ToString("yyyy-MM-dd")
$timePrefix = New-SafeTimestamp -Date $startedAt
$resolvedCacheRoot = Resolve-WorkspacePath -Path $CacheRoot
$resolvedRunDir = Join-Path $resolvedCacheRoot $dateFolderName
New-Item -ItemType Directory -Force -Path $resolvedRunDir | Out-Null

$inputCopyPath = Join-Path $resolvedRunDir "${timePrefix}_input.md"
$resolvedOutputFile = Join-Path $resolvedRunDir "${timePrefix}_output.md"
$jsonlPath = Join-Path $resolvedRunDir "${timePrefix}.jsonl"
$stderrPath = Join-Path $resolvedRunDir "${timePrefix}.stderr.log"
$summaryPath = Join-Path $resolvedRunDir "${timePrefix}.summary.json"
$guidePath = Join-Path $scriptDir "README.md"

Set-Content -LiteralPath $inputCopyPath -Encoding UTF8 -Value $prompt

$codexWorkingRoot = $root
$effectivePrompt = $prompt
if (-not $LoadRepositoryInstructions) {
    $codexWorkingRoot = Join-Path ([System.IO.Path]::GetTempPath()) "codex-workspace-runner\$dateFolderName\$timePrefix"
    New-Item -ItemType Directory -Force -Path $codexWorkingRoot | Out-Null

    $effectivePrompt = @"
You are running from an isolated Codex runner directory to avoid automatic repository instruction loading.

Workspace root:
$root

Rules for this runner task:
- Use the workspace root path above when the task asks for repository files.
- Do not read `AGENTS.md`, `.github/copilot-instructions.md`, or other repository instruction routers unless the user request explicitly needs repository coding, document editing, or instruction compliance.
- Prefer direct answers for smoke tests and simple questions.
- Keep file reads narrow and avoid broad logs or generated files.

Task request:
$prompt
"@
}

$arguments = @("exec", "--json", "--ignore-user-config")
if ($LoadRepositoryInstructions) {
    $arguments += @("--cd", $root)
}
else {
    $arguments += @("--skip-git-repo-check", "--cd", $codexWorkingRoot, "--add-dir", $root)
}
$arguments += @("--sandbox", $Sandbox, "--model", $Model)
if ($ReasoningEffort -ne "default") {
    $arguments += @("--config", "model_reasoning_effort=`"$ReasoningEffort`"")
}
$arguments += $CodexArgs + @($effectivePrompt)
$usageTotals = New-UsageTotals
$turnCount = 0
$lastAgentMessage = $null

Write-Host "Workspace: $root"
Write-Host "Codex working root: $codexWorkingRoot"
Write-Host "Input: $resolvedInputFile"
Write-Host "Input copy: $inputCopyPath"
Write-Host "Output: $resolvedOutputFile"
Write-Host "JSONL: $jsonlPath"
Write-Host "Summary: $summaryPath"
Write-Host "Sandbox: $Sandbox"
Write-Host "Model: $Model"
Write-Host "Reasoning effort: $ReasoningEffort"
Write-Host "User config: ignored"
Write-Host "Repository instructions: $(if ($LoadRepositoryInstructions) { 'loaded from workspace' } else { 'isolated by default' })"
Write-Host "Codex command: $resolvedCodexCommand"

& $resolvedCodexCommand @arguments 2> $stderrPath | ForEach-Object {
    $line = $_.ToString()
    Add-Content -LiteralPath $jsonlPath -Encoding UTF8 -Value $line

    $event = $null
    try {
        $event = $line | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        Write-Host $line
    }

    if ($null -ne $event) {
        if ($event.type -eq "item.completed" -and $null -ne $event.item) {
            if ($event.item.type -eq "agent_message" -and $null -ne $event.item.text) {
                $lastAgentMessage = [string] $event.item.text
            }
        }

        if ($event.type -eq "turn.completed" -and $null -ne $event.usage) {
            $turnCount += 1
            Add-UsageValue -Totals $usageTotals -Usage $event.usage -Name "input_tokens"
            Add-UsageValue -Totals $usageTotals -Usage $event.usage -Name "cached_input_tokens"
            Add-UsageValue -Totals $usageTotals -Usage $event.usage -Name "output_tokens"
            Add-UsageValue -Totals $usageTotals -Usage $event.usage -Name "reasoning_output_tokens"
        }
    }
}

$exitCode = $LASTEXITCODE
$endedAt = Get-Date
$outputWritten = $false

if (-not [string]::IsNullOrWhiteSpace($lastAgentMessage)) {
    Set-Content -LiteralPath $resolvedOutputFile -Encoding UTF8 -Value $lastAgentMessage
    $outputWritten = $true
}

$summary = [ordered]@{
    started_at       = $startedAt.ToUniversalTime().ToString("o")
    ended_at         = $endedAt.ToUniversalTime().ToString("o")
    duration_seconds = [Math]::Round(($endedAt - $startedAt).TotalSeconds, 3)
    exit_code        = $exitCode
    workspace        = $root
    input_file       = $resolvedInputFile
    input_copy_file  = $inputCopyPath
    output_file      = $resolvedOutputFile
    output_written   = $outputWritten
    ignore_user_config = $true
    codex_command    = $resolvedCodexCommand
    codex_working_root = $codexWorkingRoot
    load_repository_instructions = [bool] $LoadRepositoryInstructions
    sandbox          = $Sandbox
    model            = $Model
    reasoning_effort = $ReasoningEffort
    codex_args       = $CodexArgs
    turn_count       = $turnCount
    usage            = $usageTotals
    jsonl_path       = $jsonlPath
    stderr_path      = $stderrPath
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host ""
if ($outputWritten) {
    Write-Host "Final message written: $resolvedOutputFile"
}
else {
    Write-Warning "No final agent message was captured; output file was not written."
}

Write-Host "Token usage:"
Write-Host (Format-UsageLine -Usage $usageTotals)
Write-Host "Turns: $turnCount"
Write-Host "Summary: $summaryPath"
if (Test-Path -LiteralPath $guidePath -PathType Leaf) {
    Write-Host "Result guide: $guidePath"
}

exit $exitCode
