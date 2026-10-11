Option Explicit
Dim shell, fso, folder, part, python, candidate
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
folder = fso.GetParentFolderName(WScript.ScriptFullName)
python = ""
For Each part In Split(shell.Environment("PROCESS")("PATH"), ";")
    If Len(part) > 0 Then
        candidate = fso.BuildPath(Replace(part, """", ""), "pythonw.exe")
        If fso.FileExists(candidate) Then
            python = candidate
            Exit For
        End If
    End If
Next
If Len(python) = 0 Then
    MsgBox "Python could not be found. Install Python with Tkinter and add it to PATH.", 48, "Pomodoro Bar"
Else
    shell.CurrentDirectory = folder
    shell.Run """" & python & """ """ & fso.BuildPath(folder, "timer.pyw") & """", 0, False
End If
