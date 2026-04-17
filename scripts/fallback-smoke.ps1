[CmdletBinding()]
param(
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
Set-Location $repoRoot

dotnet run --configuration $Configuration -- scripts/fallback-smoke.ss
if ($LASTEXITCODE -ne 0) {
    throw "fallback smoke run returned exit code $LASTEXITCODE"
}