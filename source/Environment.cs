namespace Lisp;

public class Env
{
    public Dictionary<Symbol, object> table = new(ReferenceEqualityComparer.Instance);

    private static LispException Unbound(Symbol id) => new($"Unbound variable {id}");

    public Env Extend(Pair? syms, Pair? vals, int capacity = 0) =>
        Pair.IsNull(syms)
            ? new LocalEnvironment(null, null, this, 0)
            : new LocalEnvironment(syms, vals, this, capacity);

    public virtual object Bind(Symbol id, object val) => throw Unbound(id);
    public virtual object Apply(Symbol id) => throw Unbound(id);
}

public sealed class LocalEnvironment : Env
{
    private readonly Env env;

    public LocalEnvironment(Pair? inSyms, Pair? inVals, Env inEnv, int capacity = 0)
    {
        InterpreterContext.RecordEnvFrame();
        env = inEnv;
        if (capacity > 0)
            table = new(capacity, ReferenceEqualityComparer.Instance);
        AddBindings(inSyms, inVals);
    }

    private void AddBindings(Pair? symbols, Pair? values)
    {
        for (; symbols != null; symbols = symbols.CdrPair)
        {
            var currentSymbol = symbols.car as Symbol;
            // Old-style rest syntax: (. rest) or (a b . rest) encoded as list with dot symbol.
            if (Symbol.IsEqual(".", currentSymbol))
            {
                table.Add(symbols.CdrPair!.car as Symbol ?? throw new LispException("bad . syntax"), values ?? Pair.Empty);
                break;
            }
            table.Add(currentSymbol!, values!.car!);
            values = values.CdrPair;
            // New-style proper dotted pair: (a b . rest) where rest is the Symbol cdr of the last node.
            if (symbols.cdr is Symbol restSym)
            {
                table.Add(restSym, values ?? Pair.Empty);
                break;
            }
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
