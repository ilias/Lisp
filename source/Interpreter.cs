namespace Lisp;

public static class Interpreter
{
    private sealed record CliOptionSpec(
        string LongName,
        string? ShortName,
        bool RequiresValue,
        string? ValueName,
        string Description);

    private static readonly CliOptionSpec[] CliOptionSpecs =
    [
        new("help", "h", false, null, "Show this help text and exit"),
        new("version", "v", false, null, "Show version and exit"),
        new("no-init", "n", false, null, "Skip loading init.ss (useful for scripting)"),
        new("stats", "s", false, null, "Print execution statistics after each expression"),
        new("no-color", "C", false, null, "Disable ANSI color output"),
        new("primitive-profile", "p", true, "NAME", "Primitive profile"),
        new("load", "l", true, "FILE", "Load and evaluate FILE (repeatable)"),
        new("eval", "e", true, "EXPR", "Evaluate EXPR (repeatable)"),
        new("lib-path", "L", true, "DIR", "Add DIR to load search paths (repeatable)"),
    ];

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

    private static string DescribeOption(CliOptionSpec spec)
    {
        var longPart = spec.RequiresValue
            ? $"--{spec.LongName} {spec.ValueName}"
            : $"--{spec.LongName}";

        if (string.IsNullOrEmpty(spec.ShortName))
            return longPart;

        var shortPart = spec.RequiresValue
            ? $"-{spec.ShortName} {spec.ValueName}"
            : $"-{spec.ShortName}";

        return $"{shortPart}, {longPart}";
    }

    private static string FormatOptionError(string token)
    {
        var longName = token.StartsWith("--", StringComparison.Ordinal)
            ? token[2..]
            : token.StartsWith("-", StringComparison.Ordinal) ? token[1..] : token;
        var suggestions = CliOptionSpecs
            .Select(s => s.LongName)
            .Where(n => n.Contains(longName, StringComparison.OrdinalIgnoreCase)
                     || longName.Contains(n, StringComparison.OrdinalIgnoreCase)
                     || n.StartsWith(longName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (suggestions.Count == 0)
            return $"unknown option '{token}'";

        return $"unknown option '{token}'. Did you mean --{suggestions[0]}?";
    }

    private static void PrintHelp(Version? ver)
    {
        Console.WriteLine($"Lisp {ver} - Scheme interpreter");
        Console.WriteLine();
        Console.WriteLine("Usage: Lisp [options] [file ...]");
        Console.WriteLine("       Lisp [options] -- [file ...]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        int optionWidth = CliOptionSpecs.Select(DescribeOption).Max(s => s.Length) + 2;
        foreach (var spec in CliOptionSpecs)
            Console.WriteLine($"  {DescribeOption(spec).PadRight(optionWidth)}{spec.Description}");
        Console.WriteLine();
        Console.WriteLine($"Primitive profiles: {string.Join(", ", Prim.GetPrimitiveProfiles().OrderBy(n => n, StringComparer.OrdinalIgnoreCase))} (default: {Prim.DefaultPrimitiveProfile})");
        Console.WriteLine();
        Console.WriteLine("If files/--load/--eval are provided, commands run in order and then exit.");
        Console.WriteLine("Without script arguments the interactive REPL is started.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Lisp --eval \"(+ 1 2)\"");
        Console.WriteLine("  Lisp -l script.ss -e \"(main)\"");
        Console.WriteLine("  Lisp --primitive-profile=core -- --weird-file-name.ss");
        Console.WriteLine();
        Console.WriteLine("REPL shortcuts:");
        Console.WriteLine("  Ctrl+C  Interrupt current evaluation; at prompt exits REPL");
        Console.WriteLine("  Ctrl+D  Exit (EOF)");
        Console.WriteLine("  :help   Show REPL command help");
    }

    private static bool TryResolveProfile(string value, out string profile, out string? error)
    {
        var knownProfiles = Prim.GetPrimitiveProfiles().ToArray();
        if (knownProfiles.Any(p => string.Equals(p, value, StringComparison.OrdinalIgnoreCase)))
        {
            profile = value.Trim().ToLowerInvariant();
            error = null;
            return true;
        }

        profile = Prim.DefaultPrimitiveProfile;
        error = $"--primitive-profile expects one of: {string.Join(", ", knownProfiles.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))}";
        return false;
    }

    private static bool TryFindLongOption(string name, out CliOptionSpec spec)
    {
        foreach (var candidate in CliOptionSpecs)
        {
            if (string.Equals(candidate.LongName, name, StringComparison.Ordinal))
            {
                spec = candidate;
                return true;
            }
        }

        spec = null!;
        return false;
    }

    private static bool TryFindShortOption(string name, out CliOptionSpec spec)
    {
        foreach (var candidate in CliOptionSpecs)
        {
            if (string.Equals(candidate.ShortName, name, StringComparison.Ordinal))
            {
                spec = candidate;
                return true;
            }
        }

        spec = null!;
        return false;
    }

    private static bool ApplyOption(
        CliOptionSpec spec,
        string? value,
        ref bool showHelp,
        ref bool showVersion,
        ref bool noInit,
        ref bool stats,
        ref bool noColor,
        ref string primitiveProfile,
        List<string> libPaths,
        List<CliAction> actions,
        out string? error)
    {
        error = null;
        switch (spec.LongName)
        {
            case "help":
                showHelp = true;
                return true;
            case "version":
                showVersion = true;
                return true;
            case "no-init":
                noInit = true;
                return true;
            case "stats":
                stats = true;
                return true;
            case "no-color":
                noColor = true;
                return true;
            case "primitive-profile":
                if (!TryResolveProfile(value!, out var profile, out error))
                    return false;
                primitiveProfile = profile;
                return true;
            case "load":
                actions.Add(new CliAction(CliActionKind.LoadFile, value!));
                return true;
            case "eval":
                actions.Add(new CliAction(CliActionKind.EvalExpr, value!));
                return true;
            case "lib-path":
                libPaths.Add(value!);
                return true;
            default:
                error = $"internal error: unsupported option '--{spec.LongName}'";
                return false;
        }
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

            if (arg == "--")
            {
                for (int rest = i + 1; rest < args.Length; rest++)
                    actions.Add(new CliAction(CliActionKind.LoadFile, args[rest]));
                return true;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                string body = arg[2..];
                int equalsIndex = body.IndexOf('=');
                string longName = equalsIndex >= 0 ? body[..equalsIndex] : body;
                string? inlineValue = equalsIndex >= 0 ? body[(equalsIndex + 1)..] : null;

                if (!TryFindLongOption(longName, out var spec))
                {
                    error = FormatOptionError(arg);
                    return false;
                }

                string? value = null;
                if (spec.RequiresValue)
                {
                    if (inlineValue != null)
                    {
                        value = inlineValue;
                    }
                    else if (i + 1 < args.Length)
                    {
                        value = args[++i];
                    }
                    else
                    {
                        error = $"--{spec.LongName} requires {spec.ValueName}";
                        return false;
                    }
                }
                else if (inlineValue != null)
                {
                    error = $"--{spec.LongName} does not accept a value";
                    return false;
                }

                if (!ApplyOption(
                        spec,
                        value,
                        ref showHelp,
                        ref showVersion,
                        ref noInit,
                        ref stats,
                        ref noColor,
                        ref primitiveProfile,
                        libPaths,
                        actions,
                        out error))
                    return false;

                continue;
            }

            if (arg.StartsWith("-", StringComparison.Ordinal) && arg.Length > 1)
            {
                string body = arg[1..];
                int equalsIndex = body.IndexOf('=');
                string shortName = equalsIndex >= 0 ? body[..equalsIndex] : body;
                string? inlineValue = equalsIndex >= 0 ? body[(equalsIndex + 1)..] : null;

                if (!TryFindShortOption(shortName, out var spec))
                {
                    error = FormatOptionError(arg);
                    return false;
                }

                string? value = null;
                if (spec.RequiresValue)
                {
                    if (inlineValue != null)
                    {
                        value = inlineValue;
                    }
                    else if (i + 1 < args.Length)
                    {
                        value = args[++i];
                    }
                    else
                    {
                        error = $"-{spec.ShortName} requires {spec.ValueName}";
                        return false;
                    }
                }
                else if (inlineValue != null)
                {
                    error = $"-{spec.ShortName} does not accept a value";
                    return false;
                }

                if (!ApplyOption(
                        spec,
                        value,
                        ref showHelp,
                        ref showVersion,
                        ref noInit,
                        ref stats,
                        ref noColor,
                        ref primitiveProfile,
                        libPaths,
                        actions,
                        out error))
                    return false;

                continue;
            }

            actions.Add(new CliAction(CliActionKind.LoadFile, arg));
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
