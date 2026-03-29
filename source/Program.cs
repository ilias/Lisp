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

    public static long Iterations
    {
        get => RuntimeContext.Iterations;
        set => RuntimeContext.Iterations = value;
    }

    public static long TailCalls
    {
        get => RuntimeContext.TailCalls;
        set => RuntimeContext.TailCalls = value;
    }

    public static long EnvFrames
    {
        get => RuntimeContext.EnvFrames;
        set => RuntimeContext.EnvFrames = value;
    }

    public static long PrimCalls
    {
        get => RuntimeContext.PrimCalls;
        set => RuntimeContext.PrimCalls = value;
    }

    public static long InterpEmits
    {
        get => RuntimeContext.InterpEmits;
        set => RuntimeContext.InterpEmits = value;
    }

    public static long InterpExecs
    {
        get => RuntimeContext.InterpExecs;
        set => RuntimeContext.InterpExecs = value;
    }

    public static long TreeWalkCalls
    {
        get => RuntimeContext.TreeWalkCalls;
        set => RuntimeContext.TreeWalkCalls = value;
    }

    public static long TotalExprs
    {
        get => RuntimeContext.TotalExprs;
        set => RuntimeContext.TotalExprs = value;
    }

    public static long TotalIterations
    {
        get => RuntimeContext.TotalIterations;
        set => RuntimeContext.TotalIterations = value;
    }

    public static long TotalTailCalls
    {
        get => RuntimeContext.TotalTailCalls;
        set => RuntimeContext.TotalTailCalls = value;
    }

    public static long TotalEnvFrames
    {
        get => RuntimeContext.TotalEnvFrames;
        set => RuntimeContext.TotalEnvFrames = value;
    }

    public static long TotalPrimCalls
    {
        get => RuntimeContext.TotalPrimCalls;
        set => RuntimeContext.TotalPrimCalls = value;
    }

    public static long TotalInterpEmits
    {
        get => RuntimeContext.TotalInterpEmits;
        set => RuntimeContext.TotalInterpEmits = value;
    }

    public static long TotalInterpExecs
    {
        get => RuntimeContext.TotalInterpExecs;
        set => RuntimeContext.TotalInterpExecs = value;
    }

    public static long TotalTreeWalkCalls
    {
        get => RuntimeContext.TotalTreeWalkCalls;
        set => RuntimeContext.TotalTreeWalkCalls = value;
    }

    public static long TotalAllocated
    {
        get => RuntimeContext.TotalAllocated;
        set => RuntimeContext.TotalAllocated = value;
    }

    public static double TotalElapsedMs
    {
        get => RuntimeContext.TotalElapsedMs;
        set => RuntimeContext.TotalElapsedMs = value;
    }

    public static Dictionary<string, long> InterpEmitKinds => RuntimeContext.InterpEmitKinds;
    public static Dictionary<string, long> InterpExecKinds => RuntimeContext.InterpExecKinds;
    public static Dictionary<string, long> TotalInterpEmitKinds => RuntimeContext.TotalInterpEmitKinds;
    public static Dictionary<string, long> TotalInterpExecKinds => RuntimeContext.TotalInterpExecKinds;

    public static Program? current
    {
        get => InterpreterContext.Current?.Program;
        private set => InterpreterContext.Current = value?.Context;
    }

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
        RuntimeContext.StatsAllocStart = GC.GetTotalAllocatedBytes(precise: false);
        RuntimeContext.StatsGc0 = GC.CollectionCount(0);
        RuntimeContext.StatsGc1 = GC.CollectionCount(1);
        RuntimeContext.StatsGc2 = GC.CollectionCount(2);
    }

    public static void RecordInterpEmit(Expression expr)
        => InterpreterContext.RecordInterpEmit(expr);

    public static void RecordInterpExec(Expression expr)
        => InterpreterContext.RecordInterpExec(expr);

    public static void EndStats(Stopwatch sw)
    {
        sw.Stop();
        long allocDelta = GC.GetTotalAllocatedBytes(precise: false) - RuntimeContext.StatsAllocStart;
        long heapBytes = GC.GetTotalMemory(false);
        int gc0 = GC.CollectionCount(0) - RuntimeContext.StatsGc0;
        int gc1 = GC.CollectionCount(1) - RuntimeContext.StatsGc1;
        int gc2 = GC.CollectionCount(2) - RuntimeContext.StatsGc2;
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
        WriteStatsReport(
            ConsoleOutput.WriteStats,
            ConsoleOutput.WriteStatsSegments,
            title: "  stats:",
            elapsedMs: sw.Elapsed.TotalMilliseconds,
            iterations: Iterations,
            tailCalls: TailCalls,
            envFrames: EnvFrames,
            primCalls: PrimCalls,
            interpEmits: InterpEmits,
            interpExecs: InterpExecs,
            treeWalkCalls: TreeWalkCalls,
            allocatedBytes: allocDelta,
            heapBytes: heapBytes,
            gc0: gc0,
            gc1: gc1,
            gc2: gc2,
            emitKinds: InterpEmitKinds,
            execKinds: InterpExecKinds);
    }

    public static void PrintTotals()
    {
        ConsoleOutput.WriteStatsTotal($"  totals ({TotalExprs:N0} exprs):");
        WriteStatsReport(
            ConsoleOutput.WriteStatsTotal,
            ConsoleOutput.WriteStatsTotalSegments,
            title: null,
            elapsedMs: TotalElapsedMs,
            iterations: TotalIterations,
            tailCalls: TotalTailCalls,
            envFrames: TotalEnvFrames,
            primCalls: TotalPrimCalls,
            interpEmits: TotalInterpEmits,
            interpExecs: TotalInterpExecs,
            treeWalkCalls: TotalTreeWalkCalls,
            allocatedBytes: TotalAllocated,
            heapBytes: null,
            gc0: null,
            gc1: null,
            gc2: null,
            emitKinds: TotalInterpEmitKinds,
            execKinds: TotalInterpExecKinds);
    }

    private static void WriteStatsReport(
        Action<string> writeLine,
        Action<IEnumerable<ConsoleOutput.Segment>> writeSegments,
        string? title,
        double elapsedMs,
        long iterations,
        long tailCalls,
        long envFrames,
        long primCalls,
        long interpEmits,
        long interpExecs,
        long treeWalkCalls,
        long allocatedBytes,
        long? heapBytes,
        int? gc0,
        int? gc1,
        int? gc2,
        Dictionary<string, long> emitKinds,
        Dictionary<string, long> execKinds)
    {
        if (!string.IsNullOrEmpty(title))
            writeLine(title);

        WriteStatsField(writeSegments, "elapsed", $"{elapsedMs,10:F3} ms");
        WriteStatsField(writeSegments, "status", FormatStatusSummary(interpEmits, interpExecs, treeWalkCalls), GetStatusColor(interpEmits, interpExecs, treeWalkCalls));
        WriteStatsField(writeSegments, "runtime", FormatRuntimePathSummary(interpExecs, treeWalkCalls), GetRuntimeColor(interpExecs, treeWalkCalls));
        WriteStatsField(writeSegments, "work", $"closures={iterations:N0}, prims={primCalls:N0}");
        WriteStatsField(writeSegments, "control", $"tail-calls={tailCalls:N0}, env-frames={envFrames:N0}");
        WriteStatsField(writeSegments, "throughput", FormatThroughputSummary(elapsedMs, iterations, primCalls, interpExecs, treeWalkCalls));
        WriteStatsField(writeSegments, "fallback", FormatFallbackSummary(interpEmits, interpExecs, treeWalkCalls), GetFallbackColor(interpEmits, interpExecs, treeWalkCalls));
        WriteStatsField(writeSegments, "memory", $"allocated={FormatBytes(allocatedBytes)}{FormatOptionalMemory(heapBytes, gc0, gc1, gc2)}");

        if (emitKinds.Count > 0)
            WriteStatsField(writeSegments, "emit-kinds", FormatCounterSummary(emitKinds), ConsoleColor.DarkYellow);
        if (execKinds.Count > 0)
            WriteStatsField(writeSegments, "exec-kinds", FormatCounterSummary(execKinds), ConsoleColor.Yellow);
    }

    private static void WriteStatsField(Action<IEnumerable<ConsoleOutput.Segment>> writeSegments, string label, string value, ConsoleColor? valueColor = null)
    {
        writeSegments(
        [
            new("    "),
            new($"{label,-11}", ConsoleColor.Gray),
            new(value, valueColor),
        ]);
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

    private static string FormatCounterSummary(Dictionary<string, long> counters)
    {
        var ordered = counters.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).ToArray();
        int shown = Math.Min(4, ordered.Length);
        string summary = string.Join(", ", ordered.Take(shown).Select(kv => $"{kv.Key}={kv.Value:N0}"));
        if (ordered.Length > shown)
            summary += $", +{ordered.Length - shown} more";
        return summary;
    }

    private static string FormatFallbackSummary(long interpEmits, long interpExecs, long treeWalkCalls)
    {
        if (interpEmits == 0 && interpExecs == 0 && treeWalkCalls == 0)
            return "none";

        List<string> parts = [];
        if (interpEmits != 0) parts.Add($"sites={interpEmits:N0}");
        if (interpExecs != 0) parts.Add($"runs={interpExecs:N0}");
        if (treeWalkCalls != 0) parts.Add($"tree-walk={treeWalkCalls:N0}");
        return string.Join(", ", parts);
    }

    private static string FormatThroughputSummary(double elapsedMs, long iterations, long primCalls, long interpExecs, long treeWalkCalls)
    {
        if (elapsedMs <= 0.0)
            return "n/a";

        if (!HasMeaningfulThroughputSample(elapsedMs, iterations, primCalls))
            return "sample too small";

        double closuresPerMs = iterations / elapsedMs;
        double primsPerMs = primCalls / elapsedMs;
        double fallbackPerKClosures = iterations == 0 ? 0.0 : ((interpExecs + treeWalkCalls) * 1000.0) / iterations;
        string summary = $"closures/ms={closuresPerMs:F1}, prims/ms={primsPerMs:F1}";
        if (interpExecs != 0 || treeWalkCalls != 0)
            summary += $", fallbacks/1k-closures={fallbackPerKClosures:F2}";
        return summary;
    }

    private static bool HasMeaningfulThroughputSample(double elapsedMs, long iterations, long primCalls) =>
        elapsedMs >= 1.0 && (iterations >= 100 || primCalls >= 100);

    private static string FormatOptionalMemory(long? heapBytes, int? gc0, int? gc1, int? gc2)
    {
        List<string> parts = [];
        if (heapBytes != null) parts.Add($"heap={FormatBytes(heapBytes.Value)}");
        if (gc0 != null && gc1 != null && gc2 != null) parts.Add($"gc={gc0}/{gc1}/{gc2}");
        return parts.Count == 0 ? string.Empty : ", " + string.Join(", ", parts);
    }

    private static string FormatRuntimePathSummary(long interpExecs, long treeWalkCalls)
    {
        if (interpExecs == 0 && treeWalkCalls == 0)
            return "vm-only";
        if (interpExecs > 0 && treeWalkCalls == 0)
            return $"vm + interp fallback ({interpExecs:N0} run{(interpExecs == 1 ? string.Empty : "s")})";
        if (interpExecs == 0)
            return $"vm + tree-walk fallback ({treeWalkCalls:N0} call{(treeWalkCalls == 1 ? string.Empty : "s")})";
        return $"vm + fallback (interp={interpExecs:N0}, tree-walk={treeWalkCalls:N0})";
    }

    private static string FormatStatusSummary(long interpEmits, long interpExecs, long treeWalkCalls)
    {
        if (interpExecs == 0 && treeWalkCalls == 0 && interpEmits == 0)
            return "clean vm path";
        if (interpExecs == 0 && treeWalkCalls == 0)
            return $"clean run, fallback sites present ({interpEmits:N0})";
        if (interpExecs != 0 && treeWalkCalls == 0)
            return "interp fallback observed";
        if (interpExecs == 0)
            return "tree-walk fallback observed";
        return "multiple fallback paths observed";
    }

    private static ConsoleColor GetStatusColor(long interpEmits, long interpExecs, long treeWalkCalls)
    {
        if (interpExecs == 0 && treeWalkCalls == 0 && interpEmits == 0)
            return ConsoleColor.Green;
        if (interpExecs == 0 && treeWalkCalls == 0)
            return ConsoleColor.DarkYellow;
        if (interpExecs != 0 && treeWalkCalls != 0)
            return ConsoleColor.Red;
        return ConsoleColor.Yellow;
    }

    private static ConsoleColor GetRuntimeColor(long interpExecs, long treeWalkCalls)
    {
        if (interpExecs == 0 && treeWalkCalls == 0)
            return ConsoleColor.Green;
        if (interpExecs != 0 && treeWalkCalls != 0)
            return ConsoleColor.Red;
        return ConsoleColor.Yellow;
    }

    private static ConsoleColor GetFallbackColor(long interpEmits, long interpExecs, long treeWalkCalls)
    {
        if (interpEmits == 0 && interpExecs == 0 && treeWalkCalls == 0)
            return ConsoleColor.Green;
        if (interpExecs == 0 && treeWalkCalls == 0)
            return ConsoleColor.DarkYellow;
        if (interpExecs != 0 && treeWalkCalls != 0)
            return ConsoleColor.Red;
        return ConsoleColor.Yellow;
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
        if (ShowInputLines) Console.WriteLine($">> {exp[..^after.Length].Trim()}");
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