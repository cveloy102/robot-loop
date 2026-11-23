$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$registry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{2496BC21-4BF2-42C1-8A25-3F99594A2819}_is1'
if (Test-Path $registry) { throw 'An existing installation was found; installer smoke test skipped to preserve it.' }
if (Get-Process RobotLoop -ErrorAction SilentlyContinue) { throw 'Close Robot Loop before the installer smoke test.' }
$target = Join-Path $PSScriptRoot 'installed-smoke'
if (Test-Path -LiteralPath $target) { throw 'Test install directory already exists.' }
$settingsFile = Join-Path $env:LOCALAPPDATA 'RobotLoop\settings.json'
$settingsHash = if (Test-Path $settingsFile) { (Get-FileHash $settingsFile).Hash } else { $null }
$installer = Join-Path $root 'dist\RobotLoop-Setup-1.2.3.exe'
$install = Start-Process -FilePath $installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',('/DIR="' + $target + '"'),('/LOG="' + (Join-Path $PSScriptRoot 'installer-smoke.log') + '"')) -WindowStyle Hidden -PassThru -Wait
if ($install.ExitCode -ne 0) { throw "Installer exited with $($install.ExitCode)" }
if ((Get-FileHash (Join-Path $target 'RobotLoop.exe')).Hash -ne (Get-FileHash (Join-Path $root 'dist\RobotLoop.exe')).Hash) { throw 'Installed executable differs from release.' }
$entry = Get-ItemProperty $registry
if ($entry.DisplayVersion -ne '1.2.3') { throw 'Incorrect uninstall entry version.' }
Write-Output 'PASS: offline install, release hash and Windows uninstall entry'
$uninstaller = Join-Path $target 'unins000.exe'
$uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -WindowStyle Hidden -PassThru -Wait
if ($uninstall.ExitCode -ne 0) { throw "Uninstaller exited with $($uninstall.ExitCode)" }
if (Test-Path (Join-Path $target 'RobotLoop.exe')) { throw 'Application executable was not removed.' }
if (Test-Path $registry) { throw 'Uninstall entry was not removed.' }
if ($settingsHash -and (Get-FileHash $settingsFile).Hash -ne $settingsHash) { throw 'User settings changed during installation test.' }
Write-Output 'PASS: uninstall removed application and registry entry; user settings preserved'
