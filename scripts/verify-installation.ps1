param([switch]$PreflightOnly, [string]$ExpectedVersion)
$ErrorActionPreference = 'Stop'
if (-not ('PomodoroInstallPaths' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class PomodoroInstallPaths {
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern int GetCurrentPackageFullName(ref uint length,StringBuilder name);
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode,SetLastError=true)] public static extern uint GetFinalPathNameByHandle(SafeFileHandle file,StringBuilder name,uint length,uint flags);
}
'@
}
$length = [uint32]0
$packageResult = [PomodoroInstallPaths]::GetCurrentPackageFullName([ref]$length,$null)
if ($packageResult -ne 15700) {
    throw 'Packaged process detected. Do not install or verify Prod here: AppData may be redirected. Run this script from an ordinary Windows PowerShell session outside Codex.'
}
$local = [Environment]::GetFolderPath('LocalApplicationData')
# Child processes can report no package identity while their file operations
# still inherit redirection. Verify a newly written file's physical location.
$probe = Join-Path $local ('.PomodoroBar-install-probe-' + [Guid]::NewGuid().ToString('N') + '.tmp')
$stream = [IO.File]::Create($probe)
try {
    $buffer = New-Object Text.StringBuilder 2048
    if ([PomodoroInstallPaths]::GetFinalPathNameByHandle($stream.SafeFileHandle,$buffer,2048,0) -eq 0) { throw 'Could not resolve installation preflight probe.' }
    $physicalProbe = $buffer.ToString() -replace '^\\\\\?\\',''
    if (-not $physicalProbe.Equals([IO.Path]::GetFullPath($probe),[StringComparison]::OrdinalIgnoreCase)) {
        throw "AppData writes are redirected to $physicalProbe. Run installation from an ordinary Windows session outside Codex."
    }
} finally { $stream.Dispose(); Remove-Item -LiteralPath $probe -ErrorAction SilentlyContinue }
if ($PreflightOnly) { return }
$exe = Join-Path $local 'PomodoroBar/current/PomodoroBar.exe'
$stream = [IO.File]::OpenRead($exe)
try {
    $buffer = New-Object Text.StringBuilder 2048
    if ([PomodoroInstallPaths]::GetFinalPathNameByHandle($stream.SafeFileHandle,$buffer,2048,0) -eq 0) { throw 'Could not resolve physical installation path.' }
    $physical = $buffer.ToString() -replace '^\\\\\?\\',''
    if (-not $physical.Equals([IO.Path]::GetFullPath($exe),[StringComparison]::OrdinalIgnoreCase)) { throw "Redirected installation: $physical" }
} finally { $stream.Dispose() }
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $local 'PomodoroBar/current/PomodoroBar.dll')).ProductVersion.Split('+')[0]
if ($ExpectedVersion -and $version -ne $ExpectedVersion) { throw "Expected $ExpectedVersion, installed $version" }
[pscustomobject]@{ PhysicalExecutable=$physical; Version=$version; Packaged=$false }
