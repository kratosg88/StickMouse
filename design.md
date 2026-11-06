# Design
Charcoal #14171A canvas, #1C2126 surfaces, warm-white primary text, muted slate secondary text, mint #9DDABE accent. Segoe UI in pixel-based logical typography; all paint/layout coordinates use DeviceDpi/96. Minimum content width 800 logical pixels; smaller windows scroll instead of clipping. Initial window fits the working area. Cross-monitor DPI changes relayout controls; physical mixed-DPI validation remains pending.

Compact brand header; left column groups controller illustration, two-line connection status and L3/R3 reference; right column groups settings with consistent tracks and value chips. Start is visibly disabled with No controller detected. Connected state enables mint Start; active state becomes Stop. No automatic activation on hotplug. Inversion has a matching switch. Values/disabled state are communicated without relying on color alone.

Sliders have track-only pointer hit areas, keyboard/focus/hover support and accessible current values. Button and switch preserve keyboard navigation. Setup WPF screen shares palette, hierarchy and action treatment; installed/missing states are explicit, missing runtime is selectable and Next initiates installation. Native window chrome is retained; dark title bar requested where supported.

Artifacts: preview.png (disconnected), .verify/connected-preview.png (simulated connection), setup-preview.png (simulated missing runtime).

Updates: compact Check for updates control and status row under settings; unavailable checks/downloads show inline status without modal blocking or disabling Start. Automatic checks after startup/every six hours, manual retry, Download then Restart & update. Version shown in the row. Height includes the footer; smaller work areas scroll.

Publisher attribution: small by CoderKnight text beneath the app wordmark and CODERKNIGHT in the setup brand line.

Gamepad branding replaces the plus mark in header, tray, taskbar/titlebar and executable icon resource. Tray startup is automatic with no Start in tray control. Open/Start/Pause/Quit are available in the gamepad context menu; one tray hint explains recovery. Main window preview intentionally renders UI even though normal startup is hidden.
