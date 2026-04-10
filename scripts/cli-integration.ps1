$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
Set-Location $repoRoot

Write-Host 'Running CLI integration checks...'

$evalOutput = & dotnet run --configuration Release -- --no-init --eval '(+ 2 3)'
if ($LASTEXITCODE -ne 0) {
    throw "--eval returned exit code $LASTEXITCODE"
}
if (($evalOutput | Out-String).Trim() -notmatch '5$') {
    throw "--eval output did not end with 5. Output: $evalOutput"
}

$libDir = Join-Path $repoRoot '_cli_lib'
New-Item -ItemType Directory -Force -Path $libDir | Out-Null
$libFile = Join-Path $libDir 'cli-test.ss'
Set-Content -Path $libFile -Value '(define cli-test-value 77)'

$loadOutput = & dotnet run --configuration Release -- --lib-path $libDir --eval '(load "cli-test.ss")' --eval 'cli-test-value'
if ($LASTEXITCODE -ne 0) {
    throw "--lib-path/--eval load returned exit code $LASTEXITCODE"
}
if (($loadOutput | Out-String).Trim() -notmatch '77$') {
    throw "CLI load output did not end with 77. Output: $loadOutput"
}

$missingOutput = & dotnet run --configuration Release -- --no-init --load missing-file.ss 2>&1
if ($LASTEXITCODE -eq 0) {
    throw 'Missing file load unexpectedly succeeded'
}

Write-Host 'CLI integration checks passed.'
