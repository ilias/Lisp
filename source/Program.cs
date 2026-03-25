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
    public static long InterpEmits;
    public static long InterpExecs;
    public static long TreeWalkCalls;
    public static long TotalExprs;
    public static long TotalIterations;
    public static long TotalTailCalls;
    public static long TotalEnvFrames;
    public static long TotalPrimCalls;
    public static long TotalInterpEmits;
    public static long TotalInterpExecs;
    public static long TotalTreeWalkCalls;
    public static long TotalAllocated;
    public static double TotalElapsedMs;
    public static Dictionary<string, long> InterpEmitKinds = [];
    public static Dictionary<string, long> InterpExecKinds = [];
    public static Dictionary<string, long> TotalInterpEmitKinds = [];
    public static Dictionary<string, long> TotalInterpExecKinds = [];
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
        TotalExprs = TotalIterations = TotalTailCalls = TotalEnvFrames = TotalPrimCalls = 0;
        TotalInterpEmits = TotalInterpExecs = TotalTreeWalkCalls = TotalAllocated = 0;
        TotalElapsedMs = 0.0;
        TotalInterpEmitKinds.Clear();
        TotalInterpExecKinds.Clear();
    }

    public static void BeginStats()
    {
        Iterations = TailCalls = EnvFrames = PrimCalls = 0;
        InterpEmits = InterpExecs = TreeWalkCalls = 0;
        InterpEmitKinds.Clear();
        InterpExecKinds.Clear();
        _statsAllocStart = GC.GetTotalAllocatedBytes(precise: false);
        _statsGC0 = GC.CollectionCount(0);
        _statsGC1 = GC.CollectionCount(1);
        _statsGC2 = GC.CollectionCount(2);
    }

    public static void RecordInterpEmit(Expression expr)
    {
        InterpEmits++;
        AddCounter(InterpEmitKinds, GetExpressionKind(expr));
    }

    public static void RecordInterpExec(Expression expr)
    {
        InterpExecs++;
        AddCounter(InterpExecKinds, GetExpressionKind(expr));
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
        TotalInterpEmits += InterpEmits;
        TotalInterpExecs += InterpExecs;
        TotalTreeWalkCalls += TreeWalkCalls;
        TotalAllocated += allocDelta;
        TotalElapsedMs += sw.Elapsed.TotalMilliseconds;
        MergeCounters(TotalInterpEmitKinds, InterpEmitKinds);
        MergeCounters(TotalInterpExecKinds, InterpExecKinds);
        string runtimeSummary = FormatRuntimePathSummary(InterpExecs, TreeWalkCalls);
        ConsoleOutput.WriteStats($"  time:       {sw.Elapsed.TotalMilliseconds,10:F3} ms");
        ConsoleOutput.WriteStats($"  iterations: {Iterations,10:N0}   (closure calls)");
        ConsoleOutput.WriteStats($"  tail-calls: {TailCalls,10:N0}   (TCO bounces)");
        ConsoleOutput.WriteStats($"  env-frames: {EnvFrames,10:N0}   (scopes created)");
        ConsoleOutput.WriteStats($"  primitives: {PrimCalls,10:N0}   (built-in calls)");
        ConsoleOutput.WriteStats($"  interp-emits:{InterpEmits,10:N0}   (compiler fallback sites)");
        ConsoleOutput.WriteStats($"  interp-execs:{InterpExecs,10:N0}   (runtime AST fallbacks)");
        ConsoleOutput.WriteStats($"  tree-walk:  {TreeWalkCalls,10:N0}   (closure evals outside VM)");
        ConsoleOutput.WriteStats($"  runtime:    {runtimeSummary}");
        ConsoleOutput.WriteStats($"  allocated:  {FormatBytes(allocDelta),10}   (this eval)");
        ConsoleOutput.WriteStats($"  heap:       {FormatBytes(heapBytes),10}   (live GC heap)");
        ConsoleOutput.WriteStats($"  gc[0/1/2]:  {gc0}/{gc1}/{gc2}");
        WriteCounterSummary(ConsoleOutput.WriteStats, "  interp-kinds emit:", InterpEmitKinds);
        WriteCounterSummary(ConsoleOutput.WriteStats, "  interp-kinds exec:", InterpExecKinds);
    }

    public static void PrintTotals()
    {
        string runtimeSummary = FormatRuntimePathSummary(TotalInterpExecs, TotalTreeWalkCalls);
        ConsoleOutput.WriteStatsTotal($"  ── totals ({TotalExprs:N0} exprs) ──────────────────");
        ConsoleOutput.WriteStatsTotal($"  total time: {TotalElapsedMs,10:F3} ms");
        ConsoleOutput.WriteStatsTotal($"  total iter: {TotalIterations,10:N0}   (closure calls)");
        ConsoleOutput.WriteStatsTotal($"  total tail: {TotalTailCalls,10:N0}   (TCO bounces)");
        ConsoleOutput.WriteStatsTotal($"  total env:  {TotalEnvFrames,10:N0}   (scopes created)");
        ConsoleOutput.WriteStatsTotal($"  total prim: {TotalPrimCalls,10:N0}   (built-in calls)");
        ConsoleOutput.WriteStatsTotal($"  total ie:   {TotalInterpEmits,10:N0}   (compiler fallback sites)");
        ConsoleOutput.WriteStatsTotal($"  total ix:   {TotalInterpExecs,10:N0}   (runtime AST fallbacks)");
        ConsoleOutput.WriteStatsTotal($"  total tw:   {TotalTreeWalkCalls,10:N0}   (closure evals outside VM)");
        ConsoleOutput.WriteStatsTotal($"  total path: {runtimeSummary}");
        ConsoleOutput.WriteStatsTotal($"  total alloc:{FormatBytes(TotalAllocated),10}   (since reset)");
        WriteCounterSummary(ConsoleOutput.WriteStatsTotal, "  total ie kinds:", TotalInterpEmitKinds);
        WriteCounterSummary(ConsoleOutput.WriteStatsTotal, "  total ix kinds:", TotalInterpExecKinds);
    }

    private static void AddCounter(Dictionary<string, long> counters, string key) =>
        counters[key] = counters.GetValueOrDefault(key) + 1;

    private static void MergeCounters(Dictionary<string, long> totals, Dictionary<string, long> counters)
    {
        foreach (var kv in counters)
            totals[kv.Key] = totals.GetValueOrDefault(kv.Key) + kv.Value;
    }

    private static string GetExpressionKind(Expression expr) => expr switch
    {
        LetSyntax letSyntax => letSyntax.IsLetrec ? "LetRecSyntax" : "LetSyntax",
        _ => expr.GetType().Name,
    };

    private static void WriteCounterSummary(Action<string> writeLine, string label, Dictionary<string, long> counters)
    {
        if (counters.Count == 0) return;
        string summary = string.Join(", ", counters.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value:N0}"));
        writeLine($"{label} {summary}");
    }

    private static string FormatRuntimePathSummary(long interpExecs, long treeWalkCalls)
    {
        if (interpExecs == 0 && treeWalkCalls == 0)
            return "vm-only";
        if (interpExecs > 0 && treeWalkCalls == 0)
            return $"mixed: interp-only fallback ({interpExecs:N0})";
        if (interpExecs == 0)
            return $"mixed: tree-walk-only fallback ({treeWalkCalls:N0})";
        return $"mixed: interp={interpExecs:N0}, tree-walk={treeWalkCalls:N0}";
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

        string currentExpr = after.Length == 0 ? exp : exp[..^after.Length];
        return Eval(currentExpr);
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