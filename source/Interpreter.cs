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

    private static void RunRepl(InterpreterHost host)
    {
        var runtime = host.Runtime;
        // Disable automatic history-on-read; we add only complete expressions.
        ReadLine.HistoryEnabled = false;
        host.Program.Context.EndProgram = false;
        runtime.EndProgram = false;
        runtime.EnsureCancelHandlerRegistered();
        runtime.LoadPersistentHistory(IsInteractive, line => ReadLine.AddHistory(line));
        host.PrintReplCommandHelp();
        try
        {
            while (!runtime.EndProgram && !host.Program.Context.EndProgram)
            {
                try
                {
                    var input = ReadSubmission(runtime);
                    if (input == null) break;
                    if (input.Length == 0) continue;
                    if (host.TryHandleReplCommand(input)) continue;
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
            runtime.FlushPersistentHistory(IsInteractive);
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
