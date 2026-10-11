param(
    [Parameter(Mandatory=$true)][string]$InstallerPath,
    [Parameter(Mandatory=$true)][string]$ExpectedVersion,
    [string]$NativeSettingsPath,
    [string]$ResultPath
)
$ErrorActionPreference = 'Stop'
try {
    & (Join-Path $PSScriptRoot 'verify-installation.ps1') -PreflightOnly
    $local = [Environment]::GetFolderPath('LocalApplicationData')
    $data = Join-Path $local 'PomodoroBar.Data/settings.json'
    if ($NativeSettingsPath -and -not (Test-Path -LiteralPath $data)) {
        New-Item -ItemType Directory -Path (Split-Path $data) -Force | Out-Null
        Copy-Item -LiteralPath $NativeSettingsPath -Destination $data
    }
    $installer = Start-Process -FilePath $InstallerPath -ArgumentList '--silent' -WindowStyle Hidden -Wait -PassThru
    if ($installer.ExitCode -ne 0) { throw "Installer exited $($installer.ExitCode)" }
    $verified = & (Join-Path $PSScriptRoot 'verify-installation.ps1') -ExpectedVersion $ExpectedVersion
    $launcher = Join-Path $local 'PomodoroBar/Pomodoro Bar.exe'
    $shell = New-Object -ComObject WScript.Shell
    $desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Pomodoro Timer.lnk'
    $links = @($desktopLink)
    $pinned = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Microsoft/Internet Explorer/Quick Launch/User Pinned/TaskBar'
    Get-ChildItem -LiteralPath $pinned -Filter '*.lnk' -ErrorAction SilentlyContinue | ForEach-Object {
        $candidate = $shell.CreateShortcut($_.FullName)
        if ($candidate.TargetPath -match 'PomodoroBar') { $links += $_.FullName }
    }
    foreach ($path in $links) {
        $link = $shell.CreateShortcut($path)
        $link.TargetPath = $launcher; $link.Arguments = ''; $link.WorkingDirectory = Split-Path $launcher
        $link.IconLocation = $verified.PhysicalExecutable + ',0'; $link.Description = 'Pomodoro Bar - native Windows timer'
        $link.Save()
        $check = $shell.CreateShortcut($path)
        if ($check.TargetPath -ne $launcher -or $check.IconLocation -ne ($verified.PhysicalExecutable+',0')) { throw "Shortcut mismatch: $path" }
    }
    Start-Process -FilePath $launcher -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $running = Get-Process PomodoroBar -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $verified.PhysicalExecutable } | Select-Object -First 1
        if (-not $running) { Start-Sleep -Milliseconds 250 }
    } while (-not $running -and [DateTime]::UtcNow -lt $deadline)
    if (-not $running) { throw 'Installed app did not start from the verified installation.' }
    $result = @{ Success=$true; Version=$verified.Version; PhysicalExecutable=$verified.PhysicalExecutable; Launcher=$launcher; Shortcuts=$links; ProcessId=$running.Id }
} catch {
    $result = @{ Success=$false; Error=$_.Exception.Message }
    if ($ResultPath) { $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ResultPath -Encoding UTF8 }
    throw
}
if ($ResultPath) { $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ResultPath -Encoding UTF8 }
$result | ConvertTo-Json -Depth 4
