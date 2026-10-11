param([string]$ExpectedVersion)
$ErrorActionPreference = 'Stop'
$installed = & (Join-Path $PSScriptRoot 'verify-installation.ps1') -ExpectedVersion $ExpectedVersion
$target = $installed.PhysicalExecutable
$local = [Environment]::GetFolderPath('LocalApplicationData')
$stub = Join-Path $local 'PomodoroBar/Pomodoro Bar.exe'
$desktop = [Environment]::GetFolderPath('Desktop')
$startMenu = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs'
$canonical = @((Join-Path $desktop 'Pomodoro Bar.lnk'), (Join-Path $startMenu 'Pomodoro Bar.lnk'))
$roots = @($desktop, [Environment]::GetFolderPath('CommonDesktopDirectory'), (Join-Path $env:USERPROFILE 'Desktop'), [Environment]::GetFolderPath('StartMenu'), [Environment]::GetFolderPath('CommonStartMenu')) | Select-Object -Unique
$pinned = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Microsoft/Internet Explorer/Quick Launch/User Pinned/TaskBar'
$shell = New-Object -ComObject WScript.Shell
$changed = @(); $retired = @()
foreach ($path in $canonical) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Installer shortcut missing: $path" }
    $link = $shell.CreateShortcut($path)
    if ($link.TargetPath -notin @($target, $stub) -or $link.Arguments) { throw "Unexpected shortcut ownership: $path" }
    # Match Velopack's native main-executable target. Keep its existing AppUserModelID.
    $link.TargetPath = $target; $link.WorkingDirectory = Split-Path $target
    $link.IconLocation = $target + ',0'; $link.Save(); $changed += $path
}
foreach ($root in $roots) {
    Get-ChildItem -LiteralPath $root -Filter '*.lnk' -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
        $path = $_.FullName
        if ($path -in $canonical -or $path -match '(?i)backup|BEFORE-|\.bak') { return }
        $link = $shell.CreateShortcut($path)
        if ($link.TargetPath -notin @($target, $stub) -or $link.Arguments) { return }
        # Retain confirmed duplicate launchers outside Desktop/Start Menu indexing.
        $archive = Join-Path $local 'PomodoroBar.Data/retired-shortcuts'
        New-Item -ItemType Directory -Path $archive -Force | Out-Null
        $destination = Join-Path $archive ([Guid]::NewGuid().ToString('N') + '-' + $_.Name)
        Move-Item -LiteralPath $path -Destination $destination
        $retired += [pscustomobject]@{Original=$path;Retained=$destination}
    }
}
Get-ChildItem -LiteralPath $pinned -Filter '*.lnk' -ErrorAction SilentlyContinue | ForEach-Object {
    $link = $shell.CreateShortcut($_.FullName)
    if ($link.TargetPath -in @($target, $stub) -and -not $link.Arguments) {
        $link.TargetPath=$target; $link.WorkingDirectory=Split-Path $target; $link.IconLocation=$target+',0'; $link.Save(); $changed += $_.FullName
    }
}
$verified = & (Join-Path $PSScriptRoot 'verify-installation.ps1') -ExpectedVersion $ExpectedVersion -VerifyShortcuts
[pscustomobject]@{Target=$target;Changed=$changed;Retired=$retired;Verified=$verified}
