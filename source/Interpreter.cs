namespace Lisp;

public static class Interpreter
{
    public static bool EndProgram = false;

    private static void ColorWriteLine(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

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
            Console.WriteLine($"\nerror loading 'init.ss': {e.Message}");
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
                prog.Eval(File.ReadAllText(file));
            }
            catch (Exception e)
            {
                Console.WriteLine($"error in '{file}': {e.Message}");
            }
        }

        return true;
    }

    private static int ParenDepth(string text)
    {
        int depth = 0;
        bool inString = false;
        for (int index = 0; index < text.Length; index++)
        {
            char ch = text[index];
            if (inString)
            {
                if (ch == '\\') index++;
                else if (ch == '"') inString = false;
                continue;
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

    private static string? ReadSubmission()
    {
        Console.Write("lisp> ");
        string? line = Console.ReadLine();
        if (line == null) return null;
        if (string.IsNullOrWhiteSpace(line)) return string.Empty;

        var buffer = new StringBuilder();
        buffer.AppendLine(line);
        while (ParenDepth(buffer.ToString()) > 0)
        {
            Console.Write("...    ");
            line = Console.ReadLine();
            if (line == null) break;
            buffer.AppendLine(line);
        }

        return buffer.ToString();
    }

    private static void EvaluateSubmission(Program prog, string input)
    {
        while (input.Trim().Length > 0)
        {
            var result = prog.EvalOne(input, out input);
            if (result != null) ColorWriteLine($"{Util.Dump(result)}\n", ConsoleColor.Yellow);
        }
    }

    private static void RunRepl(Program prog)
    {
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
                Console.WriteLine($"error: {e.Message}");
            }
        }
    }

    [STAThread]
    static void Main(string[] args)
    {
        var ver = Assembly.GetEntryAssembly()?.GetName().Version;
        Console.WriteLine($"*** Lisp ver {ver} - Copyright (c) 2003 by Ilias H. Mavreas ***\n");
        var prog = new Program();
        LoadInit(prog);
        if (RunFiles(prog, args)) return;
        RunRepl(prog);
    }
}
