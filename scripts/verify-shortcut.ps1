$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. (Join-Path $root 'Setup.ps1') -LoadFunctions
$test=Join-Path $root ('.verify/shortcut-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $test|Out-Null
$first=Ensure-DesktopShortcut -DesktopDirectory $test
$second=Ensure-DesktopShortcut -DesktopDirectory $test
if($first -ne $second){throw 'Shortcut creation was not idempotent'}
$shell=New-Object -ComObject WScript.Shell
try {
 $link=$shell.CreateShortcut($first)
 try {
  if($link.TargetPath -ne (Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe')){throw 'Wrong shortcut target'}
  if($link.Arguments -notlike '*-QuickLaunch' -or $link.Arguments -notlike ('*"'+(Join-Path $root 'Setup.ps1')+'"*')){throw 'Shortcut does not invoke quick launch'}
  if($link.WorkingDirectory -ne $root -or $link.IconLocation -notlike '*StickMouse.dll,0'){throw 'Shortcut working directory/icon mismatch'}
 } finally {[Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)|Out-Null}
 $collision=Join-Path $test 'collision';New-Item -ItemType Directory $collision|Out-Null
 $unrelated=Join-Path $collision 'StickMouse.lnk';$other=$shell.CreateShortcut($unrelated)
 try{$other.TargetPath=Join-Path $env:SystemRoot 'System32/notepad.exe';$other.Description='Unrelated user shortcut';$other.Save()}finally{[Runtime.InteropServices.Marshal]::FinalReleaseComObject($other)|Out-Null}
 $created=Ensure-DesktopShortcut -DesktopDirectory $collision
 if([IO.Path]::GetFileName($created) -ne 'StickMouse - CoderKnight.lnk'){throw 'Shortcut name collision not handled'}
 $kept=$shell.CreateShortcut($unrelated)
 try{if($kept.Description -ne 'Unrelated user shortcut'){throw 'User shortcut overwritten'}}finally{[Runtime.InteropServices.Marshal]::FinalReleaseComObject($kept)|Out-Null}
} finally {[Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)|Out-Null}
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ShortcutIconProbe {
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]public static extern uint ExtractIconEx(string file,int index,out IntPtr large,out IntPtr small,uint count);
 [DllImport("user32.dll")]public static extern bool DestroyIcon(IntPtr handle);
}
"@
$large=[IntPtr]::Zero;$small=[IntPtr]::Zero
try{if([ShortcutIconProbe]::ExtractIconEx((Join-Path $root 'bin/Release/net8.0-windows/StickMouse.dll'),0,[ref]$large,[ref]$small,1) -lt 1){throw 'Gamepad icon is not extractable from shortcut resource'}}finally{if($large -ne [IntPtr]::Zero){[ShortcutIconProbe]::DestroyIcon($large)|Out-Null};if($small -ne [IntPtr]::Zero){[ShortcutIconProbe]::DestroyIcon($small)|Out-Null}}
'PASS: real .lnk creation/readback, quick-launch arguments, gamepad icon extraction, idempotency, unrelated shortcut preservation (workspace-only fixtures)'
