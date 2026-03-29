namespace Lisp;

internal abstract record InitEntry;
internal sealed record InitMacro(Pair Def) : InitEntry;
internal sealed record InitExpr(Expression E) : InitEntry;

public class Program
{
    private static InterpreterContext RuntimeContext => InterpreterContext.RequireCurrent();

    public static bool lastValue
    {
        get => RuntimeContext.LastValue;
        set => RuntimeContext.LastValue = value;
    }

    public static bool Stats
    {
        get => RuntimeContext.Stats;
        set => RuntimeContext.Stats = value;
    }

    public static bool ShowInputLines
    {
        get => RuntimeContext.ShowInputLines;
        set => RuntimeContext.ShowInputLines = value;
    }

    internal static Program? current
    {
        get => InterpreterContext.Current?.Program;
        private set => InterpreterContext.Current = value?.Context;
    }

    public static Env CurrentInitEnv => RequireCurrent().initEnv;

    public Env initEnv;
    internal InterpreterContext Context { get; }
    private static readonly Symbol _sMacro = Symbol.Create("macro");

    private static readonly string[] _primsToRegister =
    [
        "exact?", "inexact?", "number?", "rational?", "integer?", "real?", "complex?", "isPrime",
        "floor", "ceiling", "round", "truncate",
        "exact->inexact", "inexact->exact",
        "p-adic",
        "numerator", "denominator",
        "real-part", "imag-part", "make-rectangular", "make-polar", "magnitude", "angle",
        "error-object?", "error-object-message", "error-object-irritants",
        "%raise", "%try-handler", "%make-error-object",
        "load",
    ];

    public Program()
    {
        Context = new InterpreterContext { Program = this };
        current = this;
        initEnv = new Extended_Env(null, null, new Env());
    }

    internal static Program RequireCurrent() =>
        InterpreterContext.RequireCurrent().Program ?? throw new InvalidOperationException("No active interpreter instance");

    public static void ResetTotals()
        => RuntimeStats.ResetTotals();

    public static void BeginStats()
        => InterpreterContext.BeginStats();

    public static void RecordInterpEmit(Expression expr)
        => InterpreterContext.RecordInterpEmit(expr);

    public static void RecordInterpExec(Expression expr)
        => InterpreterContext.RecordInterpExec(expr);

    public static void EndStats(Stopwatch sw)
        => RuntimeStats.EndExpression(sw);

    public static void PrintTotals()
        => RuntimeStats.PrintTotals();

    private static bool TryGetTopLevelDefinition(object? parsedObj, out Pair? definition, out object result)
    {
        switch (parsedObj)
        {
            case Pair p when p.car?.ToString() == "macro":
                definition = p.cdr!;
                result = p.cdr!.car!;
                return true;
            case Pair p when p.car?.ToString() == "define-syntax":
                definition = Macro.TranslateDefineSyntax(p);
                Util.PropagateSourceDeep(p, definition);
                result = p.cdr!.car!;
                return true;
            default:
                definition = null;
                result = null!;
                return false;
        }
    }

    private static object? ExpandTopLevelForm(object? parsedObj)
    {
        var expanded = Macro.Check(parsedObj);
        Util.PropagateSourceDeep(parsedObj, expanded);
        if (Expression.IsTraceOn(_sMacro))
            ConsoleOutput.WriteTrace(Util.Dump("macro:   ", expanded!));
        return expanded;
    }

    private static object? ParseWithContext(string text, Util.SourceDocument document, int baseOffset, out string after)
    {
        using var _ = Util.PushSourceContext(document, baseOffset);
        return Util.Parse(text, out after);
    }

    private static Expression CompileTopLevelForm(object parsedObj) =>
        parsedObj is Pair dp && dp.car?.ToString() == "DEFINE"
            ? new Define(dp)
            : Expression.Parse(parsedObj);

    private object EvalCompiledTopLevel(object parsedObj)
    {
        var sw = RuntimeStats.StartExpression();
        var answer = Eval(CompileTopLevelForm(parsedObj));
        RuntimeStats.EndExpression(sw);
        return answer;
    }

    private object EvalTopLevelForm(object? parsedObj)
    {
        var expanded = ExpandTopLevelForm(parsedObj)!;
        return expanded is Pair defPair && defPair.car?.ToString() == "DEFINE"
            ? new Define(defPair).Eval(initEnv)
            : EvalCompiledTopLevel(expanded);
    }

    public void LoadInit(string path)
    {
        var stamp = File.GetLastWriteTimeUtc(path);
        if (InitCacheStore.TryGet(path, stamp) is { } cachedEntries)
        {
            Macro.Clear();
            foreach (var entry in cachedEntries)
                switch (entry)
                {
                    case InitMacro m: Macro.Add(m.Def); break;
                    case InitExpr ie: Vm.Execute(BytecodeCompiler.CompileTop(ie.E), initEnv); break;
                }
            RegisterPrimsAfterInit();
            return;
        }

        var text = File.ReadAllText(path);
        var document = new Util.SourceDocument(text, path);
        List<InitEntry> cache = [];
        var exp = text;
        while (true)
        {
            var parsedObj = ParseWithContext(exp, document, text.Length - exp.Length, out var after);
            if (RuntimeContext.ShowInputLines) Console.WriteLine($">> {exp[..^after.Length].Trim()}");
            if (parsedObj == null) break;
            if (TryGetTopLevelDefinition(parsedObj, out var definition, out _))
            {
                if (definition != null)
                {
                    cache.Add(new InitMacro(definition));
                    Macro.Add(definition);
                }
            }
            else
            {
                var expanded = ExpandTopLevelForm(parsedObj)!;
                var compiled = CompileTopLevelForm(expanded);
                cache.Add(new InitExpr(compiled));
                Vm.Execute(BytecodeCompiler.CompileTop(compiled), initEnv);
            }
            if (after == "") break;
            exp = after;
        }
        InitCacheStore.Save(path, stamp, cache);
        RegisterPrimsAfterInit();
    }

    private void RegisterPrimsAfterInit()
    {
        foreach (var name in _primsToRegister)
            if (Prim.list.TryGetValue(name, out var p))
                initEnv.table[Symbol.Create(name)] = p;
        if (Prim.list.TryGetValue("inexact->exact", out var e2e)) initEnv.table[Symbol.Create("exact")] = e2e;
        if (Prim.list.TryGetValue("exact->inexact", out var e2i)) initEnv.table[Symbol.Create("inexact")] = e2i;
    }

    public object Eval(Expression exp)
    {
        var chunk = BytecodeCompiler.CompileTop(exp);
        return Vm.Execute(chunk, initEnv);
    }

    public object EvalOne(string exp, out string after)
        => EvalOne(exp, out after, sourceName: null);

    public object EvalOne(string exp, out string after, string? sourceName)
    {
        var document = new Util.SourceDocument(exp, sourceName);
        var parsedObj = ParseWithContext(exp, document, 0, out after);
        if (RuntimeContext.ShowInputLines) Console.WriteLine($">> {exp[..^after.Length].Trim()}");
        if (TryGetTopLevelDefinition(parsedObj, out var definition, out var result))
        {
            if (definition != null) Macro.Add(definition);
            return result;
        }

        string currentExpr = after.Length == 0 ? exp : exp[..^after.Length];
        return Eval(currentExpr);
    }

    public object Eval(string exp) => Eval(exp, sourceName: null);

    public object Eval(string exp, string? sourceName)
    {
        var document = new Util.SourceDocument(exp, sourceName);
        string fullText = exp;
        object answer = Pair.Empty;
        while (true)
        {
            var parsedObj = ParseWithContext(exp, document, fullText.Length - exp.Length, out var after);
            if (RuntimeContext.ShowInputLines) Console.WriteLine($">> {exp[..^after.Length].Trim()}");
            if (TryGetTopLevelDefinition(parsedObj, out var definition, out answer))
            {
                if (definition != null) Macro.Add(definition);
                if (after == "") return answer;
                exp = after;
                continue;
            }
            answer = EvalTopLevelForm(parsedObj);
            if (after != "" && !RuntimeContext.LastValue) ConsoleOutput.WriteResult(answer);
            if (after == "") return answer;
            exp = after;
        }
    }
}