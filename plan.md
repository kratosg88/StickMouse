# Plan
Task: v1.2.0 app/runtime and native settings UI. Add controller-to-mouse customization while preserving L3/R3 defaults, no hotplug autostart, explicit Start, tray/desktop recovery, offline use and signed six-file OTA compatibility.

Scope: Bindings.cs model, ControllerMouse.cs input engine, MouseButtonLatch.cs five-button state, Program.cs native dispatch, MappingWindow.cs settings editor, Interface.cs persistence and Controls.cs current mapping reference. No drivers, keyboard macros, game hooks or login startup changes.

Completed: current UI captured; mappings and draft/apply editor implemented; settings migration, all 16 input routes, shared ownership, trigger hysteresis, edge/repeat timing and release cleanup verified. scripts/verify.ps1 passed with zero build warnings; native main/editor top/editor bottom screenshots reviewed. Package and publish after client signature/archive verification. Stitch unavailable: fresh OAuth token could not be obtained; use existing palette/layout brief.

Remaining manual gates: physical XInput controller, protected-window input, clean Windows runtime/UAC, redirected real Desktop and mixed-DPI displays. Tests inject event sinks, never control the real pointer.
