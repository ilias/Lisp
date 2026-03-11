using System;
using System.Collections;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Globalization;
using System.Text;
using Lisp.Environment;
using Lisp.Expressions;
using Lisp.Macros;
using Lisp.Programs;

namespace Lisp
{
    public class Util
    {
        public static string GAC;
        static Util()
        {
            var root = System.Environment.GetEnvironmentVariable("systemroot");
            var ver  = System.Environment.Version.ToString();
            GAC = $"{root}\\Microsoft.NET\\Framework\\v{ver[..ver.LastIndexOf('.')]}\\";
        }
        public static Type[] GetTypes(object[] objs) =>
            Array.ConvertAll(objs, o => o?.GetType() ?? typeof(object));
        public static object CallMethod(Pair args, bool staticCall)
        {
            var objs  = args.cdr?.cdr != null ? args.cdr.cdr.ToArray() : null;
            var types = objs != null ? GetTypes(objs) : Array.Empty<Type>();
            var type  = staticCall ? GetType(args.car!.ToString()!) : args.car!.GetType();
            try
            {
                // First try exact-signature lookup (fast path)
                var method = type!.GetMethod(args.cdr!.car!.ToString()!, types);
                if (method != null)
                    return method.Invoke(args.car, objs)!;
                // Fallback: use InvokeMember with DefaultBinder so numeric type coercion
                // works (e.g. Double argument matching an int parameter)
                var flags = BindingFlags.InvokeMethod
                          | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
                return type.InvokeMember(args.cdr!.car!.ToString()!, flags, null, args.car, objs)!;
            }
            catch (System.Reflection.TargetInvocationException tie)
            {
                throw tie.InnerException ?? tie;
            }
        }
        public static Type? GetType(string tname)
        {
            // Type.GetType handles System.Private.CoreLib types and the calling assembly
            Type? type = Type.GetType(tname);
            if (type != null) return type;
            // Search all loaded assemblies (finds Lisp types and other loaded assemblies)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                if ((type = asm.GetType(tname)) != null) return type;
            // Handle 'file@class or '~file@class syntax
            var comp = tname.Split('@');
            comp[0] = comp[0].Replace("~", GAC);  // replace ~ with the GAC directory
            if (comp.Length == 2) // 'file@class or 'path\file@class
                try
                {
                    if ((type = Assembly.LoadFrom(comp[0]).GetType(comp[1])) != null)
                        return type;
                }
                catch { }
            return null;
        }
        static public void Throw(string message) => throw new LispException(message);
        public static string ParseRemainder
        {
            // [ThreadStatic] initializers only execute on the first thread; use a backing
            // field with null-coalescing so every thread sees "" as the default.
            get => _parseRemainder ?? "";
            set => _parseRemainder = value;
        }
        [ThreadStatic] private static string? _parseRemainder;
        public static object? ParseOne(string content)
        {
            var result = Parse(content, out var after);
            ParseRemainder = after ?? "";
            return result;
        }
        static public string Dump(string title, params object?[] args)
        {
            var output = new StringBuilder($"[{title} ");
            foreach (object? o in args)
                output.Append(Dump(o)).Append(' ');
            return output.Append(']').ToString();
        }
        // Format a double using the shortest round-trip representation, stripping the
        // Prints the shortest decimal that round-trips back to the same double (Grisu3/Ryu
        // via the "R" specifier on .NET 5+).  A decimal point is always included so the
        // reader knows the value is inexact: 3.0 → "3.", 1e100 → "1E+100." etc.
        private static string FormatDouble(double d)
        {
            if (double.IsNaN(d))              return "+nan.0";
            if (double.IsPositiveInfinity(d)) return "+inf.0";
            if (double.IsNegativeInfinity(d)) return "-inf.0";
            // "R" chooses the shortest decimal that round-trips (shortest representation).
            var s = d.ToString("R", CultureInfo.InvariantCulture);
            // Trim unnecessary trailing zeros after the decimal point, but keep at least
            // one digit so "3.0" becomes "3." (Scheme inexact marker) not "3".
            if (s.Contains('.') && !s.Contains('E') && !s.Contains('e'))
            {
                s = s.TrimEnd('0');          // "3.14000" → "3.14", "3.0" → "3."
                if (s.EndsWith('.')) { /* keep the dot */ }
            }
            // Ensure there is always a decimal point so readers know it is inexact.
            if (!s.Contains('.') && !s.Contains('E') && !s.Contains('e'))
                s += ".";
            return s;
        }

        static public string Dump(object? exp)
        {
            return exp switch
            {
                _ when Pair.IsNull(exp) => "()",
                string s                => $"\"{s}\"",
                bool b                  => b ? "#t" : "#f",
                char c                  => $"#\\{c}",
                double d                => FormatDouble(d),
                BigInteger bi           => bi.ToString(),
                Pair { car: Symbol quot } p when quot.ToString() == "quote" => $"'{Dump(p.cdr!.car)}",
                ICollection             => FormatCollection(exp),
                _                       => exp?.ToString() ?? "()",
            };
        }

        static private string FormatCollection(object? exp)
        {
            var sb = new StringBuilder("(");
            foreach (object? o in (ICollection)exp!) sb.Append(Dump(o)).Append(' ');
            return (exp is ArrayList ? "#" : "") + sb.Append(')').ToString().Replace(" )", ")");
        }
        private static bool IsSymbolStopChar(char c) =>
            c is '(' or ')' or '\n' or '\r' or '\t'
            or ' ' or '#' or ',' or '\'' or '"';

        // Public API: parse one S-expression from str; set after to the unparsed remainder.
        // Internally delegates to the index-based core to avoid O(n) string slicing per token.
        static public object? Parse(string str, out string after)
        {
            int pos = 0;
            // Skip leading whitespace once here so callers always get a trimmed remainder.
            while (pos < str.Length && str[pos] is ' ' or '\t' or '\r' or '\n') pos++;
            var result = ParseAt(str, ref pos);
            // Trim leading whitespace from 'after' so the next call's leading-whitespace skip
            // has nothing to do — preserving the original contract.
            while (pos < str.Length && str[pos] is ' ' or '\t' or '\r' or '\n') pos++;
            after = pos >= str.Length ? "" : str[pos..];
            return result;
        }

        // Core recursive parser that advances 'pos' in-place through 'str' without
        // ever creating intermediate substrings.
        private static object? ParseAt(string str, ref int pos)
        {
            // Skip leading whitespace
            while (pos < str.Length && str[pos] is ' ' or '\t' or '\r' or '\n') pos++;
            if (pos >= str.Length) return null;

            char ch = str[pos];

            // ── Numbers ─────────────────────────────────────────────────────────────
            if (char.IsDigit(ch) || (ch == '-' && pos + 1 < str.Length && char.IsDigit(str[pos + 1])))
            {
                int start = pos++;
                bool hasDot = false;
                while (pos < str.Length && (char.IsAsciiDigit(str[pos]) || (!hasDot && str[pos] == '.')))
                {
                    if (str[pos] == '.') hasDot = true;
                    pos++;
                }
                var span = str.AsSpan(start, pos - start);
                if (hasDot)
                    return (object)double.Parse(span, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (int.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv))
                    return (object)iv;
                return (object)BigInteger.Parse(span, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }

            switch (ch)
            {
                // ── Line comment ─────────────────────────────────────────────────────
                case ';':
                    while (pos < str.Length && str[pos] is not '\n' and not '\r') pos++;
                    return ParseAt(str, ref pos);

                // ── Unquote / unquote-splicing ───────────────────────────────────────
                case ',':
                    pos++;
                    bool splicing = pos < str.Length && str[pos] == '@';
                    if (splicing) pos++;
                    return Pair.Cons(Symbol.Create(splicing ? ",@" : ","),
                        new Pair(ParseAt(str, ref pos)));

                // ── Quote ────────────────────────────────────────────────────────────
                case '\'':
                    pos++;
                    return Pair.Cons(Symbol.Create("quote"), new Pair(ParseAt(str, ref pos)));

                // ── Quasiquote ───────────────────────────────────────────────────────
                case '`':
                    pos++;
                    return Pair.Cons(Symbol.Create("quote"), new Pair(ParseAt(str, ref pos)));

                // ── # dispatch ───────────────────────────────────────────────────────
                case '#':
                    pos++;
                    if (pos >= str.Length) return null;
                    switch (str[pos++])
                    {
                        case '\\':
                            if (pos < str.Length) return str[pos++];
                            return null;
                        case '(':
                            pos--; // step back to '(' so list parser sees it
                            var vec = (Pair?)ParseAt(str, ref pos);
                            return new ArrayList(vec!.ToArray());
                        default:
                            return str[pos - 1] == 't';
                    }

                // ── String literal ───────────────────────────────────────────────────
                case '"':
                {
                    pos++; // skip opening "
                    var cVal = new StringBuilder();
                    while (pos < str.Length && str[pos] != '"')
                    {
                        if (str[pos] == '\\')
                        {
                            pos++;
                            cVal.Append(str[pos] == 'n' ? "\n" : str[pos].ToString());
                        }
                        else
                            cVal.Append(str[pos]);
                        pos++;
                    }
                    if (pos < str.Length) pos++; // skip closing "
                    return cVal.ToString();
                }

                // ── List ─────────────────────────────────────────────────────────────
                case '(':
                {
                    pos++; // skip '('
                    Pair? retval = null;
                    for (object? item; (item = ParseAt(str, ref pos)) != null;)
                        retval = Pair.Append(retval, item);
                    return retval ?? new Pair(null);
                }

                // ── End of list ──────────────────────────────────────────────────────
                case ')':
                    pos++;
                    return null;

                // ── Lambda shorthand: \x,y.body ──────────────────────────────────────
                case '\\':
                {
                    pos++;
                    int start = pos;
                    while (pos < str.Length && str[pos] != '.') pos++;
                    var paramStr = str[start..pos];
                    pos++; // skip '.'
                    Pair? vars = null;
                    foreach (var id in paramStr.Split(','))
                        if (vars is null) vars = new Pair(Symbol.Create(id));
                        else             vars.Append(Symbol.Create(id));
                    return new Pair("LAMBDA", new Pair(vars, new Pair(ParseAt(str, ref pos))));
                }

                // ── Symbol ───────────────────────────────────────────────────────────
                default:
                {
                    int start = pos;
                    while (pos < str.Length && !IsSymbolStopChar(str[pos])) pos++;
                    return Symbol.Create(str[start..pos]);
                }
            }
        }
    }

    // Thrown by (throw ...) in Lisp code; distinguishes user errors from interpreter errors.
    public sealed class LispException(string message) : Exception(message) { }

    public class Symbol
    {
        public static readonly Dictionary<string, Symbol> syms = [];
        public static int symNum = 1000;
        private readonly string val;

        private Symbol(string val) => this.val = val;

        public static Symbol GenSym() => Create($"_sym_{symNum++}");

        public static Symbol Create(string name) =>
            syms.TryGetValue(name, out var sym) ? sym : syms[name] = new Symbol(name);

        // Gensyms produced during macro expansion are stored in a separate table so they
        // don't accumulate in the main symbol table.  Cleared after each expansion.
        private static readonly Dictionary<string, Symbol> gensymTable = [];
        public  static Symbol CreateGensym(string name) =>
            gensymTable.TryGetValue(name, out var gs) ? gs : gensymTable[name] = new Symbol(name);
        internal static void ClearGensyms() => gensymTable.Clear();

        public static bool IsEqual(string id, object? obj) =>
            obj is Symbol s && id == s.val;

        public override string ToString() => val;
    }

        public class Closure
        {
            public Pair? ids, body, rawBody;
            public Env   env;
            public Env?  inEnv;
            // Cached once at construction; avoids O(n) Pair traversal in EvalClosure and Extend.
            public readonly int arity;
            private static readonly Symbol _sClosure = Symbol.Create("closure");

            public Closure(Pair? ids, Pair? body, Env env, Pair? rawBody = null)
            {
                this.ids = ids; this.body = body; this.env = env; this.inEnv = env;
                this.rawBody = rawBody;
                this.arity = ids?.Count ?? 0;
            }

            public object Eval(Pair? args)
            {
                if (Program.Stats) Program.Iterations++;
                if (Expression.IsTraceOn(_sClosure))
                    Console.WriteLine(Util.Dump("closure: ", ids, body, args));
                inEnv = env.Extend(ids, args, arity);
                // Evaluate every body expression except the last one normally.
                // The last expression is evaluated in tail position so that tail calls
                // return a TailCall instead of recursing, enabling the trampoline in App.Eval.
                Expression? pending = null;
                foreach (Expression exp in body!)
                {
                    pending?.Eval(inEnv);
                    pending = exp;
                }
                return pending != null ? pending.EvalTail(inEnv) : null!;
            }

            public override string ToString() => Util.Dump("closure", ids, body);
        }

    public class Pair : ICollection, IEnumerable<object?>
    {
        public static Pair Append(Pair? link, object? obj)
        {
            if (link == null) return new Pair(obj);
            link.Append(obj);
            return link;
        }
        public static bool IsNull(object? obj) =>
            obj == null || (obj is Pair p && p.car == null && p.cdr == null);

        public static Pair Cons(object obj, object p)
        {
            var newPair = new Pair(obj);
            if (IsNull(p)) return newPair;
            newPair.cdr = (p == null || p is Pair) ? (Pair?)p : new Pair(p);
            return newPair;
        }

        public void Append(object? obj)
        {
            Pair curr = this;
            while (curr.cdr != null)
                curr = curr.cdr;
            curr.cdr = new Pair(obj);
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

        // Return a value-type enumerator so foreach on Pair avoids a heap allocation.
        public PairEnumerator GetEnumerator() => new PairEnumerator(this);
        IEnumerator<object?> IEnumerable<object?>.GetEnumerator() => new PairEnumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => new PairEnumerator(this);

        public bool   IsSynchronized => false;
        public object SyncRoot       => this;

        public object[] ToArray()
        {
            // Single pass — avoids the double traversal that Count + CopyTo would cause.
            var list = new List<object>();
            for (var p = this; p != null; p = p.cdr)
                list.Add(p.car!);
            return list.ToArray();
        }
        public object? car;
        public Pair?   cdr;
        public Pair(object? car) : this(car, null) { }
        public Pair(object? car, Pair? cdr) { this.car = car; this.cdr = cdr; }
        public override string ToString() => Util.Dump(this);

        public struct PairEnumerator : IEnumerator<object?>, IEnumerator
        {
            private readonly Pair root;
            private Pair? current;
            public PairEnumerator(Pair pair) { root = pair; current = null; }
            // Public non-nullable Current: pattern-based foreach uses this, keeping
            // all existing "foreach (object x in pair)" call-sites warning-free.
            public object  Current                         => current!.car!;
            object? IEnumerator<object?>.Current           => current!.car;
            object  IEnumerator.Current                    => current!.car!;
            public bool MoveNext()
            {
                if (current == null)     { current = root;        return true; }
                if (current.cdr != null) { current = current.cdr; return true; }
                return false;
            }
            public void Reset() => current = null;
            public void Dispose() { }
        }
    }

    namespace Macros
    {
        public class Macro
        {
            public static Dictionary<object, object?> macros = [];
            // Shared counter: incremented before each clause-match attempt so pattern
            // variables (?x) in different expansions get distinct name suffixes.
            private static int _symbol = 0;

            public static void Add(Pair obj)
            {
                macros[obj.car!] = obj.cdr;
            }

            // Translate (define-syntax name (syntax-rules (lits...) (pattern template)...))
            // into the internal Pair format that Add() expects: (name (lits...) clauses...).
            // The two notational differences handled here are:
            //   1. Pattern head: the macro name → replaced with _
            //   2. Ellipsis: syntax-rules 'sym ...' (two tokens) → internal 'sym...' (one symbol)
            //      Pair-subpatterns like '(a b) ...' are already compatible and kept intact.
            public static Pair? TranslateDefineSyntax(Pair ds)
            {
                // ds = (define-syntax name (syntax-rules (lits...) clause...))
                if (ds.cdr is not Pair np) return null;
                var name = np.car;
                if (np.cdr is not Pair srCell || srCell.car is not Pair sr
                    || sr.car?.ToString() != "syntax-rules") return null;
                var lits = sr.cdr?.car as Pair;          // may be null for empty ()
                Pair? clauses = null;
                if (sr.cdr?.cdr != null)
                    foreach (object rawClause in sr.cdr.cdr)
                    {
                        if (rawClause is not Pair clause) continue;
                        var origPat = clause.car as Pair;   // (name pats...)
                        var tmpl    = clause.cdr?.car;      // template expression
                        if (origPat == null || tmpl == null) continue;
                        var tPat  = MergeEllipsis(origPat, replaceHead: true);
                        var tTmpl = tmpl is Pair tp ? (object?)MergeEllipsis(tp, replaceHead: false) : tmpl;
                        clauses = Pair.Append(clauses, new Pair(tPat, new Pair(tTmpl, null)));
                    }
                return new Pair(name, new Pair(lits, clauses));
            }

            // Walk a Pair list, performing two rewrites at every level:
            //   sym ...   (symbol immediately followed by '...' token) → sym...
            //   _ in non-head position → fresh ?wc_N wildcard (matches, discards)
            // replaceHead=true: replace the first element with _ (top-level pattern).
            private static int _wcCounter = 0;
            private static Pair? MergeEllipsis(Pair? p, bool replaceHead)
            {
                if (p == null) return null;
                Pair? result = null;
                bool first = replaceHead;
                while (p != null)
                {
                    var elem = p.car;
                    p = p.cdr;
                    if (first) { result = Pair.Append(result, Symbol.Create("_")); first = false; continue; }
                    // _ wildcard in non-head pattern position → unique throwaway variable
                    if (elem is Symbol ws && ws.ToString() == "_")
                    { result = Pair.Append(result, Symbol.Create($"?wc{_wcCounter++}")); continue; }
                    // sym ... (simple symbol followed by ...) → sym...
                    if (elem is Symbol sym && !sym.ToString().Contains("...") && p?.car?.ToString() == "...")
                    { result = Pair.Append(result, Symbol.Create(sym + "...")); p = p.cdr; continue; }
                    // Recurse into sub-pairs (don't replace their head)
                    if (elem is Pair sub) elem = MergeEllipsis(sub, replaceHead: false);
                    result = Pair.Append(result, elem);
                }
                return result;
            }

            // Public entry point.  Creates a fresh per-expansion context, runs the expansion,
            // then purges the gensym cache so it doesn't accumulate indefinitely.
            static public object? Check(object? obj)
            {
                if (macros.Count == 0) return obj;
                var result = new MacroExpander().Expand(obj);
                Symbol.ClearGensyms();
                return result;
            }

            // All mutable expansion state lives in this instance so nested / recursive
            // macro expansions are completely independent of each other.
            private class MacroExpander
            {
                Dictionary<object, object?> vars = [];
                Dictionary<object, object?> cons = [];
                Dictionary<object, object?> temp = [];
                bool more = false;

                object? Variable(object? v, object? val, bool all)
                {
                    var sym = Symbol.Create(all ? (v!.ToString()! + "...") : v!.ToString()!);
                    return (vars[sym] = all ? Pair.Append(vars.GetValueOrDefault(sym) as Pair, val) : val!);
                }

                bool IsMatch(Pair? obj, Pair? pat, bool all)
                {
                    for (; pat != null; pat = pat.cdr)
                        if (Pair.IsNull(pat.car) && Pair.IsNull(obj?.car))
                            obj = obj?.cdr;
                        else if (Pair.IsNull(pat.car) && !Pair.IsNull(obj?.car))
                            return false;
                        else if (pat.car is Symbol patSym && patSym.ToString().IndexOf("...") > 0)
                        { // is the last item (variable containing ... in name takes rest
                            Variable(pat.car, obj, all);
                            return true;
                        }
                        else if (obj == null) return false;
                        else if (pat.car is Symbol && cons.ContainsKey(pat.car)) // is a constant
                        {
                            if (obj.car != cons[pat.car]) return false;
                            obj = obj.cdr;
                        }
                        else if (pat.cdr != null && pat.cdr.car?.ToString() == "...")
                        {
                            foreach (object x in obj!)
                                if (!IsMatch(x as Pair, pat.car as Pair, true))
                                    return false;
                            return true;
                        }
                        else if (pat.car is Symbol) // variable
                        {
                            Variable(pat.car, obj.car, all);
                            obj = obj.cdr;
                        }
                        else if (IsMatch(obj.car as Pair, pat.car as Pair, all)) // first element
                            obj = obj.cdr;
                        else
                            return false;
                    return obj == null;
                }

                object? Transform(object? obj, bool repeat)
                {
                    if (obj == null) return null;
                    if (obj is not Pair)
                        return vars.ContainsKey(obj) ? vars[obj] : obj;  // var or val or name
                    Pair? retval = null;
                    for (; obj != null; obj = (obj as Pair)?.cdr)
                    {
                        var current = (Pair)obj;
                        object? o = current.car;
                        Pair? next = current.cdr;
                        // Symbol bound in macro vars: handle non-variadic, spread, and repeat modes.
                        if (o is Symbol sym && vars.ContainsKey(sym))
                        {
                            if (!sym.ToString().Contains("..."))  // non-variadic: substitute value directly
                                retval = Pair.Append(retval, vars[sym]);
                            else if (!repeat)                      // variadic, spread mode: expand all values
                            {
                                if (vars[sym] != null)
                                    foreach (object x in (Pair)vars[sym]!)
                                        retval = Pair.Append(retval, x);
                            }
                            else                                   // variadic, repeat mode: advance one value
                            {
                                if (!temp.ContainsKey(sym)) temp[sym] = vars[sym];
                                retval = Pair.Append(retval, (temp[sym] as Pair)!.car);
                                more = more && temp[sym] != null && (temp[sym] as Pair)!.cdr != null;
                            }
                        }
                        else if (o is Symbol genSym && genSym.ToString()[0] == '?')
                            // Use the separate gensym cache so these don't accumulate in Symbol.syms.
                            retval = Pair.Append(retval, Symbol.CreateGensym(genSym.ToString() + _symbol));
                        else if (next?.car?.ToString() == "...")
                        { // (any) ... => repeat (any) until empty variable data - using car
                            more = true;
                            temp = [];
                            while (more)
                            {
                                retval = Pair.Append(retval, (object?)Transform(o!, true));
                                foreach (object xx in vars.Keys)
                                    if (temp.TryGetValue(xx, out var tv) && tv is Pair tp)
                                        temp[xx] = tp.cdr;
                            }
                            temp = [];
                            obj = next;
                        }
                        else if (o is not Pair)  // constant value
                            retval = Pair.Append(retval, o);
                        else                     // nested pair: recurse
                            retval = Pair.Append(retval, (object?)Transform(o!, repeat));
                    }
                    return retval;
                }

                public object? Expand(object? obj)
                {
                    if (obj is not Pair objPair) return obj;
                    if (objPair.car is Symbol && macros.ContainsKey(objPair.car))
                    {
                        var macroEntry = (Pair)macros[objPair.car]!;
                        foreach (object o in macroEntry.cdr!)
                        {
                            _symbol++;
                            vars = [];
                            cons = [];
                            cons[Symbol.Create("_")] = objPair.car;
                            if (macroEntry.car != null)
                                foreach (object x in (Pair)macroEntry.car!)
                                    if (x != null) cons[x] = x;
                            var clause = (Pair)o;
                            if (IsMatch(objPair, (Pair)clause.car!, false))
                            {
                                if (Expression.IsTraceOn(Symbol.Create("match")))
                                    Console.WriteLine("MATCH {0}: {1} ==> {2}",
                                        objPair.car, clause.car, clause.cdr!.car);
                                // Each recursive expansion gets its own fresh MacroExpander.
                                obj = Macro.Check(Transform(clause.cdr!.car, false));
                                break;
                            }
                        }
                    }
                    if (obj is not Pair resultPair) return obj;
                    Pair? retval = null, retvalTail = null;
                    foreach (object o in resultPair)
                    {
                        var node = new Pair(Macro.Check(o));
                        if (retvalTail == null) retval = retvalTail = node;
                        else { retvalTail.cdr = node; retvalTail = node; }
                    }
                    return retval;
                }
            }
        }
    }

    namespace Programs
    {
        // A compiled init.ss entry: either a macro definition (which updates Macro.macros)
        // or a compiled Expression to evaluate against a fresh environment.
        internal abstract class InitEntry { }
        internal sealed class InitMacro(Pair def)   : InitEntry { public readonly Pair Def = def; }
        internal sealed class InitExpr(Expression e) : InitEntry { public readonly Expression E = e; }

        public class Program
        {
            static public bool lastValue = true;
            static public bool Stats      = false;
            static public long Iterations = 0;
            // [ThreadStatic] ensures each thread (and thus each embedded Program instance
            // running on its own thread) has its own current-program pointer.  null on
            // threads that have never created a Program is the correct default.
            [ThreadStatic] static public Program? current;
            public Env initEnv;

            // Cached compiled forms from init.ss.  Populated on the first call to
            // LoadInit; subsequent calls replay the compiled list without re-parsing.
            private static List<InitEntry>? _initCache;
            private static string?          _initCachePath;    // path used to build the cache
            private static DateTime         _initCacheStamp;   // file mod-time of cached init.ss

            public Program()
            {
                current = this;
                this.initEnv = new Extended_Env(null!, null!, new Env(), false);
            }

            // Load (or replay) init.ss.  First call parses and compiles; subsequent calls
            // re-execute the already-compiled Expression list against the new environment.
            public void LoadInit(string path)
            {
                var stamp = File.GetLastWriteTimeUtc(path);
                if (_initCache != null && _initCachePath == path && _initCacheStamp == stamp)
                {
                    // Replay: restore macros and re-evaluate each compiled entry.
                    Macro.macros.Clear();
                    foreach (var entry in _initCache)
                    {
                        if (entry is InitMacro m) Macro.Add(m.Def);
                        else                      ((InitExpr)entry).E.Eval(initEnv);
                    }
                    return;
                }

                // First load (or init.ss changed): parse, compile, cache, and evaluate.
                var text  = File.ReadAllText(path);
                var cache = new List<InitEntry>();
                var exp   = text;
                while (true)
                {
                    var parsedObj = Util.Parse(exp, out var after);
                    if (parsedObj == null) break;
                    if (parsedObj is Pair rawMacro && rawMacro.car?.ToString() == "macro")
                    {
                        cache.Add(new InitMacro(rawMacro.cdr!));
                        Macro.Add(rawMacro.cdr!);
                    }
                    else if (parsedObj is Pair ds && ds.car?.ToString() == "define-syntax")
                    {
                        var md = Macro.TranslateDefineSyntax(ds);
                        if (md != null) { cache.Add(new InitMacro(md)); Macro.Add(md); }
                    }
                    else
                    {
                        parsedObj = Macro.Check(parsedObj);
                        // DEFINE shortcut preserved: wrap as a compiled expression
                        Expression compiled;
                        if (parsedObj is Pair defPair && defPair.car?.ToString() == "DEFINE")
                            compiled = new Define(defPair);
                        else
                            compiled = Expression.Parse(parsedObj!);
                        cache.Add(new InitExpr(compiled));
                        compiled.Eval(initEnv);
                    }
                    if (after == "") break;
                    exp = after;
                }
                _initCache      = cache;
                _initCachePath  = path;
                _initCacheStamp = stamp;
            }

            public object Eval(Expression exp)
            {
                return exp.Eval(initEnv);
            }
            // Evaluates one expression from the front of 'exp', sets 'after' to the remainder.
            public object EvalOne(string exp, out string after)
            {
                var parsedObj = Util.Parse(exp, out after);
                if (parsedObj is Pair rawMacro && rawMacro.car?.ToString() == "macro")
                {
                    Macro.Add(rawMacro.cdr!);
                    return rawMacro.cdr!.car!;
                }
                if (parsedObj is Pair ds && ds.car?.ToString() == "define-syntax")
                {
                    var md = Macro.TranslateDefineSyntax(ds);
                    if (md != null) Macro.Add(md);
                    return ds.cdr!.car!;
                }
                parsedObj = Macro.Check(parsedObj);
                if (Expression.IsTraceOn(Symbol.Create("macro")))
                    Console.WriteLine(Util.Dump("macro:   ", parsedObj!));
                if (parsedObj is Pair defPair && defPair.car?.ToString() == "DEFINE")
                    return new Define(defPair).Eval(initEnv);
                var sw = Stats ? Stopwatch.StartNew() : null;
                if (Stats) Iterations = 0;
                var answer = Eval(Expression.Parse(parsedObj!));
                if (answer is Pair answerPair && answerPair.car is Var v)
                {
                    answerPair.car = v.GetName();
                    answer = Eval(Expression.Parse(answerPair));
                }
                if (Stats && sw != null)
                {
                    sw.Stop();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"  time: {sw.Elapsed.TotalMilliseconds:F3} ms  iterations: {Iterations:N0}");
                    Console.ResetColor();
                }
                return answer;
            }
            public object Eval(string exp)
            {
                object answer = new Pair(null);
                while (true)
                {
                    var parsedObj = Util.Parse(exp, out var after);
                    if (parsedObj is Pair rawMacro && rawMacro.car?.ToString() == "macro")
                    {
                        Macro.Add(rawMacro.cdr!);
                        answer = rawMacro.cdr!.car!;
                        if (after == "") return answer;
                        exp = after; continue;
                    }
                    if (parsedObj is Pair ds && ds.car?.ToString() == "define-syntax")
                    {
                        var md = Macro.TranslateDefineSyntax(ds);
                        if (md != null) Macro.Add(md);
                        answer = ds.cdr!.car!;
                        if (after == "") return answer;
                        exp = after; continue;
                    }
                    parsedObj = Macro.Check(parsedObj);
                    if (Expression.IsTraceOn(Symbol.Create("macro")))
                        Console.WriteLine(Util.Dump("macro:   ", parsedObj!));
                    if (parsedObj is Pair defPair && defPair.car?.ToString() == "DEFINE")
                    {
                        answer = new Define(defPair).Eval(initEnv);
                        if (after == "") return answer;
                        exp = after; continue;
                    }
                    var sw = Stats ? Stopwatch.StartNew() : null;
                    if (Stats) Iterations = 0;
                    answer = Eval(Expression.Parse(parsedObj!));
                    if (answer is Pair answerPair && answerPair.car is Var v)
                    { // evaluate again if the first (car) is an unevaluated variable
                        answerPair.car = v.GetName();
                        answer = Eval(Expression.Parse(answerPair));
                    }
                    if (Stats && sw != null)
                    {
                        sw.Stop();
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"  time: {sw.Elapsed.TotalMilliseconds:F3} ms  iterations: {Iterations:N0}");
                        Console.ResetColor();
                    }
                    if (after != "" && !lastValue) Console.WriteLine(Util.Dump(answer));
                    if (after == "") return answer;
                    exp = after;
                }
            }
        }
    }
    namespace Environment
    {
        // Symbols are interned singletons — reference equality is both correct and faster
        // than the default string-based GetHashCode/Equals on Symbol.
        sealed class SymbolRefComparer : IEqualityComparer<Symbol>
        {
            public static readonly SymbolRefComparer Instance = new();
            public bool Equals(Symbol? x, Symbol? y) => ReferenceEquals(x, y);
            public int  GetHashCode(Symbol s)        => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s);
        }

        public class Env
        {
            // Use reference-equality comparer: since symbols are interned, this avoids
            // string hashing on every variable lookup.
            public Dictionary<Symbol, object> table =
                new(SymbolRefComparer.Instance);
            public Env Extend(Pair? syms, Pair? vals, int capacity = 0)
            {
                if (Pair.IsNull(syms))
                {
                    // Even with no params we must create a child scope so that
                    // internal 'define' writes to a fresh env instead of
                    // mutating the caller's scope (required by letrec, named let, etc.).
                    var child = new Extended_Env(null, null, this, false, 0);
                    return child;
                }
                return new Extended_Env(syms, vals, this, true, capacity);
            }
            public virtual object Bind(Symbol id, object val) => throw new Exception($"Unbound variable {id}");
            public virtual object Apply(Symbol id)            => throw new Exception($"Unbound variable {id}");
        }

        public class Extended_Env : Env
        {
            Env env;
            public override string ToString() => Util.Dump("env", table, env);
            public Extended_Env(Pair? inSyms, Pair? inVals, Env inEnv, bool eval, int capacity = 0)
            {
                env = inEnv;
                // Pre-size the dictionary to the known param count so the internal array
                // is allocated once rather than doubling 0 → 2 → 4 → ... on each Add.
                if (capacity > 0)
                    table = new Dictionary<Symbol, object>(capacity, SymbolRefComparer.Instance);
                for (; inSyms != null; inSyms = inSyms.cdr)
                {
                    var currSym = inSyms.car as Symbol;
                    if (Symbol.IsEqual(".", currSym)) // R5RS 4.1.4 rest args
                    {
                        table.Add(inSyms.cdr!.car as Symbol ?? throw new Exception("bad . syntax"), inVals!);
                        break;
                    }
                    table.Add(currSym!, inVals!.car!);
                    inVals = inVals.cdr;
                }
            }
            public override object Bind(Symbol id, object val)
            {
                if (!table.ContainsKey(id)) return env.Bind(id, val);
                table[id] = val;
                return id;
            }
            public override object Apply(Symbol id) =>
                table.TryGetValue(id, out var v) ? v : env.Apply(id);
        }
    }

    namespace Expressions
    {
        // Represents a deferred tail call. Returned by Closure.Eval / App.EvalTail when the
        // last expression in a body is itself a function application. The trampoline in
        // App.Eval unwraps these in a loop instead of recursing, giving O(1) stack TCO.
        public sealed class TailCall(Closure closure, Pair? args)
        {
            public readonly Closure Closure = closure;
            public readonly Pair?   Args    = args;
        }

        public abstract class Expression
        {
            static public bool Trace = false;  // use (trace on) or (trace off)
            public static HashSet<Symbol> traceHash = [];
            private static readonly Symbol _sAll = Symbol.Create("_all_");
            public static bool IsTraceOn(Symbol s) =>
                Trace && (traceHash.Contains(s) || traceHash.Contains(_sAll));
            public abstract object Eval(Env env);
            // Override in subclasses that can be in tail position to avoid stack growth.
            public virtual  object EvalTail(Env env) => Eval(env);
            static public Pair? Eval_Rands(Pair? rands, Env env)
            {
                if (rands == null) return null;
                // Maintain a tail pointer so each append is O(1) rather than O(n),
                // making the full evaluation O(n) instead of O(n²).
                Pair? head = null, tail = null;
                foreach (object obj in rands)
                {
                    var o = ((Expression)obj).Eval(env);
                    if (obj is CommaAt && o is Pair spliced)
                    {
                        foreach (object oo in spliced)
                        {
                            var node = new Pair(oo);
                            if (tail == null) head = tail = node;
                            else { tail.cdr = node; tail = node; }
                        }
                    }
                    else
                    {
                        var node = new Pair(o);
                        if (tail == null) head = tail = node;
                        else { tail.cdr = node; tail = node; }
                    }
                }
                return head;
            }
            static public Expression Parse(object? a)
            {
                if (a is Symbol sym) return new Var(sym);
                if (a is not Pair pair) return new Lit(a);
                Pair? args = pair.cdr;
                Pair? body = null;
                switch (pair.car?.ToString())
                {
                    case "IF":     // (if test then else)
                        return new If(Parse(args!.car), Parse(args.cdr!.car), Parse(args.cdr!.cdr!.car));
                    case "DEFINE": // (define name <body>)
                        return new Define(pair);
                    case "EVAL":   // (eval ((if #f '* '+) 2 3))
                        return new Evaluate(Parse(args!.car));
                    case "LAMBDA": // (lambda () body), (lambda (x ...) body) 
                        var rawBodyArgs = args!.cdr;
                        {
                            Pair? bodyTail = null;
                            foreach (object obj in args!.cdr!)
                            {
                                var node = new Pair(Parse(obj));
                                if (bodyTail == null) body = bodyTail = node;
                                else { bodyTail.cdr = node; bodyTail = node; }
                            }
                        }
                        return new Lambda(args.car as Pair, body, rawBodyArgs);
                    case "quote":  // (quote <body>) or '<body>
                        return new Lit(args!.car);
                    case "set!":
                        return new Assignment(args!.car as Symbol ?? throw new Exception("set! requires a symbol"), Parse(args.cdr!.car));
                    case "TRY":    // (try exp1 catch-exp)
                        return new Try(Parse(args!.car), Parse(args.cdr!.car));
                    default:
                        if (args != null)
                        {
                            Pair? bodyTail = null;
                            foreach (object obj in args)
                            {
                                var node = new Pair(Parse(obj));
                                if (bodyTail == null) body = bodyTail = node;
                                else { bodyTail.cdr = node; bodyTail = node; }
                            }
                        }
                        if (pair.car?.ToString() == ",@") return new CommaAt(body);
                        if (Prim.list.TryGetValue(pair.car!.ToString()!, out var prim))
                            return new Prim(prim, body);
                        return new App(Parse(pair.car), body);
                }
            }
        }

        public class Lit(object? datum) : Expression
        {
            public override object Eval(Env env) => datum is Pair p ? Comma(p, env)! : datum!;
            public Pair? Comma(Pair o, Env env)
            {
                Pair? retVal = null;
                foreach (object car in o)
                    if (car is not Pair cp)
                        retVal = Pair.Append(retVal, car);
                    else if (Symbol.IsEqual(",", cp.car))
                        retVal = Pair.Append(retVal, Parse(cp.cdr!.car).Eval(env));
                    else if (Symbol.IsEqual(",@", cp.car))
                    {
                        var ev = Parse(cp.cdr!.car).Eval(env);
                        if (ev is Pair evPair) // ,@( ... )
                            foreach (object oo in evPair)
                                retVal = Pair.Append(retVal, oo);
                        else
                            retVal = ev == null ? retVal : Pair.Append(retVal, ev);
                    }
                    else
                        retVal = Pair.Append(retVal, Comma(cp, env));
                return retVal;
            }
            public override string ToString()  => Util.Dump("lit", datum);
            public string        GetName()      => datum!.ToString()!;
        }

        public class Evaluate(Expression datum) : Expression
        {
            public override object Eval(Env env) => datum.Eval(env) switch
            {
                null     => null!,
                string s => Parse(Program.current!.Eval(s)).Eval(env),
                var o    => Parse(o).Eval(env),
            };
            public override string ToString() => Util.Dump("EVAL", datum);
        }

        public class Var(Symbol id) : Expression
        {
            public readonly Symbol id = id;
            public override object Eval(Env env) => env.Apply(id);
            public string GetName()              => id.ToString();
            public override string ToString()    => Util.Dump("var", id);
        }

        public class Lambda(Pair? ids, Pair? body, Pair? rawBody = null) : Expression
        {
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
            public override object Eval(Env env)
            {
                // Use the Symbol object directly from the Pair rather than round-tripping
                // through Symbol.Create(name-string).  This preserves object identity for
                // gensym symbols produced by macro expansion (which live in the separate
                // gensymTable and would otherwise produce a different Symbol object).
                var sym = datum.cdr!.car is Symbol s ? s : Symbol.Create(datum.cdr!.car!.ToString()!);
                env.table[sym] = Parse(datum.cdr!.cdr!.car).Eval(env);
                return sym;
            }
            public override string ToString() => Util.Dump("DEFINE", datum);
        }

        public delegate object Primitive(Pair args);

        public class Prim(Primitive prim, Pair? rands) : Expression
        {
            public override object Eval(Env env)    => prim(Eval_Rands(rands, env)!);
            public override string ToString()       => Util.Dump("prim", prim, rands);

            public static readonly Dictionary<string, Primitive> list = new()
            {
                ["LESSTHAN"]    = LessThan_prim,
                ["new"]         = New_Prim,
                ["get"]         = Get_Prim,
                ["set"]         = Set_Prim,
                ["call"]        = Call_Prim,
                ["call-static"] = Call_Static_Prim,
                ["env"]         = Env_Prim,
                // Built-in list primitives (bypass closure overhead)
                ["car"]         = Car_Prim,
                ["cdr"]         = Cdr_Prim,
                ["null?"]       = NullQ_Prim,
                ["pair?"]       = PairQ_Prim,
                ["cons"]        = Cons_Prim,
                ["not"]         = Not_Prim,
                // Built-in arithmetic primitives (bypass CALLNATIVE + reflection)
                ["+"]           = Add_Prim,
                ["-"]           = Sub_Prim,
                ["*"]           = Mul_Prim,
                ["/"]           = Div_Prim,
                // Built-in comparison primitives (bypass COMPARE-ALL chain)
                ["<"]           = Lt_Prim,
                [">"]           = Gt_Prim,
                ["<="]          = Le_Prim,
                [">="]          = Ge_Prim,
                ["zero?"]       = ZeroQ_Prim,
                ["number?"]     = NumberQ_Prim,
                ["eqv?"]        = EqvQ_Prim,
                ["todouble"]    = ToDouble_Prim,
                ["tointeger"]   = ToInt_Prim,
                // = and equal? bypass the COMPARE-ALL/closure chain entirely
                ["="]           = Eq_Prim,
                ["equal?"]      = EqualQ_Prim,
            };
            public static object New_Prim(Pair args)
            {
                var type = Util.GetType(args.car!.ToString()!)
                    ?? throw new Exception($"Unknown type: {args.car}");
                if (Pair.IsNull(args.cdr))
                    return Activator.CreateInstance(type)!;
                // Coerce Symbol arguments to string so e.g. (new 'StreamReader 'file.ss) works
                var ctorArgs = args.cdr!.ToArray()
                    .Select(a => a is Symbol ? (object)a.ToString()! : a)
                    .ToArray();
                return Activator.CreateInstance(type, ctorArgs)!;
            }
            public static object LessThan_prim(Pair args) =>
                Arithmetic.LessThan(args.car!, args.cdr!.car!);
            public static object Env_Prim(Pair? args)
            {
                var globalEnv = Program.current!.initEnv;
                string? filter = Pair.IsNull(args) ? null : args?.car?.ToString();
                foreach (var kv in globalEnv.table.OrderBy(k => k.Key.ToString()))
                {
                    if (filter != null && kv.Key.ToString() != filter) continue;
                    if (kv.Value is Closure closure)
                    {
                        var sb = new System.Text.StringBuilder("(define (");
                        sb.Append(kv.Key);
                        if (closure.ids != null)
                            foreach (object p in closure.ids)
                            { sb.Append(' '); sb.Append(p); }
                        sb.Append(')');
                        if (closure.rawBody != null)
                            foreach (object b in closure.rawBody)
                            { sb.Append(' '); sb.Append(Util.Dump(b)); }
                        sb.Append(')');
                        Console.WriteLine(sb);
                    }
                }
                return new Pair(null);
            }
            public static object Call_Prim(Pair args)        => Util.CallMethod(args, false);
            public static object Call_Static_Prim(Pair args)  => Util.CallMethod(args, true);
            public static object Get_Prim(Pair args)
            {
                return SetGet(args, BindingFlags.GetField | BindingFlags.GetProperty);
            }
            public static object Set_Prim(Pair args)
            {
                return SetGet(args, BindingFlags.SetField | BindingFlags.SetProperty);
            }
            public static object SetGet(Pair arg, BindingFlags flags)
            {
                object[] index = arg.cdr?.cdr != null ? arg.cdr.cdr.ToArray() : [];
                Type? t = arg.car is Symbol ? Util.GetType(arg.car!.ToString()!) : arg.car!.GetType();
                if (t == null)
                    throw new Exception("Unknown type: " + arg.car);
                BindingFlags f = BindingFlags.Default | flags;
                string memberName = arg.cdr!.car!.ToString()!;
                try
                {
                    return t.InvokeMember(memberName, f, null, arg.car, index)!;
                }
                catch (MissingMethodException)
                {
                    // Retry with numeric args coerced to Int32 (e.g. ArrayList indexer requires Int32,
                    // but arithmetic may yield Double). Only coerce whole-number doubles.
                    object[] coerced = (object[])index.Clone();
                    bool changed = false;
                    for (int ci = 0; ci < coerced.Length; ci++)
                    {
                        if (coerced[ci] is double dv && dv == Math.Floor(dv)) { coerced[ci] = (int)dv; changed = true; }
                    }
                    if (changed)
                        return t.InvokeMember(memberName, f, null, arg.car, coerced)!;
                    throw;
                }
            }

            // ── Built-in list primitives ─────────────────────────────────────────────
            public static object Car_Prim(Pair args) =>
                args?.car is Pair p && !Pair.IsNull(p) ? p.car! :
                throw new LispException($"car: not a pair: {Util.Dump(args?.car)}");

            public static object Cdr_Prim(Pair args)
            {
                if (args?.car is Pair p && !Pair.IsNull(p)) return p.cdr!;
                throw new LispException($"cdr: not a pair: {Util.Dump(args?.car)}");
            }

            public static object NullQ_Prim (Pair args) => Pair.IsNull(args?.car);
            public static object PairQ_Prim (Pair args) => args?.car is Pair p2 && !Pair.IsNull(p2);
            public static object Cons_Prim  (Pair args) => Pair.Cons(args!.car!, args.cdr!.car!);
            public static object Not_Prim   (Pair args) => args?.car is bool b && !b;

            // ── Built-in arithmetic primitives ───────────────────────────────────────
            public static object Add_Prim(Pair args)
            {
                if (args == null) return 0;
                var acc = args.car!;
                for (var p = args.cdr; p != null; p = p.cdr)
                    acc = Arithmetic.AddObj(acc, p.car!);
                return acc;
            }

            public static object Sub_Prim(Pair args)
            {
                if (args == null) return 0;
                if (args.cdr == null) return Arithmetic.NegObj(args.car!);
                var acc = args.car!;
                for (var p = args.cdr; p != null; p = p.cdr)
                    acc = Arithmetic.SubObj(acc, p.car!);
                return acc;
            }

            public static object Mul_Prim(Pair args)
            {
                if (args == null) return 1;
                var acc = args.car!;
                for (var p = args.cdr; p != null; p = p.cdr)
                    acc = Arithmetic.MulObj(acc, p.car!);
                return acc;
            }

            public static object Div_Prim(Pair args)
            {
                if (args == null) return 1;
                if (args.cdr == null) return Arithmetic.DivObj(1, args.car!);
                var acc = args.car!;
                for (var p = args.cdr; p != null; p = p.cdr)
                    acc = Arithmetic.DivObj(acc, p.car!);
                return acc;
            }

            // ── Built-in comparison primitives ───────────────────────────────────────
            public static object Lt_Prim(Pair args)
            {
                if (args?.cdr == null) return true;
                for (var p = args; p?.cdr != null; p = p.cdr)
                    if (!Arithmetic.LessThan(p.car!, p.cdr!.car!)) return false;
                return true;
            }

            public static object Gt_Prim(Pair args)
            {
                if (args?.cdr == null) return true;
                for (var p = args; p?.cdr != null; p = p.cdr)
                    if (!Arithmetic.LessThan(p.cdr!.car!, p.car!)) return false;
                return true;
            }

            public static object Le_Prim(Pair args)
            {
                if (args?.cdr == null) return true;
                for (var p = args; p?.cdr != null; p = p.cdr)
                    if (Arithmetic.LessThan(p.cdr!.car!, p.car!)) return false;  // b<a means !(a<=b)
                return true;
            }

            public static object Ge_Prim(Pair args)
            {
                if (args?.cdr == null) return true;
                for (var p = args; p?.cdr != null; p = p.cdr)
                    if (Arithmetic.LessThan(p.car!, p.cdr!.car!)) return false;  // a<b means !(a>=b)
                return true;
            }

            public static object ZeroQ_Prim  (Pair args) => args?.car switch {
                int i => i == 0, double d => d == 0.0, BigInteger bi => bi.IsZero, _ => (object)false };
            public static object NumberQ_Prim(Pair args) => args?.car is int or double or BigInteger;

            // ── Frequently called conversion / equality helpers ───────────────────────
            // eqv?    = (call x 'Equals y)   — avoids closure creation + reflection
            // todouble/tointeger — avoids closure + call-static reflection
            public static object EqvQ_Prim    (Pair args) => object.Equals(args?.car, args?.cdr?.car);
            public static object ToDouble_Prim(Pair args) => args?.car is BigInteger bi ? (object)(double)bi : Convert.ToDouble(args?.car ?? 0.0);
            public static object ToInt_Prim   (Pair args) => args?.car is BigInteger bi ? (object)(int)bi : Convert.ToInt32(args?.car ?? 0);

            // ── = and equal? ─────────────────────────────────────────────────────────
            // Replaces the init.ss variadic COMPARE-ALL + map + reverse chain for =.
            // Handles n-ary usage: (= a b c) means a==b && b==c.
            public static object Eq_Prim(Pair? args)
            {
                if (args?.cdr == null) return true;
                for (var p = args; p?.cdr != null; p = p.cdr)
                    if (!EqCS(p.car!, p.cdr!.car!)) return false;
                return true;
            }

            // Full structural equal? in C#: replaces the cond-heavy Lisp closure.
            public static object EqualQ_Prim(Pair? args)
            {
                if (args?.cdr == null) return true;
                for (var p = args; p?.cdr != null; p = p.cdr)
                    if (!EqualCS(p.car, p.cdr!.car)) return false;
                return true;
            }

            // Numeric equality fast path (= semantics): double-compare numbers,
            // try Convert.ToDouble for other types (e.g. Type objects fail → Equals).
            private static bool EqCS(object a, object b)
            {
                if (ReferenceEquals(a, b)) return true;    // covers null==null, same symbol
                if (a is int    ia && b is int    ib) return ia == ib;  // hottest path
                if (a is BigInteger || b is BigInteger)
                    return Arithmetic.IsNumericEqual(a, b);
                if (a is int or double &&
                    b is int or double)
                    return Convert.ToDouble(a) == Convert.ToDouble(b);
                try   { return Convert.ToDouble(a) == Convert.ToDouble(b); }
                catch { return object.Equals(a, b); }   // non-numeric: fall back to Equals
            }

            // Structural equality (equal? semantics): recurses into pairs and vectors.
            private static bool EqualCS(object? a, object? b)
            {
                if (ReferenceEquals(a, b)) return true;
                if (Pair.IsNull(a) && Pair.IsNull(b)) return true;
                if (Pair.IsNull(a) || Pair.IsNull(b)) return false;
                if (a is BigInteger || b is BigInteger)
                    return (a is int or double or BigInteger) &&
                           (b is int or double or BigInteger) &&
                           Arithmetic.IsNumericEqual(a!, b!);
                if (a is int or double)
                    return b is int or double &&
                           Convert.ToDouble(a) == Convert.ToDouble(b);
                if (a is Pair pa && b is Pair pb)
                {
                    if (Pair.IsNull(pa) != Pair.IsNull(pb)) return false;
                    return EqualCS(pa.car, pb.car) &&
                           EqualCS(pa.cdr, pb.cdr);
                }
                if (a is ArrayList al &&
                    b is ArrayList bl)
                {
                    if (al.Count != bl.Count) return false;
                    for (int i = 0; i < al.Count; i++)
                        if (!EqualCS(al[i], bl[i])) return false;
                    return true;
                }
                return object.Equals(a, b);
            }
        }

        public class If(Expression test, Expression tX, Expression eX) : Expression
        {
            // Evaluate the test, returning false only when the result is exactly (bool)false.
            // Non-bool values (e.g. numbers, pairs) are treated as truthy, matching the
            // original try/(bool)cast/catch behaviour without the exception overhead.
            private bool EvalTest(Env env)
            {
                try
                {
                    var v = test.Eval(env);
                    return v is not bool b || b;
                }
                catch (LispException) { throw; }
                catch { return true; }  // non-bool / error → truthy
            }
            public override object Eval(Env env)
            {
                var res = EvalTest(env);
                return res ? tX.Eval(env) : eX.Eval(env);
            }
            // In tail position, propagate tail context to whichever branch is taken.
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
                try   { return tryX.Eval(env); }
                catch { return catchX.Eval(env); }
            }
            public override string ToString() => Util.Dump("TRY", tryX, catchX);
        }

        public class Assignment(Symbol id, Expression val) : Expression
        {
            public override object Eval(Env env) => env.Bind(id, val.Eval(env));
            public override string ToString()    => Util.Dump("set!", id, val);
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
            public static bool CarryOn = false;

            // Drives TCO: loop while values are TailCall, then return the real result.
            private static object Trampoline(object result)
            {
                while (result is TailCall tc)
                    result = tc.Closure.Eval(tc.Args);
                return result;
            }

            // Applies a closure in either tail or non-tail position.
            // In tail position we return a TailCall token; the trampoline above drives it.
            private static object Dispatch(Closure closure, Pair? args, bool tail) =>
                tail ? (object)new TailCall(closure, args) : Trampoline(closure.Eval(args!));

            public override object     Eval(Env env) => EvalImpl(env, tail: false);
            public override object EvalTail(Env env) => EvalImpl(env, tail: true);

            private object EvalImpl(Env env, bool tail)
            {
                if (rator is Var traced && IsTraceOn(traced.id))
                    Console.WriteLine(Util.Dump("call: ", traced.id, rands));
                var proc = rator.Eval(env);
                return proc switch
                {
                    Closure closure          => EvalClosure(closure, env, tail),
                    Var pv                   => tail ? Parse(new Pair(pv.GetName(), rands)).EvalTail(env)  // allow ((if #f + *) 2 3) ==> 6
                                                     : Parse(new Pair(pv.GetName(), rands)).Eval(env),
                    Pair { car: Closure pc } => Dispatch(pc, Eval_Rands(rands, env), tail),
                    _                        => throw new Exception($"invalid operator {proc?.GetType()} {proc}"),
                };
            }

            private object EvalClosure(Closure closure, Env env, bool tail)
            {
                var evaledArgs = Eval_Rands(rands, env);
                if (CarryOn && rands != null && closure.ids != null)
                {
                    // Advance past the closure's required params without calling .Count
                    // (which would traverse the full list on every iteration = O(n²)).
                    var rem = rands;
                    for (int i = 0; i < closure.arity; i++) rem = rem?.cdr;
                    if (rem != null) // more args supplied than params → curried application
                    {
                        var inner = (Closure)Trampoline(closure.Eval(evaledArgs!));
                        return Dispatch(inner, Eval_Rands(rem, env)!, tail);
                    }
                }
                return Dispatch(closure, evaledArgs, tail);
            }

            public override string ToString() => Util.Dump("app", rator, rands);
        }
    }

    public static class Arithmetic
    {
        static double D(object a) => a is BigInteger bi ? (double)bi : Convert.ToDouble(a);
        static int    I(object a) => a is BigInteger bi ? (int)bi    : Convert.ToInt32(a);
        static BigInteger BI(object a) => a switch
        {
            BigInteger bi => bi,
            int i         => i,
            double d      => (BigInteger)d,
            _             => (BigInteger)Convert.ToInt64(a),
        };

        // Normalize: if a BigInteger fits in int, demote it back to int to keep
        // the common case fast and to avoid pushing everything into BigInteger.
        static object Normalize(BigInteger v) =>
            v >= int.MinValue && v <= int.MaxValue ? (object)(int)v : (object)v;

        // Checked int arithmetic: on overflow, promote to BigInteger.
        public static object AddObj(object a, object b)
        {
            if (a is int ia && b is int ib)
            {
                try { return checked(ia + ib); }
                catch (OverflowException) { return Normalize((BigInteger)ia + ib); }
            }
            if (a is BigInteger || b is BigInteger)
                return Normalize(BI(a) + BI(b));
            return (object)(D(a) + D(b));
        }

        public static object SubObj(object a, object b)
        {
            if (a is int ia && b is int ib)
            {
                try { return checked(ia - ib); }
                catch (OverflowException) { return Normalize((BigInteger)ia - ib); }
            }
            if (a is BigInteger || b is BigInteger)
                return Normalize(BI(a) - BI(b));
            return (object)(D(a) - D(b));
        }

        public static object MulObj(object a, object b)
        {
            if (a is int ia && b is int ib)
            {
                try { return checked(ia * ib); }
                catch (OverflowException) { return Normalize((BigInteger)ia * ib); }
            }
            if (a is BigInteger || b is BigInteger)
                return Normalize(BI(a) * BI(b));
            return (object)(D(a) * D(b));
        }

        public static object DivObj(object a, object b)
        {
            if (a is double || b is double) return D(a) / D(b);
            if (a is BigInteger || b is BigInteger)
            {
                var ba = BI(a); var bb = BI(b);
                return BigInteger.Remainder(ba, bb).IsZero
                    ? Normalize(ba / bb)
                    : (object)(D(a) / D(b));
            }
            int ia = I(a), ib = I(b);
            return ia % ib == 0 ? (object)(ia / ib) : (object)(D(a) / D(b));
        }

        public static object NegObj(object a) => a switch
        {
            double d      => (object)(-d),
            int i         => i == int.MinValue ? (object)(-(BigInteger)i) : (object)(-i),
            BigInteger bi => Normalize(-bi),
            _             => (object)(-I(a)),
        };

        public static object IDivObj(object a, object b)
        {
            if (a is BigInteger || b is BigInteger)
                return Normalize(BI(a) / BI(b));
            return I(a) / I(b);
        }

        public static object ModObj(object a, object b)
        {
            if (a is BigInteger || b is BigInteger)
                return Normalize(BI(a) % BI(b));
            return I(a) % I(b);
        }

        public static object PowObj(object a, object b)
        {
            // If both are exact integers and the exponent is non-negative, keep exact.
            if (b is int ib2 && ib2 >= 0 && (a is int || a is BigInteger))
                return Normalize(BigInteger.Pow(BI(a), ib2));
            return Math.Pow(D(a), D(b));
        }

        public static bool LessThan(object a, object b)
        {
            if (a is int ia && b is int ib) return ia < ib;
            if (a is BigInteger || b is BigInteger) return BI(a) < BI(b);
            return D(a) < D(b);
        }

        // Numeric equality across int/BigInteger/double.
        public static bool IsNumericEqual(object a, object b)
        {
            // Both exact (int or BigInteger): compare as BigInteger
            if ((a is int || a is BigInteger) && (b is int || b is BigInteger))
                return BI(a) == BI(b);
            // At least one inexact: compare as double
            return D(a) == D(b);
        }

        public static object XorObj   (object a, object b) => I(a) ^ I(b);
        public static object BitAndObj(object a, object b) => I(a) & I(b);
        public static object BitOrObj (object a, object b) => I(a) | I(b);
        public static object BitXorObj(object a, object b) => I(a) ^ I(b);
    }

    public class Interpreter
    {
        public static bool EndProgram = false;
        [STAThread]
        static void Main(string[] args)
        {
            var ver = Assembly.GetEntryAssembly()?.GetName().Version;
            Console.WriteLine($"*** Lisp ver {ver} - Copyright (c) 2003 by Ilias H. Mavreas ***\n");
            var prog = new Program();
            var initPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "init.ss");
            if (File.Exists(initPath))
            {
                try
                {
                    Console.Write("Initializing: loading 'init.ss'...");
                    prog.LoadInit(initPath);
                }
                catch (Exception e) { Console.WriteLine($"\nerror loading 'init.ss': {e.Message}"); }
            }
            else
            {
                Console.WriteLine($"Warning: 'init.ss' not found at {initPath}");
            }
            // If filenames were passed on the command line, load each one and exit
            if (args.Length > 0)
            {
                foreach (var file in args)
                {
                    if (!File.Exists(file))
                    {
                        Console.WriteLine($"error: file not found: {file}");
                        continue;
                    }
                    try
                    {
                        Console.WriteLine($"Loading '{file}'...");
                        using var reader = File.OpenText(file);
                        prog.Eval(reader.ReadToEnd());
                    }
                    catch (Exception e) { Console.WriteLine($"error in '{file}': {e.Message}"); }
                }
                return;
            }

            // Interactive REPL
            static void ColorWriteLine(string text, ConsoleColor color)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
                Console.ResetColor();
            }
            // Returns the net paren depth of a string, skipping string literals and ; comments.
            static int ParenDepth(string s)
            {
                int depth = 0;
                bool inStr = false;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (inStr) { if (c == '\\') i++; else if (c == '"') inStr = false; continue; }
                    if (c == ';') { while (i + 1 < s.Length && s[i + 1] != '\n') i++; continue; }
                    if (c == '"') { inStr = true; continue; }
                    if (c == '(') depth++;
                    else if (c == ')') depth--;
                }
                return depth;
            }

            while (!EndProgram)
                try
                {
                    Console.Write("lisp> ");
                    string? line = Console.ReadLine();
                    if (line == null) break;                    // EOF / Ctrl+Z
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var val = new StringBuilder();
                    val.Append(line); val.Append('\n');
                    while (ParenDepth(val.ToString()) > 0)
                    {
                        Console.Write("...    ");
                        line = Console.ReadLine();
                        if (line == null) break;
                        val.Append(line); val.Append('\n');
                    }
                    var input = val.ToString();
                    while (input.Trim().Length > 0)
                    {
                        var result = prog.EvalOne(input, out input);
                        if (result != null) ColorWriteLine($"{Util.Dump(result)}\n", ConsoleColor.Yellow);
                    }
                }
                catch (Exception e) { Console.WriteLine($"error: {e.Message}"); }
        }
    }
}