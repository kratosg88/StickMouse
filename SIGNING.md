# Windows code signing and zero-cost distribution

Current choice: individual developer, zero spending, GitHub Releases. Public Authenticode trust is not configured. Publisher/company display metadata is CoderKnight; signed OTA manifests protect update integrity independently of Windows Authenticode or SmartScreen reputation.

A free self-signed certificate is useful for development but is not publicly trusted and will not remove warnings on other users' computers. This project does not install any certificates into Root, TrustedPeople or TrustedPublisher. The Microsoft Store MSIX route offers free Store-managed signing after developer onboarding/certification; Store packaging, listing and update policy are a separate distribution task.

## Optional development certificate
Run `powershell -ExecutionPolicy Bypass -File scripts/New-DevelopmentCertificate.ps1` only if you want a local development signing identity. It creates a non-exportable code-signing key in the current user's Personal/My store, valid for one year. It does not create public trust. Record the public thumbprint; never share private keys or PFX passwords in chat or GitHub. This optional script has been prepared, not executed.

## Optional Authenticode release signing
With an appropriate certificate and accessible private key already in CurrentUser/My, run New-Release.ps1 with `-CertificateThumbprint <thumbprint>` alongside its existing version/key/output arguments. Files are copied to release staging, Authenticode-signed, then ZIP/hash/OTA-signed. Only DLL and setup script are signed; CMD files cannot carry a normal Authenticode publisher identity. A future signed native bootstrapper would require a compatible distribution/OTA migration.

Sign-Release.ps1 locates the installed Windows SDK SignTool, requires code-signing EKU/private key/valid dates and a trusted chain, signs with SHA-256, requests timestamping and verifies signatures. For a local staging experiment only, `-AllowUntrustedDevelopment` skips public-trust/timestamp requirements; do not publish that experiment as a trusted release. The scripts never buy a certificate, change root trust or silently accept a weaker signing identity. No real certificate was available to run the successful public-signing path.

Official references:
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options
- https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart
- https://learn.microsoft.com/en-us/windows/desktop/seccrypto/signtool
- https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.security/set-authenticodesignature
