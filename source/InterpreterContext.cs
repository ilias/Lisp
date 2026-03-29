namespace Lisp;

public sealed class InterpreterContext
{
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

    public static void RecordIteration()
    {
        if (Current is { Stats: true } context)
            context.Iterations++;
    }

    public static void RecordTailCall()
    {
        if (Current is { Stats: true } context)
            context.TailCalls++;
    }

    public static void RecordEnvFrame()
    {
        if (Current is { Stats: true } context)
            context.EnvFrames++;
    }

    public static void RecordPrimCall()
    {
        if (Current is { Stats: true } context)
            context.PrimCalls++;
    }

    public static void RecordTreeWalkCall()
    {
        if (Current is { Stats: true } context)
            context.TreeWalkCalls++;
    }

    public static void RecordInterpEmit(Expression expr)
    {
        if (Current is not { Stats: true } context)
            return;

        context.InterpEmits++;
        AddCounter(context.InterpEmitKinds, GetExpressionKind(expr));
    }

    public static void RecordInterpExec(Expression expr)
    {
        if (Current is not { Stats: true } context)
            return;

        context.InterpExecs++;
        AddCounter(context.InterpExecKinds, GetExpressionKind(expr));
    }

    private static void AddCounter(Dictionary<string, long> counters, string key) =>
        counters[key] = counters.GetValueOrDefault(key) + 1;

    private static string GetExpressionKind(Expression expr) => expr switch
    {
        LetSyntax letSyntax => letSyntax.IsLetrec ? "LetRecSyntax" : "LetSyntax",
        _ => expr.GetType().Name,
    };
}