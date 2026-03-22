namespace Lisp;

public static class Interpreter
{
    public static bool EndProgram = false;

    [STAThread]
    static void Main(string[] args)
    {
        var ver = Assembly.GetEntryAssembly()?.GetName().Version;
        Console.WriteLine($"*** Lisp ver {ver} - Copyright (c) 2003 by Ilias H. Mavreas ***\n");
        var prog = new Program();
        var initPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "init.ss");
        if (File.Exists(initPath))
        {
            try
            {
                Console.Write("Initializing: loading 'init.ss'...");
                prog.LoadInit(initPath);
            }
            catch (Exception e) { Console.WriteLine($"\nerror loading 'init.ss': {e.Message}"); }
        }
        else
        {
            Console.WriteLine($"Warning: 'init.ss' not found at {initPath}");
        }
        if (args.Length > 0)
        {
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
                catch (Exception e) { Console.WriteLine($"error in '{file}': {e.Message}"); }
            }
            return;
        }

        static void ColorWriteLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        static int ParenDepth(string s)
        {
            int depth = 0;
            bool inStr = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inStr) { if (c == '\\') i++; else if (c == '"') inStr = false; continue; }
                if (c == ';') { while (i + 1 < s.Length && s[i + 1] != '\n') i++; continue; }
                if (c == '"') { inStr = true; continue; }
                if (c == '(') depth++;
                else if (c == ')') depth--;
            }
            return depth;
        }

        while (!EndProgram)
            try
            {
                Console.Write("lisp> ");
                string? line = Console.ReadLine();
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;
                var val = new StringBuilder();
                val.Append(line);
                val.Append('\n');
                while (ParenDepth(val.ToString()) > 0)
                {
                    Console.Write("...    ");
                    line = Console.ReadLine();
                    if (line == null) break;
                    val.Append(line);
                    val.Append('\n');
                }
                var input = val.ToString();
                while (input.Trim().Length > 0)
                {
                    var result = prog.EvalOne(input, out input);
                    if (result != null) ColorWriteLine($"{Util.Dump(result)}\n", ConsoleColor.Yellow);
                }
            }
            catch (Exception e) { Console.WriteLine($"error: {e.Message}"); }
    }
}
