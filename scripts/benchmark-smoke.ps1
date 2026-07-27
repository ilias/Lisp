$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $output = & dotnet run -- --benchmark 8 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet run exited with $LASTEXITCODE`n$output"
    }
    if ($output -notmatch 'Benchmark') {
        throw "benchmark output did not contain 'Benchmark'`n$output"
    }
    Write-Host $output
}
finally {
    Pop-Location
}
