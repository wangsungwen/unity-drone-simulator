param(
    [string]$Sdk = "$PSScriptRoot/../../.build_artifacts/unity-tools/android-sdk",
    [string]$Jdk = 'C:/Program Files/Java/jdk-11.0.32.1'
)
$ErrorActionPreference = 'Stop'
$env:JAVA_HOME = $Jdk
$sdkRoot = (Resolve-Path -LiteralPath $Sdk).Path
$buildTools = Join-Path $sdkRoot 'build-tools/34.0.0'
$androidJar = Join-Path $sdkRoot 'platforms/android-34/android.jar'
$minimumApiJar = Join-Path $sdkRoot 'platforms/android-21/android.jar'
$output = Join-Path $PSScriptRoot '../Builds/AndroidLegacyRemote'
$work = Join-Path $output ('work/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force "$work/classes", "$work/dex", "$work/gen" | Out-Null
function Run([string]$Exe, [string[]]$Arguments) {
    & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Exe failed with exit code $LASTEXITCODE" }
}
Run "$buildTools/aapt.exe" @('package','-f','-m','-M',"$PSScriptRoot/AndroidManifest.xml",'-S',"$PSScriptRoot/res",'-I',$androidJar,'-J',"$work/gen",'-F',"$work/resources.apk")
$sources = @(Get-ChildItem "$PSScriptRoot/src" -Filter '*.java' -Recurse | ForEach-Object FullName)
Run "$Jdk/bin/javac.exe" (@('-encoding','UTF-8','-source','8','-target','8','-bootclasspath',$minimumApiJar,'-d',"$work/classes") + $sources)
Run "$Jdk/bin/jar.exe" @('cf',"$work/classes.jar",'-C',"$work/classes",'.')
Run "$buildTools/d8.bat" @('--min-api','21','--lib',$androidJar,'--output',"$work/dex", "$work/classes.jar")
Copy-Item -LiteralPath "$work/resources.apk" -Destination "$work/unsigned.apk" -Force
Push-Location "$work/dex"
try { Run "$buildTools/aapt.exe" @('add',"$work/unsigned.apk",'classes.dex') } finally { Pop-Location }
Run "$buildTools/zipalign.exe" @('-f','4',"$work/unsigned.apk", "$work/aligned.apk")
$key = Join-Path $env:USERPROFILE '.android/debug.keystore'
if (-not (Test-Path -LiteralPath $key)) { throw 'Android debug.keystore missing. Provide a test signing key before building.' }
$apk = Join-Path $output 'DroneLegacyRemote.apk'
Run "$buildTools/apksigner.bat" @('sign','--ks',$key,'--ks-key-alias','androiddebugkey','--ks-pass','pass:android','--key-pass','pass:android','--v1-signing-enabled','true','--v2-signing-enabled','true','--out',$apk,"$work/aligned.apk")
Run "$buildTools/apksigner.bat" @('verify','--verbose','--min-sdk-version','21',$apk)
$badging = & "$buildTools/aapt.exe" dump badging $apk
if ($LASTEXITCODE -ne 0 -or -not ($badging -match "sdkVersion:'21'") -or -not ($badging -match "versionName='1.0.0'")) { throw 'APK compatibility metadata check failed' }
if ($badging -match 'native-code:') { throw 'Unexpected native CPU dependency' }
Copy-Item -LiteralPath "$PSScriptRoot/README.md" -Destination "$output/安裝說明.md" -Force
$zip = Join-Path $output '../DroneLegacyRemote-v1.0.0-Android5.zip'
Compress-Archive -Path $apk,"$output/安裝說明.md" -DestinationPath $zip -Force
Get-FileHash -LiteralPath $apk,$zip -Algorithm SHA256 | Select-Object Path,Hash | ConvertTo-Json | Set-Content -Encoding UTF8 "$output/checksums.json"
Write-Output "Built and verified: $apk"
Write-Output "Packaged: $zip"
