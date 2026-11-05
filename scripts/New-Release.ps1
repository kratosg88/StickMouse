param([Parameter(Mandatory=$true)][string]$Version,[Parameter(Mandatory=$true)][string]$KeyPath,[Parameter(Mandatory=$true)][string]$OutputDirectory,[string]$Notes='StickMouse improvements and fixes.')
$ErrorActionPreference='Stop'
$dotnet=(Get-Command dotnet.exe -ErrorAction SilentlyContinue).Source
if(-not $dotnet -or -not (& $dotnet --list-sdks)){$dotnet=Join-Path $env:USERPROFILE '.codex/tools/dotnet-sdk-8/dotnet.exe'}
if(-not (Test-Path -LiteralPath $dotnet)){throw 'Install the .NET 8 SDK or put dotnet.exe on PATH to publish a release.'}
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Use a three-part release version, such as 1.1.1.'}
Add-Type -AssemblyName System.Security,System.IO.Compression.FileSystem
$root=Split-Path $PSScriptRoot -Parent
$trust=Get-Content -Raw (Join-Path $root 'UpdateTrust.json')|ConvertFrom-Json
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp=$false
try {
    $secret=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes([IO.Path]::GetFullPath($KeyPath)),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
    try{$rsa.FromXmlString([Text.Encoding]::UTF8.GetString($secret))}finally{[Array]::Clear($secret,0,$secret.Length)}
    if($rsa.ToXmlString($false) -ne $trust.PublicKey){throw 'Signing key does not match the public key shipped in the application.'}
    & $dotnet build (Join-Path $root 'StickMouse.csproj') -c Release "-p:Version=$Version" --nologo
    if($LASTEXITCODE -ne 0){throw 'Release build failed.'}
    & $dotnet (Join-Path $root 'bin/Release/net8.0-windows/StickMouse.dll') --self-test
    if($LASTEXITCODE -ne 0){throw 'Release self-tests failed.'}
    $output=[IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Force $output|Out-Null
    $zip=Join-Path $output 'StickMouse-Portable.zip'
    if(Test-Path -LiteralPath $zip){throw 'Output package already exists. Choose a new release directory.'}
    $stage=Join-Path $output ('stage-'+[guid]::NewGuid().ToString('N'))
    $files=@('Launch.cmd','Setup.ps1','README.md','bin/Release/net8.0-windows/StickMouse.dll','bin/Release/net8.0-windows/StickMouse.deps.json','bin/Release/net8.0-windows/StickMouse.runtimeconfig.json')
    foreach($file in $files){$target=Join-Path $stage $file;New-Item -ItemType Directory -Force (Split-Path $target)|Out-Null;Copy-Item -LiteralPath (Join-Path $root $file) -Destination $target}
    [IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip)
    $manifest=@{Version=$Version;PackageUrl="https://github.com/$($trust.Repository)/releases/download/v$Version/StickMouse-Portable.zip";Sha256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash;Size=(Get-Item -LiteralPath $zip).Length;Notes=$Notes}|ConvertTo-Json -Compress
    $payload=[Text.Encoding]::UTF8.GetBytes($manifest)
    $signature=$rsa.SignData($payload,'SHA256')
    $envelope=@{Payload=[Convert]::ToBase64String($payload);Signature=[Convert]::ToBase64String($signature)}|ConvertTo-Json -Compress
    [IO.File]::WriteAllText((Join-Path $output 'update.json'),$envelope,(New-Object Text.UTF8Encoding $false))
    & $dotnet (Join-Path $root 'bin/Release/net8.0-windows/StickMouse.dll') --verify-release $output
    if($LASTEXITCODE -ne 0){throw 'Client-side release verification failed. Do not publish.'}
    "Release ready: upload StickMouse-Portable.zip and update.json to the public GitHub release tagged v$Version; mark it Latest. Never upload the signing key."
}finally{$rsa.Dispose()}
