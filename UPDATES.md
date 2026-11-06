# Publishing StickMouse updates

The public repository is https://github.com/kratosg88/StickMouse. Clients fetch the stable asset `https://github.com/kratosg88/StickMouse/releases/latest/download/update.json`. Each release contains **StickMouse-Portable.zip** and **update.json**. GitHub's Latest release must be a published stable release, not a draft/prerelease.

1. Keep the existing `UpdateTrust.json` public key. Its matching private key is Windows-user-protected, stored outside this source/distribution folder. Never upload the private key or regenerate the public key for routine releases. This build's local key is in the workspace's `work/keys/stickmouse-release.dpapi`; signing requires the same Windows user/profile. Back up that profile and encrypted key securely. Losing the key requires a manually distributed new trust identity.
2. Change code, update version/docs, run `powershell -ExecutionPolicy Bypass -File scripts/verify.ps1`.
3. Run `powershell -ExecutionPolicy Bypass -File scripts/New-Release.ps1 -Version 1.1.1 -KeyPath <absolute-private-key-path> -OutputDirectory <new-empty-release-directory> -Notes 'Changes in this release'`.
4. The script builds the requested version, runs client self-tests, packages only known application files, signs metadata with SHA-256/RSA-PKCS1, and verifies the signed release through the client's production verifier. Inspect the results before upload.
5. In GitHub create release tag **v1.1.1** on the matching source commit. Upload the two generated assets, copy the release notes, and publish as **Latest**. Future clients check automatically on launch/every six hours; no account, credentials or GitHub API token is stored in clients.
6. Smoke-test an older updater-enabled client: check, download, stop mouse control, Restart & update, confirm version/settings. Use a disposable installation first. Never test forced termination on a user's working install.

`Initialize-Signing.ps1 -KeyPath <outside-source-path>` is for a new product signing identity only. It writes a DPAPI-protected private key and public UpdateTrust.json; it refuses to overwrite an existing key. Key material is never placed in client packages. It is not an Authenticode Windows code-signing certificate; normal Windows reputation prompts may still apply.

Update protocol v1 intentionally permits six application file paths and the current net8.0-windows runtime layout. A runtime-major migration or new package file requires a compatible bridge release or manual installer, not simply uploading a changed ZIP. Caught file-write failures roll back; power-loss recovery is manual using the retained backup. No forced minimum version or mandatory update gate exists.

References: https://docs.github.com/en/repositories/releasing-projects-on-github/linking-to-releases and https://learn.microsoft.com/dotnet/standard/security/cryptographic-signatures

v1.1.2 adds visible-window startup confirmation and existing-window restore. The original six-file update layout and OTA trust identity are retained. New-Release.ps1 optionally accepts CertificateThumbprint for Authenticode staging signatures before ZIP/hash/signing; omit it for the current free GitHub build. SIGNING.md documents development-only self-signing and certificate requirements.

Final v1.1.2 combines startup recovery with automatic tray operation and gamepad branding. The package retains six known file slots, so v1.1.0/v1.1.1 clients can accept it. Native icon resources are embedded in the DLL/apphost build; no extra client asset file is required.

v1.1.3 creates a gamepad desktop shortcut after setup and repairs it during direct/OTA startup. Desktop quick launch checks local requirements then opens/restores the main UI. Shortcut failures are nonfatal SM115. Six-file package and existing OTA signing identity remain compatible. Keep the extracted folder in place.

v1.2.0 adds persistent button/trigger mappings, configurable cursor/scroll stick roles, scroll speed, middle/Back/Forward clicks, double-click, drag toggle, wheel and direction/precision/pause actions. L3/R3 defaults are unchanged; right stick defaults to scrolling. Mapping editor pauses control and applies a draft explicitly. Five-button accepted-transition tracking preserves failed releases. Older preferences migrate; the signed six-file OTA package is unchanged.
