namespace Lisp;

public abstract class Expression
{
    public static bool Trace = false;
    public static HashSet<Symbol> traceHash = [];
    private static readonly Symbol _sAll = Symbol.Create("_all_");
    private static readonly Symbol _sDefineSyntax = Symbol.Create("define-syntax");
    public SourceSpan? Source { get; private set; }

    public static bool IsTraceOn(Symbol s) =>
        Trace && (traceHash.Contains(s) || traceHash.Contains(_sAll));

    internal T WithSource<T>(SourceSpan? source) where T : Expression
    {
        Source ??= source;
        return (T)this;
    }

    private static T AttachSource<T>(T expression, object? sourceObj) where T : Expression =>
        expression.WithSource<T>(Util.GetSource(sourceObj));

    public abstract object Eval(Env env);
    public virtual object EvalTail(Env env) => Eval(env);

    public static Pair? Eval_Rands(Pair? rands, Env env)
    {
        if (rands == null) return null;
        Pair? head = null;
        Pair? tail = null;
        foreach (object obj in rands)
        {
            var o = ((Expression)obj).Eval(env);
            if (obj is CommaAt && o is Pair spliced)
                foreach (object oo in spliced)
                    Pair.AppendTail(ref head, ref tail, oo);
            else
                Pair.AppendTail(ref head, ref tail, o);
        }
        return head;
    }

    private static Pair? ParseBody(Pair? forms)
    {
        if (forms == null) return null;
        Pair? body = null;
        Pair? bodyTail = null;
        foreach (object obj in forms)
            Pair.AppendTail(ref body, ref bodyTail, Parse(obj));
        return body;
    }

    private static Pair? TranslateLetSyntaxBinding(Pair binding)
    {
        var name = binding.car!;
        var transformer = binding.CdrPair?.car;
        if (transformer == null) return null;
        if (transformer is Pair transformerPair && transformerPair.car?.ToString() == "syntax-rules")
        {
            var defineSyntax = new Pair(_sDefineSyntax, new Pair(name, new Pair(transformer, null)));
            Util.PropagateSourceDeep(binding, defineSyntax);
            var translated = Macro.TranslateDefineSyntax(defineSyntax);
            Util.PropagateSourceDeep(binding, translated);
            return translated;
        }
        var syntaxRules = Macro.TranslateSyntaxRules(name, transformer as Pair, binding.CdrPair?.CdrPair)
            ?? new Pair(name, binding.CdrPair!);
        Util.PropagateSourceDeep(binding, syntaxRules);
        return syntaxRules;
    }

    private static List<(object, Pair)> ParseLetSyntaxBindings(Pair? bindPairs)
    {
        List<(object, Pair)> bindings = [];
        if (bindPairs == null || Pair.IsNull(bindPairs)) return bindings;
        foreach (object bp in bindPairs)
        {
            if (bp is not Pair binding) continue;
            var translated = TranslateLetSyntaxBinding(binding);
            if (translated != null) bindings.Add((binding.car!, translated));
        }
        return bindings;
    }

    protected static object EvalInPosition(Expression expression, Env env, bool tail) =>
        tail ? expression.EvalTail(env) : expression.Eval(env);

    public static Expression Parse(object? a)
    {
        if (a is Symbol sym) return AttachSource(new Var(sym), a);
        if (a is not Pair pair) return AttachSource(new Lit(a), a);

        return AttachSource(ParsePair(pair), pair);
    }

    private static Expression ParsePair(Pair pair)
    {
        Pair? args = pair.CdrPair;
        return ParseSpecialForm(pair, args) ?? ParseApplication(pair, args);
    }

    private static LispException FormError(Pair form, string message) =>
        new LispException(message).AttachSchemeContext(Util.GetSource(form), null);

    private static Pair RequireArgRange(Pair form, string name, Pair? args, int min, int max)
    {
        int count = args?.Count ?? 0;
        if (count < min || count > max)
        {
            string expected = min == max
                ? $"exactly {min} argument{(min == 1 ? "" : "s")}"
                : $"{min} or {max} arguments";
            throw FormError(form, $"{name}: expected {expected}, got {count}");
        }

        return args!;
    }

    private static Pair RequireMinArgs(Pair form, string name, Pair? args, int min)
    {
        int count = args?.Count ?? 0;
        if (count < min)
            throw FormError(form, $"{name}: expected at least {min} argument{(min == 1 ? "" : "s")}, got {count}");

        return args!;
    }

    private static Expression ParseIfForm(Pair pair, Pair? args)
    {
        var validated = RequireArgRange(pair, "if", args, 2, 3);
        var elseCell = validated.CdrPair!.CdrPair;
        return new If(
            Parse(validated.car),
            Parse(validated.CdrPair.car),
            !Pair.IsNull(elseCell) ? Parse(elseCell!.car) : AttachSource(new Lit(false), pair));
    }

    private static Expression ParseEvalForm(Pair pair, Pair? args)
    {
        var validated = RequireArgRange(pair, "eval", args, 1, 1);
        return new Evaluate(Parse(validated.car));
    }

    private static Expression ParseLambdaForm(Pair pair, Pair? args)
    {
        var validated = RequireMinArgs(pair, "lambda", args, 2);
        return new Lambda(validated.car as Pair, ParseBody(validated.CdrPair), validated.CdrPair);
    }

    private static Expression ParseQuoteForm(Pair pair, Pair? args)
    {
        int count = args?.Count ?? 0;
        if (count == 0)
            return new Lit(Pair.Empty);

        var validated = RequireArgRange(pair, "quote", args, 1, 1);
        return new Lit(validated.car);
    }

    private static Expression ParseSetForm(Pair pair, Pair? args)
    {
        var validated = RequireArgRange(pair, "set!", args, 2, 2);
        if (validated.car is not Symbol symbol)
            throw FormError(pair, "set!: expected a symbol as the first argument");

        return new Assignment(symbol, Parse(validated.CdrPair!.car));
    }

    private static Expression ParseTryForm(Pair pair, Pair? args)
    {
        var validated = RequireArgRange(pair, "try", args, 2, 2);
        return new Try(Parse(validated.car), Parse(validated.CdrPair!.car));
    }

    private static Expression ParseTryContForm(Pair pair, Pair? args)
    {
        var validated = RequireArgRange(pair, "try-cont", args, 2, 3);
        return !Pair.IsNull(validated.CdrPair?.CdrPair)
            ? new TryCont(Parse(validated.car), Parse(validated.CdrPair!.car), Parse(validated.CdrPair!.CdrPair!.car))
            : new TryCont(Parse(validated.car), Parse(validated.CdrPair!.car));
    }

    private static Expression ParseLetSyntaxForm(Pair pair, Pair? args)
    {
        var validated = RequireMinArgs(pair, pair.car!.ToString()!, args, 1);
        return new LetSyntax(
            pair.car!.ToString()!.Contains("letrec", StringComparison.OrdinalIgnoreCase),
            ParseLetSyntaxBindings(validated.car as Pair),
            validated.CdrPair as Pair);
    }

    private static Expression? ParseSpecialForm(Pair pair, Pair? args) => pair.car?.ToString() switch
    {
        "IF" => ParseIfForm(pair, args),
        "DEFINE" => new Define(pair),
        "DEFINE-LIBRARY" or "define-library" => new DefineLibraryForm(pair, args),
        "EVAL" => ParseEvalForm(pair, args),
        "LAMBDA" => ParseLambdaForm(pair, args),
        "quote" => ParseQuoteForm(pair, args),
        "set!" => ParseSetForm(pair, args),
        "TRY" => ParseTryForm(pair, args),
        "TRY-CONT" => ParseTryContForm(pair, args),
        "LET-SYNTAX" or "LETREC-SYNTAX" or "let-syntax" or "letrec-syntax" =>
            ParseLetSyntaxForm(pair, args),
        _ => null,
    };

    private static Expression ParseApplication(Pair pair, Pair? args)
    {
        var body = ParseBody(args);
        var carName = pair.car?.ToString()!;
        if (carName == ",@") return new CommaAt(body);
        if (Prim.TryGetPrimitive(carName, out var prim))
            return new Prim(prim, body);
        return new App(Parse(pair.car), body);
    }
}

public class Lit(object? datum) : Expression
{
    public object? Datum => datum;
    public override object Eval(Env env) => datum is Pair pair ? EvalQuotedPair(pair, env)! : datum!;

    private static Pair? EvalQuotedPair(Pair pair, Env env)
    {
        if (Pair.IsNull(pair)) return pair;
        Pair? result = null;
        Pair? resultTail = null;

        void AppendValue(object? value) => Pair.AppendTail(ref result, ref resultTail, value);

        for (var cur = pair; cur != null && !Pair.IsNull(cur); cur = cur.CdrPair)
        {
            var item = cur.car;
            if (item is Pair quotedPair)
            {
                if (Symbol.IsEqual(",", quotedPair.car))
                {
                    AppendValue(Parse(quotedPair.CdrPair!.car).Eval(env));
                }
                else if (Symbol.IsEqual(",@", quotedPair.car))
                {
                    var evaluated = Parse(quotedPair.CdrPair!.car).Eval(env);
                    if (evaluated is Pair splice)
                    {
                        foreach (object splicedItem in splice)
                            AppendValue(splicedItem);
                    }
                    else if (evaluated != null)
                    {
                        AppendValue(evaluated);
                    }
                }
                else
                {
                    AppendValue(EvalQuotedPair(quotedPair, env));
                }
            }
            else
            {
                AppendValue(item);
            }

            // Preserve dotted tail (non-Pair, non-null cdr)
            if (cur.cdr != null && cur.CdrPair == null)
            {
                resultTail!.cdr = cur.cdr;
                break;
            }
        }

        return result;
    }

    public override string ToString() => Util.Dump("lit", datum);
    public string GetName() => datum!.ToString()!;
}

public class Evaluate(Expression datum) : Expression
{
    public Expression DatumExpr => datum;

    internal static object EvalDatumInEnv(object? datumValue, Env env) => datumValue switch
    {
        null => null!,
        string s => EvalParsedInEnv(Program.RequireCurrent().Eval(s), env),
        var o => EvalParsedInEnv(o, env),
    };

    private static object EvalParsedInEnv(object? parsedObj, Env env)
    {
        var parsedExpr = Parse(parsedObj);
        return Vm.Execute(BytecodeCompiler.CompileTop(parsedExpr), env);
    }

    public override object Eval(Env env) => EvalDatumInEnv(datum.Eval(env), env);

    public override string ToString() => Util.Dump("EVAL", datum);
}

public class Var(Symbol id) : Expression
{
    public readonly Symbol id = id;
    public override object Eval(Env env) => env.Apply(id);
    public string GetName() => id.ToString();
    public override string ToString() => Util.Dump("var", id);
}

public class Lambda(Pair? ids, Pair? body, Pair? rawBody = null) : Expression
{
    public Pair? Ids => ids;
    public Pair? Body => body;
    public Pair? RawBody => rawBody;
    private static readonly Symbol _sLambda = Symbol.Create("lambda");
    private Chunk? _compiledChunk;

    public Chunk GetOrCompileChunk() => _compiledChunk ??= BytecodeCompiler.CompileLambdaChunk(this);

    public override object Eval(Env env)
    {
        if (IsTraceOn(_sLambda))
            ConsoleOutput.WriteTrace(Util.Dump("lambda: ", ids, body));
        return new VmClosure(GetOrCompileChunk(), env);
    }

    public override string ToString() => Util.Dump("LAMBDA", ids, body);
}

public class Define(Pair datum) : Expression
{
    private static Pair ValidateDatum(Pair datum)
    {
        static LispException FormError(Pair form, string message) =>
            new LispException(message).AttachSchemeContext(Util.GetSource(form), null);

        int count = datum.CdrPair?.Count ?? 0;
        if (count < 2)
            throw FormError(datum, $"define: expected at least 2 arguments, got {count}");

        if (datum.CdrPair?.car is not Symbol)
            throw FormError(datum, "define: expected a symbol as the first argument");

        return datum;
    }

    private static Expression ParseValueExpression(Pair? forms)
    {
        static Pair? ParseBodyForms(Pair? bodyForms)
        {
            if (bodyForms == null) return null;
            Pair? body = null;
            Pair? bodyTail = null;
            foreach (object obj in bodyForms)
                Pair.AppendTail(ref body, ref bodyTail, Parse(obj));
            return body;
        }

        if (forms == null || Pair.IsNull(forms))
            throw new LispException("define: expected at least 2 arguments, got 1");

        if (Pair.IsNull(forms.CdrPair))
            return Parse(forms.car);

        return new App(new Lambda(null, ParseBodyForms(forms), forms), null);
    }

    private readonly Pair datum = ValidateDatum(datum);

    public Symbol NameSym => datum.CdrPair!.car is Symbol s ? s : Symbol.Create(datum.CdrPair!.car!.ToString()!);
    public Expression ValExpr => ParseValueExpression(datum.CdrPair!.CdrPair);

    public override object Eval(Env env)
    {
        var sym = datum.CdrPair!.car is Symbol s2 ? s2 : Symbol.Create(datum.CdrPair!.car!.ToString()!);
        var value = ValExpr.Eval(env);
        Util.ApplyDocComment(value, sym.ToString());
        env.table[sym] = value;
        return sym;
    }

    public override string ToString() => Util.Dump("DEFINE", datum);
}

public delegate object Primitive(Pair args);

public class If(Expression test, Expression tX, Expression eX) : Expression
{
    public Expression Test => test;
    public Expression ThenExpr => tX;
    public Expression ElseExpr => eX;

    private bool EvalTest(Env env)
    {
        var v = test.Eval(env);
        return v is not bool b || b;
    }

    private object EvalBranch(Env env, bool tail) =>
        EvalInPosition(EvalTest(env) ? tX : eX, env, tail);

    public override object Eval(Env env) => EvalBranch(env, tail: false);

    public override object EvalTail(Env env) => EvalBranch(env, tail: true);

    public override string ToString() => Util.Dump("IF", test, tX, eX);
}

public class Try(Expression tryX, Expression catchX) : Expression
{
    public Expression TryExpr => tryX;
    public Expression CatchExpr => catchX;

    public override object Eval(Env env)
    {
        try { return tryX.Eval(env); }
        catch (Exception ex) when (ExceptionDisplay.IsCatchableByTry(ex)) { return catchX.Eval(env); }
    }

    public override string ToString() => Util.Dump("TRY", tryX, catchX);
}

public class TryCont(Expression? tag, Expression tryX, Expression catchX) : Expression
{
    public TryCont(Expression tryX, Expression catchX) : this(null, tryX, catchX) { }

    public Expression? TagExpr => tag;
    public Expression TryExpr => tryX;
    public Expression CatchExpr => catchX;

    public override object Eval(Env env)
    {
        object? t = tag?.Eval(env);
        try { return tryX.Eval(env); }
        catch (ContinuationException ce)
        {
            if (t == null || ReferenceEquals(ce.Tag, t))
                return catchX.Eval(env);
            throw;
        }
    }

    public override string ToString() => Util.Dump("TRY-CONT", tag, tryX, catchX);
}

public class Assignment(Symbol id, Expression val) : Expression
{
    public Symbol Id => id;
    public Expression ValExpr => val;
    public override object Eval(Env env) => env.Bind(id, val.Eval(env));
    public override string ToString() => Util.Dump("set!", id, val);
}

public class LetSyntax(bool isLetrec, List<(object name, Pair def)> bindings, Pair? rawBody) : Expression
{
    public bool IsLetrec => isLetrec;
    public IReadOnlyList<(object name, Pair def)> Bindings => bindings;
    public Pair? RawBody => rawBody;

    private static void RestoreMacros(Dictionary<object, object?> saved)
    {
        Macro.Restore(saved);
    }

    private T WithScopedBindings<T>(Func<T> action)
    {
        var saved = Macro.Snapshot();
        try
        {
            foreach (var (name, def) in bindings)
                Macro.Set(name, def.CdrPair);
            return action();
        }
        finally
        {
            RestoreMacros(saved);
        }
    }

    public Expression[] ExpandBodyExpressions() => WithScopedBindings<Expression[]>(() =>
    {
        if (rawBody == null || Pair.IsNull(rawBody)) return Array.Empty<Expression>();

        List<Expression> expanded = [];
        foreach (object form in rawBody)
        {
            var expandedForm = Macro.Check(form)!;
            Util.PropagateSourceDeep(form, expandedForm);
            expanded.Add(Expression.Parse(expandedForm));
        }
        return [.. expanded];
    });

    private static object EvalExpandedForm(Expression expanded, Env env)
        => Vm.Execute(BytecodeCompiler.CompileTop(expanded), env);

    public override object Eval(Env env)
    {
        object result = Pair.Empty;
        foreach (var expanded in ExpandBodyExpressions())
            result = EvalExpandedForm(expanded, env);
        return result;
    }

    public override string ToString() => Util.Dump(isLetrec ? "LETREC-SYNTAX" : "LET-SYNTAX");
}

public class DefineLibraryForm(Pair form, Pair? args) : Expression
{
    private static LispException FormError(Pair source, string message) =>
        new LispException(message).AttachSchemeContext(Util.GetSource(source), null);

    private static object UnwrapQuoted(object? token) =>
        token is Pair q && Symbol.IsEqual("quote", q.car) && !Pair.IsNull(q.CdrPair)
            ? q.CdrPair!.car!
            : token!;

    private static bool IsClauseHead(object? token, string head) =>
        token is Pair clause && clause.car is Symbol s && string.Equals(s.ToString(), head, StringComparison.Ordinal);

    private static bool LooksLikeClauseSyntax(Pair? tail) =>
        tail != null && (IsClauseHead(tail.car, "export") || IsClauseHead(tail.car, "import") || IsClauseHead(tail.car, "begin"));

    private static Pair? ParseLegacyExports(Pair? rawExports, Pair source)
    {
        Pair? exports = null;
        Pair? exportsTail = null;
        for (var cur = rawExports; cur != null; cur = cur.CdrPair)
        {
            var item = UnwrapQuoted(cur.car);
            if (item is not Symbol)
                throw FormError(source, $"define-library: export must be an identifier symbol, got {Util.Dump(cur.car)}");
            Pair.AppendTail(ref exports, ref exportsTail, item);
        }
        return exports;
    }

    private static object EvalTopLevelInEnv(object? rawForm, Env env)
    {
        var expanded = Macro.Check(rawForm)!;
        Util.PropagateSourceDeep(rawForm, expanded);
        if (expanded is Pair defPair && defPair.car?.ToString() == "DEFINE")
            return new Define(defPair).Eval(env);

        var expression = Parse(expanded);
        return Vm.Execute(BytecodeCompiler.CompileTop(expression), env);
    }

    public override object Eval(Env env)
    {
        if (args == null || Pair.IsNull(args))
            throw FormError(form, "define-library: expected library name");

        var libraryNameToken = UnwrapQuoted(args.car);
        var remainder = args.CdrPair;

        if (!LooksLikeClauseSyntax(remainder))
        {
            var legacyExports = ParseLegacyExports(remainder, form);
            return Prim.DefineLibrary_Prim(new Pair(libraryNameToken, legacyExports));
        }

        Pair? exportList = null;
        Pair? exportTail = null;

        for (var cur = remainder; cur != null; cur = cur.CdrPair)
        {
            if (cur.car is not Pair clause || clause.car is not Symbol head)
                throw FormError(form, "define-library: each clause must be a list headed by export/import/begin");

            if (!string.Equals(head.ToString(), "export", StringComparison.Ordinal))
                continue;

            for (var exportCur = clause.CdrPair; exportCur != null; exportCur = exportCur.CdrPair)
            {
                var token = UnwrapQuoted(exportCur.car);
                if (token is not Symbol)
                    throw FormError(form, $"define-library: export must be an identifier symbol, got {Util.Dump(exportCur.car)}");
                Pair.AppendTail(ref exportList, ref exportTail, token);
            }
        }

        var libName = Prim.DefineLibrary_Prim(new Pair(libraryNameToken, exportList));
        var moduleEnv = Program.RequireCurrent().TryGetModuleLocal(libName.ToString()!)
            ?? throw FormError(form, "define-library: module was not registered");

        using var importScope = InterpreterContext.PushImportTarget(moduleEnv);

        for (var cur = remainder; cur != null; cur = cur.CdrPair)
        {
            if (cur.car is not Pair clause || clause.car is not Symbol head)
                throw FormError(form, "define-library: each clause must be a list headed by export/import/begin");

            switch (head.ToString())
            {
                case "export":
                    break;
                case "import":
                    if (clause.CdrPair == null)
                        throw FormError(form, "define-library: import clause requires at least one import set");
                    Prim.Import_Prim(clause.CdrPair);
                    break;
                case "begin":
                    for (var bodyCur = clause.CdrPair; bodyCur != null; bodyCur = bodyCur.CdrPair)
                        EvalTopLevelInEnv(bodyCur.car, moduleEnv);
                    break;
                default:
                    throw FormError(form, $"define-library: unsupported clause '{head}'");
            }
        }

        return libName;
    }

    public override string ToString() => Util.Dump("DEFINE-LIBRARY", args);
}

public class CommaAt(Pair? args) : Expression
{
    public Pair? Rands => args;

    public override object Eval(Env env)
    {
        var o = args == null ? null : Eval_Rands(args, env);
        return o!.Count == 1 ? o.car! : o;
    }

    public override string ToString() => Util.Dump(",@", args);
}

public class App(Expression rator, Pair? args) : Expression
{
    public Expression Rator => rator;
    public Pair? Rands => args;
    public static bool AutoCurry = false;

    private static object Trampoline(object result)
    {
        while (result is TailCall tc)
        {
            InterpreterContext.RecordTailCall();
            result = tc.Closure.Eval(tc.Args);
        }
        return result;
    }

    private static object Dispatch(Closure closure, Pair? args, bool tail) =>
        tail ? new TailCall(closure, args) : Trampoline(closure.Eval(args!));

    public override object Eval(Env env) => EvalImpl(env, tail: false);
    public override object EvalTail(Env env) => EvalImpl(env, tail: true);

    private object EvalImpl(Env env, bool tail)
    {
        if (rator is Var traced && IsTraceOn(traced.id))
            ConsoleOutput.WriteTrace(Util.Dump("call: ", traced.id, args));
        var proc = rator.Eval(env);
        return proc switch
        {
            Closure closure => EvalClosure(closure, env, tail),
            Primitive prim => prim(Eval_Rands(args, env)!),
            Var pv => EvalInPosition(Parse(new Pair(pv.GetName(), args)), env, tail),
            Pair { car: Closure pc } => Dispatch(pc, Eval_Rands(args, env), tail),
            _ => throw new LispException($"invalid operator {proc?.GetType()} {proc}"),
        };
    }

    private object EvalClosure(Closure closure, Env env, bool tail)
    {
        var evaledArgs = Eval_Rands(args, env);
        if (AutoCurry && args != null && closure.ids != null)
        {
            var rem = args;
            for (int i = 0; i < closure.arity; i++) rem = rem?.CdrPair;
            if (rem != null)
            {
                var inner = (Closure)Trampoline(closure.Eval(evaledArgs!));
                return Dispatch(inner, Eval_Rands(rem, env)!, tail);
            }
        }
        return Dispatch(closure, evaledArgs, tail);
    }

    public override string ToString() => Util.Dump("app", rator, args);
}
