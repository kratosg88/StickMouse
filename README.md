# StickMouse

A Windows desktop companion for an Xbox / XInput-compatible controller.

## Open
Extract the complete folder, then double-click **Launch.cmd**. Every launch opens a ready check before starting the app. Keep Setup.ps1 and the bin folder beside the launcher.

The ready check detects Windows, processor architecture, application files and .NET 8 Desktop Runtime. If the runtime is missing, select its checkbox and click Next. Setup retrieves the current .NET 8 release from Microsoft, verifies SHA-512 and the Microsoft publisher signature, and runs its installer. Approve the Windows administrator prompt if asked. No restart is forced. Failed/cancelled installation stays on the setup screen with an error and can be retried. Installed components are skipped. No download occurs before Next; normal launches with a suitable runtime work offline. Windows itself must be upgraded/repaired separately when required.

## Controller
The app starts stopped. It polls for XInput controllers automatically. While disconnected it says **No controller detected** and disables **Start controller mouse**. Connect a controller, then click Start. Click Stop or press F8 to pause. Disconnecting stops control and releases held mouse buttons; reconnecting requires Start again. The app waits for L3 and R3 to be released before arming clicks.

- Left stick: move cursor.
- Press L3: left-click; hold to drag.
- Press R3: right-click.
- Triggers are unused.

Sensitivity sets nominal relative movement speed (Windows pointer settings also affect the result). Dead zone reduces drift. Response curve increases precision near the stick center. Vertical inversion and Reset defaults are available. Sliders support dragging, arrows, Home/End, keyboard focus and accessible values.

Changes apply immediately and save automatically after 450ms, with a final save on close. Preferences: `%LOCALAPPDATA%\StickMouse\settings.json`. Corrupt settings fall back to defaults with visible error code SM002.

## Compatibility and validation
Windows 10/11, Windows PowerShell 5.1/WPF supplied by Windows, and .NET 8 Desktop Runtime. No third-party packages, service, startup registration or driver installation. Native DualSense/DualShock input is **not supported directly**: use an already configured XInput compatibility setup. Other apps still receive controller input. Elevated/protected game windows may reject injected mouse events. No anti-cheat compatibility claim is made.

Build, motion math, input state/failure handling, settings/autosave, connection gating and offline setup tests pass. Native disconnected/connected UI renders were inspected; connected renders use simulated state. Physical USB/Bluetooth controller behavior, different-DPI monitors and an actual runtime download/install/UAC cycle still need end-to-end testing on suitable hardware/a clean Windows machine. The .NET 8 channel should be migrated before publishing a long-lived release; this prototype uses the installed .NET 8 SDK.

## Development
`dotnet build StickMouse.csproj -c Release`

`powershell -ExecutionPolicy Bypass -File scripts\verify.ps1`

Setup checks only: `powershell -ExecutionPolicy Bypass -File Setup.ps1 -CheckOnly`.

Setup UI without installation: `powershell -STA -ExecutionPolicy Bypass -File Setup.ps1 -PreviewMissing`.

Diagnostics: `%LOCALAPPDATA%\StickMouse\runtime.log` and `setup.log`. SM001 = rejected mouse input, SM002 = settings load failure, SM003 = settings save failure, SM104-SM112 = setup failure, SM900 = unexpected shutdown. Runtime diagnostics rotate at 256 KiB with one previous file. Installer files remain in a uniquely named StickMouse folder under Windows Temp for maintenance/diagnosis. No telemetry uploads.

Microsoft documentation: https://learn.microsoft.com/dotnet/core/install/windows
Release metadata: https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json

## Automatic updates
Updater-enabled clients check GitHub Releases in the background on launch and every six hours while open. Check for updates also works manually. No internet, timeout, 404, bad feed, bad signature or failed download prevents using the current version. This is pull-based delivery: closed/offline clients discover releases the next time they check. Earlier prototype builds without this updater need this version installed once.

Release metadata is signed with the owner's RSA key; clients pin the public key and repository. Newer versions only. Downloads are size-limited and SHA-256 verified. The user chooses Download, then Restart & update while controller mouse mode is stopped. Current settings in LocalAppData remain outside the application update. Detected write failures restore application files from backup. Backup/staging files remain in LocalAppData/StickMouse/updates for recovery; power loss/forced termination during replacement is not crash-atomic and may require manual restoration from backup. Protected/read-only installations must be updated from a user-writable folder. No admin update service or forced updates.

Publisher guide: UPDATES.md. Public source/release repository: https://github.com/kratosg88/StickMouse
