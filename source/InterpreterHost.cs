namespace Lisp;

public sealed class InterpreterHost
{
    public Program Program { get; }
    public InterpreterRuntime Runtime { get; }
    public IReadOnlyList<string> SessionHistory => Runtime.SessionHistory;
    public bool StartupMessagesEnabled { get; }

    public InterpreterHost(string? primitiveProfile = null, bool statsEnabled = false, bool startupMessagesEnabled = false)
    {
        Runtime = new InterpreterRuntime();
        Program = new Program(primitiveProfile);
        StartupMessagesEnabled = startupMessagesEnabled;
        if (statsEnabled)
            Program.Stats = true;
    }

    private T WithCurrentContext<T>(Func<T> action)
    {
        var previous = InterpreterContext.Current;
        try
        {
            InterpreterContext.Current = Program.Context;
            return action();
        }
        finally
        {
            InterpreterContext.Current = previous;
        }
    }

    private void WithCurrentContext(Action action)
    {
        var previous = InterpreterContext.Current;
        try
        {
            InterpreterContext.Current = Program.Context;
            action();
        }
        finally
        {
            InterpreterContext.Current = previous;
        }
    }

    public void AddLibraryPath(string path)
        => WithCurrentContext(() =>
        {
            var runtimeContext = InterpreterContext.RequireCurrent();
            try
            {
                runtimeContext.LibrarySearchPaths.Add(Path.GetFullPath(path));
            }
            catch
            {
                runtimeContext.LibrarySearchPaths.Add(path);
            }
        });

    public void LoadInit(string path)
        => WithCurrentContext(() => Program.LoadInit(path));

    public void LoadInitFromBaseDirectory()
    {
        var initPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "init.ss");
        if (!File.Exists(initPath))
        {
            if (StartupMessagesEnabled)
                Console.WriteLine($"Warning: 'init.ss' not found at {initPath}");
            return;
        }

        try
        {
            if (StartupMessagesEnabled)
                Console.Write("Initializing: loading 'init.ss'...");
            LoadInit(initPath);
        }
        catch (Exception e)
        {
            if (StartupMessagesEnabled)
                Console.WriteLine();
            Console.WriteLine(ExceptionDisplay.FormatForConsole("error loading 'init.ss': ", e));
        }
    }

    public object Eval(string expr, string sourceName = "<host>")
        => WithCurrentContext(() => Runtime.ExecuteWithEvaluationScope(() => Program.Eval(expr, sourceName)));

    public object EvalFile(string filePath)
        => WithCurrentContext(() => Runtime.ExecuteWithEvaluationScope(() => Program.Eval(File.ReadAllText(filePath), filePath)));

    internal object EvalReplOne(ref string input)
    {
        string local = input;
        var result = WithCurrentContext(() => Runtime.ExecuteWithEvaluationScope(() => Program.EvalOne(local, out local, "<repl>")));
        input = local;
        return result;
    }

    public void PrintReplCommandHelp()
    {
        Console.WriteLine("REPL commands:");
        Console.WriteLine("  :help                 Show REPL command help");
        Console.WriteLine("  :env [pattern]        Show environment bindings (optional wildcard filter)");
        Console.WriteLine("  :doc NAME             Show docs for a symbol");
        Console.WriteLine("  :load FILE            Load and evaluate a Scheme source file");
        Console.WriteLine("  :time EXPR            Evaluate expression and print elapsed time");
        Console.WriteLine("  :bench [N]            Run the built-in benchmark N times (default 3)");
        Console.WriteLine("  :stats                Show accumulated runtime stats totals");
        Console.WriteLine("  :disasm NAME [MODE]   Disassemble a procedure binding (mode: auto|full|compact)");
        Console.WriteLine("  :history [N]          Show recent REPL submissions (default 20)");
        Console.WriteLine("  :history /pattern/   Show matching history entries");
        Console.WriteLine("  :quit / :exit         Exit the REPL");
        Console.WriteLine("Ctrl+C while evaluating interrupts; Ctrl+C at prompt exits.");
    }

    private static string EscapeSchemeString(string text)
        => text.Replace("\\", "\\\\", StringComparison.Ordinal)
               .Replace("\"", "\\\"", StringComparison.Ordinal);

    internal bool TryHandleReplCommand(string input)
    {
        var trimmed = input.Trim();
        if (!trimmed.StartsWith(':'))
            return false;

        var body = trimmed[1..].Trim();
        if (body.Length == 0)
        {
            PrintReplCommandHelp();
            return true;
        }

        var splitAt = body.IndexOfAny([' ', '\t']);
        var command = (splitAt >= 0 ? body[..splitAt] : body).ToLowerInvariant();
        var arg = splitAt >= 0 ? body[(splitAt + 1)..].Trim() : string.Empty;

        switch (command)
        {
            case "help":
                PrintReplCommandHelp();
                return true;

            case "quit":
            case "exit":
                Runtime.EndProgram = true;
                return true;

            case "env":
                PrintResult(arg.Length == 0 ? Eval("(env)", "<repl-command>") : Eval($"(env \"{EscapeSchemeString(arg)}\")", "<repl-command>"));
                return true;

            case "doc":
                if (arg.Length == 0)
                {
                    Console.WriteLine("usage: :doc NAME");
                    return true;
                }
                PrintResult(Eval($"(doc '{arg})", "<repl-command>"));
                return true;

            case "disasm":
                if (arg.Length == 0)
                {
                    Console.WriteLine("usage: :disasm NAME [auto|full|compact]");
                    return true;
                }
                {
                    var parts = arg.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    string targetExpr = arg;
                    string modeExpr = string.Empty;

                    if (parts.Length > 1)
                    {
                        string trailing = parts[^1].ToLowerInvariant();
                        if (trailing is "auto" or "full" or "compact")
                        {
                            int modeStart = arg.LastIndexOf(parts[^1], StringComparison.Ordinal);
                            if (modeStart <= 0)
                            {
                                Console.WriteLine("usage: :disasm NAME [auto|full|compact]");
                                return true;
                            }

                            targetExpr = arg[..modeStart].TrimEnd();
                            if (targetExpr.Length == 0)
                            {
                                Console.WriteLine("usage: :disasm NAME [auto|full|compact]");
                                return true;
                            }

                            modeExpr = $" '{trailing}";
                        }
                    }

                    PrintResult(Eval($"(disasm {targetExpr}{modeExpr})", "<repl-command>"));
                }
                return true;

            case "load":
                if (arg.Length == 0)
                {
                    Console.WriteLine("usage: :load FILE");
                    return true;
                }
                try
                {
                    var path = Path.GetFullPath(arg);
                    if (!File.Exists(path))
                    {
                        Console.WriteLine($"error: file not found: {arg}");
                        return true;
                    }
                    EvalFile(path);
                    Console.WriteLine($"Loaded '{path}'.");
                }
                catch (Exception e)
                {
                    Console.WriteLine(ExceptionDisplay.FormatForConsole("error: ", e));
                }
                return true;

            case "time":
                if (arg.Length == 0)
                {
                    Console.WriteLine("usage: :time EXPR");
                    return true;
                }
                try
                {
                    var sw = Stopwatch.StartNew();
                    PrintResult(Eval(arg, "<repl-command>"));
                    sw.Stop();
                    Console.WriteLine($"; elapsed {sw.Elapsed.TotalMilliseconds:F3} ms");
                }
                catch (Exception e)
                {
                    Console.WriteLine(ExceptionDisplay.FormatForConsole("error: ", e));
                }
                return true;

            case "bench":
                {
                    int iterations = 3;
                    if (arg.Length != 0)
                    {
                        if (!int.TryParse(arg, out iterations) || iterations < 1)
                        {
                            Console.WriteLine("usage: :bench [N]");
                            return true;
                        }
                    }

                    RunBenchmark(iterations);
                    return true;
                }

            case "stats":
                Program.PrintTotals();
                return true;

            case "history":
                {
                    const int defaultCount = 20;
                    if (arg.Length == 0)
                    {
                        var count = defaultCount;
                        if (count < 1) count = 1;
                        var take = Math.Min(count, Runtime.SessionHistory.Count);
                        if (take == 0)
                        {
                            Console.WriteLine("(no history for this session)");
                            return true;
                        }

                        int start = Runtime.SessionHistory.Count - take;
                        for (int i = start; i < Runtime.SessionHistory.Count; i++)
                            Console.WriteLine($"{i + 1,4}: {Runtime.SessionHistory[i]}");
                        return true;
                    }

                    if (int.TryParse(arg, out var parsedCount))
                    {
                        var count = Math.Max(1, parsedCount);
                        var take = Math.Min(count, Runtime.SessionHistory.Count);
                        if (take == 0)
                        {
                            Console.WriteLine("(no history for this session)");
                            return true;
                        }

                        int start = Runtime.SessionHistory.Count - take;
                        for (int i = start; i < Runtime.SessionHistory.Count; i++)
                            Console.WriteLine($"{i + 1,4}: {Runtime.SessionHistory[i]}");
                        return true;
                    }

                    var pattern = arg.Trim();
                    if (pattern.Length == 0)
                    {
                        Console.WriteLine("usage: :history [N] | :history /pattern/");
                        return true;
                    }

                    var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                    bool found = false;
                    for (int i = 0; i < Runtime.SessionHistory.Count; i++)
                    {
                        var entry = Runtime.SessionHistory[i];
                        if (regex.IsMatch(entry))
                        {
                            Console.WriteLine($"{i + 1,4}: {entry}");
                            found = true;
                        }
                    }

                    if (!found)
                        Console.WriteLine("(no matching history entries)");
                    return true;
                }

            default:
                Console.WriteLine($"unknown REPL command ':{command}'. Try :help");
                return true;
        }
    }

    private static void PrintResult(object? result)
    {
        if (result == null)
            return;
        ConsoleOutput.WriteResult(result);
        Console.WriteLine();
    }

    private void RunBenchmark(int iterations)
    {
        var cases = new[]
        {
            (Name: "arithmetic", Expression: "(let loop ((i 0) (acc 0)) (if (= i 20000) acc (loop (+ i 1) (+ acc i))))"),
            (Name: "list-build", Expression: "(let loop ((i 0) (xs '())) (if (= i 4000) xs (loop (+ i 1) (cons i xs))))"),
            (Name: "string-join", Expression: "(let loop ((i 0) (acc \"\")) (if (= i 2000) acc (loop (+ i 1) (string-append acc \"x\"))))"),
        };

        Console.WriteLine($"Benchmark ({iterations} iterations each):");
        foreach (var benchmarkCase in cases)
        {
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var result = Eval(benchmarkCase.Expression, "<benchmark>");
                _ = result;
            }
            stopwatch.Stop();
            double avgMs = stopwatch.Elapsed.TotalMilliseconds / iterations;
            Console.WriteLine($"  {benchmarkCase.Name,-12} {stopwatch.Elapsed.TotalMilliseconds:F3} ms total / {avgMs:F3} ms avg");
        }
    }
}
