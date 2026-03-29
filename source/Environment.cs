namespace Lisp;

public class Env
{
    public Dictionary<Symbol, object> table = new(ReferenceEqualityComparer.Instance);

    private static LispException Unbound(Symbol id) => new($"Unbound variable {id}");

    public Env Extend(Pair? syms, Pair? vals, int capacity = 0) =>
        Pair.IsNull(syms)
            ? new Extended_Env(null, null, this, 0)
            : new Extended_Env(syms, vals, this, capacity);

    public virtual object Bind(Symbol id, object val) => throw Unbound(id);
    public virtual object Apply(Symbol id) => throw Unbound(id);
}

public sealed class Extended_Env : Env
{
    private readonly Env env;

    public Extended_Env(Pair? inSyms, Pair? inVals, Env inEnv, int capacity = 0)
    {
        InterpreterContext.RecordEnvFrame();
        env = inEnv;
        if (capacity > 0)
            table = new(capacity, ReferenceEqualityComparer.Instance);
        AddBindings(inSyms, inVals);
    }

    private void AddBindings(Pair? symbols, Pair? values)
    {
        for (; symbols != null; symbols = symbols.cdr)
        {
            var currentSymbol = symbols.car as Symbol;
            if (Symbol.IsEqual(".", currentSymbol))
            {
                table.Add(symbols.cdr!.car as Symbol ?? throw new LispException("bad . syntax"), values ?? Pair.Empty);
                break;
            }
            table.Add(currentSymbol!, values!.car!);
            values = values.cdr;
        }
    }

    public override string ToString() => Util.Dump("env", table, env);

    public override object Bind(Symbol id, object val)
    {
        if (!table.ContainsKey(id)) return env.Bind(id, val);
        table[id] = val;
        return id;
    }

    public override object Apply(Symbol id) =>
        table.TryGetValue(id, out var v) ? v : env.Apply(id);
}

public sealed record TailCall(Closure Closure, Pair? Args);
