namespace Lisp;

public sealed class InterpreterHost
{
    public Program Program { get; }
    public InterpreterRuntime Runtime { get; }

    public InterpreterHost(string? primitiveProfile, bool statsEnabled)
    {
        Runtime = new InterpreterRuntime();
        Program = new Program(primitiveProfile);
        if (statsEnabled)
            Program.Stats = true;
    }

    public void AddLibraryPath(string path)
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
    }

    public void LoadInitFromBaseDirectory()
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
            Program.LoadInit(initPath);
        }
        catch (Exception e)
        {
            Console.WriteLine();
            Console.WriteLine(ExceptionDisplay.FormatForConsole("error loading 'init.ss': ", e));
        }
    }

    public object Eval(string expr, string sourceName = "<host>")
        => Runtime.ExecuteWithEvaluationScope(() => Program.Eval(expr, sourceName));

    public void EvalFile(string filePath)
        => Runtime.ExecuteWithEvaluationScope(() => Program.Eval(File.ReadAllText(filePath), filePath));

    internal object EvalReplOne(ref string input)
    {
        string local = input;
        var result = Runtime.ExecuteWithEvaluationScope(() => Program.EvalOne(local, out local, "<repl>"));
        input = local;
        return result;
    }
}
