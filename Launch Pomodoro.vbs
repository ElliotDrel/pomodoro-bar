Option Explicit
Dim shell, fso, folder, executable, args, legacy
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
folder = fso.GetParentFolderName(WScript.ScriptFullName)
executable = shell.ExpandEnvironmentStrings("%LOCALAPPDATA%\PomodoroBar\current\PomodoroBar.exe")
If Not fso.FileExists(executable) Then
    MsgBox "Install Pomodoro Bar from https://github.com/ElliotDrel/pomodoro-bar/releases/latest", 48, "Pomodoro Bar"
Else
    shell.CurrentDirectory = folder
    args = """" & executable & """"
    legacy = fso.BuildPath(folder, "settings.json")
    If fso.FileExists(legacy) Then args = args & " --import-settings """ & legacy & """"
    shell.Run args, 1, False
End If
