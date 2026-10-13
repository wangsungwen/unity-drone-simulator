$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
Add-Type -Path @((Join-Path $project 'Assets/DroneTrainer/Scripts/Core/ControlLogic.cs'), (Join-Path $project 'Assets/DroneTrainer/Scripts/Core/ExamRules.cs'), (Join-Path $project 'tests/CoreTests.cs'))
[CoreTests]::Run()
# PowerShell 7 ships Roslyn. This checks C# syntax only, not Unity API binding.
$syntaxFiles = Get-ChildItem -LiteralPath (Join-Path $project 'Assets') -Filter '*.cs' -Recurse
$parseErrors = @()
foreach ($file in $syntaxFiles) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($file.FullName))
    foreach ($diagnostic in $tree.GetDiagnostics()) {
        if ($diagnostic.Severity.ToString() -eq 'Error') { $parseErrors += "$($file.Name): $diagnostic" }
    }
}
if ($parseErrors.Count -gt 0) { throw ($parseErrors -join "`n") }
Write-Output "PASS: parsed $($syntaxFiles.Count) C# files (syntax only; Unity compilation still required)"
