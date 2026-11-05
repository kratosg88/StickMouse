param([Parameter(Mandatory=$true)][string]$KeyPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security
$root=Split-Path $PSScriptRoot -Parent
$path=[IO.Path]::GetFullPath($KeyPath)
if($path.StartsWith(([IO.Path]::GetFullPath($root)+[IO.Path]::DirectorySeparatorChar),[StringComparison]::OrdinalIgnoreCase)){throw 'Store the signing key outside the distributable/source directory.'}
if(Test-Path -LiteralPath $path){throw 'Signing key already exists. It will not be replaced.'}
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider 3072
$rsa.PersistKeyInCsp=$false
try {
    $private=[Text.Encoding]::UTF8.GetBytes($rsa.ToXmlString($true))
    $encrypted=[Security.Cryptography.ProtectedData]::Protect($private,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
    [Array]::Clear($private,0,$private.Length)
    New-Item -ItemType Directory -Force (Split-Path $path)|Out-Null
    [IO.File]::WriteAllBytes($path,$encrypted)
    $trust=@{Repository='kratosg88/StickMouse';PublicKey=$rsa.ToXmlString($false)}|ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $root 'UpdateTrust.json'),$trust,(New-Object Text.UTF8Encoding $false))
    'Signing identity created. Private key is protected for this Windows user; only the public key goes into clients.'
}finally{$rsa.Dispose()}
