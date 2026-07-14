' Launch the Clawdmeter tray in a fully-detached hidden window.
' The .vbs is the primary double-click target — Windows executes
' WScript with no parent console, so it doesn't share std handles
' with the daemon. The pythonw child is then hidden + detached and
' the .vbs exits cleanly. The .vbs auto-deletes itself on success.
Option Explicit
Dim fso, shell, repoRoot, pyexe, tray, vbs

Set fso    = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")

' The .vbs lives in <repo>/scripts/windows/ — walk up two levels to
' reach the repository root. Use the canonical FileSystemObject so the
' path is always absolute (no MSYS / Cygwin / UNC ambiguity).
repoRoot = fso.GetParentFolderName(WScript.ScriptFullName)
repoRoot = fso.GetParentFolderName(repoRoot)

pyexe = repoRoot & "\.venv\Scripts\pythonw.exe"
tray  = repoRoot & "\daemon\tray_windows.py"

If Not fso.FileExists(pyexe) Then
    WScript.Echo "Python venv not found: " & pyexe
    WScript.Quit 1
End If
If Not fso.FileExists(tray) Then
    WScript.Echo "Tray script not found: " & tray
    WScript.Quit 1
End If

' Run returns immediately (bWaitOnReturn=False), with window hidden
' (bShow=0 == SW_HIDE). The pythonw inherits a fresh null std set so
' there is nothing to wait on.
shell.Run """" & pyexe & """ """ & tray & """", 0, False

' Self-delete: arrange to remove this .vbs after the script returns.
' FSO.DeleteFile is immediate (works since the script has already
' finished reading the file into memory and is now in tear-down).
On Error Resume Next
fso.DeleteFile WScript.ScriptFullName
On Error Goto 0
