$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $input = @'
(+ 1 2)
(* 2 3)
'@
    $output = $input | dotnet run -- --batch --quiet 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet run exited with $LASTEXITCODE`n$output"
    }
    if ($output -notmatch '3') {
        throw "batch output did not include the first result`n$output"
    }
    if ($output -notmatch '6') {
        throw "batch output did not include the second result`n$output"
    }
    Write-Host $output
}
finally {
    Pop-Location
}
