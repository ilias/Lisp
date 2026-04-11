namespace Lisp;

public static class Interpreter
{
    private enum CliActionKind
    {
        LoadFile,
        EvalExpr,
    }

    private sealed record CliAction(CliActionKind Kind, string Value);

    private static bool RunActions(InterpreterHost host, IReadOnlyList<CliAction> actions)
    {
        bool hadError = false;

        foreach (var action in actions)
        {
            if (action.Kind == CliActionKind.LoadFile)
            {
                var file = action.Value;
                if (!File.Exists(file))
                {
                    Console.WriteLine($"error: file not found: {file}");
                    hadError = true;
                    continue;
                }

                try
                {
                    Console.WriteLine($"Loading '{file}'...");
                    host.EvalFile(file);
                }
                catch (Exception e)
                {
                    Console.WriteLine(ExceptionDisplay.FormatForConsole($"error in '{file}': ", e));
                    hadError = true;
                }
                continue;
            }

            try
            {
                var result = host.Eval(action.Value, "<command-line>");
                if (result != null)
                {
                    ConsoleOutput.WriteResult(result);
                    Console.WriteLine();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(ExceptionDisplay.FormatForConsole("error in --eval: ", e));
                hadError = true;
            }
        }

        return !hadError;
    }

    private static int ParenDepth(string text)
    {
        int depth = 0;
        bool inString = false;
        int blockDepth = 0;
        for (int index = 0; index < text.Length; index++)
        {
            char ch = text[index];

            // Inside a #| ... |# block comment (supports nesting per R7RS)
            if (blockDepth > 0)
            {
                if (ch == '#' && index + 1 < text.Length && text[index + 1] == '|')
                { blockDepth++; index++; }
                else if (ch == '|' && index + 1 < text.Length && text[index + 1] == '#')
                { blockDepth--; index++; }
                continue;
            }

            if (inString)
            {
                if (ch == '\\') index++;
                else if (ch == '"') inString = false;
                continue;
            }

            if (ch == '#' && index + 1 < text.Length)
            {
                if (text[index + 1] == '\\')
                {
                    // Character literal #\x or #\newline — skip the char or full name
                    index += 2;
                    if (index < text.Length && char.IsLetter(text[index]))
                        while (index < text.Length && char.IsLetter(text[index])) index++;
                    else if (index < text.Length)
                        index++;
                    index--;
                    continue;
                }
                if (text[index + 1] == '|')
                {
                    blockDepth++;
                    index++;
                    continue;
                }
            }

            if (ch == ';')
            {
                while (index + 1 < text.Length && text[index + 1] != '\n') index++;
                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '(') depth++;
            else if (ch == ')') depth--;
        }

        return depth;
    }

    // Use ReadLine library only when stdin is an interactive terminal; fall back to
    // Console.ReadLine() when stdin is redirected (pipes, scripts) so that EOF
    // (null return) is detected correctly and we don't loop forever.
    private static bool IsInteractive => !Console.IsInputRedirected;

    private static string? ReadPromptLine(string prompt)
    {
        if (IsInteractive)
            return ReadLine.Read(prompt, "");
        // Non-interactive: print prompt to stderr so it doesn't pollute piped output,
        // then read from the redirected stdin. Returns null at EOF.
        Console.Error.Write(prompt);
        return Console.ReadLine();
    }

    private static string? ReadSubmission(InterpreterRuntime runtime)
    {
        string? firstLine = ReadPromptLine("lisp> ");
        if (firstLine == null) return null;
        if (string.IsNullOrWhiteSpace(firstLine)) return string.Empty;

        var buffer = new StringBuilder();
        buffer.AppendLine(firstLine);
        while (ParenDepth(buffer.ToString()) > 0)
        {
            string? continuation = ReadPromptLine("...    ");
            if (continuation == null) break;
            buffer.AppendLine(continuation);
        }

        var submission = buffer.ToString();
        if (!string.IsNullOrWhiteSpace(submission))
        {
            var trimmed = submission.Trim();
            ReadLine.AddHistory(trimmed);
            runtime.AddSessionHistory(trimmed);
        }
        return submission;
    }

    private static string GetHistoryFilePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(root, "Lisp");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "history.txt");
    }

    private static void LoadPersistentHistory()
    {
        if (!IsInteractive) return;
        try
        {
            var path = GetHistoryFilePath();
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadLines(path))
                if (!string.IsNullOrWhiteSpace(line))
                    ReadLine.AddHistory(line);
        }
        catch
        {
            // Ignore history persistence failures; REPL should still run.
        }
    }

    private static void FlushPersistentHistory(InterpreterRuntime runtime)
    {
        if (!IsInteractive || runtime.SessionHistory.Count == 0) return;
        try
        {
            var path = GetHistoryFilePath();
            var previous = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string>();
            previous.AddRange(runtime.SessionHistory);
            if (previous.Count > InterpreterRuntime.MaxHistoryEntries)
                previous = previous[^InterpreterRuntime.MaxHistoryEntries..];
            File.WriteAllLines(path, previous);
        }
        catch
        {
            // Ignore history persistence failures on shutdown.
        }
    }

    private static void EvaluateSubmission(InterpreterHost host, string input)
    {
        while (input.Trim().Length > 0)
        {
            var result = host.EvalReplOne(ref input);

            if (result != null)
            {
                ConsoleOutput.WriteResult(result);
                Console.WriteLine();
            }
        }
    }

    private static string EscapeSchemeString(string text)
        => text.Replace("\\", "\\\\", StringComparison.Ordinal)
               .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static bool EvaluateForReplCommand(InterpreterHost host, string expr)
    {
        var result = host.Eval(expr, "<repl-command>");
        if (result != null)
        {
            ConsoleOutput.WriteResult(result);
            Console.WriteLine();
        }
        return true;
    }

    private static void PrintReplCommandHelp()
    {
        Console.WriteLine("REPL commands:");
        Console.WriteLine("  :help                 Show REPL command help");
        Console.WriteLine("  :env [pattern]        Show environment bindings (optional wildcard filter)");
        Console.WriteLine("  :doc NAME             Show docs for a symbol");
        Console.WriteLine("  :load FILE            Load and evaluate a Scheme source file");
        Console.WriteLine("  :time EXPR            Evaluate expression and print elapsed time");
        Console.WriteLine("  :disasm NAME          Disassemble a procedure binding");
        Console.WriteLine("  :history [N]          Show recent REPL submissions (default 20)");
        Console.WriteLine("  :quit / :exit         Exit the REPL");
        Console.WriteLine("Ctrl+C while evaluating interrupts; Ctrl+C at prompt exits.");
    }

    private static bool TryHandleReplCommand(InterpreterHost host, string input)
    {
        var runtime = host.Runtime;
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
        var arg = splitAt >= 0 ? body[(splitAt + 1)..].Trim() : "";

        switch (command)
        {
            case "help":
                PrintReplCommandHelp();
                return true;

            case "quit":
            case "exit":
                runtime.EndProgram = true;
                return true;

            case "env":
                return arg.Length == 0
                    ? EvaluateForReplCommand(host, "(env)")
                    : EvaluateForReplCommand(host, $"(env \"{EscapeSchemeString(arg)}\")");

            case "doc":
                if (arg.Length == 0)
                {
                    Console.WriteLine("usage: :doc NAME");
                    return true;
                }
                return EvaluateForReplCommand(host, $"(doc '{arg})");

            case "disasm":
                if (arg.Length == 0)
                {
                    Console.WriteLine("usage: :disasm NAME");
                    return true;
                }
                return EvaluateForReplCommand(host, $"(disasm {arg})");

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
                    host.EvalFile(path);
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
                    var ok = EvaluateForReplCommand(host, arg);
                    sw.Stop();
                    Console.WriteLine($"; elapsed {sw.Elapsed.TotalMilliseconds:F3} ms");
                    return ok;
                }
                catch (Exception e)
                {
                    Console.WriteLine(ExceptionDisplay.FormatForConsole("error: ", e));
                    return true;
                }

            case "history":
                {
                    int count = 20;
                    if (arg.Length != 0 && !int.TryParse(arg, out count))
                    {
                        Console.WriteLine("usage: :history [N]");
                        return true;
                    }

                    if (count < 1) count = 1;
                    var take = Math.Min(count, runtime.SessionHistory.Count);
                    if (take == 0)
                    {
                        Console.WriteLine("(no history for this session)");
                        return true;
                    }

                    int start = runtime.SessionHistory.Count - take;
                    for (int i = start; i < runtime.SessionHistory.Count; i++)
                        Console.WriteLine($"{i + 1,4}: {runtime.SessionHistory[i]}");
                    return true;
                }

            default:
                Console.WriteLine($"unknown REPL command ':{command}'. Try :help");
                return true;
        }
    }

    private static void RunRepl(InterpreterHost host)
    {
        var runtime = host.Runtime;
        // Disable automatic history-on-read; we add only complete expressions.
        ReadLine.HistoryEnabled = false;
        runtime.EndProgram = false;
        runtime.EnsureCancelHandlerRegistered();
        LoadPersistentHistory();
        PrintReplCommandHelp();
        try
        {
            while (!runtime.EndProgram)
            {
                try
                {
                    var input = ReadSubmission(runtime);
                    if (input == null) break;
                    if (input.Length == 0) continue;
                    if (TryHandleReplCommand(host, input)) continue;
                    EvaluateSubmission(host, input);
                }
                catch (UserInterruptException)
                {
                    Console.WriteLine("^C interrupted");
                }
                catch (Exception e)
                {
                    Console.WriteLine(ExceptionDisplay.FormatForConsole("error: ", e));
                }
            }
        }
        finally
        {
            FlushPersistentHistory(runtime);
        }
    }

    private static void PrintHelp(Version? ver)
    {
        Console.WriteLine($"Lisp {ver} - Scheme interpreter");
        Console.WriteLine();
        Console.WriteLine("Usage: Lisp [options] [file ...]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --help       Show this help text and exit");
        Console.WriteLine("  --version    Show version and exit");
        Console.WriteLine("  --no-init    Skip loading init.ss (useful for scripting)");
        Console.WriteLine("  --stats      Print execution statistics after each expression");
        Console.WriteLine("  --no-color   Disable ANSI color output");
        Console.WriteLine("  --primitive-profile NAME  Primitive profile: core or full (default: full)");
        Console.WriteLine("  --load FILE  Load and evaluate FILE (can be repeated)");
        Console.WriteLine("  --eval EXPR  Evaluate EXPR (can be repeated)");
        Console.WriteLine("  --lib-path DIR  Add DIR to load search paths (can be repeated)");
        Console.WriteLine();
        Console.WriteLine("If files/--load/--eval are provided, commands run in order and then exit.");
        Console.WriteLine("Without script arguments the interactive REPL is started.");
        Console.WriteLine();
        Console.WriteLine("REPL shortcuts:");
        Console.WriteLine("  Ctrl+C  Interrupt current evaluation; at prompt exits REPL");
        Console.WriteLine("  Ctrl+D  Exit (EOF)");
        Console.WriteLine("  :help   Show REPL command help");
    }

    private static bool TryParseCommandLine(
        string[] args,
        out bool showHelp,
        out bool showVersion,
        out bool noInit,
        out bool stats,
        out bool noColor,
        out string primitiveProfile,
        out List<string> libPaths,
        out List<CliAction> actions,
        out string? error)
    {
        showHelp = false;
        showVersion = false;
        noInit = false;
        stats = false;
        noColor = false;
        primitiveProfile = Prim.DefaultPrimitiveProfile;
        libPaths = [];
        actions = [];
        error = null;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                    showHelp = true;
                    break;
                case "--version":
                    showVersion = true;
                    break;
                case "--no-init":
                    noInit = true;
                    break;
                case "--stats":
                    stats = true;
                    break;
                case "--no-color":
                    noColor = true;
                    break;
                case "--primitive-profile":
                    if (i + 1 >= args.Length)
                    {
                        error = "--primitive-profile requires a profile name";
                        return false;
                    }
                    primitiveProfile = Prim.ResolvePrimitiveProfile(args[++i]);
                    break;
                case "--load":
                    if (i + 1 >= args.Length)
                    {
                        error = "--load requires a file path";
                        return false;
                    }
                    actions.Add(new CliAction(CliActionKind.LoadFile, args[++i]));
                    break;
                case "--eval":
                    if (i + 1 >= args.Length)
                    {
                        error = "--eval requires an expression";
                        return false;
                    }
                    actions.Add(new CliAction(CliActionKind.EvalExpr, args[++i]));
                    break;
                case "--lib-path":
                    if (i + 1 >= args.Length)
                    {
                        error = "--lib-path requires a directory";
                        return false;
                    }
                    libPaths.Add(args[++i]);
                    break;
                default:
                    if (arg.StartsWith("--primitive-profile=", StringComparison.Ordinal))
                    {
                        var profile = arg[("--primitive-profile=".Length)..];
                        primitiveProfile = Prim.ResolvePrimitiveProfile(profile);
                        break;
                    }
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        error = $"unknown option '{arg}'";
                        return false;
                    }
                    actions.Add(new CliAction(CliActionKind.LoadFile, arg));
                    break;
            }
        }

        return true;
    }

    [STAThread]
    static int Main(string[] args)
    {
        var ver = Assembly.GetEntryAssembly()?.GetName().Version;

        if (!TryParseCommandLine(
                args,
                out var showHelp,
                out var showVersion,
                out var noInit,
                out var stats,
                out var noColor,
            out var primitiveProfile,
                out var libPaths,
                out var actions,
                out var parseError))
        {
            Console.WriteLine($"error: {parseError}");
            Console.WriteLine("Try --help");
            return 2;
        }

        if (showVersion)
        {
            Console.WriteLine($"Lisp {ver}");
            return 0;
        }

        if (noColor) ConsoleOutput.NoColor = true;

        if (showHelp)
        {
            PrintHelp(ver);
            return 0;
        }

        Console.WriteLine($"*** Lisp ver {ver} - Copyright (c) 2003 by Ilias H. Mavreas ***\n");

        var host = new InterpreterHost(primitiveProfile, statsEnabled: stats);
        foreach (var libPath in libPaths)
            host.AddLibraryPath(libPath);

        if (!noInit) host.LoadInitFromBaseDirectory();
        if (actions.Count > 0)
            return RunActions(host, actions) ? 0 : 1;

        RunRepl(host);
        return 0;
    }
}
