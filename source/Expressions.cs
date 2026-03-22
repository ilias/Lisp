namespace Lisp;

public abstract class Expression
{
    public static bool Trace = false;
    public static HashSet<Symbol> traceHash = [];
    private static readonly Symbol _sAll = Symbol.Create("_all_");

    public static bool IsTraceOn(Symbol s) =>
        Trace && (traceHash.Contains(s) || traceHash.Contains(_sAll));

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

    public static Expression Parse(object? a)
    {
        if (a is Symbol sym) return new Var(sym);
        if (a is not Pair pair) return new Lit(a);
        Pair? args = pair.cdr;
        Pair? body = null;
        switch (pair.car?.ToString())
        {
            case "IF":
                return new If(Parse(args!.car), Parse(args.cdr!.car), Parse(args.cdr!.cdr!.car));
            case "DEFINE":
                return new Define(pair);
            case "EVAL":
                return new Evaluate(Parse(args!.car));
            case "LAMBDA":
                var rawBodyArgs = args!.cdr;
                {
                    Pair? bodyTail = null;
                    foreach (object obj in args.cdr!)
                        Pair.AppendTail(ref body, ref bodyTail, Parse(obj));
                }
                return new Lambda(args.car as Pair, body, rawBodyArgs);
            case "quote":
                return new Lit(args!.car);
            case "set!":
                return new Assignment(args!.car as Symbol ?? throw new Exception("set! requires a symbol"), Parse(args.cdr!.car));
            case "TRY":
                return new Try(Parse(args!.car), Parse(args.cdr!.car));
            case "TRY-CONT":
                if (args!.cdr?.cdr != null)
                    return new TryCont(Parse(args.car), Parse(args.cdr!.car), Parse(args.cdr!.cdr!.car));
                return new TryCont(Parse(args.car), Parse(args.cdr!.car));
            case "LET-SYNTAX":
            case "LETREC-SYNTAX":
            case "let-syntax":
            case "letrec-syntax":
            {
                bool isLetrec = pair.car!.ToString()!.Contains("letrec") || pair.car!.ToString()!.Contains("LETREC");
                var bindPairs = args?.car as Pair;
                List<(object, Pair)> bindings = [];
                if (bindPairs != null && !Pair.IsNull(bindPairs))
                    foreach (object bp in bindPairs)
                    {
                        if (bp is not Pair bpair) continue;
                        var bname = bpair.car!;
                        var second = bpair.cdr?.car;
                        if (second == null) continue;
                        Pair? md;
                        if (second is Pair secondPair && secondPair.car?.ToString() == "syntax-rules")
                        {
                            var ds = new Pair(Symbol.Create("define-syntax"), new Pair(bname, new Pair(second, null)));
                            md = Macro.TranslateDefineSyntax(ds);
                        }
                        else
                        {
                            md = new Pair(bname, bpair.cdr!);
                        }
                        if (md != null) bindings.Add((bname, md));
                    }
                var rawBody = args?.cdr as Pair;
                return new LetSyntax(isLetrec, bindings, rawBody);
            }
            default:
                if (args != null)
                {
                    Pair? bodyTail = null;
                    foreach (object obj in args)
                        Pair.AppendTail(ref body, ref bodyTail, Parse(obj));
                }
                var carName = pair.car?.ToString()!;
                if (carName == ",@") return new CommaAt(body);
                if (Prim.list.TryGetValue(carName, out var prim))
                    return new Prim(prim, body);
                return new App(Parse(pair.car), body);
        }
    }
}

public class Lit(object? datum) : Expression
{
    public object? Datum => datum;
    public override object Eval(Env env) => datum is Pair p ? Comma(p, env)! : datum!;

    public Pair? Comma(Pair o, Env env)
    {
        if (Pair.IsNull(o)) return o;
        Pair? retVal = null;
        Pair? retValTail = null;
        void AppendVal(object? val) => Pair.AppendTail(ref retVal, ref retValTail, val);
        foreach (object car in o)
            if (car is not Pair cp)
                AppendVal(car);
            else if (Symbol.IsEqual(",", cp.car))
                AppendVal(Parse(cp.cdr!.car).Eval(env));
            else if (Symbol.IsEqual(",@", cp.car))
            {
                var ev = Parse(cp.cdr!.car).Eval(env);
                if (ev is Pair evPair)
                    foreach (object oo in evPair)
                        AppendVal(oo);
                else if (ev != null)
                    AppendVal(ev);
            }
            else
                AppendVal(Comma(cp, env));
        return retVal;
    }

    public override string ToString() => Util.Dump("lit", datum);
    public string GetName() => datum!.ToString()!;
}

public class Evaluate(Expression datum) : Expression
{
    public override object Eval(Env env) => datum.Eval(env) switch
    {
        null => null!,
        string s => Parse(Program.current!.Eval(s)).Eval(env),
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

    public override object Eval(Env env)
    {
        if (IsTraceOn(_sLambda))
            Console.WriteLine(Util.Dump("lambda: ", ids, body));
        return new Closure(ids, body, env, rawBody);
    }

    public override string ToString() => Util.Dump("LAMBDA", ids, body);
}

public class Define(Pair datum) : Expression
{
    public Symbol NameSym => datum.cdr!.car is Symbol s ? s : Symbol.Create(datum.cdr!.car!.ToString()!);
    public Expression ValExpr => Parse(datum.cdr!.cdr!.car);

    public override object Eval(Env env)
    {
        var sym = datum.cdr!.car is Symbol s2 ? s2 : Symbol.Create(datum.cdr!.car!.ToString()!);
        env.table[sym] = Parse(datum.cdr!.cdr!.car).Eval(env);
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
        try
        {
            var v = test.Eval(env);
            return v is not bool b || b;
        }
        catch (ContinuationException) { throw; }
        catch (LispException) { throw; }
        catch { return true; }
    }

    public override object Eval(Env env)
    {
        var res = EvalTest(env);
        return res ? tX.Eval(env) : eX.Eval(env);
    }

    public override object EvalTail(Env env)
    {
        var res = EvalTest(env);
        return res ? tX.EvalTail(env) : eX.EvalTail(env);
    }

    public override string ToString() => Util.Dump("IF", test, tX, eX);
}

public class Try(Expression tryX, Expression catchX) : Expression
{
    public override object Eval(Env env)
    {
        try { return tryX.Eval(env); }
        catch (ContinuationException) { throw; }
        catch { return catchX.Eval(env); }
    }

    public override string ToString() => Util.Dump("TRY", tryX, catchX);
}

public class TryCont(Expression? tag, Expression tryX, Expression catchX) : Expression
{
    public TryCont(Expression tryX, Expression catchX) : this(null, tryX, catchX) { }

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
    public override object Eval(Env env)
    {
        var saved = new Dictionary<object, object?>(Macro.macros);
        try
        {
            foreach (var (name, def) in bindings)
                Macro.macros[name] = def.cdr;

            object result = Pair.Empty;
            if (rawBody != null && !Pair.IsNull(rawBody))
                foreach (object form in rawBody)
                {
                    var expanded = Macro.Check(form);
                    result = Expression.Parse(expanded!).Eval(env);
                }
            return result;
        }
        finally
        {
            Macro.macros.Clear();
            foreach (var kv in saved) Macro.macros[kv.Key] = kv.Value;
        }
    }

    public override string ToString() => Util.Dump(isLetrec ? "LETREC-SYNTAX" : "LET-SYNTAX");
}

public class CommaAt(Pair? rands) : Expression
{
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
            if (Program.Stats) Program.TailCalls++;
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
            Console.WriteLine(Util.Dump("call: ", traced.id, rands));
        var proc = rator.Eval(env);
        return proc switch
        {
            Closure closure => EvalClosure(closure, env, tail),
            Primitive prim => prim(Eval_Rands(rands, env)!),
            Var pv => tail ? Parse(new Pair(pv.GetName(), rands)).EvalTail(env) : Parse(new Pair(pv.GetName(), rands)).Eval(env),
            Pair { car: Closure pc } => Dispatch(pc, Eval_Rands(rands, env), tail),
            _ => throw new Exception($"invalid operator {proc?.GetType()} {proc}"),
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
