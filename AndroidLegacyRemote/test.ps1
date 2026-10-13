param([string]$Jdk = 'C:/Program Files/Java/jdk-11.0.32.1')
$ErrorActionPreference = 'Stop'
$work = Join-Path $PSScriptRoot '../Builds/AndroidLegacyRemote/tests'
New-Item -ItemType Directory -Force $work | Out-Null
$json = Join-Path $work 'json.jar'
# Desktop test runtime only. The APK uses Android's built-in org.json implementation.
if (-not (Test-Path -LiteralPath $json)) {
    Invoke-WebRequest 'https://repo.maven.apache.org/maven2/org/json/json/20240303/json-20240303.jar' -OutFile $json
}
if ((Get-FileHash $json -Algorithm SHA256).Hash -ne '3CF6CD6892E32E2B4C1C39E0F52F5248A2F5B37646FDFBB79A66B46B618414ED') { throw 'Test dependency hash mismatch' }
& "$Jdk/bin/javac.exe" -encoding UTF-8 -source 8 -target 8 -cp $json -d $work "$PSScriptRoot/src/tw/traininglab/legacyremote/RemoteClient.java" "$PSScriptRoot/tests/ProtocolTest.java"
if ($LASTEXITCODE -ne 0) { throw 'Test compile failed' }
& "$Jdk/bin/java.exe" -cp "$work;$json" tw.traininglab.legacyremote.ProtocolTest
if ($LASTEXITCODE -ne 0) { throw 'Protocol tests failed' }
