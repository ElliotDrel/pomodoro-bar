' Launch installation/verification PowerShell from Task Scheduler without a console.
' Usage: wscript.exe background.ps1.vbs "absolute-path-to-script.ps1"
Option Explicit
Dim shell, command, script
If WScript.Arguments.Count <> 1 Then WScript.Quit 2
script = WScript.Arguments(0)
Set shell = CreateObject("WScript.Shell")
command = """" & shell.ExpandEnvironmentStrings("%SystemRoot%") & "\System32\WindowsPowerShell\v1.0\powershell.exe"" -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File """ & script & """"
WScript.Quit shell.Run(command, 0, True)
