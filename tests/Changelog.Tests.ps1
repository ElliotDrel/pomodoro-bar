$ErrorActionPreference = 'Stop'
$validator = Join-Path (Split-Path $PSScriptRoot) 'scripts/changelog.ps1'
$source = [IO.File]::ReadAllText((Join-Path (Split-Path $PSScriptRoot) 'CHANGELOG.md'))
$directory = Join-Path ([IO.Path]::GetTempPath()) ('Pomodoro-Changelog-' + [Guid]::NewGuid().ToString('N'))
$fixture = Join-Path $directory 'CHANGELOG.md'
$count = 0
function Reject([string]$content, [string]$message) {
    [IO.File]::WriteAllText($fixture, $content)
    try { & $validator -Version 1.1.4 -ChangelogPath $fixture | Out-Null }
    catch { if ($_.Exception.Message -like $message) { $script:count++; return }; throw }
    throw "Validator accepted invalid input: $message"
}
New-Item -ItemType Directory -Path $directory | Out-Null
Push-Location $directory
try {
    [IO.File]::WriteAllText($fixture, $source)
    & $validator -Version 1.1.4 -ChangelogPath $fixture -OutputPath (Join-Path $directory 'notes.md')
    $notes = [IO.File]::ReadAllText((Join-Path $directory 'notes.md'))
    if ($notes -notmatch '^## \[1.1.4\]' -or $notes -match '\[Unreleased\]|## \[1.1.3\]') { throw 'Wrong version was extracted.' }; $count++
    Reject ($source.Replace('## [Unreleased]', '## Pending')) '*needs an Unreleased section*'
    Reject ($source + "`n## [1.1.4] - 2026-10-11`n- Duplicate") '*Duplicate changelog entry*'
    Reject ($source.Replace('## [1.1.4] - 2026-10-11', '## [1.1.4] - invalid')) '*Invalid version/date heading*'
    Reject "## [Unreleased]`n## [1.1.4] - 2026-10-11`n" '*Empty changelog entry*'
    Reject "## [Unreleased]`n## [1.0.0] - 2026-10-11`n- Initial release`n" '*Missing dated release entry*'
    [IO.File]::WriteAllText($fixture, $source)
    git init -q
    git config user.name 'Changelog fixture'
    git config user.email 'fixture@example.invalid'
    git add CHANGELOG.md
    git commit -qm 'Initial fixture'
    $base = (git rev-parse HEAD).Trim()
    [IO.File]::WriteAllText((Join-Path $directory 'change.txt'), 'A change without a log entry')
    git add change.txt
    git commit -qm 'Missing log fixture'
    try { & $validator -Version 1.1.4 -ChangelogPath $fixture -BaseRef $base | Out-Null; throw 'Missing log update was accepted.' }
    catch { if ($_.Exception.Message -notlike '*Changes require an update*') { throw }; $count++ }
    [IO.File]::AppendAllText($fixture, "`n<!-- Fixture log updated. -->`n")
    git add CHANGELOG.md
    git commit -qm 'Log updated fixture'
    & $validator -Version 1.1.4 -ChangelogPath $fixture -BaseRef $base
    $count++
    Write-Host "PASS: $count changelog assertions."
} finally {
    Pop-Location
    $resolved = (Resolve-Path -LiteralPath $directory).Path
    if ($resolved -ne [IO.Path]::GetFullPath($directory) -or (Split-Path $resolved -Leaf) -notmatch '^Pomodoro-Changelog-[a-f0-9]{32}$') { throw 'Unexpected fixture cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
