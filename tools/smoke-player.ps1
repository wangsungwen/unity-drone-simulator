param([string]$Player = '')
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
if (-not $Player) { $Player = Join-Path $project 'Builds/Windows/DroneTrainingLab.exe' }
if (-not (Test-Path -LiteralPath $Player)) { throw 'Build Windows first.' }
$log = Join-Path $project 'TestResults/player-startup.log'
New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null
$process = Start-Process -FilePath $Player -ArgumentList @('-batchmode','-nographics','-logFile',('"' + $log + '"')) -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Seconds 8
    if ($process.HasExited) { throw ('Player exited unexpectedly: ' + $process.ExitCode) }
    $content = Get-Content -LiteralPath $log -Raw
    if ($content -match '(?m)(NullReferenceException|MissingReferenceException|TypeLoadException|DllNotFoundException|Unhandled Exception|ArgumentException)') {
        throw "Player startup failed. See $log"
    }
    if ($content -notmatch 'Initialize engine version') { throw 'Unity player did not initialize.' }
    Write-Output 'PASS: built Windows player starts without reported exceptions (headless startup only)'
} finally {
    # This test owns only this process. Never stop other user-launched Unity apps.
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
}
