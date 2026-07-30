namespace Lisp;

public sealed record DebugFrameSnapshot(string ProcedureName, string Expression, string? SourceLocation);

public sealed class InterpreterContext
{
    private sealed class ImportTargetScope(Env? previous) : IDisposable
    {
        public void Dispose() => _importTargetEnv = previous;
    }

    private sealed class SourceNameScope(string? previous) : IDisposable
    {
        public void Dispose() => _currentSourceName = previous;
    }

    private sealed class CancellationTokenScope(CancellationToken previous, bool hadPrevious) : IDisposable
    {
        public void Dispose()
        {
            _evaluationCancellationToken = previous;
            _hasEvaluationCancellationToken = hadPrevious;
        }
    }

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
        Dictionary<string, long> ExecKinds,
        Dictionary<string, long> ExecSites);

    [ThreadStatic] private static InterpreterContext? _current;
    [ThreadStatic] private static string? _currentSourceName;
    [ThreadStatic] private static Env? _importTargetEnv;
    [ThreadStatic] private static CancellationToken _evaluationCancellationToken;
    [ThreadStatic] private static bool _hasEvaluationCancellationToken;

    /// <summary>
    /// Set by the Ctrl+C handler (on any thread) to request cancellation of the current evaluation.
    /// Checked on every interpreter iteration and cleared before the exception is thrown.
    /// </summary>
    public static volatile bool InterruptRequested;

    public static InterpreterContext? Current
    {
        get => _current;
        set => _current = value;
    }

    public static string? CurrentSourceName => _currentSourceName;
    public static Env? ImportTargetEnv => _importTargetEnv;
    public static CancellationToken CurrentCancellationToken =>
        _hasEvaluationCancellationToken ? _evaluationCancellationToken : CancellationToken.None;

    public static IDisposable PushSourceName(string sourceName)
    {
        var previous = _currentSourceName;
        _currentSourceName = sourceName;
        return new SourceNameScope(previous);
    }

    public static IDisposable PushImportTarget(Env env)
    {
        var previous = _importTargetEnv;
        _importTargetEnv = env;
        return new ImportTargetScope(previous);
    }

    public static IDisposable PushCancellationToken(CancellationToken cancellationToken)
    {
        var previous = _evaluationCancellationToken;
        var hadPrevious = _hasEvaluationCancellationToken;
        _evaluationCancellationToken = cancellationToken;
        _hasEvaluationCancellationToken = true;
        return new CancellationTokenScope(previous, hadPrevious);
    }

    public static InterpreterContext RequireCurrent() =>
        _current ?? throw new InvalidOperationException("No active interpreter context");

    public Program? Program { get; set; }

    public Dictionary<object, object?> Macros { get; } = [];
    public Dictionary<object, string> MacroDocComments { get; } = [];
    public Dictionary<string, Env> Modules { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Util.SourceDocument> SourceDocuments { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Symbol> Symbols { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Symbol> Gensyms { get; } = new(StringComparer.Ordinal);
    public int MacroSymbolCounter { get; set; }
    public int MacroWildcardCounter { get; set; }

    public bool LastValue { get; set; } = true;
    public bool Stats { get; set; }
    public bool Profile { get; set; }
    public bool DebugEnabled { get; set; }
    public bool DebugSingleStep { get; set; }
    public bool DebugPaused { get; set; }
    public bool DebuggerInteractive { get; set; }
    public bool ShowInputLines { get; set; }
    public bool TraceIndent { get; set; } = true;
    public bool TraceShowCode { get; set; }
    public bool TraceShowSource { get; set; }
    public bool TraceCompact { get; set; }
    public int TraceCompactMinRun { get; set; } = 4;
    public int TraceDepth { get; set; }
    public string? TraceCompactSymbol { get; set; }
    public int TraceCompactDepth { get; set; }
    public int TraceCompactCount { get; set; }
    public bool EndProgram { get; set; } = false;
    public HashSet<string> Breakpoints { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<DebugFrameSnapshot> DebugBacktrace { get; } = [];
    public string? DebugCurrentProcedure { get; private set; }
    public string? DebugCurrentExpressionText { get; private set; }
    public string? DebugCurrentSourceLocation { get; private set; }
    public Env? DebugCurrentEnvironment { get; set; }
    private readonly List<(string Name, object? Value)> _debugLocals = [];
    public IReadOnlyList<(string Name, object? Value)> DebugLocals => _debugLocals;
    public ConsoleColor? InputLineColor { get; set; }
    public List<string> LibrarySearchPaths { get; } = [];

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
    public Dictionary<string, long> InterpExecSites { get; } = [];
    public Dictionary<string, long> TotalInterpEmitKinds { get; } = [];
    public Dictionary<string, long> TotalInterpExecKinds { get; } = [];
    public Dictionary<string, long> TotalInterpExecSites { get; } = [];

    public static bool IsStatsEnabled => Current?.Stats == true;
    public static bool IsProfileEnabled => Current?.Profile == true;

    public void RegisterSourceDocument(Util.SourceDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.SourceName))
            return;

        SourceDocuments[document.SourceName!] = document;
    }

    public bool TryGetSourceDocument(string? sourceName, out Util.SourceDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(sourceName))
            return false;

        return SourceDocuments.TryGetValue(sourceName!, out document);
    }

    public void NotifyDebugHit(Expression expr)
    {
        if (!DebugEnabled && !DebugSingleStep && Breakpoints.Count == 0)
            return;

        string label = expr switch
        {
            Var v => v.GetName(),
            _ => expr.ToString() ?? "<unknown>",
        };

        if (expr.Source is { } span && !string.IsNullOrWhiteSpace(span.SourceName))
            ConsoleOutput.WriteTrace($"[debug] {label} @ {span.FormatLocation()}");
        else
            ConsoleOutput.WriteTrace($"[debug] {label}");
    }

    public void ResetDebugFrameStack()
    {
        DebugBacktrace.Clear();
        DebugCurrentEnvironment = null;
    }

    public void PushDebugFrame(string? procedureName, Expression expr, Env? env)
    {
        DebugBacktrace.Add(new DebugFrameSnapshot(
            procedureName ?? "<procedure>",
            expr.ToString() ?? "<unknown>",
            expr.Source?.FormatLocation()));
        CaptureDebugFrame(procedureName, expr, env);
    }

    public void PopDebugFrame()
    {
        if (DebugBacktrace.Count > 0)
            DebugBacktrace.RemoveAt(DebugBacktrace.Count - 1);
    }

    public void CaptureDebugFrame(string? procedureName, Expression expr, Env? env)
    {
        DebugCurrentProcedure = procedureName ?? "<procedure>";
        DebugCurrentExpressionText = expr.ToString() ?? "<unknown>";
        DebugCurrentSourceLocation = expr.Source?.FormatLocation();
        DebugCurrentEnvironment = env;
        _debugLocals.Clear();
        if (env != null)
            foreach (var binding in env.table)
                _debugLocals.Add((binding.Key.ToString() ?? "<symbol>", binding.Value));
    }

    public void TryPause(Expression expr, string? procedureName, Env? env)
    {
        if (DebugPaused)
            return;

        if (!DebuggerInteractive)
            return;

        string label = expr switch
        {
            Var v => v.GetName(),
            _ => expr.ToString() ?? "<unknown>",
        };

        bool shouldPauseByBreakpoint = Breakpoints.Any(bp =>
            label.Contains(bp, StringComparison.OrdinalIgnoreCase)
            || (procedureName?.Contains(bp, StringComparison.OrdinalIgnoreCase) ?? false)
            || (expr.Source?.FormatLocation().Contains(bp, StringComparison.OrdinalIgnoreCase) ?? false));

        if (DebugSingleStep && DebugEnabled)
        {
            CaptureDebugFrame(procedureName, expr, env);
            DebugSingleStep = false;
            DebugPaused = true;
            throw new DebuggerPauseException("debugger paused", expr, procedureName, expr.Source, _debugLocals);
        }

        if (!shouldPauseByBreakpoint || !DebugEnabled)
            return;

        CaptureDebugFrame(procedureName, expr, env);
        DebugPaused = true;
        throw new DebuggerPauseException("debugger paused", expr, procedureName, expr.Source, _debugLocals);
    }

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
        context.TotalInterpExecSites.Clear();
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
        context.InterpExecSites.Clear();
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
        MergeCounters(context.TotalInterpExecSites, context.InterpExecSites);

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
            ExecKinds: context.InterpExecKinds,
            ExecSites: context.InterpExecSites);
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
            ExecKinds: context.TotalInterpExecKinds,
            ExecSites: context.TotalInterpExecSites);
    }

    public static void RecordIteration()
    {
        if (Current is { } context)
        {
            if (InterruptRequested || (_hasEvaluationCancellationToken && _evaluationCancellationToken.IsCancellationRequested))
            {
                InterruptRequested = false;
                throw new UserInterruptException();
            }
            context.Iterations++;
        }
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
        if (context.Stats || context.Profile)
            AddCounter(context.InterpEmitKinds, GetExpressionKind(expr));
    }

    public static void RecordInterpExec(Expression expr)
    {
        if (Current is not { } context)
            return;

        context.InterpExecs++;
        AddCounter(context.InterpExecSites, GetExpressionSite(expr));
        if (context.Stats || context.Profile)
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

    private static string GetExpressionSite(Expression expr)
    {
        const int maxTextLength = 96;

        string location = expr.Source?.FormatLocation() ?? "<unknown>";
        string text = expr.ToString() ?? expr.GetType().Name;
        if (text.Length > maxTextLength)
            text = text[..(maxTextLength - 3)] + "...";

        return $"{location}  {text}";
    }
}