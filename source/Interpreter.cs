namespace Lisp;

public static class Interpreter
{
    public static bool EndProgram = false;

    private static void LoadInit(Program prog)
    {
        var initPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "init.ss");
        if (!File.Exists(initPath))
        {
            Console.WriteLine($"Warning: 'init.ss' not found at {initPath}");
            return;
        }

        try
        {
            Console.Write("Initializing: loading 'init.ss'...");
            prog.LoadInit(initPath);
        }
        catch (Exception e)
        {
            Console.WriteLine();
            Console.WriteLine(ExceptionDisplay.FormatForConsole("error loading 'init.ss': ", e));
        }
    }

    private static bool RunFiles(Program prog, string[] args)
    {
        if (args.Length == 0) return false;

        foreach (var file in args)
        {
            if (!File.Exists(file))
            {
                Console.WriteLine($"error: file not found: {file}");
                continue;
            }

            try
            {
                Console.WriteLine($"Loading '{file}'...");
                prog.Eval(File.ReadAllText(file), file);
            }
            catch (Exception e)
            {
                Console.WriteLine(ExceptionDisplay.FormatForConsole($"error in '{file}': ", e));
            }
        }

        return true;
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

    private static string? ReadSubmission()
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
            ReadLine.AddHistory(submission.Trim());
        return submission;
    }

    private static void EvaluateSubmission(Program prog, string input)
    {
        while (input.Trim().Length > 0)
        {
            var result = prog.EvalOne(input, out input, "<repl>");
            if (result != null)
            {
                ConsoleOutput.WriteResult(result);
                Console.WriteLine();
            }
        }
    }

    private static void RunRepl(Program prog)
    {
        // Disable automatic history-on-read; we add only complete expressions.
        ReadLine.HistoryEnabled = false;
        while (!EndProgram)
        {
            try
            {
                var input = ReadSubmission();
                if (input == null) break;
                if (input.Length == 0) continue;
                EvaluateSubmission(prog, input);
            }
            catch (Exception e)
            {
                Console.WriteLine(ExceptionDisplay.FormatForConsole("error: ", e));
            }
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
        Console.WriteLine();
        Console.WriteLine("Files are evaluated in order and the program exits.");
        Console.WriteLine("Without file arguments the interactive REPL is started.");
        Console.WriteLine();
        Console.WriteLine("REPL shortcuts:");
        Console.WriteLine("  Ctrl+C  Exit");
        Console.WriteLine("  Ctrl+D  Exit (EOF)");
    }

    [STAThread]
    static void Main(string[] args)
    {
        var ver = Assembly.GetEntryAssembly()?.GetName().Version;

        bool showHelp    = false;
        bool showVersion = false;
        bool noInit      = false;
        bool stats       = false;
        bool noColor     = false;
        var  files       = new List<string>();

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--help":     showHelp    = true; break;
                case "--version":  showVersion = true; break;
                case "--no-init":  noInit      = true; break;
                case "--stats":    stats       = true; break;
                case "--no-color": noColor     = true; break;
                default:
                    if (arg.StartsWith("--"))
                        Console.WriteLine($"warning: unknown option '{arg}' (try --help)");
                    else
                        files.Add(arg);
                    break;
            }
        }

        if (showVersion)
        {
            Console.WriteLine($"Lisp {ver}");
            return;
        }

        if (noColor) ConsoleOutput.NoColor = true;

        if (showHelp)
        {
            PrintHelp(ver);
            return;
        }

        Console.WriteLine($"*** Lisp ver {ver} - Copyright (c) 2003 by Ilias H. Mavreas ***\n");

        var prog = new Program();
        if (stats) Program.Stats = true;
        if (!noInit) LoadInit(prog);
        if (RunFiles(prog, [.. files])) return;
        RunRepl(prog);
    }
}
