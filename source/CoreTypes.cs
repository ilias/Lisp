namespace Lisp;

public sealed class Symbol
{
    private static readonly ConcurrentDictionary<string, Symbol> _syms = [];
    public static IReadOnlyDictionary<string, Symbol> syms => _syms;
    private static int symNum = 1000;
    private readonly string val;

    private Symbol(string val) => this.val = val;

    public static Symbol GenSym() => Create($"_sym_{Interlocked.Increment(ref symNum) - 1}");

    public static Symbol Create(string name) =>
        _syms.GetOrAdd(name, static key => new Symbol(key));

    private static readonly ConcurrentDictionary<string, Symbol> gensymTable = [];

    public static Symbol CreateGensym(string name) =>
        gensymTable.GetOrAdd(name, static key => new Symbol(key));

    internal static void ClearGensyms() => gensymTable.Clear();

    public static bool IsEqual(string id, object? obj) =>
        obj is Symbol s && id == s.val;

    public override string ToString() => val;
}

public class Closure
{
    public Pair? ids;
    public Pair? body;
    public Pair? rawBody;
    public Env env;
    public readonly int arity;
    public string? DebugName { get; set; }
    private static readonly Symbol _sClosure = Symbol.Create("closure");

    public Closure(Pair? ids, Pair? body, Env env, Pair? rawBody = null)
    {
        this.ids = ids;
        this.body = body;
        this.env = env;
        this.rawBody = rawBody;
        arity = ids?.Count ?? 0;
    }

    public virtual object Eval(Pair? args)
    {
        if (Program.Stats) Program.Iterations++;
        if (Program.Stats) Program.TreeWalkCalls++;
        if (Expression.IsTraceOn(_sClosure))
            ConsoleOutput.WriteTrace(Util.Dump("closure: ", ids, body, args));
        var callEnv = env.Extend(ids, args, arity);
        Expression? pending = null;
        foreach (Expression exp in body!)
        {
            pending?.Eval(callEnv);
            pending = exp;
        }
        return pending != null ? pending.EvalTail(callEnv) : null!;
    }

    public override string ToString() => Util.Dump("closure", ids, body);
}

public sealed class Pair : ICollection, IEnumerable<object?>
{
    public static Pair Empty { get; } = new(null);
    public object? car;
    public Pair? cdr;

    private bool IsEmptyPair => car == null && cdr == null;

    public Pair(object? car, Pair? cdr = null)
    {
        this.car = car;
        this.cdr = cdr;
    }

    public static Pair Append(Pair? link, object? obj)
    {
        if (link == null) return new Pair(obj);
        link.Append(obj);
        return link;
    }

    public static void AppendTail(ref Pair? head, ref Pair? tail, object? value)
    {
        var node = new Pair(value);
        if (tail is null) head = tail = node;
        else { tail.cdr = node; tail = node; }
    }

    public static bool IsNull(object? obj) =>
        obj == null || (obj is Pair p && p.IsEmptyPair);

    public static Pair Cons(object obj, object p)
    {
        var newPair = new Pair(obj);
        if (IsNull(p)) return newPair;
        newPair.cdr = p is Pair pair ? pair : new Pair(p);
        return newPair;
    }

    public void Append(object? obj)
    {
        GetTail(this).cdr = new Pair(obj);
    }

    private static Pair GetTail(Pair pair)
    {
        var current = pair;
        while (current.cdr != null)
            current = current.cdr;
        return current;
    }

    public int Count
    {
        get { int n = 0; foreach (var _ in this) n++; return n; }
    }

    public void CopyTo(Array array, int index)
    {
        if (array.Length < Count + index) throw new ArgumentException();
        foreach (var obj in this) array.SetValue(obj, index++);
    }

    public PairEnumerator GetEnumerator() => new(this);
    IEnumerator<object?> IEnumerable<object?>.GetEnumerator() => new PairEnumerator(this);
    IEnumerator IEnumerable.GetEnumerator() => new PairEnumerator(this);
    public bool IsSynchronized => false;
    public object SyncRoot => this;

    public object[] ToArray()
    {
        List<object> list = [];
        for (var p = this; p != null; p = p.cdr)
            list.Add(p.car!);
        return [.. list];
    }

    public override string ToString() => Util.Dump(this);

    public struct PairEnumerator : IEnumerator<object?>, IEnumerator
    {
        private readonly Pair root;
        private Pair? current;

        public PairEnumerator(Pair pair)
        {
            root = pair;
            current = null;
        }

        public object Current => current!.car!;
        object? IEnumerator<object?>.Current => current!.car;
        object IEnumerator.Current => current!.car!;

        public bool MoveNext()
        {
            if (current == null)
            {
                if (root.IsEmptyPair) return false;
                current = root;
                return true;
            }
            if (current.cdr != null)
            {
                current = current.cdr;
                return true;
            }
            return false;
        }

        public void Reset() => current = null;
        public void Dispose() { }
    }
}