namespace Lisp;

internal abstract record InitEntry;
internal sealed record InitMacro(Pair Def, string? DocComment) : InitEntry;
internal sealed record InitExpr(Expression E, string? DocComment) : InitEntry;

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

    private static void PrintInputLine(string text)
    {
        var color = RuntimeContext.InputLineColor;
        if (color.HasValue) Console.ForegroundColor = color.Value;
        Console.WriteLine($">> {text}");
        if (color.HasValue) Console.ResetColor();
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

    // Module system (interpreter-local via InterpreterContext)
    public static void RegisterModule(string name, Env env) => RuntimeContext.Modules[name] = env;

    public static Env? GetModule(string name) => RuntimeContext.Modules.TryGetValue(name, out var env) ? env : null;

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
        "load", "new",
        "->string", "->int", "->double", "->bool", "typeof", "cast",
        "define-library", "import", "env-set!", "env-ref",
    ];

    public Program()
    {
        Context = new InterpreterContext { Program = this };
        current = this;
        initEnv = new LocalEnvironment(null, null, new Env());
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

    private static LispException TopLevelFormError(object? form, string message) =>
        new LispException(message).AttachSchemeContext(Util.GetSource(form), null);

    private static void ValidateSyntaxRuleClauses(Pair? clauses, object? sourceForm, string owner)
    {
        if (clauses == null || Pair.IsNull(clauses))
            throw TopLevelFormError(sourceForm, $"{owner}: expected at least one syntax-rules clause");

        foreach (object rawClause in clauses)
        {
            if (rawClause is not Pair clause || clause.car is not Pair || Pair.IsNull(clause.cdr))
                throw TopLevelFormError(rawClause, $"{owner}: each syntax-rules clause must contain a pattern and template");
        }
    }

    private static Pair ValidateMacroDefinition(Pair form)
    {
        var args = form.CdrPair;
        int count = args?.Count ?? 0;
        if (count < 3)
            throw TopLevelFormError(form, $"macro: expected at least 3 arguments, got {count}");

        if (args?.car is not Symbol)
            throw TopLevelFormError(form, "macro: expected a symbol as the first argument");

        if (args.CdrPair?.car is not Pair)
            throw TopLevelFormError(form, "macro: expected a literal identifier list as the second argument");

        ValidateSyntaxRuleClauses(args.CdrPair?.CdrPair, form, "macro");
        return args;
    }

    private static Pair ValidateDefineSyntaxDefinition(Pair form)
    {
        var args = form.CdrPair;
        int count = args?.Count ?? 0;
        if (count != 2)
            throw TopLevelFormError(form, $"define-syntax: expected exactly 2 arguments, got {count}");

        if (args?.car is not Symbol)
            throw TopLevelFormError(form, "define-syntax: expected a symbol as the first argument");

        if (args.CdrPair?.car is not Pair syntaxRules || syntaxRules.car?.ToString() != "syntax-rules")
            throw TopLevelFormError(form, "define-syntax: expected a syntax-rules transformer");

        if (syntaxRules.CdrPair?.car is not Pair)
            throw TopLevelFormError(syntaxRules, "syntax-rules: expected a literal identifier list");

        ValidateSyntaxRuleClauses(syntaxRules.CdrPair?.CdrPair, syntaxRules, "syntax-rules");

        return Macro.TranslateDefineSyntax(form)
            ?? throw TopLevelFormError(form, "define-syntax: invalid syntax-rules definition");
    }

    private static bool TryGetTopLevelDefinition(object? parsedObj, out Pair? definition, out object result)
    {
        switch (parsedObj)
        {
            case Pair p when p.car?.ToString() == "macro":
                definition = ValidateMacroDefinition(p);
                result = definition.car!;
                return true;
            case Pair p when p.car?.ToString() == "define-syntax":
                definition = ValidateDefineSyntaxDefinition(p);
                Util.PropagateSourceDeep(p, definition);
                result = p.CdrPair!.car!;
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
        if (parsedObj is Pair dl && (dl.car?.ToString() == "define-library" || dl.car?.ToString() == "DEFINE-LIBRARY"))
            return EvalCompiledTopLevel(dl);

        var expanded = ExpandTopLevelForm(parsedObj)!;
        return expanded is Pair defPair && defPair.car?.ToString() == "DEFINE"
            ? new Define(defPair).Eval(initEnv)
            : EvalCompiledTopLevel(expanded);
    }

    public void LoadInit(string path)
    {
        using var _sourceScope = InterpreterContext.PushSourceName(path);
        var stamp = File.GetLastWriteTimeUtc(path);
        if (InitCacheStore.TryGet(path, stamp) is { } cachedEntries)
        {
            Macro.Clear();
            foreach (var entry in cachedEntries)
                switch (entry)
                {
                    case InitMacro m:
                        if (m.DocComment != null) Macro.SetDocComment(m.Def.car!, m.DocComment);
                        Macro.Add(m.Def);
                        break;
                    case InitExpr ie:
                        if (ie.DocComment != null) Util.SetPendingDocComment(ie.DocComment);
                        Vm.Execute(BytecodeCompiler.CompileTop(ie.E), initEnv);
                        break;
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
            var docComment = Util.ExtractDocComment(exp);
            var parsedObj = ParseWithContext(exp, document, text.Length - exp.Length, out var after);
            if (RuntimeContext.ShowInputLines) PrintInputLine(exp[..^after.Length].Trim());
            if (parsedObj == null) break;
            if (TryGetTopLevelDefinition(parsedObj, out var definition, out var macroName))
            {
                if (definition != null)
                {
                    cache.Add(new InitMacro(definition, docComment));
                    if (docComment != null) Macro.SetDocComment(macroName, docComment);
                    Macro.Add(definition);
                }
            }
            else
            {
                var expanded = ExpandTopLevelForm(parsedObj)!;
                var compiled = CompileTopLevelForm(expanded);
                cache.Add(new InitExpr(compiled, docComment));
                Util.SetPendingDocComment(docComment);
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
        using var _sourceScope = sourceName is null ? null : InterpreterContext.PushSourceName(sourceName);

        var document = new Util.SourceDocument(exp, sourceName);
        var docComment = Util.ExtractDocComment(exp);
        var parsedObj = ParseWithContext(exp, document, 0, out after);
        if (RuntimeContext.ShowInputLines) PrintInputLine(exp[..^after.Length].Trim());
        if (TryGetTopLevelDefinition(parsedObj, out var definition, out var result))
        {
            if (definition != null)
            {
                if (docComment != null) Macro.SetDocComment(result, docComment);
                Macro.Add(definition);
            }
            return result;
        }
        Util.SetPendingDocComment(docComment);

        string currentExpr = after.Length == 0 ? exp : exp[..^after.Length];
        return Eval(currentExpr);
    }

    public object Eval(string exp) => Eval(exp, sourceName: null);

    public object Eval(string exp, string? sourceName)
    {
        using var _sourceScope = sourceName is null ? null : InterpreterContext.PushSourceName(sourceName);

        var document = new Util.SourceDocument(exp, sourceName);
        string fullText = exp;
        object answer = Pair.Empty;
        while (true)
        {
            var docComment = Util.ExtractDocComment(exp);
            var parsedObj = ParseWithContext(exp, document, fullText.Length - exp.Length, out var after);
            if (RuntimeContext.ShowInputLines) PrintInputLine(exp[..^after.Length].Trim());
            if (TryGetTopLevelDefinition(parsedObj, out var definition, out answer))
            {
                if (definition != null)
                {
                    if (docComment != null) Macro.SetDocComment(answer, docComment);
                    Macro.Add(definition);
                }
                if (after == "") return answer;
                exp = after;
                continue;
            }
            Util.SetPendingDocComment(docComment);
            answer = EvalTopLevelForm(parsedObj);
            if (after != "" && !RuntimeContext.LastValue) ConsoleOutput.WriteResult(answer);
            if (after == "") return answer;
            exp = after;
        }
    }
}