namespace Lisp;

internal abstract record InitEntry;
internal sealed record InitMacro(Pair Def) : InitEntry;
internal sealed record InitExpr(Expression E) : InitEntry;

public class Program
{
    public static bool lastValue = true;
    public static bool Stats = false;
    public static bool ShowInputLines = false;
    public static long Iterations;
    public static long TailCalls;
    public static long EnvFrames;
    public static long PrimCalls;
    public static long TotalExprs;
    public static long TotalIterations;
    public static long TotalTailCalls;
    public static long TotalEnvFrames;
    public static long TotalPrimCalls;
    public static long TotalAllocated;
    public static double TotalElapsedMs;
    [ThreadStatic] public static Program? current;

    public Env initEnv;

    private static long _statsAllocStart;
    private static int _statsGC0;
    private static int _statsGC1;
    private static int _statsGC2;
    private static List<InitEntry>? _initCache;
    private static string? _initCachePath;
    private static DateTime _initCacheStamp;
    private static readonly Symbol _sMacro = Symbol.Create("macro");

    private static readonly string[] _primsToRegister =
    [
        "exact?", "inexact?", "number?", "rational?", "integer?", "real?", "complex?",
        "floor", "ceiling", "round", "truncate",
        "exact->inexact", "inexact->exact",
        "numerator", "denominator",
        "real-part", "imag-part", "make-rectangular", "make-polar", "magnitude", "angle",
        "error-object?", "error-object-message", "error-object-irritants",
        "%raise", "%try-handler", "%make-error-object",
    ];

    public Program()
    {
        current = this;
        initEnv = new Extended_Env(null, null, new Env());
    }

    public static void ResetTotals()
    {
        TotalExprs = TotalIterations = TotalTailCalls = TotalEnvFrames = TotalPrimCalls = TotalAllocated = 0;
        TotalElapsedMs = 0.0;
    }

    public static void BeginStats()
    {
        Iterations = TailCalls = EnvFrames = PrimCalls = 0;
        _statsAllocStart = GC.GetTotalAllocatedBytes(precise: false);
        _statsGC0 = GC.CollectionCount(0);
        _statsGC1 = GC.CollectionCount(1);
        _statsGC2 = GC.CollectionCount(2);
    }

    public static void EndStats(Stopwatch sw)
    {
        sw.Stop();
        long allocDelta = GC.GetTotalAllocatedBytes(precise: false) - _statsAllocStart;
        long heapBytes = GC.GetTotalMemory(false);
        int gc0 = GC.CollectionCount(0) - _statsGC0;
        int gc1 = GC.CollectionCount(1) - _statsGC1;
        int gc2 = GC.CollectionCount(2) - _statsGC2;
        TotalExprs++;
        TotalIterations += Iterations;
        TotalTailCalls += TailCalls;
        TotalEnvFrames += EnvFrames;
        TotalPrimCalls += PrimCalls;
        TotalAllocated += allocDelta;
        TotalElapsedMs += sw.Elapsed.TotalMilliseconds;
        ConsoleOutput.WriteStats($"  time:       {sw.Elapsed.TotalMilliseconds,10:F3} ms");
        ConsoleOutput.WriteStats($"  iterations: {Iterations,10:N0}   (closure calls)");
        ConsoleOutput.WriteStats($"  tail-calls: {TailCalls,10:N0}   (TCO bounces)");
        ConsoleOutput.WriteStats($"  env-frames: {EnvFrames,10:N0}   (scopes created)");
        ConsoleOutput.WriteStats($"  primitives: {PrimCalls,10:N0}   (built-in calls)");
        ConsoleOutput.WriteStats($"  allocated:  {FormatBytes(allocDelta),10}   (this eval)");
        ConsoleOutput.WriteStats($"  heap:       {FormatBytes(heapBytes),10}   (live GC heap)");
        ConsoleOutput.WriteStats($"  gc[0/1/2]:  {gc0}/{gc1}/{gc2}");
    }

    public static void PrintTotals()
    {
        ConsoleOutput.WriteStatsTotal($"  ── totals ({TotalExprs:N0} exprs) ──────────────────");
        ConsoleOutput.WriteStatsTotal($"  total time: {TotalElapsedMs,10:F3} ms");
        ConsoleOutput.WriteStatsTotal($"  total iter: {TotalIterations,10:N0}   (closure calls)");
        ConsoleOutput.WriteStatsTotal($"  total tail: {TotalTailCalls,10:N0}   (TCO bounces)");
        ConsoleOutput.WriteStatsTotal($"  total env:  {TotalEnvFrames,10:N0}   (scopes created)");
        ConsoleOutput.WriteStatsTotal($"  total prim: {TotalPrimCalls,10:N0}   (built-in calls)");
        ConsoleOutput.WriteStatsTotal($"  total alloc:{FormatBytes(TotalAllocated),10}   (since reset)");
    }

    private static string FormatBytes(long bytes) =>
        bytes >= 1_048_576 ? $"{bytes / 1_048_576.0:F2} MB" :
        bytes >= 1_024 ? $"{bytes / 1_024.0:F1} KB" :
        $"{bytes} B";

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
        if (Expression.IsTraceOn(_sMacro))
            ConsoleOutput.WriteTrace(Util.Dump("macro:   ", expanded!));
        return expanded;
    }

    private static Expression CompileTopLevelForm(object parsedObj) =>
        parsedObj is Pair dp && dp.car?.ToString() == "DEFINE"
            ? new Define(dp)
            : Expression.Parse(parsedObj);

    private object EvalCompiledTopLevel(object parsedObj)
    {
        var sw = Stats ? Stopwatch.StartNew() : null;
        if (Stats) BeginStats();
        var answer = Eval(CompileTopLevelForm(parsedObj));
        if (answer is Pair answerPair && answerPair.car is Var v)
        {
            answerPair.car = v.GetName();
            answer = Eval(Expression.Parse(answerPair));
        }
        if (Stats && sw != null) EndStats(sw);
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
        if (_initCache != null && _initCachePath == path && _initCacheStamp == stamp)
        {
            Macro.macros.Clear();
            foreach (var entry in _initCache)
                switch (entry)
                {
                    case InitMacro m: Macro.Add(m.Def); break;
                    case InitExpr ie: Vm.Execute(BytecodeCompiler.CompileTop(ie.E), initEnv); break;
                }
            RegisterPrimsAfterInit();
            return;
        }

        var text = File.ReadAllText(path);
        List<InitEntry> cache = [];
        var exp = text;
        while (true)
        {
            var parsedObj = Util.Parse(exp, out var after);
            if (ShowInputLines) Console.WriteLine($">> {exp[..^after.Length].Trim()}");
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
        _initCache = cache;
        _initCachePath = path;
        _initCacheStamp = stamp;
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
    {
        var parsedObj = Util.Parse(exp, out after);
        if (ShowInputLines) Console.WriteLine($">> {exp[..^after.Length].Trim()}");
        if (TryGetTopLevelDefinition(parsedObj, out var definition, out var result))
        {
            if (definition != null) Macro.Add(definition);
            return result;
        }
        return EvalTopLevelForm(parsedObj);
    }

    public object Eval(string exp)
    {
        object answer = Pair.Empty;
        while (true)
        {
            var parsedObj = Util.Parse(exp, out var after);
            if (ShowInputLines) Console.WriteLine($">> {exp[..^after.Length].Trim()}");
            if (TryGetTopLevelDefinition(parsedObj, out var definition, out answer))
            {
                if (definition != null) Macro.Add(definition);
                if (after == "") return answer;
                exp = after;
                continue;
            }
            answer = EvalTopLevelForm(parsedObj);
            if (after != "" && !lastValue) ConsoleOutput.WriteResult(answer);
            if (after == "") return answer;
            exp = after;
        }
    }
}