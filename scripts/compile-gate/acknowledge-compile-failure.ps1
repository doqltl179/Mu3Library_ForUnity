param(
    [Parameter(Mandatory = $true)]
    [string]$TriageNote,
    [string]$StatusFile
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")

if ([string]::IsNullOrWhiteSpace($StatusFile)) {
    $StatusFile = Join-Path $repoRoot "tasks\compile-status.json"
}

if ([string]::IsNullOrWhiteSpace($TriageNote)) {
    throw "TriageNote must contain a non-empty summary of the recorded failure."
}

$trimmedTriageNote = $TriageNote.Trim()
$todoPath = Join-Path $repoRoot "tasks\todo.md"

if (-not (Test-Path $todoPath)) {
    throw "tasks/todo.md must exist before a failed compile can be acknowledged."
}

if (-not (Select-String -Path $todoPath -SimpleMatch $trimmedTriageNote -Quiet)) {
    throw "TriageNote must already be recorded in tasks/todo.md before acknowledgment."
}

if (-not (Test-Path $StatusFile)) {
    throw "Compile status file does not exist: $StatusFile"
}

$status = Get-Content -Path $StatusFile -Raw | ConvertFrom-Json

if ($status.state -notin @("failed", "failed-stale")) {
    throw "Only failed compile states can be acknowledged. Current state: $($status.state)"
}

$status.state = "triaged-failed"
$status | Add-Member -NotePropertyName triageAcknowledgedAt -NotePropertyValue ((Get-Date).ToString("o")) -Force
$status | Add-Member -NotePropertyName triageNote -NotePropertyValue $trimmedTriageNote -Force

$status | ConvertTo-Json -Depth 6 | Set-Content -Path $StatusFile -Encoding utf8
Write-Host "Compile failure acknowledged as triaged-failed."