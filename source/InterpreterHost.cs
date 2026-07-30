namespace Lisp;

public sealed class InterpreterHost
{
    public Program Program { get; }
    public InterpreterRuntime Runtime { get; }
    public IReadOnlyList<string> SessionHistory => Runtime.SessionHistory;
    public bool StartupMessagesEnabled { get; }

    public InterpreterHost(string? primitiveProfile = null, bool statsEnabled = false, bool profileEnabled = false, bool startupMessagesEnabled = false)
    {
        Runtime = new InterpreterRuntime();
        Program = new Program(primitiveProfile);
        StartupMessagesEnabled = startupMessagesEnabled;
        if (statsEnabled)
            Program.Stats = true;
        if (profileEnabled)
            Program.Profile = true;
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
        => WithCurrentContext(() => Runtime.ExecuteWithEvaluationScope(() =>
        {
            var context = Program.Context;
            var previousInteractive = context.DebuggerInteractive;
            context.DebuggerInteractive = false;
            try
            {
                return (object)EvalWithDebugger(expr, sourceName)!;
            }
            finally
            {
                context.DebuggerInteractive = previousInteractive;
            }
        }));

    public object EvalFile(string filePath)
        => WithCurrentContext(() => Runtime.ExecuteWithEvaluationScope(() =>
        {
            var context = Program.Context;
            var previousInteractive = context.DebuggerInteractive;
            context.DebuggerInteractive = false;
            try
            {
                return (object)EvalWithDebugger(File.ReadAllText(filePath), filePath)!;
            }
            finally
            {
                context.DebuggerInteractive = previousInteractive;
            }
        }));

    internal object EvalReplOne(ref string input)
    {
        string local = input;
        var result = WithCurrentContext(() => Runtime.ExecuteWithEvaluationScope(() =>
        {
            var context = Program.Context;
            var previousInteractive = context.DebuggerInteractive;
            context.DebuggerInteractive = true;
            try
            {
                try
                {
                    return Program.EvalOne(local, out local, "<repl>");
                }
                catch (DebuggerPauseException pause)
                {
                    return HandleDebuggerPause(pause) ? Program.EvalOne(local, out local, "<repl>") : null!;
                }
            }
            finally
            {
                context.DebuggerInteractive = previousInteractive;
            }
        }));
        input = local;
        return result;
    }

    public void PrintReplCommandHelp()
    {
        Console.WriteLine("REPL commands:");
        Console.WriteLine("  :help                 Show REPL command help");
        Console.WriteLine("  :env [pattern]        Show environment bindings (optional wildcard filter)");
        Console.WriteLine("  :doc NAME             Show docs for a symbol");
        Console.WriteLine("  :expand EXPR          Show macro-expanded form without evaluating it");
        Console.WriteLine("  :load FILE            Load and evaluate a Scheme source file");
        Console.WriteLine("  :time EXPR            Evaluate expression and print elapsed time");
        Console.WriteLine("  :bench [N]            Run the built-in benchmark N times (default 3)");
        Console.WriteLine("  :stats                Show accumulated runtime stats totals");
        Console.WriteLine("  :profile [EXPR]       Show accumulated profile totals or profile a supplied expression");
        Console.WriteLine("  :disasm NAME [MODE]   Disassemble a procedure binding (mode: auto|full|compact)");
        Console.WriteLine("  :break [NAME]         Add a breakpoint, list breakpoints, or clear them with :break clear");
        Console.WriteLine("  :continue             Resume after a debugger pause");
        Console.WriteLine("  :step                 Pause on the next evaluated expression");
        Console.WriteLine("  :next                 Alias for :step");
        Console.WriteLine("  :backtrace            Show the current debugger backtrace");
        Console.WriteLine("  :locals               Show the current frame locals");
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

            case "expand":
                if (arg.Length == 0)
                {
                    Console.WriteLine("usage: :expand EXPR");
                    return true;
                }
                try
                {
                    var expanded = WithCurrentContext(() => Program.Expand(arg, "<repl-command>"));
                    if (expanded == null)
                    {
                        Console.WriteLine("(no expression)");
                        return true;
                    }

                    ConsoleOutput.WriteResult(expanded);
                    Console.WriteLine();
                }
                catch (Exception e)
                {
                    Console.WriteLine(ExceptionDisplay.FormatForConsole("error: ", e));
                }
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

            case "profile":
                if (arg.Length == 0)
                {
                    Program.PrintProfileTotals();
                    return true;
                }
                try
                {
                    Program.ResetProfile();
                    Program.Profile = true;
                    var result = Eval(arg, "<repl-command>");
                    Program.Profile = false;
                    Console.WriteLine($"; profile: {arg}");
                    PrintResult(result);
                    Program.PrintProfileTotals();
                }
                catch (Exception e)
                {
                    Program.Profile = false;
                    Console.WriteLine(ExceptionDisplay.FormatForConsole("error: ", e));
                }
                return true;

            case "break":
                {
                    if (arg.Length == 0)
                    {
                        if (Program.Context.Breakpoints.Count == 0)
                            Console.WriteLine("(no breakpoints)");
                        else
                            foreach (var breakpoint in Program.Context.Breakpoints)
                                Console.WriteLine($"  {breakpoint}");
                        return true;
                    }

                    if (arg.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        Program.Context.Breakpoints.Clear();
                        Console.WriteLine("breakpoints cleared");
                        return true;
                    }

                    Program.Context.Breakpoints.Add(arg);
                    Console.WriteLine($"breakpoint added: {arg}");
                    return true;
                }

            case "continue":
                Program.Context.DebugPaused = false;
                Program.Context.DebugSingleStep = false;
                Console.WriteLine("resuming");
                return true;

            case "step":
            case "next":
                Program.Context.DebugSingleStep = true;
                Program.Context.DebugPaused = false;
                Console.WriteLine("single-step enabled");
                return true;

            case "backtrace":
                if (Program.Context.DebugBacktrace.Count == 0)
                    Console.WriteLine("(no active backtrace)");
                else
                    for (int i = 0; i < Program.Context.DebugBacktrace.Count; i++)
                    {
                        var frame = Program.Context.DebugBacktrace[i];
                        Console.WriteLine($"{i + 1,2}: {frame.ProcedureName} :: {frame.Expression}");
                        if (!string.IsNullOrWhiteSpace(frame.SourceLocation))
                            Console.WriteLine($"      at {frame.SourceLocation}");
                    }
                return true;

            case "locals":
                if (Program.Context.DebugLocals.Count == 0)
                    Console.WriteLine("(no locals available)");
                else
                    foreach (var local in Program.Context.DebugLocals)
                        Console.WriteLine($"  {local.Name} = {Util.Dump(local.Value)}");
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

    private object? EvalWithDebugger(string expr, string sourceName)
    {
        while (true)
        {
            try
            {
                return Program.Eval(expr, sourceName);
            }
            catch (DebuggerPauseException pause)
            {
                if (!HandleDebuggerPause(pause))
                    return null;
            }
        }
    }

    private bool HandleDebuggerPause(DebuggerPauseException pause)
    {
        var context = Program.Context;
        Console.WriteLine("[debug] paused");
        Console.WriteLine($"  procedure: {pause.ProcedureName ?? "<procedure>"}");
        Console.WriteLine($"  source: {pause.Source?.FormatLocation() ?? "<unknown>"}");
        Console.WriteLine($"  expr: {pause.Expression}");
        if (pause.Locals.Count > 0)
        {
            Console.WriteLine("  locals:");
            foreach (var local in pause.Locals)
                Console.WriteLine($"    {local.Name} = {Util.Dump(local.Value)}");
        }

        while (true)
        {
            string? line = Console.IsInputRedirected
                ? null
                : ReadLine.Read("debug> ", "");
            if (line == null)
            {
                context.DebugPaused = false;
                context.DebugSingleStep = false;
                return false;
            }

            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                context.DebugPaused = false;
                context.DebugSingleStep = false;
                return true;
            }

            if (trimmed.StartsWith(':'))
            {
                if (TryHandleReplCommand(trimmed))
                    continue;
            }

            switch (trimmed.ToLowerInvariant())
            {
                case "continue":
                case "c":
                    context.DebugPaused = false;
                    context.DebugSingleStep = false;
                    return true;
                case "step":
                case "s":
                    context.DebugSingleStep = true;
                    context.DebugPaused = false;
                    return true;
                case "next":
                    context.DebugSingleStep = true;
                    context.DebugPaused = false;
                    return true;
                case "backtrace":
                case "bt":
                    if (context.DebugBacktrace.Count == 0)
                        Console.WriteLine("(no active backtrace)");
                    else
                        for (int i = 0; i < context.DebugBacktrace.Count; i++)
                        {
                            var frame = context.DebugBacktrace[i];
                            Console.WriteLine($"{i + 1,2}: {frame.ProcedureName} :: {frame.Expression}");
                            if (!string.IsNullOrWhiteSpace(frame.SourceLocation))
                                Console.WriteLine($"      at {frame.SourceLocation}");
                        }
                    break;
                case "locals":
                case "l":
                    if (context.DebugLocals.Count == 0)
                        Console.WriteLine("(no locals available)");
                    else
                        foreach (var local in context.DebugLocals)
                            Console.WriteLine($"  {local.Name} = {Util.Dump(local.Value)}");
                    break;
                case "quit":
                case "q":
                    context.DebugPaused = false;
                    context.DebugSingleStep = false;
                    return false;
                default:
                    Console.WriteLine("unknown debugger command. Try: continue, step, next, backtrace, locals, or :help");
                    break;
            }
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
