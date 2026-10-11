$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet run --project src/PomodoroBar.csproj -c Dev
    if ($LASTEXITCODE -ne 0) { throw "Dev exited with code $LASTEXITCODE" }
} finally { Pop-Location }
