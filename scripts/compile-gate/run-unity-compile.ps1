param(
    [ValidateSet("builtin", "urp", "both")]
    [string]$Target = "both",
    [string]$EditorPath,
    [string]$StatusFile,
    [string]$LogFile = "-"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")

if ([string]::IsNullOrWhiteSpace($StatusFile)) {
    $StatusFile = Join-Path $repoRoot "tasks\compile-status.json"
}

$projectMap = @{
    builtin = Join-Path $repoRoot "UnityProject_BuiltIn"
    urp = Join-Path $repoRoot "UnityProject_URP"
}

function Resolve-UnityEditorPath {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (-not (Test-Path $RequestedPath)) {
            throw "Unity editor path does not exist: $RequestedPath"
        }

        return (Resolve-Path $RequestedPath).Path
    }

    $candidateRoots = @(
        "C:\Program Files\Unity\Hub\Editor",
        "C:\Program Files\Unity"
    )

    foreach ($root in $candidateRoots) {
        if (-not (Test-Path $root)) {
            continue
        }

        $editorDirectories = Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending

        foreach ($directory in $editorDirectories) {
            $unityExe = Join-Path $directory.FullName "Editor\Unity.exe"
            if (Test-Path $unityExe) {
                return [string]$unityExe
            }
        }

    }

    throw "Could not find a Unity editor installation. Pass -EditorPath explicitly."
}

function Write-CompileStatus {
    param(
        [string]$State,
        [string]$TargetLabel,
        [string]$UnityEditorPath,
        [object[]]$Results,
        [string]$StartedAt,
        [string]$CompletedAt = $null,
        [string]$ActiveTarget = $null,
        [int]$ProcessId = 0,
        [string]$ProcessStartedAt = $null,
        [string]$LogPath = $null,
        [string]$StaleReason = $null
    )

    $statusDir = Split-Path -Parent $StatusFile
    if (-not (Test-Path $statusDir)) {
        New-Item -Path $statusDir -ItemType Directory -Force | Out-Null
    }

    [pscustomobject]@{
        state = $State
        targetLabel = $TargetLabel
        unityEditorPath = $UnityEditorPath
        startedAt = $StartedAt
        completedAt = $CompletedAt
        activeTarget = $ActiveTarget
        processId = $ProcessId
        processStartedAt = $ProcessStartedAt
        logPath = $LogPath
        staleReason = $StaleReason
        results = $Results
    } |
        ConvertTo-Json -Depth 5 |
        Set-Content -Path $StatusFile -Encoding utf8
}

function Get-UnityProcessIds {
    $unityProcesses = Get-CimInstance -ClassName Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue
    if (-not $unityProcesses) {
        return @()
    }

    return @($unityProcesses | ForEach-Object { [int]$_.ProcessId })
}

function Wait-ForUnityBatchProcess {
    param(
        [int[]]$KnownProcessIds,
        [string]$LogMarker
    )

    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        $candidate = Get-CimInstance -ClassName Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
            Where-Object {
                $_.CommandLine -and
                $_.CommandLine -like "*$LogMarker*" -and
                $_.ProcessId -notin $KnownProcessIds
            } |
            Sort-Object CreationDate -Descending |
            Select-Object -First 1

        if ($candidate) {
            return Get-Process -Id $candidate.ProcessId -ErrorAction Stop
        }

        [System.Threading.Thread]::Sleep(200)
    }

    throw "Could not locate the Unity batch process for log marker '$LogMarker'."
}

function Find-OpenUnityEditorProcess {
    param([string]$ProjectPath)

    $normalizedProjectPath = [System.IO.Path]::GetFullPath($ProjectPath).Replace("\", "/")

    return Get-CimInstance -ClassName Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.CommandLine -and
            $_.CommandLine -notmatch "(?i)-batchmode" -and
            $_.CommandLine.Replace("\", "/") -like "*$normalizedProjectPath*"
        } |
        Select-Object -First 1
}

$unityExe = Resolve-UnityEditorPath -RequestedPath $EditorPath
$targets = if ($Target -eq "both") { @("builtin", "urp") } else { @($Target) }
$targetLabel = ($targets -join ",")
$startedAt = (Get-Date).ToString("o")
$logTimestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$defaultLogDirectory = Join-Path $repoRoot "log\compile-gate"
$results = @()
$compileProcess = Get-Process -Id $PID

$overallExitCode = 0

try {
    foreach ($entry in $targets) {
        $projectPath = $projectMap[$entry]
        $openEditorProcess = Find-OpenUnityEditorProcess -ProjectPath $projectPath
        if ($openEditorProcess) {
            $message = "Unity editor is already open for '{0}' (PID {1}). Close the existing editor instance before running batch compile verification." -f $entry, $openEditorProcess.ProcessId
            Write-Host $message

            $results += [pscustomobject]@{
                target = $entry
                projectPath = $projectPath
                exitCode = 1
                logPath = $null
                error = $message
                blockingProcessId = [int]$openEditorProcess.ProcessId
            }

            if ($overallExitCode -eq 0) {
                $overallExitCode = 1
            }

            break
        }

        $logPath = if ($LogFile -eq "-") {
            if (-not (Test-Path $defaultLogDirectory)) {
                New-Item -Path $defaultLogDirectory -ItemType Directory -Force | Out-Null
            }

            Join-Path $defaultLogDirectory ("compile-{0}-{1}.log" -f $logTimestamp, $entry)
        }
        elseif ($targets.Count -gt 1) {
            $directory = [System.IO.Path]::GetDirectoryName($LogFile)
            if ([string]::IsNullOrWhiteSpace($directory)) {
                $directory = (Get-Location).Path
            }

            if (-not (Test-Path $directory)) {
                New-Item -Path $directory -ItemType Directory -Force | Out-Null
            }

            $baseName = [System.IO.Path]::GetFileNameWithoutExtension($LogFile)
            $extension = [System.IO.Path]::GetExtension($LogFile)
            Join-Path $directory ("{0}-{1}{2}" -f $baseName, $entry, $extension)
        }
        else {
            $directory = [System.IO.Path]::GetDirectoryName($LogFile)
            if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path $directory)) {
                New-Item -Path $directory -ItemType Directory -Force | Out-Null
            }

            $LogFile
        }

        $args = @(
            "-batchmode",
            "-projectPath", $projectPath,
            "-executeMethod", "CompileGateBatchEntryPoint.Run",
            "-logFile", $logPath
        )

        Write-Host ("Starting compile gate for {0} using {1}" -f $entry, $unityExe)
        Write-CompileStatus -State "running" -TargetLabel $targetLabel -UnityEditorPath $unityExe -Results $results -StartedAt $startedAt -ActiveTarget $entry -ProcessId $compileProcess.Id -ProcessStartedAt $compileProcess.StartTime.ToString("o") -LogPath $logPath

        $existingUnityProcessIds = Get-UnityProcessIds
        Start-Process -FilePath $unityExe -ArgumentList $args | Out-Null
        $unityProcess = Wait-ForUnityBatchProcess -KnownProcessIds $existingUnityProcessIds -LogMarker ([System.IO.Path]::GetFileName($logPath))
        $null = $unityProcess.Handle
        $null = $unityProcess.WaitForExit()
        $unityProcess.Refresh()
        $exitCode = $unityProcess.ExitCode
        Write-Host ("Compile gate finished for {0} with exit code {1}" -f $entry, $exitCode)

        $retainedLogPath = $null
        if (Test-Path $logPath) {
            Get-Content -Path $logPath
            $retainedLogPath = $logPath
        }

        $results += [pscustomobject]@{
            target = $entry
            projectPath = $projectPath
            exitCode = $exitCode
            logPath = $retainedLogPath
        }

        if ($exitCode -ne 0 -and $overallExitCode -eq 0) {
            $overallExitCode = $exitCode
        }
    }
}
catch {
    $overallExitCode = if ($overallExitCode -ne 0) { $overallExitCode } else { 1 }
    $results += [pscustomobject]@{
        target = if ($entry) { $entry } else { "unknown" }
        projectPath = if ($projectPath) { $projectPath } else { $null }
        exitCode = $null
        logPath = if ($logPath -and (Test-Path $logPath)) { $logPath } else { $null }
        error = $_.Exception.Message
    }
    Write-Host ("Compile gate failed before completion: {0}" -f $_.Exception.Message)
}

$completedAt = (Get-Date).ToString("o")
$finalState = if ($overallExitCode -eq 0) { "succeeded" } else { "failed" }
$failedResult = $results | Where-Object { $_.exitCode -ne 0 } | Select-Object -First 1

Write-CompileStatus -State $finalState -TargetLabel $targetLabel -UnityEditorPath $unityExe -Results $results -StartedAt $startedAt -CompletedAt $completedAt -ActiveTarget $(if ($failedResult) { $failedResult.target } else { $null }) -LogPath $(if ($failedResult) { $failedResult.logPath } else { $null })
Write-Host ("Compile gate final state: {0}" -f $finalState)
exit $overallExitCode