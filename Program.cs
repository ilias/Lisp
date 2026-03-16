using System;
using System.Collections;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Lisp;

public static class Util
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
        var types = objs != null ? GetTypes(objs) : Type.EmptyTypes;
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
        catch (TargetInvocationException tie)
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
    public static void Throw(string message) => throw new LispException(message);
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
    public static string Dump(string title, params object?[] args)
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

    private static string FormatComplex(Complex z)
    {
        if (z.Imaginary == 0.0) return FormatDouble(z.Real);
        var re  = z.Real == 0.0 ? "" : FormatDouble(z.Real);
        var im  = z.Imaginary;
        string imStr = im ==  1.0 ? "+i"
                     : im == -1.0 ? "-i"
                     : im > 0.0   ? "+" + FormatDouble(im) + "i"
                                  :       FormatDouble(im) + "i";
        return re + imStr;
    }

    public static string Dump(object? exp)
    {
        return exp switch
        {
            _ when Pair.IsNull(exp) => "()",
            string s                => $"\"{s}\"",
            bool b                  => b ? "#t" : "#f",
            char c                  => $"#\\{c}",
            double d                => FormatDouble(d),
            BigInteger bi           => bi.ToString(),
            Rational r              => r.ToString(),
            Complex z               => FormatComplex(z),
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

    // After parsing a real number, checks for +mi / -mi / i complex suffixes.
    // Returns null if no suffix found (pos unchanged on null return).
    private static object? ParseComplexSuffix(string str, ref int pos, double realVal)
    {
        // Pure imaginary: trailing 'i' with stop char (or end) after it
        if (pos < str.Length && str[pos] == 'i' &&
            (pos + 1 >= str.Length || IsSymbolStopChar(str[pos + 1])))
        {
            pos++;
            return realVal == 0.0 ? (object)0.0 : new Complex(0.0, realVal);
        }
        // Complex: realVal [+/-] digits ['.' digits] ['e' [+/-] digits] 'i'
        if (pos < str.Length && (str[pos] == '+' || str[pos] == '-'))
        {
            int  sepPos  = pos;
            bool imagNeg = str[pos] == '-';
            pos++;
            int  imagStart  = pos;
            bool imagHasDot = false, imagHasExp = false;
            while (pos < str.Length && (char.IsAsciiDigit(str[pos]) || (!imagHasDot && str[pos] == '.')))
            {
                if (str[pos] == '.') imagHasDot = true;
                pos++;
            }
            if (pos < str.Length && (str[pos] == 'e' || str[pos] == 'E'))
            {
                int eS = pos; pos++;
                if (pos < str.Length && (str[pos] == '+' || str[pos] == '-')) pos++;
                if (pos < str.Length && char.IsAsciiDigit(str[pos]))
                { while (pos < str.Length && char.IsAsciiDigit(str[pos])) pos++; imagHasExp = true; }
                else pos = eS;
            }
            if (pos < str.Length && str[pos] == 'i' &&
                (pos + 1 >= str.Length || IsSymbolStopChar(str[pos + 1])))
            {
                pos++; // consume 'i'
                double imag;
                if (pos - 1 == imagStart) // no digits between sign and 'i': +i or -i
                    imag = imagNeg ? -1.0 : 1.0;
                else
                {
                    int len = (pos - 1) - imagStart;
                    double mag = (imagHasDot || imagHasExp)
                        ? double.Parse(str.AsSpan(imagStart, len), NumberStyles.Float, CultureInfo.InvariantCulture)
                        : (double)BigInteger.Parse(str.AsSpan(imagStart, len), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    imag = imagNeg ? -mag : mag;
                }
                return imag == 0.0 ? (object)realVal : new Complex(realVal, imag);
            }
            pos = sepPos; // not a complex suffix, back up
        }
        return null;
    }

    // Tries to parse a symbol token as a pure-imaginary complex literal: +i, -i, +Ni, -Ni.
    private static object? TryParseImaginary(string tok)
    {
        if (tok.Length < 2 || tok[^1] != 'i') return null;
        char first = tok[0];
        if (first != '+' && first != '-') return null;
        bool neg     = first == '-';
        var  numPart = tok.AsSpan(1, tok.Length - 2); // strip sign and trailing 'i'
        if (numPart.Length == 0) return new Complex(0.0, neg ? -1.0 : 1.0);
        if (double.TryParse(numPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            return new Complex(0.0, neg ? -d : d);
        if (BigInteger.TryParse(numPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger bi))
            return new Complex(0.0, neg ? -(double)bi : (double)bi);
        return null;
    }

    // Public API: parse one S-expression from str; set after to the unparsed remainder.
    // Internally delegates to the index-based core to avoid O(n) string slicing per token.
    public static object? Parse(string str, out string after)
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
            bool hasExp = false;
            while (pos < str.Length && (char.IsAsciiDigit(str[pos]) || (!hasDot && str[pos] == '.')))
            {
                if (str[pos] == '.') hasDot = true;
                pos++;
            }
            // Consume optional scientific-notation exponent: e+300, e-5, e10, E300, etc.
            if (pos < str.Length && (str[pos] == 'e' || str[pos] == 'E'))
            {
                int expStart = pos;
                pos++; // skip 'e'/'E'
                if (pos < str.Length && (str[pos] == '+' || str[pos] == '-')) pos++;
                if (pos < str.Length && char.IsAsciiDigit(str[pos]))
                {
                    while (pos < str.Length && char.IsAsciiDigit(str[pos])) pos++;
                    hasExp = true;
                }
                else
                {
                    pos = expStart; // not a valid exponent, back up
                }
            }
            var span = str.AsSpan(start, pos - start);
            if (hasDot || hasExp)
            {
                var rv = double.Parse(span, NumberStyles.Float, CultureInfo.InvariantCulture);
                return ParseComplexSuffix(str, ref pos, rv) ?? (object)rv;
            }
            // Rational literal: n/d  (e.g. 3/4, -1/2)
            if (pos < str.Length && str[pos] == '/' &&
                pos + 1 < str.Length && char.IsAsciiDigit(str[pos + 1]))
            {
                var numer = BigInteger.Parse(span, NumberStyles.Integer, CultureInfo.InvariantCulture);
                pos++; // skip '/'
                int dStart = pos;
                while (pos < str.Length && char.IsAsciiDigit(str[pos])) pos++;
                var denom = BigInteger.Parse(str.AsSpan(dStart, pos - dStart),
                                             NumberStyles.Integer, CultureInfo.InvariantCulture);
                if (denom.IsZero) throw new LispException("division by zero in rational literal");
                return new Rational(numer, denom).Normalize();
            }
            // Integer: may have a complex suffix (ni, n+mi, n-mi)
            object numVal;
            if (int.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv))
                numVal = iv;
            else
                numVal = BigInteger.Parse(span, NumberStyles.Integer, CultureInfo.InvariantCulture);
            {
                double asD = numVal is int i2 ? (double)i2 : (double)(BigInteger)numVal;
                return ParseComplexSuffix(str, ref pos, asD) ?? numVal;
            }
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
                    {
                        if (pos >= str.Length) return null;
                        int nameStart = pos;
                        while (pos < str.Length && char.IsLetter(str[pos])) pos++;
                        int nameLen = pos - nameStart;
                        if (nameLen > 1)
                        {
                            var name = str[nameStart..pos].ToLowerInvariant();
                            return name switch
                            {
                                "newline"             => (object)'\n',
                                "space"               => (object)' ',
                                "tab"                 => (object)'\t',
                                "nul" or "null"       => (object)'\0',
                                "return"              => (object)'\r',
                                "escape" or "altmode" => (object)'\x1B',
                                "delete" or "rubout"  => (object)'\x7F',
                                "backspace"           => (object)'\b',
                                "alarm"               => (object)'\a',
                                _ => throw new LispException($"Unknown character name: #\\{name}")
                            };
                        }
                        // nameLen==0: non-letter (e.g. #\ space); nameLen==1: single letter
                        return nameLen == 0 ? (object)str[pos++] : (object)str[nameStart];
                    }
                    case '(':
                        pos--; // step back to '(' so list parser sees it
                        var vec = (Pair?)ParseAt(str, ref pos);
                        return new ArrayList(vec!.ToArray());
                    // Radix prefix literals
                    case 'b': case 'B':
                    {
                        bool neg = pos < str.Length && str[pos] == '-';
                        if (neg || (pos < str.Length && str[pos] == '+')) pos++;
                        int start = pos;
                        while (pos < str.Length && (str[pos] == '0' || str[pos] == '1')) pos++;
                        if (pos == start) return Symbol.Create(neg ? "#b-" : "#b");
                        BigInteger bi = BigInteger.Zero;
                        for (int i = start; i < pos; i++) bi = (bi << 1) | (str[i] - '0');
                        if (neg) bi = -bi;
                        return bi >= int.MinValue && bi <= int.MaxValue ? (object)(int)bi : (object)bi;
                    }
                    case 'o': case 'O':
                    {
                        bool neg = pos < str.Length && str[pos] == '-';
                        if (neg || (pos < str.Length && str[pos] == '+')) pos++;
                        int start = pos;
                        while (pos < str.Length && str[pos] >= '0' && str[pos] <= '7') pos++;
                        if (pos == start) return Symbol.Create(neg ? "#o-" : "#o");
                        BigInteger bi = BigInteger.Zero;
                        for (int i = start; i < pos; i++) bi = (bi << 3) | (str[i] - '0');
                        if (neg) bi = -bi;
                        return bi >= int.MinValue && bi <= int.MaxValue ? (object)(int)bi : (object)bi;
                    }
                    case 'x': case 'X':
                    {
                        bool neg = pos < str.Length && str[pos] == '-';
                        if (neg || (pos < str.Length && str[pos] == '+')) pos++;
                        int start = pos;
                        while (pos < str.Length && char.IsAsciiHexDigit(str[pos])) pos++;
                        if (pos == start) return Symbol.Create(neg ? "#x-" : "#x");
                        BigInteger bi = BigInteger.Zero;
                        for (int i = start; i < pos; i++)
                        {
                            int d = str[i] >= '0' && str[i] <= '9' ? str[i] - '0'
                                  : str[i] >= 'a' && str[i] <= 'f' ? str[i] - 'a' + 10
                                                                     : str[i] - 'A' + 10;
                            bi = (bi << 4) | d;
                        }
                        if (neg) bi = -bi;
                        return bi >= int.MinValue && bi <= int.MaxValue ? (object)(int)bi : (object)bi;
                    }
                    case 'd': case 'D':
                        return ParseAt(str, ref pos);  // decimal prefix: parse normally
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
                // Unwrap (\x. body) → (LAMBDA (x) body): when the lambda shorthand
                // is the only element in a list, the list parser would otherwise build
                // ((LAMBDA (x) body)) — a zero-arg application that crashes at eval.
                // A shorthand Pair is identified by its car being the string "LAMBDA"
                // (not a Symbol, which is what macro-expanded lambda produces).
                if (retval?.cdr == null && retval?.car is Pair lp && lp.car is string ls && ls == "LAMBDA")
                    return retval.car;
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
                var tok = str[start..pos];
                // R7RS special float literals readable back from Dump output.
                return tok switch
                {
                    "+inf.0"             => (object)double.PositiveInfinity,
                    "-inf.0"             => (object)double.NegativeInfinity,
                    "+nan.0" or "-nan.0" => (object)double.NaN,
                    _                    => TryParseImaginary(tok) ?? (object)Symbol.Create(tok)
                };
            }
        }
    }
}

// Thrown by (throw ...) in Lisp code; distinguishes user errors from interpreter errors.
public sealed class LispException(string message) : Exception(message) { }

// Thrown exclusively by (escape-continuation val) — the call/cc escape mechanism.
// Distinguished from LispException so TRY (user try) doesn't swallow continuations.
// 'Tag' ties the exception to a specific call/cc invocation so nested continuations
// can be correctly discriminated by their own TryCont handler.
public sealed class ContinuationException(object? value, object tag) : Exception("continuation escape")
{
    public readonly object? Value = value;
    public readonly object  Tag   = tag;
}

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
            if (current == null)
            {
                // An empty-list sentinel Pair{car=null,cdr=null} has no elements.
                if (root.car == null && root.cdr == null) return false;
                current = root;
                return true;
            }
            if (current.cdr != null) { current = current.cdr; return true; }
            return false;
        }
        public void Reset() => current = null;
        public void Dispose() { }
    }
}

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
    public static object? Check(object? obj)
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
                else if (pat.car is Symbol patSym && patSym.ToString().Contains("..."))
                { // is the last item (variable containing ... in name takes rest
                    Variable(pat.car, obj, all);
                    return true;
                }
                else if (obj == null) return false;
                else if (pat.car is Symbol && cons.TryGetValue(pat.car, out var constVal)) // is a constant
                {
                    if (obj.car != constVal) return false;
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
                return vars.TryGetValue(obj, out var v) ? v : obj;  // var or val or name
            Pair? retval = null;
            for (; obj != null; obj = (obj as Pair)?.cdr)
            {
                var current = (Pair)obj;
                object? o = current.car;
                Pair? next = current.cdr;
                // Symbol bound in macro vars: handle non-variadic, spread, and repeat modes.
                if (o is Symbol sym && vars.TryGetValue(sym, out var symVal))
                {
                    if (!sym.ToString().Contains("..."))  // non-variadic: substitute value directly
                        retval = Pair.Append(retval, symVal);
                    else if (!repeat)                      // variadic, spread mode: expand all values
                    {
                        if (symVal != null)
                            foreach (object x in (Pair)symVal!)
                                retval = Pair.Append(retval, x);
                    }
                    else                                   // variadic, repeat mode: advance one value
                    {
                        temp.TryAdd(sym, symVal);
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
            if (Pair.IsNull(objPair)) return objPair; // empty list () is atomic – don't expand
            if (objPair.car is Symbol && macros.TryGetValue(objPair.car, out var macroVal))
            {
                var macroEntry = (Pair)macroVal!;
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


// A compiled init.ss entry: either a macro definition (which updates Macro.macros)
// or a compiled Expression to evaluate against a fresh environment.
internal abstract class InitEntry { }
internal sealed class InitMacro(Pair def)   : InitEntry { public readonly Pair Def = def; }
internal sealed class InitExpr(Expression e) : InitEntry { public readonly Expression E = e; }

public class Program
{
    public static bool lastValue = true;
    public static bool Stats      = false;
    public static long Iterations = 0;   // closure calls
    public static long TailCalls  = 0;   // TCO trampoline bounces
    public static long EnvFrames  = 0;   // environment frames allocated
    public static long PrimCalls  = 0;   // built-in primitive calls

    // Cumulative totals across all expressions evaluated while Stats is on.
    public static long TotalExprs      = 0;
    public static long TotalIterations = 0;
    public static long TotalTailCalls  = 0;
    public static long TotalEnvFrames  = 0;
    public static long TotalPrimCalls  = 0;
    public static long TotalAllocated  = 0;
    public static double TotalElapsedMs = 0.0;

    public static void ResetTotals()
    {
        TotalExprs = TotalIterations = TotalTailCalls = TotalEnvFrames = TotalPrimCalls = TotalAllocated = 0;
        TotalElapsedMs = 0.0;
    }

    // Snapshot fields captured at BeginStats(), compared in EndStats().
    private static long _statsAllocStart;
    private static int  _statsGC0, _statsGC1, _statsGC2;

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
        long allocDelta  = GC.GetTotalAllocatedBytes(precise: false) - _statsAllocStart;
        long heapBytes   = GC.GetTotalMemory(false);
        int  gc0 = GC.CollectionCount(0) - _statsGC0;
        int  gc1 = GC.CollectionCount(1) - _statsGC1;
        int  gc2 = GC.CollectionCount(2) - _statsGC2;
        // Accrue into running totals.
        TotalExprs++;
        TotalIterations += Iterations;
        TotalTailCalls  += TailCalls;
        TotalEnvFrames  += EnvFrames;
        TotalPrimCalls  += PrimCalls;
        TotalAllocated  += allocDelta;
        TotalElapsedMs  += sw.Elapsed.TotalMilliseconds;
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
        bytes >= 1_024     ? $"{bytes / 1_024.0:F1} KB" :
                             $"{bytes} B";
    // [ThreadStatic] ensures each thread (and thus each embedded Program instance
    // running on its own thread) has its own current-program pointer.  null on
    // threads that have never created a Program is the correct default.
    [ThreadStatic] public static Program? current;
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
            RegisterPrimsAfterInit();
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
        RegisterPrimsAfterInit();
    }

    // After init.ss is loaded, overwrite the Scheme-closure definitions of key
    // predicates and functions with C# primitive delegates so that first-class
    // uses like (map exact? ...) or (filter number? ...) see the updated versions.
    private static readonly string[] _primsToRegister =
    [
        "exact?", "inexact?", "number?", "rational?", "integer?", "real?", "complex?",
        "floor", "ceiling", "round", "truncate",
        "exact->inexact", "inexact->exact",
        "numerator", "denominator",
        "real-part", "imag-part", "make-rectangular", "make-polar", "magnitude", "angle",
    ];
    private void RegisterPrimsAfterInit()
    {
        foreach (var name in _primsToRegister)
            if (Prim.list.TryGetValue(name, out var p))
                initEnv.table[Symbol.Create(name)] = p;
        // Update the exact / inexact aliases defined in init.ss
        if (Prim.list.TryGetValue("inexact->exact", out var e2e)) initEnv.table[Symbol.Create("exact")]   = e2e;
        if (Prim.list.TryGetValue("exact->inexact", out var e2i)) initEnv.table[Symbol.Create("inexact")] = e2i;
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
            if (Stats) BeginStats();
            answer = Eval(Expression.Parse(parsedObj!));
            if (answer is Pair answerPair && answerPair.car is Var v)
            { // evaluate again if the first (car) is an unevaluated variable
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
        if (Program.Stats) Program.EnvFrames++;
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
                table.Add(inSyms.cdr!.car as Symbol ?? throw new Exception("bad . syntax"), inVals ?? new Pair(null));
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
    public static bool Trace = false;  // use (trace on) or (trace off)
    public static HashSet<Symbol> traceHash = [];
    private static readonly Symbol _sAll = Symbol.Create("_all_");
    public static bool IsTraceOn(Symbol s) =>
        Trace && (traceHash.Contains(s) || traceHash.Contains(_sAll));
    public abstract object Eval(Env env);
    // Override in subclasses that can be in tail position to avoid stack growth.
    public virtual  object EvalTail(Env env) => Eval(env);
    public static Pair? Eval_Rands(Pair? rands, Env env)
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
    public static Expression Parse(object? a)
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
            case "TRY":      // (try exp1 catch-exp)
                return new Try(Parse(args!.car), Parse(args.cdr!.car));
            case "TRY-CONT": // (try-cont body catch)  OR  (try-cont tag body catch)
                if (args!.cdr?.cdr != null)   // 3-arg tagged form
                    return new TryCont(Parse(args.car),
                                       Parse(args.cdr!.car),
                                       Parse(args.cdr!.cdr!.car));
                return new TryCont(Parse(args.car), Parse(args.cdr!.car));
            case "LET-SYNTAX":
            case "LETREC-SYNTAX":
            case "let-syntax":
            case "letrec-syntax":
            {
                bool isLetrec = pair.car!.ToString()!.Contains("letrec") ||
                                pair.car!.ToString()!.Contains("LETREC");
                // args.car = binding list ((name sr) ...)
                // args.cdr = body forms
                var bindPairs = args?.car as Pair;
                var bindings  = new List<(object, Pair)>();
                if (bindPairs != null && !Pair.IsNull(bindPairs))
                    foreach (object bp in bindPairs)
                    {
                        if (bp is not Pair bpair) continue;
                        var bname  = bpair.car!;
                        var second = bpair.cdr?.car;  // (syntax-rules ...) OR (literal-list)
                        if (second == null) continue;
                        Pair? md;
                        if (second is Pair secondPair && secondPair.car?.ToString() == "syntax-rules")
                        {
                            // Standard R7RS format: (name (syntax-rules (lits) clause...))
                            var ds = new Pair(Symbol.Create("define-syntax"),
                                              new Pair(bname, new Pair(second, null)));
                            md = Macro.TranslateDefineSyntax(ds);
                        }
                        else
                        {
                            // Native macro format: (name (lits) (pat tmpl) ...)
                            // macros[name] must be ((lits) (pat tmpl) ...) = bpair.cdr
                            // Store as (bname . bpair.cdr) so that def.cdr = bpair.cdr.
                            md = new Pair(bname, bpair.cdr!);
                        }
                        if (md != null) bindings.Add((bname, md));
                    }
                // Body forms are stored raw (un-macro-expanded, un-parsed) so that
                // the local syntax bindings are visible when the body is compiled.
                var rawBody = args?.cdr as Pair;
                return new LetSyntax(isLetrec, bindings, rawBody);
            }
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
        if (Pair.IsNull(o)) return o; // '() stays as the empty-list sentinel
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
    public override object Eval(Env env)
    {
        if (Program.Stats) Program.PrimCalls++;
        return prim(Eval_Rands(rands, env)!);
    }
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
        // Built-in comparison primitives
        ["<"]           = Lt_Prim,
        [">"]           = Gt_Prim,
        ["<="]          = Le_Prim,
        [">="]          = Ge_Prim,
        ["zero?"]       = ZeroQ_Prim,
        ["number?"]     = NumberQ_Prim,
        ["eqv?"]        = EqvQ_Prim,
        ["todouble"]    = ToDouble_Prim,
        ["tointeger"]   = ToInt_Prim,
        ["="]                      = Eq_Prim,
        ["equal?"]                 = EqualQ_Prim,
        ["escape-continuation"]    = EscapeContinuation_Prim,
        ["escape-continuation/tag"]= EscapeContinuationTag_Prim,
        ["dynamic-wind-body"]      = DynamicWindBody_Prim,
        // ── Type predicates (R7RS numeric tower) ─────────────────────────────
        ["exact?"]       = ExactQ_Prim,
        ["inexact?"]     = InexactQ_Prim,
        ["rational?"]    = RationalQ_Prim,
        ["integer?"]     = IntegerQ_Prim,
        ["real?"]        = RealQ_Prim,
        ["complex?"]     = ComplexQ_Prim,
        // ── Exact ↔ inexact conversion ────────────────────────────────────────
        ["exact->inexact"] = ExactToInexact_Prim,
        ["inexact->exact"] = InexactToExact_Prim,
        // ── Rational accessors ────────────────────────────────────────────────
        ["numerator"]    = Numerator_Prim,
        ["denominator"]  = Denominator_Prim,
        // ── Complex number constructors / accessors ───────────────────────────
        ["real-part"]        = RealPart_Prim,
        ["imag-part"]        = ImagPart_Prim,
        ["make-rectangular"] = MakeRect_Prim,
        ["make-polar"]       = MakePolar_Prim,
        ["magnitude"]        = Magnitude_Prim,
        ["angle"]            = Angle_Prim,
        // ── Rounding (updated to handle Rational) ─────────────────────────────
        ["floor"]    = Floor_Prim,
        ["ceiling"]  = Ceiling_Prim,
        ["round"]    = Round_Prim,
        ["truncate"] = Truncate_Prim,
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
                var sb = new StringBuilder("(define (");
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
        int i       => i == 0,
        double d    => d == 0.0,
        BigInteger bi => bi.IsZero,
        Rational r  => r.Numer.IsZero,
        Complex z   => z == Complex.Zero,
        _           => (object)false };
    public static object NumberQ_Prim(Pair args) => args?.car is int or double or BigInteger or Rational or Complex;

    // ── Frequently called conversion / equality helpers ───────────────────────
    // eqv?    = (call x 'Equals y)   — avoids closure creation + reflection
    // todouble/tointeger — avoids closure + call-static reflection
    public static object EqvQ_Prim    (Pair args) => object.Equals(args?.car, args?.cdr?.car);
    public static object ToDouble_Prim(Pair args) => args?.car switch
    {
        BigInteger bi => (object)(double)bi,
        Rational r    => (object)r.ToDouble(),
        Complex z     => (object)z.Real,
        var x         => (object)Convert.ToDouble(x ?? 0.0),
    };
    public static object ToInt_Prim(Pair args) => args?.car switch
    {
        BigInteger bi => (object)(int)bi,
        Rational r    => (object)(int)(r.Numer / r.Denom),
        double d      => (object)(int)d,
        var x         => (object)Convert.ToInt32(x ?? 0),
    };

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
        // Rational and Complex: use IsNumericEqual for value comparison
        if (a is Complex || b is Complex || a is Rational || b is Rational || a is BigInteger || b is BigInteger)
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

    // ── escape-continuation: throws ContinuationException, used by call/cc ──────
    // No-tag form: tag defaults to a singleton "any" object (legacy / single-call use).
    private static readonly object _legacyTag = new object();
    public static object EscapeContinuation_Prim(Pair args) =>
        throw new ContinuationException(args?.car, _legacyTag);

    // ── escape-continuation/tag val tag: tagged form used by the improved call/cc ─
    // args = (value tag). The ContinuationException is only caught by the TryCont
    // that holds the same tag, so nested continuations don't interfere.
    public static object EscapeContinuationTag_Prim(Pair args) =>
        throw new ContinuationException(args?.car, args?.cdr?.car ?? _legacyTag);

    // ── dynamic-wind-body: runs thunk(), then always runs after(), re-throws ────
    public static object DynamicWindBody_Prim(Pair args)
    {
        var thunk = args?.car      as Closure
            ?? throw new LispException("dynamic-wind: thunk must be a closure");
        var after = args?.cdr?.car as Closure
            ?? throw new LispException("dynamic-wind: after must be a closure");
        Exception? exc    = null;
        object     result = new Pair(null);
        try   { result = CallClosure(thunk); }
        catch (Exception e) { exc = e; }
        CallClosure(after);
        if (exc != null) ExceptionDispatchInfo.Capture(exc).Throw();
        return result;
    }

    // Invoke a zero-argument Lisp closure, trampolining tail calls.
    private static object CallClosure(Closure c)
    {
        object r = c.Eval(null);
        while (r is TailCall tc)
        {
            if (Program.Stats) Program.TailCalls++;
            r = tc.Closure.Eval(tc.Args);
        }
        return r;
    }

    // ── Type predicates ───────────────────────────────────────────────────────
    public static object ExactQ_Prim   (Pair args) => args?.car is int or BigInteger or Rational;
    public static object InexactQ_Prim (Pair args) => args?.car is double or Complex;
    public static object RationalQ_Prim(Pair args) =>
        args?.car is int or BigInteger or Rational ||
        (args?.car is double d1 && !double.IsNaN(d1) && !double.IsInfinity(d1));
    public static object IntegerQ_Prim (Pair args) =>
        args?.car is int or BigInteger ||
        (args?.car is Rational ri && ri.Denom.IsOne) ||
        (args?.car is double di && !double.IsNaN(di) && !double.IsInfinity(di) && di == Math.Floor(di));
    public static object ComplexQ_Prim (Pair args) => NumberQ_Prim(args);    // all numbers are complex
    public static object RealQ_Prim    (Pair args) =>
        args?.car is int or BigInteger or Rational or double ||
        (args?.car is Complex zr && zr.Imaginary == 0.0);

    // ── Exact <-> Inexact conversion ──────────────────────────────────────────
    public static object ExactToInexact_Prim(Pair args) => args?.car switch
    {
        int i         => (object)(double)i,
        BigInteger bi => (object)(double)bi,
        Rational r    => (object)r.ToDouble(),
        double d      => (object)d,
        Complex z     => (object)z,
        var x         => (object)Convert.ToDouble(x),
    };
    public static object InexactToExact_Prim(Pair args) => args?.car switch
    {
        int or BigInteger or Rational => args.car!,
        double d   => Arithmetic.DoubleToExact(d),
        Complex z  => z.Imaginary == 0.0
                        ? Arithmetic.DoubleToExact(z.Real)
                        : throw new LispException("inexact->exact: cannot convert complex with nonzero imaginary"),
        var x      => throw new LispException($"inexact->exact: not a number: {Util.Dump(x)}"),
    };

    // ── Rational accessors ────────────────────────────────────────────────────
    public static object Numerator_Prim(Pair args)
    {
        var x = args?.car;
        if (x is double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return x!;
            var (n, _) = Arithmetic.GetNumerDenom(Arithmetic.DoubleToExact(d));
            return (double)n;
        }
        var (num, _) = Arithmetic.GetNumerDenom(x ?? throw new LispException("numerator: missing argument"));
        return Arithmetic.Normalize(num);
    }
    public static object Denominator_Prim(Pair args)
    {
        var x = args?.car;
        if (x is double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return (object)1.0;
            var (_, den) = Arithmetic.GetNumerDenom(Arithmetic.DoubleToExact(d));
            return (double)den;
        }
        var (_, denom) = Arithmetic.GetNumerDenom(x ?? throw new LispException("denominator: missing argument"));
        return Arithmetic.Normalize(denom);
    }

    // ── Complex number operations ─────────────────────────────────────────────
    public static object RealPart_Prim(Pair args) => args?.car switch
    {
        Complex z     => (object)z.Real,
        int i         => (object)(double)i,
        BigInteger bi => (object)(double)bi,
        Rational r    => (object)r.ToDouble(),
        double d      => (object)d,
        var x         => throw new LispException($"real-part: not a number: {Util.Dump(x)}"),
    };
    public static object ImagPart_Prim(Pair args) => args?.car switch
    {
        Complex z                              => (object)z.Imaginary,
        int or BigInteger or Rational or double => (object)0.0,
        var x => throw new LispException($"imag-part: not a number: {Util.Dump(x)}"),
    };
    public static object MakeRect_Prim(Pair args)
    {
        var re = args?.car        ?? throw new LispException("make-rectangular: missing real part");
        var im = args?.cdr?.car   ?? throw new LispException("make-rectangular: missing imag part");
        // Preserve exactness: if imaginary is exact zero, return the real part unchanged.
        if (im is int ii && ii == 0) return re;
        if (im is BigInteger bii && bii.IsZero) return re;
        if (im is Rational ri && ri.Numer.IsZero) return re;
        return new Complex(Arithmetic.D(re), Arithmetic.D(im));
    }
    public static object MakePolar_Prim(Pair args)
    {
        var mag   = Arithmetic.D(args?.car      ?? throw new LispException("make-polar: missing magnitude"));
        var angle = Arithmetic.D(args?.cdr?.car ?? throw new LispException("make-polar: missing angle"));
        return Complex.FromPolarCoordinates(mag, angle);
    }
    public static object Magnitude_Prim(Pair args) => args?.car switch
    {
        Complex z     => (object)Complex.Abs(z),
        int i         => i < 0 ? (i == int.MinValue ? (object)(-(BigInteger)i) : (object)-i) : (object)i,
        BigInteger bi => (object)BigInteger.Abs(bi),
        Rational r    => r.Numer < 0 ? (object)new Rational(-r.Numer, r.Denom) : (object)r,
        double d      => (object)Math.Abs(d),
        var x         => throw new LispException($"magnitude: not a number: {Util.Dump(x)}"),
    };
    public static object Angle_Prim(Pair args) => args?.car switch
    {
        Complex z     => (object)z.Phase,
        double d      => d >= 0.0 ? (object)0.0 : (object)Math.PI,
        int i         => (object)(i >= 0 ? 0.0 : Math.PI),
        BigInteger bi => (object)(bi >= 0 ? 0.0 : Math.PI),
        Rational r    => (object)(r.Numer >= 0 ? 0.0 : Math.PI),
        var x         => throw new LispException($"angle: not a number: {Util.Dump(x)}"),
    };

    // ── Rounding (handle Rational, delegate to Arithmetic) ───────────────────
    public static object Floor_Prim   (Pair args) => Arithmetic.FloorObj   (args?.car!);
    public static object Ceiling_Prim (Pair args) => Arithmetic.CeilingObj (args?.car!);
    public static object Round_Prim   (Pair args) => Arithmetic.RoundObj   (args?.car!);
    public static object Truncate_Prim(Pair args) => Arithmetic.TruncateObj(args?.car!);
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
        catch (ContinuationException) { throw; }  // never swallow continuation escapes
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
        catch (ContinuationException) { throw; }  // never intercept escape continuations
        catch   { return catchX.Eval(env); }
    }
    public override string ToString() => Util.Dump("TRY", tryX, catchX);
}

// TryCont: catches ContinuationException only. Used by (call/cc ...) internally.
// Optional tag: when tag != null the handler only catches continuations thrown
// with that exact tag (object-identity), letting nested call/cc continuations
// propagate to their own handler.
// Syntax:
//   (TRY-CONT body catch)       — legacy / no-tag (catches all ContinuationExceptions)
//   (TRY-CONT tag body catch)   — tagged (catches only matching-tag exceptions)
public class TryCont : Expression
{
    private readonly Expression? _tag;
    private readonly Expression  _tryX;
    private readonly Expression  _catchX;
    public TryCont(Expression tryX, Expression catchX)
        : this(null, tryX, catchX) { }
    public TryCont(Expression? tag, Expression tryX, Expression catchX)
    { _tag = tag; _tryX = tryX; _catchX = catchX; }
    public override object Eval(Env env)
    {
        object? tag = _tag?.Eval(env);
        try   { return _tryX.Eval(env); }
        catch (ContinuationException ce)
        {
            // If no tag specified, catch all (legacy behaviour).
            // If tag specified, only catch when the exception's tag matches.
            if (tag == null || ReferenceEquals(ce.Tag, tag))
                return _catchX.Eval(env);
            throw;   // belongs to an outer call/cc
        }
    }
    public override string ToString() => Util.Dump("TRY-CONT", _tag, _tryX, _catchX);
}

public class Assignment(Symbol id, Expression val) : Expression
{
    public override object Eval(Env env) => env.Bind(id, val.Eval(env));
    public override string ToString()    => Util.Dump("set!", id, val);
}

// LetSyntax / LetrecSyntax: locally-scoped syntax-rules bindings.
// Syntax (after macro-expansion, compiles to an internal LET-SYNTAX node):
//   (let-syntax    ((name (syntax-rules ...)) ...) body...)
//   (letrec-syntax ((name (syntax-rules ...)) ...) body...)
// Both save the global macro table, install the new macros, macro-expand and
// compile the body forms (so the new macros are visible during expansion), then
// evaluate the body and restore the table in all exit paths.
// letrec-syntax: all new macros are installed before any expansion (mutually recursive).
// let-syntax:    the same here — the difference matters only for macro RHS references
//               which in syntax-rules are static patterns, so both behave identically
//               in practice for the common cases.
public class LetSyntax(bool isLetrec, List<(object name, Pair def)> bindings, Pair? rawBody) : Expression
{
    public override object Eval(Env env)
    {
        // Save entire macro table
        var saved = new Dictionary<object, object?>(Macro.macros);
        try
        {
            // Install the new macros  (letrec-syntax: all at once; let-syntax: same)
            foreach (var (name, def) in bindings)
                Macro.macros[name] = def.cdr;

            // Now macro-expand and compile body forms with the local macros visible.
            object result = new Pair(null);
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
            // Restore macro table regardless of how we exit (normal / exception).
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
    public static bool CarryOn = false;

    // Drives TCO: loop while values are TailCall, then return the real result.
    private static object Trampoline(object result)
    {
        while (result is TailCall tc)
        {
            if (Program.Stats) Program.TailCalls++;
            result = tc.Closure.Eval(tc.Args);
        }
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
            Primitive prim           => prim(Eval_Rands(rands, env)!),  // first-class primitive
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

// ── Exact rational number p/q in lowest terms (q > 0) ───────────────────────
public readonly struct Rational : IEquatable<Rational>, IComparable<Rational>
{
    public readonly BigInteger Numer;  // numerator (any sign)
    public readonly BigInteger Denom;  // denominator (always positive)

    public Rational(BigInteger n, BigInteger d)
    {
        if (d.IsZero) throw new LispException("division by zero");
        if (d < BigInteger.Zero) { n = -n; d = -d; }
        var g = BigInteger.GreatestCommonDivisor(n < 0 ? -n : n, d);
        Numer = n / g;
        Denom = d / g;
    }

    // If the denominator is 1, demote to int or BigInteger; else keep as Rational.
    public object Normalize() =>
        Denom.IsOne
            ? (Numer >= int.MinValue && Numer <= int.MaxValue
                ? (object)(int)Numer
                : (object)(BigInteger)Numer)
            : (object)this;

    public double ToDouble() => (double)Numer / (double)Denom;

    public static Rational operator +(Rational a, Rational b) =>
        new(a.Numer * b.Denom + b.Numer * a.Denom, a.Denom * b.Denom);
    public static Rational operator -(Rational a, Rational b) =>
        new(a.Numer * b.Denom - b.Numer * a.Denom, a.Denom * b.Denom);
    public static Rational operator *(Rational a, Rational b) =>
        new(a.Numer * b.Numer, a.Denom * b.Denom);
    public static Rational operator /(Rational a, Rational b)
    {
        if (b.Numer.IsZero) throw new LispException("division by zero");
        return new(a.Numer * b.Denom, a.Denom * b.Numer);
    }
    public static Rational operator -(Rational a) => new(-a.Numer, a.Denom);
    public static bool operator < (Rational a, Rational b) => a.Numer * b.Denom <  b.Numer * a.Denom;
    public static bool operator > (Rational a, Rational b) => b < a;
    public static bool operator <=(Rational a, Rational b) => !(a > b);
    public static bool operator >=(Rational a, Rational b) => !(a < b);
    public static bool operator ==(Rational a, Rational b) => a.Numer == b.Numer && a.Denom == b.Denom;
    public static bool operator !=(Rational a, Rational b) => !(a == b);

    public int  CompareTo(Rational other)        => this < other ? -1 : this > other ? 1 : 0;
    public bool Equals(Rational other)           => this == other;
    public override bool Equals(object? obj)     => obj is Rational r && this == r;
    public override int  GetHashCode()           => HashCode.Combine(Numer, Denom);
    public override string ToString()            => $"{Numer}/{Denom}";
}

public static class Arithmetic
{
    // ── Private type-conversion helpers ──────────────────────────────────────
    public  static double     D(object a) => a switch
    {
        BigInteger bi => (double)bi,
        Rational r    => r.ToDouble(),
        Complex z     => z.Real,
        _             => Convert.ToDouble(a),
    };
    static int        I(object a) => a is BigInteger bi ? (int)bi : Convert.ToInt32(a);
    static BigInteger BI(object a) => a switch
    {
        BigInteger bi => bi,
        int i         => i,
        double d      => (BigInteger)d,
        Rational r    => r.Numer / r.Denom,   // truncate toward zero
        _             => (BigInteger)Convert.ToInt64(a),
    };
    static Rational  ToRational(object a) => a switch
    {
        Rational r    => r,
        int i         => new Rational(i, 1),
        BigInteger bi => new Rational(bi, 1),
        _             => throw new LispException($"cannot convert {a?.GetType().Name ?? "null"} to exact rational"),
    };
    static Complex   ToComplex(object a)  => a switch
    {
        Complex z     => z,
        double d      => new Complex(d, 0.0),
        Rational r    => new Complex(r.ToDouble(), 0.0),
        int i         => new Complex(i, 0.0),
        BigInteger bi => new Complex((double)bi, 0.0),
        _             => new Complex(Convert.ToDouble(a), 0.0),
    };

    // Normalize: if a BigInteger fits in int, demote it back to int.
    public static object Normalize(BigInteger v) =>
        v >= int.MinValue && v <= int.MaxValue ? (object)(int)v : (object)v;

    // ── Arithmetic operations ─────────────────────────────────────────────────
    public static object AddObj(object a, object b)
    {
        if (a is Complex  || b is Complex)  return Complex.Add(ToComplex(a), ToComplex(b));
        if (a is double   || b is double)   return D(a) + D(b);
        if (a is Rational || b is Rational) return (ToRational(a) + ToRational(b)).Normalize();
        if (a is int ia && b is int ib)
        {
            try { return checked(ia + ib); }
            catch (OverflowException) { return Normalize((BigInteger)ia + ib); }
        }
        return Normalize(BI(a) + BI(b));
    }

    public static object SubObj(object a, object b)
    {
        if (a is Complex  || b is Complex)  return Complex.Subtract(ToComplex(a), ToComplex(b));
        if (a is double   || b is double)   return D(a) - D(b);
        if (a is Rational || b is Rational) return (ToRational(a) - ToRational(b)).Normalize();
        if (a is int ia && b is int ib)
        {
            try { return checked(ia - ib); }
            catch (OverflowException) { return Normalize((BigInteger)ia - ib); }
        }
        return Normalize(BI(a) - BI(b));
    }

    public static object MulObj(object a, object b)
    {
        if (a is Complex  || b is Complex)  return Complex.Multiply(ToComplex(a), ToComplex(b));
        if (a is double   || b is double)   return D(a) * D(b);
        if (a is Rational || b is Rational) return (ToRational(a) * ToRational(b)).Normalize();
        if (a is int ia && b is int ib)
        {
            try { return checked(ia * ib); }
            catch (OverflowException) { return Normalize((BigInteger)ia * ib); }
        }
        return Normalize(BI(a) * BI(b));
    }

    // Division of exact integers now returns an exact Rational instead of promoting to double.
    public static object DivObj(object a, object b)
    {
        if (a is Complex  || b is Complex)  return Complex.Divide(ToComplex(a), ToComplex(b));
        if (a is double   || b is double)   return D(a) / D(b);
        if (a is Rational || b is Rational) return (ToRational(a) / ToRational(b)).Normalize();
        var bn = BI(a); var bd = BI(b);
        if (bd.IsZero) throw new LispException("division by zero");
        return new Rational(bn, bd).Normalize();
    }

    public static object NegObj(object a) => a switch
    {
        double d      => (object)(-d),
        int i         => i == int.MinValue ? (object)(-(BigInteger)i) : (object)(-i),
        BigInteger bi => Normalize(-bi),
        Rational r    => new Rational(-r.Numer, r.Denom).Normalize(),
        Complex z     => (object)Complex.Negate(z),
        _             => (object)(-I(a)),
    };

    public static object IDivObj(object a, object b)
    {
        if (a is BigInteger || b is BigInteger) return Normalize(BI(a) / BI(b));
        return (object)(I(a) / I(b));
    }

    public static object ModObj(object a, object b)
    {
        if (a is BigInteger || b is BigInteger) return Normalize(BI(a) % BI(b));
        return (object)(I(a) % I(b));
    }

    public static object PowObj(object a, object b)
    {
        if (a is Complex || b is Complex) return Complex.Pow(ToComplex(a), ToComplex(b));
        // Exact base (int / BigInteger / Rational) with exact int exponent → exact result.
        // Negative int exponent returns the exact rational reciprocal power.
        if (b is int iexp && (a is int || a is BigInteger || a is Rational))
        {
            var r       = a is Rational ra ? ra : new Rational(BI(a), BigInteger.One);
            if (iexp == 0) return 1;
            if (r.Numer.IsZero)
            {
                if (iexp < 0) throw new LispException("expt: zero base with negative exponent");
                return 0;
            }
            int  n       = iexp < 0 ? -iexp : iexp;
            bool negSign = r.Numer < BigInteger.Zero && (n % 2 == 1);
            var  absBase = r.Numer < BigInteger.Zero ? -r.Numer : r.Numer;
            var  numPow  = BigInteger.Pow(absBase, n);
            var  denPow  = BigInteger.Pow(r.Denom, n);
            var  numRes  = negSign ? -numPow : numPow;
            return iexp < 0
                ? new Rational(denPow, numRes).Normalize()   // (p/q)^-n = q^n / p^n
                : new Rational(numRes, denPow).Normalize();
        }
        return Math.Pow(D(a), D(b));
    }

    public static bool LessThan(object a, object b)
    {
        if (a is Complex || b is Complex)
            throw new LispException("<: comparison not defined for complex numbers");
        // Both exact and at least one Rational: compare exactly
        if ((a is Rational || b is Rational) && a is not double && b is not double)
            return ToRational(a) < ToRational(b);
        if (a is int ia && b is int ib) return ia < ib;
        if (a is BigInteger || b is BigInteger) return BI(a) < BI(b);
        return D(a) < D(b);
    }

    // Numeric equality across all numeric types (= semantics: value regardless of exactness).
    public static bool IsNumericEqual(object a, object b)
    {
        if (a is Complex || b is Complex) return ToComplex(a) == ToComplex(b);
        // Mixed exact/inexact: convert exact to double
        if (a is double || b is double) return D(a) == D(b);
        if (a is Rational || b is Rational) return ToRational(a) == ToRational(b);
        return BI(a) == BI(b);
    }

    // ── numerator/denominator helpers for exact types ─────────────────────────
    public static (BigInteger n, BigInteger d) GetNumerDenom(object x) => x switch
    {
        int i         => ((BigInteger)i, BigInteger.One),
        BigInteger bi => (bi, BigInteger.One),
        Rational r    => (r.Numer, r.Denom),
        _             => throw new LispException($"not an exact rational: {Util.Dump(x)}"),
    };

    // ── Convert inexact double to exact rational (exact IEEE-754 value) ───────
    public static object DoubleToExact(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d))
            throw new LispException($"inexact->exact: no exact representation for {d}");
        if (d == 0.0) return 0;
        long       bits    = BitConverter.DoubleToInt64Bits(d);
        bool       neg     = bits < 0;
        int        rawExp  = (int)((bits >> 52) & 0x7FF);
        long       rawMant = bits & 0x000FFFFFFFFFFFFFL;
        BigInteger mant    = rawExp == 0
            ? (BigInteger)rawMant                              // subnormal: no implicit leading bit
            : ((BigInteger)rawMant | 0x0010000000000000L);    // normal: add implicit bit
        int exp2 = rawExp == 0 ? -1022 - 52 : rawExp - 1023 - 52;
        if (neg) mant = -mant;
        if (exp2 >= 0) return Normalize(mant << exp2);
        return new Rational(mant, BigInteger.Pow(2, -exp2)).Normalize();
    }

    // ── floor / ceiling / truncate / round ───────────────────────────────────
    private static BigInteger FloorBI(BigInteger numer, BigInteger denom)
    {
        var (q, r) = BigInteger.DivRem(numer, denom);
        if (!r.IsZero && numer < BigInteger.Zero) q--;   // truncation toward zero: adjust for negatives
        return q;
    }
    public static object FloorObj(object x) => x switch
    {
        int or BigInteger => x,
        Rational r        => Normalize(FloorBI(r.Numer, r.Denom)),
        double d          => (object)Math.Floor(d),
        _                 => (object)Math.Floor(D(x)),
    };
    public static object CeilingObj(object x) => x switch
    {
        int or BigInteger => x,
        Rational r        => Normalize(-FloorBI(-r.Numer, r.Denom)),  // ceiling(x) = -floor(-x)
        double d          => (object)Math.Ceiling(d),
        _                 => (object)Math.Ceiling(D(x)),
    };
    public static object TruncateObj(object x) => x switch
    {
        int or BigInteger => x,
        Rational r        => Normalize(r.Numer / r.Denom),   // BigInteger division truncates toward zero
        double d          => (object)Math.Truncate(d),
        _                 => (object)Math.Truncate(D(x)),
    };
    public static object RoundObj(object x) => x switch
    {
        int or BigInteger => x,
        Rational r        => RoundRational(r),
        double d          => (object)Math.Round(d, MidpointRounding.ToEven),
        _                 => (object)Math.Round(D(x), MidpointRounding.ToEven),
    };
    private static object RoundRational(Rational r)
    {
        var f     = FloorBI(r.Numer, r.Denom);
        var fracN = r.Numer - f * r.Denom;   // 0 <= fracN < r.Denom
        var twice = fracN * 2;
        if (twice < r.Denom) return Normalize(f);
        if (twice > r.Denom) return Normalize(f + 1);
        return Normalize(f % 2 == 0 ? f : f + 1);  // halfway: round to even
    }

    // ── Bit operations (exact integers only) ─────────────────────────────────
    public static object XorObj   (object a, object b) =>
        (a is BigInteger || b is BigInteger) ? Normalize(BI(a) ^ BI(b)) : (object)(I(a) ^ I(b));
    public static object BitAndObj(object a, object b) =>
        (a is BigInteger || b is BigInteger) ? Normalize(BI(a) & BI(b)) : (object)(I(a) & I(b));
    public static object BitOrObj (object a, object b) =>
        (a is BigInteger || b is BigInteger) ? Normalize(BI(a) | BI(b)) : (object)(I(a) | I(b));
    public static object BitXorObj(object a, object b) =>
        (a is BigInteger || b is BigInteger) ? Normalize(BI(a) ^ BI(b)) : (object)(I(a) ^ I(b));
}

public static class Interpreter
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
                    prog.Eval(File.ReadAllText(file));
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
