# Plan
Current release: v1.1.3 desktop shortcut and quick launch. Preserve L3/R3 clicks, stopped controller startup, automatic tray icon, offline use and signed six-file OTA compatibility.

Completed: setup creates a gamepad desktop shortcut after successful handoff; shortcut opens or restores the UI and skips the ready-check screen when local requirements pass. Uses the Windows Desktop known folder, repairs owned shortcuts and preserves unrelated links. OTA/direct startup repairs the shortcut asynchronously. No login autostart, certificate trust changes or driver installation.

Verification: Release build and motion/input/settings/OTA/setup/tray tests; real HWND repeat-launch and explicit-open smoke; workspace-only COM shortcut creation/readback/collision and icon extraction. Release packaging verifies its signed envelope with the client. Remaining manual gates: physical XInput controller, redirected real Desktop/Explorer icon placement, clean Windows runtime install/UAC, multi-monitor DPI and older-client OTA process restart. Keep the extracted portable folder in place.
