@echo off
setlocal
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Setup.ps1"
if errorlevel 1 (
 echo StickMouse setup could not start. Run Setup.ps1 with Windows PowerShell for details.
 pause
)
