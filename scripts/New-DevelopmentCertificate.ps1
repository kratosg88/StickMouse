param([string]$Publisher='CoderKnight')
$ErrorActionPreference='Stop'
# Optional local testing only. This stores a key in Personal/My, never Root or TrustedPeople.
$cert=New-SelfSignedCertificate -Type CodeSigningCert -Subject ('CN='+$Publisher) -FriendlyName ($Publisher+' development signing only') -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(1)
[pscustomobject]@{Subject=$cert.Subject;Thumbprint=$cert.Thumbprint;Expires=$cert.NotAfter;Purpose='Development only. Not publicly trusted. No root trust installed.'}
