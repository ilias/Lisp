Set-Location q:\code\projects\csharp\lisp\bin\Release\net10.0
$p = Start-Process -PassThru -FilePath "dotnet" `
    -ArgumentList "Lisp.dll bench.ss" `
    -RedirectStandardOutput "bench_out.txt" `
    -RedirectStandardError "bench_err.txt" `
    -NoNewWindow
$p.WaitForExit()
Get-Content bench_out.txt | Select-String "time:|error"
