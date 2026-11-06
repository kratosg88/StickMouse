param([Parameter(Mandatory=$true)][string]$ApplicationRoot,[Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$CertificateThumbprint,[switch]$AllowUntrustedDevelopment,[string]$TimestampServer='https://timestamp.digicert.com',[string]$SignToolPath)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($ApplicationRoot)
$cert=Get-Item -LiteralPath ('Cert:\CurrentUser\My\'+$CertificateThumbprint) -ErrorAction Stop
if(-not $cert.HasPrivateKey -or $cert.NotAfter -lt (Get-Date) -or $cert.NotBefore -gt (Get-Date)){throw 'A current code-signing certificate with an accessible private key is required.'}
if($cert.EnhancedKeyUsageList.ObjectId -notcontains '1.3.6.1.5.5.7.3.3'){throw 'Certificate is not authorized for code signing.'}
$chain=New-Object Security.Cryptography.X509Certificates.X509Chain
try{$trusted=$chain.Build($cert)}finally{$chain.Dispose()}
if(-not $trusted -and -not $AllowUntrustedDevelopment){throw 'Public signing requires a trusted certificate chain. Self-signed certificates are development-only.'}
if(-not $SignToolPath){
 $sdk='C:\Program Files (x86)\Windows Kits\10\bin'
 $SignToolPath=Get-ChildItem -LiteralPath $sdk -Directory|Where-Object {$_.Name -match '^10\.'}|Sort-Object Name -Descending|ForEach-Object {Join-Path $_.FullName 'x64\signtool.exe'}|Where-Object {Test-Path -LiteralPath $_}|Select-Object -First 1
}
if(-not $SignToolPath -or -not (Test-Path -LiteralPath $SignToolPath)){throw 'Install the Windows SDK Signing Tools or supply -SignToolPath.'}
$timestamp=[uri]$TimestampServer
if($timestamp.Scheme -ne 'https'){throw 'Use an HTTPS timestamp service.'}
$dll=Join-Path $root 'bin\Release\net8.0-windows\StickMouse.dll'
$setup=Join-Path $root 'Setup.ps1'
if(-not (Test-Path $dll) -or -not (Test-Path $setup)){throw 'Signing staging folder is incomplete.'}
$signArgs=@('sign','/s','My','/sha1',$CertificateThumbprint,'/fd','SHA256')
if(-not $AllowUntrustedDevelopment){$signArgs+=@('/tr',$TimestampServer,'/td','SHA256')}
& $SignToolPath @signArgs $dll
if($LASTEXITCODE -ne 0){throw 'Authenticode DLL signing failed.'}
$params=@{FilePath=$setup;Certificate=$cert;HashAlgorithm='SHA256';IncludeChain='NotRoot'}
if(-not $AllowUntrustedDevelopment){$params.TimestampServer=$TimestampServer}
Set-AuthenticodeSignature @params|Out-Null
foreach($file in @($dll,$setup)){
 $signature=Get-AuthenticodeSignature -LiteralPath $file
 if($signature.SignerCertificate.Thumbprint -ne $cert.Thumbprint){throw 'Signer verification failed.'}
 if(-not $AllowUntrustedDevelopment -and ($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate)){throw 'Public signature must validate and include a trusted timestamp.'}
}
if($AllowUntrustedDevelopment){'DEVELOPMENT SIGNATURE ONLY: no public trust or SmartScreen guarantee. No root certificates were installed.'}else{'Verified timestamped Authenticode signatures; publisher: '+$cert.GetNameInfo([Security.Cryptography.X509Certificates.X509NameType]::SimpleName,$false)}
