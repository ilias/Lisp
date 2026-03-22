namespace Lisp;

public class Env
{
    public Dictionary<Symbol, object> table = new(ReferenceEqualityComparer.Instance);

    public Env Extend(Pair? syms, Pair? vals, int capacity = 0)
    {
        if (Pair.IsNull(syms))
            return new Extended_Env(null, null, this, 0);
        return new Extended_Env(syms, vals, this, capacity);
    }

    public virtual object Bind(Symbol id, object val) => throw new Exception($"Unbound variable {id}");
    public virtual object Apply(Symbol id) => throw new Exception($"Unbound variable {id}");
}

public sealed class Extended_Env : Env
{
    private readonly Env env;

    public Extended_Env(Pair? inSyms, Pair? inVals, Env inEnv, int capacity = 0)
    {
        if (Program.Stats) Program.EnvFrames++;
        env = inEnv;
        if (capacity > 0)
            table = new Dictionary<Symbol, object>(capacity, ReferenceEqualityComparer.Instance);
        for (; inSyms != null; inSyms = inSyms.cdr)
        {
            var currSym = inSyms.car as Symbol;
            if (Symbol.IsEqual(".", currSym))
            {
                table.Add(inSyms.cdr!.car as Symbol ?? throw new Exception("bad . syntax"), inVals ?? Pair.Empty);
                break;
            }
            table.Add(currSym!, inVals!.car!);
            inVals = inVals.cdr;
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
