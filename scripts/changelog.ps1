param(
    [string]$Version,
    [string]$BaseRef,
    [string]$OutputPath,
    [string]$ChangelogPath = (Join-Path (Split-Path $PSScriptRoot) 'CHANGELOG.md')
)
$ErrorActionPreference = 'Stop'
$text = [IO.File]::ReadAllText($ChangelogPath)
if ($text -notmatch '(?m)^## \[Unreleased\]\s*$') { throw 'Changelog needs an Unreleased section.' }
$headers = [regex]::Matches($text, '(?m)^## \[([^\]\r\n]+)\]([^\r\n]*)')
$seen = @{}
foreach ($header in $headers) {
    $name = $header.Groups[1].Value
    if ($seen.ContainsKey($name)) { throw "Duplicate changelog entry: $name" }
    $seen[$name] = $true
    if ($name -ne 'Unreleased') {
        if ($name -notmatch '^\d+\.\d+\.\d+$' -or $header.Groups[2].Value -notmatch '^ - \d{4}-\d{2}-\d{2}$') { throw "Invalid version/date heading: $($header.Value)" }
        $null = [DateTime]::ParseExact($header.Groups[2].Value.Substring(3), 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
        $start = $header.Index + $header.Length
        $next = [regex]::Match($text.Substring($start), '(?m)^## ')
        $body = if ($next.Success) { $text.Substring($start, $next.Index) } else { $text.Substring($start) }
        if ($body -notmatch '(?m)^- \S') { throw "Empty changelog entry: $name" }
    }
}
if ($BaseRef -and $BaseRef -notmatch '^0{40}$') {
    if ($BaseRef -notmatch '^[a-fA-F0-9]{40}$') { throw 'BaseRef must be a full commit SHA.' }
    $files = @(git diff --name-only $BaseRef HEAD --)
    if ($LASTEXITCODE -ne 0) { throw 'Could not compare the changelog with the base commit.' }
    if (@($files | Where-Object { $_ -ne 'CHANGELOG.md' }).Count -gt 0 -and 'CHANGELOG.md' -notin $files) { throw 'Changes require an update to CHANGELOG.md in the same push or PR.' }
}
if (-not $Version) {
    [xml]$project = Get-Content (Join-Path (Split-Path $PSScriptRoot) 'src/PomodoroBar.csproj') -Raw
    $Version = @($project.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0].Trim()
}
if ($Version -notmatch '^\d+\.\d+\.\d+$' -or -not $seen.ContainsKey($Version)) { throw "Missing dated release entry: $Version" }
$entry = [regex]::Match($text, '(?ms)^## \[' + [regex]::Escape($Version) + '\] - [^\r\n]+\r?\n.*?(?=^## |\z)').Value.Trim()
if ($OutputPath) {
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $entry + "`n", [Text.UTF8Encoding]::new($false))
}
Write-Host "Changelog valid; release notes ready for $Version."
