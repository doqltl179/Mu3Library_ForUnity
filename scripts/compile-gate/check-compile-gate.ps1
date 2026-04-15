$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$statusPath = Join-Path $repoRoot "tasks\compile-status.json"

if (-not (Test-Path $statusPath)) {
    [pscustomobject]@{ continue = $true } | ConvertTo-Json -Compress
    exit 0
}

try {
    $status = Get-Content -Path $statusPath -Raw | ConvertFrom-Json
}
catch {
    [pscustomobject]@{
        continue = $false
        stopReason = "Compile gate status file is unreadable."
        systemMessage = "Compile gate blocked progress because tasks/compile-status.json could not be parsed."
    } | ConvertTo-Json -Compress
    exit 2
}

if ($status.state -eq "running") {
    $process = $null
    if ($status.processId) {
        $process = Get-Process -Id $status.processId -ErrorAction SilentlyContinue
    }

    $processMatches = $false
    if ($process) {
        $expectedStart = $null
        if ($status.processStartedAt) {
            $expectedStart = [DateTime]::Parse($status.processStartedAt)
        }

        if ($expectedStart) {
            $processMatches = $process.StartTime.ToUniversalTime().ToString("o") -eq $expectedStart.ToUniversalTime().ToString("o")
        }
        else {
            $processMatches = $true
        }
    }

    if (-not $processMatches) {
        $status.state = "failed-stale"
        $status.completedAt = (Get-Date).ToString("o")
        $status.staleReason = "Compile gate recovered a stale running state because the tracked compile process was no longer active."
        $status | ConvertTo-Json -Depth 6 | Set-Content -Path $statusPath -Encoding utf8

        [pscustomobject]@{
            continue = $false
            stopReason = "Compile gate detected a stale running state. Triage the failed-stale result before continuing."
            systemMessage = "Recovered a stale compile gate state and marked it failed-stale. Review tasks/compile-status.json and record triage before starting the next unit."
        } | ConvertTo-Json -Compress
        exit 2
    }

    [pscustomobject]@{
        continue = $false
        stopReason = "Compile gate active until the current compile finishes."
        systemMessage = "A compile is still running for $($status.targetLabel). Wait until tasks/compile-status.json is updated before starting the next task."
    } | ConvertTo-Json -Compress
    exit 2
}

if ($status.state -in @("failed", "failed-stale")) {
    [pscustomobject]@{
        continue = $false
        stopReason = "Compile gate requires triage before the next unit can continue."
        systemMessage = "The latest compile gate result is $($status.state). Record the failure in tasks/todo.md and acknowledge it with scripts/compile-gate/acknowledge-compile-failure.ps1 before starting the next unit."
    } | ConvertTo-Json -Compress
    exit 2
}

[pscustomobject]@{ continue = $true } | ConvertTo-Json -Compress
exit 0