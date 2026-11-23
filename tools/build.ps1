param([string]$Compiler = "$PSScriptRoot\inno\ISCC.exe")
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    dotnet run --project tests\RobotLoop.Checks.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Checks failed' }
    dotnet publish RobotLoop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:NuGetAudit=false -o dist
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    if (-not (Test-Path -LiteralPath $Compiler)) { throw "Inno Setup compiler missing: $Compiler" }
    & $Compiler installer\RobotLoop.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
} finally { Pop-Location }
