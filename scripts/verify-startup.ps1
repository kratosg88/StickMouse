$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$data=Join-Path $root '.verify/startup-test-data'
New-Item -ItemType Directory -Force $data|Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class StartupNative {
 [StructLayout(LayoutKind.Sequential)]public struct Rect{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern uint RegisterWindowMessage(string text);
 [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]public static extern bool ShowWindow(IntPtr h,int state);
 [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
}
"@
. (Join-Path $root 'Setup.ps1') -LoadFunctions
function Launch-Test { return (Start-StickMouse -Smoke) }
$first=$null
$tray=$null
try {
 $first=Launch-Test
 if(-not $first.NativeVisible -or $first.Status -ne 'visible'){throw 'Initial startup was not visible'}
 $handle=[IntPtr][long]$first.WindowHandle
 if(-not [StartupNative]::IsWindowVisible($handle)){throw 'Actual startup HWND is hidden'}
 [StartupNative]::SetThreadDpiAwarenessContext([IntPtr](-4))|Out-Null
 $rect=New-Object StartupNative+Rect
 [StartupNative]::GetWindowRect($handle,[ref]$rect)|Out-Null
 $bitmap=New-Object Drawing.Bitmap ($rect.Right-$rect.Left),($rect.Bottom-$rect.Top)
 $graphics=[Drawing.Graphics]::FromImage($bitmap);$dc=$graphics.GetHdc()
 try{if(-not [StartupNative]::PrintWindow($handle,$dc,0)){throw 'Native UI capture failed'}}finally{$graphics.ReleaseHdc($dc);$graphics.Dispose()}
 try{$bitmap.Save((Join-Path $data 'visible-window.png'))}finally{$bitmap.Dispose()}
 [StartupNative]::ShowWindow($handle,0)|Out-Null
 if([StartupNative]::IsWindowVisible($handle)){throw 'Failed to arrange hidden-window regression'}
 $second=Launch-Test
 if($second.Status -ne 'restored' -or $second.ProcessId -ne $first.ProcessId -or -not [StartupNative]::IsWindowVisible($handle)){throw 'Repeat launch did not restore the existing instance'}
 [StartupNative]::PostMessage($handle,[StartupNative]::RegisterWindowMessage('StickMouse.StartupSmoke.Quit'),[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
 try{$process=[Diagnostics.Process]::GetProcessById([int]$first.ProcessId);if(-not $process.WaitForExit(3000)){throw 'Test window did not quit'}}catch [ArgumentException]{}
 $first=$null
 $tray=Start-StickMouse -Smoke -TraySmoke
 if($tray.Status -ne 'tray' -or $tray.NativeVisible -or -not $tray.TrayIconVisible){throw 'Intentional tray startup was not acknowledged'}
 $reopened=Launch-Test
 if($reopened.Status -ne 'restored' -or $reopened.ProcessId -ne $tray.ProcessId -or -not $reopened.NativeVisible){throw 'Tray-hidden instance did not reopen'}
 $tray=$reopened
 [StartupNative]::PostMessage([IntPtr][long]$tray.WindowHandle,[StartupNative]::RegisterWindowMessage('StickMouse.StartupSmoke.Quit'),[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
 try{$process=[Diagnostics.Process]::GetProcessById([int]$tray.ProcessId);if(-not $process.WaitForExit(3000)){throw 'Tray test window did not quit'}}catch [ArgumentException]{}
 $tray=Start-StickMouse -Smoke -TraySmoke -OpenUi
 if($tray.Status -ne 'visible' -or -not $tray.NativeVisible){throw 'Explicit desktop quick launch did not open the main UI'}
 'PASS: visible HWND, native screenshot, repeat-launch restore, intentional tray startup acknowledgement, tray reopen in same process, explicit desktop launch opens UI' 
} finally {
 foreach($known in @($first,$tray)){if($known){[StartupNative]::PostMessage([IntPtr][long]$known.WindowHandle,[StartupNative]::RegisterWindowMessage('StickMouse.StartupSmoke.Quit'),[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null}}
}
