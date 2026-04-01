namespace Lisp;

public sealed class InterpreterContext
{
    internal readonly record struct StatsReportSnapshot(
        double ElapsedMs,
        long Iterations,
        long TailCalls,
        long EnvFrames,
        long PrimCalls,
        long InterpEmits,
        long InterpExecs,
        long TreeWalkCalls,
        long AllocatedBytes,
        long? HeapBytes,
        int? Gc0,
        int? Gc1,
        int? Gc2,
        Dictionary<string, long> EmitKinds,
        Dictionary<string, long> ExecKinds);

    [ThreadStatic] private static InterpreterContext? _current;

    public static InterpreterContext? Current
    {
        get => _current;
        set => _current = value;
    }

    public static InterpreterContext RequireCurrent() =>
        _current ?? throw new InvalidOperationException("No active interpreter context");

    public Program? Program { get; set; }

    public Dictionary<object, object?> Macros { get; } = [];

    public bool LastValue { get; set; } = true;
    public bool Stats { get; set; }
    public bool ShowInputLines { get; set; }

    public long Iterations { get; set; }
    public long TailCalls { get; set; }
    public long EnvFrames { get; set; }
    public long PrimCalls { get; set; }
    public long InterpEmits { get; set; }
    public long InterpExecs { get; set; }
    public long TreeWalkCalls { get; set; }
    public long TotalExprs { get; set; }
    public long TotalIterations { get; set; }
    public long TotalTailCalls { get; set; }
    public long TotalEnvFrames { get; set; }
    public long TotalPrimCalls { get; set; }
    public long TotalInterpEmits { get; set; }
    public long TotalInterpExecs { get; set; }
    public long TotalTreeWalkCalls { get; set; }
    public long TotalAllocated { get; set; }
    public double TotalElapsedMs { get; set; }

    public long StatsAllocStart { get; set; }
    public int StatsGc0 { get; set; }
    public int StatsGc1 { get; set; }
    public int StatsGc2 { get; set; }

    public Dictionary<string, long> InterpEmitKinds { get; } = [];
    public Dictionary<string, long> InterpExecKinds { get; } = [];
    public Dictionary<string, long> TotalInterpEmitKinds { get; } = [];
    public Dictionary<string, long> TotalInterpExecKinds { get; } = [];

    public static bool IsStatsEnabled => Current?.Stats == true;

    public static void ResetTotals()
    {
        var context = RequireCurrent();
        context.TotalExprs = 0;
        context.TotalIterations = 0;
        context.TotalTailCalls = 0;
        context.TotalEnvFrames = 0;
        context.TotalPrimCalls = 0;
        context.TotalInterpEmits = 0;
        context.TotalInterpExecs = 0;
        context.TotalTreeWalkCalls = 0;
        context.TotalAllocated = 0;
        context.TotalElapsedMs = 0.0;
        context.TotalInterpEmitKinds.Clear();
        context.TotalInterpExecKinds.Clear();
    }

    public static void BeginStats()
    {
        var context = RequireCurrent();
        context.Iterations = 0;
        context.TailCalls = 0;
        context.EnvFrames = 0;
        context.PrimCalls = 0;
        context.InterpEmits = 0;
        context.InterpExecs = 0;
        context.TreeWalkCalls = 0;
        context.InterpEmitKinds.Clear();
        context.InterpExecKinds.Clear();
        context.StatsAllocStart = GC.GetTotalAllocatedBytes(precise: false);
        context.StatsGc0 = GC.CollectionCount(0);
        context.StatsGc1 = GC.CollectionCount(1);
        context.StatsGc2 = GC.CollectionCount(2);
    }

    internal static StatsReportSnapshot EndStats(Stopwatch stopwatch)
    {
        var context = RequireCurrent();
        stopwatch.Stop();
        long allocatedBytes = GC.GetTotalAllocatedBytes(precise: false) - context.StatsAllocStart;
        long heapBytes = GC.GetTotalMemory(false);
        int gc0 = GC.CollectionCount(0) - context.StatsGc0;
        int gc1 = GC.CollectionCount(1) - context.StatsGc1;
        int gc2 = GC.CollectionCount(2) - context.StatsGc2;

        context.TotalExprs++;
        context.TotalIterations += context.Iterations;
        context.TotalTailCalls += context.TailCalls;
        context.TotalEnvFrames += context.EnvFrames;
        context.TotalPrimCalls += context.PrimCalls;
        context.TotalInterpEmits += context.InterpEmits;
        context.TotalInterpExecs += context.InterpExecs;
        context.TotalTreeWalkCalls += context.TreeWalkCalls;
        context.TotalAllocated += allocatedBytes;
        context.TotalElapsedMs += stopwatch.Elapsed.TotalMilliseconds;
        MergeCounters(context.TotalInterpEmitKinds, context.InterpEmitKinds);
        MergeCounters(context.TotalInterpExecKinds, context.InterpExecKinds);

        return new StatsReportSnapshot(
            ElapsedMs: stopwatch.Elapsed.TotalMilliseconds,
            Iterations: context.Iterations,
            TailCalls: context.TailCalls,
            EnvFrames: context.EnvFrames,
            PrimCalls: context.PrimCalls,
            InterpEmits: context.InterpEmits,
            InterpExecs: context.InterpExecs,
            TreeWalkCalls: context.TreeWalkCalls,
            AllocatedBytes: allocatedBytes,
            HeapBytes: heapBytes,
            Gc0: gc0,
            Gc1: gc1,
            Gc2: gc2,
            EmitKinds: context.InterpEmitKinds,
            ExecKinds: context.InterpExecKinds);
    }

    internal static StatsReportSnapshot GetTotalsSnapshot()
    {
        var context = RequireCurrent();
        return new StatsReportSnapshot(
            ElapsedMs: context.TotalElapsedMs,
            Iterations: context.TotalIterations,
            TailCalls: context.TotalTailCalls,
            EnvFrames: context.TotalEnvFrames,
            PrimCalls: context.TotalPrimCalls,
            InterpEmits: context.TotalInterpEmits,
            InterpExecs: context.TotalInterpExecs,
            TreeWalkCalls: context.TotalTreeWalkCalls,
            AllocatedBytes: context.TotalAllocated,
            HeapBytes: null,
            Gc0: null,
            Gc1: null,
            Gc2: null,
            EmitKinds: context.TotalInterpEmitKinds,
            ExecKinds: context.TotalInterpExecKinds);
    }

    public static void RecordIteration()
    {
        if (Current is { } context)
            context.Iterations++;
    }

    public static void RecordTailCall()
    {
        if (Current is { } context)
            context.TailCalls++;
    }

    public static void RecordEnvFrame()
    {
        if (Current is { } context)
            context.EnvFrames++;
    }

    public static void RecordPrimCall()
    {
        if (Current is { } context)
            context.PrimCalls++;
    }

    public static void RecordTreeWalkCall()
    {
        if (Current is { } context)
            context.TreeWalkCalls++;
    }

    public static void RecordInterpEmit(Expression expr)
    {
        if (Current is not { } context)
            return;

        context.InterpEmits++;
        if (context.Stats)
            AddCounter(context.InterpEmitKinds, GetExpressionKind(expr));
    }

    public static void RecordInterpExec(Expression expr)
    {
        if (Current is not { } context)
            return;

        context.InterpExecs++;
        if (context.Stats)
            AddCounter(context.InterpExecKinds, GetExpressionKind(expr));
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
}