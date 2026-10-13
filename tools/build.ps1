param(
    [Parameter(Mandatory=$true)][string]$Unity,
    [ValidateSet('Windows','Android','IOS')][string]$Target = 'Windows'
)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $Unity)) { throw "Unity executable does not exist: $Unity" }
$buildTarget = @{ Windows='Win64'; Android='Android'; IOS='iOS' }[$Target]
New-Item -ItemType Directory -Force -Path (Join-Path $project 'Builds') | Out-Null
$log = Join-Path $project "Builds/build-$Target.log"
$arguments = @('-batchmode','-nographics','-quit','-projectPath',('"' + $project + '"'),
    '-buildTarget',$buildTarget,'-executeMethod',"DroneTrainer.Editor.TrainerBuild.$Target",'-logFile',('"' + $log + '"'))
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Unity build failed. See $log" }
Write-Output "Build finished. See $log and Builds/$Target."
