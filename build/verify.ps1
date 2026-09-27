# Smoke checks: startup/RAM/pause (mirrors build/verify.ps1 in AGENTS.md §9).
$ErrorActionPreference = 'Stop'
dotnet build "$PSScriptRoot\..\Backdrop.sln" -c Release
dotnet test "$PSScriptRoot\..\tests\Backdrop.Tests" -c Release --no-build
Write-Host "SMOKE OK: build + tests green."
