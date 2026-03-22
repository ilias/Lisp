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
        initEnv = new Extended_Env(null!, null!, new Env());
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
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  time:       {sw.Elapsed.TotalMilliseconds,10:F3} ms");
        Console.WriteLine($"  iterations: {Iterations,10:N0}   (closure calls)");
        Console.WriteLine($"  tail-calls: {TailCalls,10:N0}   (TCO bounces)");
        Console.WriteLine($"  env-frames: {EnvFrames,10:N0}   (scopes created)");
        Console.WriteLine($"  primitives: {PrimCalls,10:N0}   (built-in calls)");
        Console.WriteLine($"  allocated:  {FormatBytes(allocDelta),10}   (this eval)");
        Console.WriteLine($"  heap:       {FormatBytes(heapBytes),10}   (live GC heap)");
        Console.WriteLine($"  gc[0/1/2]:  {gc0}/{gc1}/{gc2}");
        Console.ResetColor();
    }

    public static void PrintTotals()
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"  ── totals ({TotalExprs:N0} exprs) ──────────────────");
        Console.WriteLine($"  total time: {TotalElapsedMs,10:F3} ms");
        Console.WriteLine($"  total iter: {TotalIterations,10:N0}   (closure calls)");
        Console.WriteLine($"  total tail: {TotalTailCalls,10:N0}   (TCO bounces)");
        Console.WriteLine($"  total env:  {TotalEnvFrames,10:N0}   (scopes created)");
        Console.WriteLine($"  total prim: {TotalPrimCalls,10:N0}   (built-in calls)");
        Console.WriteLine($"  total alloc:{FormatBytes(TotalAllocated),10}   (since reset)");
        Console.ResetColor();
    }

    private static string FormatBytes(long bytes) =>
        bytes >= 1_048_576 ? $"{bytes / 1_048_576.0:F2} MB" :
        bytes >= 1_024 ? $"{bytes / 1_024.0:F1} KB" :
        $"{bytes} B";

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
            switch (parsedObj)
            {
                case Pair p when p.car?.ToString() == "macro":
                    cache.Add(new InitMacro(p.cdr!));
                    Macro.Add(p.cdr!);
                    break;
                case Pair p when p.car?.ToString() == "define-syntax":
                    var md = Macro.TranslateDefineSyntax(p);
                    if (md != null) { cache.Add(new InitMacro(md)); Macro.Add(md); }
                    break;
                default:
                    parsedObj = Macro.Check(parsedObj);
                    var compiled = parsedObj is Pair dp && dp.car?.ToString() == "DEFINE"
                        ? (Expression)new Define(dp)
                        : Expression.Parse(parsedObj!);
                    cache.Add(new InitExpr(compiled));
                    Vm.Execute(BytecodeCompiler.CompileTop(compiled), initEnv);
                    break;
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
        switch (parsedObj)
        {
            case Pair p when p.car?.ToString() == "macro":
                Macro.Add(p.cdr!);
                return p.cdr!.car!;
            case Pair p when p.car?.ToString() == "define-syntax":
                var md = Macro.TranslateDefineSyntax(p);
                if (md != null) Macro.Add(md);
                return p.cdr!.car!;
        }
        parsedObj = Macro.Check(parsedObj);
        if (Expression.IsTraceOn(Symbol.Create("macro")))
            Console.WriteLine(Util.Dump("macro:   ", parsedObj!));
        if (parsedObj is Pair defPair && defPair.car?.ToString() == "DEFINE")
            return new Define(defPair).Eval(initEnv);
        var sw = Stats ? Stopwatch.StartNew() : null;
        if (Stats) BeginStats();
        var answer = Eval(Expression.Parse(parsedObj!));
        if (answer is Pair answerPair && answerPair.car is Var v)
        {
            answerPair.car = v.GetName();
            answer = Eval(Expression.Parse(answerPair));
        }
        if (Stats && sw != null) EndStats(sw);
        return answer;
    }

    public object Eval(string exp)
    {
        object answer = Pair.Empty;
        while (true)
        {
            var parsedObj = Util.Parse(exp, out var after);
            if (ShowInputLines) Console.WriteLine($">> {exp[..^after.Length].Trim()}");
            switch (parsedObj)
            {
                case Pair p when p.car?.ToString() == "macro":
                    Macro.Add(p.cdr!);
                    answer = p.cdr!.car!;
                    if (after == "") return answer;
                    exp = after;
                    continue;
                case Pair p when p.car?.ToString() == "define-syntax":
                    var md = Macro.TranslateDefineSyntax(p);
                    if (md != null) Macro.Add(md);
                    answer = p.cdr!.car!;
                    if (after == "") return answer;
                    exp = after;
                    continue;
            }
            parsedObj = Macro.Check(parsedObj);
            if (Expression.IsTraceOn(Symbol.Create("macro")))
                Console.WriteLine(Util.Dump("macro:   ", parsedObj!));
            if (parsedObj is Pair defPair && defPair.car?.ToString() == "DEFINE")
            {
                answer = new Define(defPair).Eval(initEnv);
                if (after == "") return answer;
                exp = after;
                continue;
            }
            var sw = Stats ? Stopwatch.StartNew() : null;
            if (Stats) BeginStats();
            answer = Eval(Expression.Parse(parsedObj!));
            if (answer is Pair answerPair && answerPair.car is Var v)
            {
                answerPair.car = v.GetName();
                answer = Eval(Expression.Parse(answerPair));
            }
            if (Stats && sw != null) EndStats(sw);
            if (after != "" && !lastValue) Console.WriteLine(Util.Dump(answer));
            if (after == "") return answer;
            exp = after;
        }
    }
}