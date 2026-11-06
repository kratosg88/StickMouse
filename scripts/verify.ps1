$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$logs=Join-Path $root '.verify'
New-Item -ItemType Directory -Force $logs | Out-Null
$log=Join-Path $logs 'build.log'
& dotnet build (Join-Path $root 'StickMouse.csproj') -c Release --nologo *> $log
if ($LASTEXITCODE -ne 0) { Get-Content $log; throw 'Build failed' }
$dll=Join-Path $root 'bin\Release\net8.0-windows\StickMouse.dll'
& dotnet $dll --self-test *> (Join-Path $logs 'self-test.log')
if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $logs 'self-test.log'); $fatal=Join-Path (Split-Path $dll) '.verify\runtime.log'; if(Test-Path $fatal){Get-Content $fatal}; throw 'Self-test failed' }
foreach($file in @('self-test.txt','interface-test.txt','input-test.txt','update-test.txt','tray-test.txt','mapping-test.txt','mapping-ui-test.txt')) { Get-Content (Join-Path (Split-Path $dll) $file) }
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'Setup.ps1') -SelfTest *> (Join-Path $logs 'setup-test.log')
if($LASTEXITCODE -ne 0) { Get-Content (Join-Path $logs 'setup-test.log'); throw 'Setup tests failed' }
Get-Content (Join-Path $logs 'setup-test.log')
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'Setup.ps1') -CheckOnly *> (Join-Path $logs 'prerequisites.json')
if($LASTEXITCODE -ne 0){throw 'Prerequisite check failed'}
& dotnet $dll --preview (Join-Path $root 'preview.png')
if($LASTEXITCODE -ne 0){throw 'UI render failed'}
& dotnet $dll --connected-preview --preview (Join-Path $logs 'connected-preview.png')
if($LASTEXITCODE -ne 0){throw 'Connected UI render failed'}
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify-startup.ps1') *> (Join-Path $logs 'startup-test.log')
if($LASTEXITCODE -ne 0){Get-Content (Join-Path $logs 'startup-test.log');throw 'Native startup verification failed'}
Get-Content (Join-Path $logs 'startup-test.log')
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify-shortcut.ps1') *> (Join-Path $logs 'shortcut-test.log')
if($LASTEXITCODE -ne 0){Get-Content (Join-Path $logs 'shortcut-test.log');throw 'Desktop shortcut verification failed'}
Get-Content (Join-Path $logs 'shortcut-test.log')
& dotnet $dll --mapping-preview --preview (Join-Path $logs 'mapping-preview.png')
if($LASTEXITCODE -ne 0){throw 'Mapping UI render failed'}
& dotnet $dll --mapping-preview --mapping-bottom-preview --preview (Join-Path $logs 'mapping-bottom-preview.png')
if($LASTEXITCODE -ne 0){throw 'Mapping bottom UI render failed'}
Write-Output "VERIFY OK mode=build-input-settings-setup-render; log=$log"

