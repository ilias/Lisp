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
        var transformer = binding.cdr?.car;
        if (transformer == null) return null;
        if (transformer is Pair transformerPair && transformerPair.car?.ToString() == "syntax-rules")
        {
            var defineSyntax = new Pair(_sDefineSyntax, new Pair(name, new Pair(transformer, null)));
            Util.PropagateSourceDeep(binding, defineSyntax);
            var translated = Macro.TranslateDefineSyntax(defineSyntax);
            Util.PropagateSourceDeep(binding, translated);
            return translated;
        }
        var syntaxRules = Macro.TranslateSyntaxRules(name, transformer as Pair, binding.cdr?.cdr)
            ?? new Pair(name, binding.cdr!);
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
        Pair? args = pair.cdr;
        return ParseSpecialForm(pair, args) ?? ParseApplication(pair, args);
    }

    private static LispException FormError(Pair form, string message) =>
        new LispException(message).AttachSchemeContext(Util.GetSource(form), null);

    private static int CountArgs(Pair? args)
    {
        int count = 0;
        for (var current = args; current != null && !Pair.IsNull(current); current = current.cdr)
            count++;
        return count;
    }

    private static Pair RequireArgRange(Pair form, string name, Pair? args, int min, int max)
    {
        int count = CountArgs(args);
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
        int count = CountArgs(args);
        if (count < min)
            throw FormError(form, $"{name}: expected at least {min} argument{(min == 1 ? "" : "s")}, got {count}");

        return args!;
    }

    private static Expression ParseIfForm(Pair pair, Pair? args)
    {
        var validated = RequireArgRange(pair, "if", args, 2, 3);
        var elseCell = validated.cdr!.cdr;
        return new If(
            Parse(validated.car),
            Parse(validated.cdr.car),
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
        return new Lambda(validated.car as Pair, ParseBody(validated.cdr), validated.cdr);
    }

    private static Expression ParseQuoteForm(Pair pair, Pair? args)
    {
        int count = CountArgs(args);
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

        return new Assignment(symbol, Parse(validated.cdr!.car));
    }

    private static Expression ParseTryForm(Pair pair, Pair? args)
    {
        var validated = RequireArgRange(pair, "try", args, 2, 2);
        return new Try(Parse(validated.car), Parse(validated.cdr!.car));
    }

    private static Expression ParseTryContForm(Pair pair, Pair? args)
    {
        var validated = RequireArgRange(pair, "try-cont", args, 2, 3);
        return !Pair.IsNull(validated.cdr?.cdr)
            ? new TryCont(Parse(validated.car), Parse(validated.cdr!.car), Parse(validated.cdr!.cdr!.car))
            : new TryCont(Parse(validated.car), Parse(validated.cdr!.car));
    }

    private static Expression ParseLetSyntaxForm(Pair pair, Pair? args)
    {
        var validated = RequireMinArgs(pair, pair.car!.ToString()!, args, 1);
        return new LetSyntax(
            pair.car!.ToString()!.Contains("letrec", StringComparison.OrdinalIgnoreCase),
            ParseLetSyntaxBindings(validated.car as Pair),
            validated.cdr as Pair);
    }

    private static Expression? ParseSpecialForm(Pair pair, Pair? args) => pair.car?.ToString() switch
    {
        "IF" => ParseIfForm(pair, args),
        "DEFINE" => new Define(pair),
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
        if (Prim.list.TryGetValue(carName, out var prim))
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

        foreach (object item in pair)
        {
            if (item is not Pair quotedPair)
            {
                AppendValue(item);
                continue;
            }

            if (Symbol.IsEqual(",", quotedPair.car))
            {
                AppendValue(Parse(quotedPair.cdr!.car).Eval(env));
                continue;
            }

            if (Symbol.IsEqual(",@", quotedPair.car))
            {
                var evaluated = Parse(quotedPair.cdr!.car).Eval(env);
                if (evaluated is Pair splice)
                {
                    foreach (object splicedItem in splice)
                        AppendValue(splicedItem);
                }
                else if (evaluated != null)
                {
                    AppendValue(evaluated);
                }
                continue;
            }

            AppendValue(EvalQuotedPair(quotedPair, env));
        }

        return result;
    }

    public override string ToString() => Util.Dump("lit", datum);
    public string GetName() => datum!.ToString()!;
}

public class Evaluate(Expression datum) : Expression
{
    public override object Eval(Env env) => datum.Eval(env) switch
    {
        null => null!,
        string s => Parse(Program.RequireCurrent().Eval(s)).Eval(env),
        var o => Parse(o).Eval(env),
    };

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
        static int CountArgs(Pair? args)
        {
            int count = 0;
            for (var current = args; current != null && !Pair.IsNull(current); current = current.cdr)
                count++;
            return count;
        }

        static LispException FormError(Pair form, string message) =>
            new LispException(message).AttachSchemeContext(Util.GetSource(form), null);

        int count = CountArgs(datum.cdr);
        if (count < 2)
            throw FormError(datum, $"define: expected at least 2 arguments, got {count}");

        if (datum.cdr?.car is not Symbol)
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

        if (Pair.IsNull(forms.cdr))
            return Parse(forms.car);

        return new App(new Lambda(null, ParseBodyForms(forms), forms), null);
    }

    private readonly Pair datum = ValidateDatum(datum);

    public Symbol NameSym => datum.cdr!.car is Symbol s ? s : Symbol.Create(datum.cdr!.car!.ToString()!);
    public Expression ValExpr => ParseValueExpression(datum.cdr!.cdr);

    public override object Eval(Env env)
    {
        var sym = datum.cdr!.car is Symbol s2 ? s2 : Symbol.Create(datum.cdr!.car!.ToString()!);
        var value = ValExpr.Eval(env);
        if (value is Closure closure && string.IsNullOrEmpty(closure.DebugName))
            closure.DebugName = sym.ToString();
        if (value is Closure cl2 && cl2.DocComment == null)
            cl2.DocComment = Util.ConsumePendingDocComment();
        else
            Util.ConsumePendingDocComment();
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
                Macro.Set(name, def.cdr);
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

public class CommaAt(Pair? rands) : Expression
{
    public Pair? Rands => rands;

    public override object Eval(Env env)
    {
        var o = rands == null ? null : Eval_Rands(rands, env);
        return o!.Count == 1 ? o.car! : o;
    }

    public override string ToString() => Util.Dump(",@", rands);
}

public class App(Expression rator, Pair? rands) : Expression
{
    public Expression Rator => rator;
    public Pair? Rands => rands;
    public static bool CarryOn = false;

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
            ConsoleOutput.WriteTrace(Util.Dump("call: ", traced.id, rands));
        var proc = rator.Eval(env);
        return proc switch
        {
            Closure closure => EvalClosure(closure, env, tail),
            Primitive prim => prim(Eval_Rands(rands, env)!),
            Var pv => EvalInPosition(Parse(new Pair(pv.GetName(), rands)), env, tail),
            Pair { car: Closure pc } => Dispatch(pc, Eval_Rands(rands, env), tail),
            _ => throw new LispException($"invalid operator {proc?.GetType()} {proc}"),
        };
    }

    private object EvalClosure(Closure closure, Env env, bool tail)
    {
        var evaledArgs = Eval_Rands(rands, env);
        if (CarryOn && rands != null && closure.ids != null)
        {
            var rem = rands;
            for (int i = 0; i < closure.arity; i++) rem = rem?.cdr;
            if (rem != null)
            {
                var inner = (Closure)Trampoline(closure.Eval(evaledArgs!));
                return Dispatch(inner, Eval_Rands(rem, env)!, tail);
            }
        }
        return Dispatch(closure, evaledArgs, tail);
    }

    public override string ToString() => Util.Dump("app", rator, rands);
}
