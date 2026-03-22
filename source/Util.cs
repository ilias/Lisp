namespace Lisp;

public static class Util
{
    public static readonly string GAC;

    static Util()
    {
        var root = Environment.GetEnvironmentVariable("systemroot");
        var ver = Environment.Version.ToString();
        GAC = $"{root}\\Microsoft.NET\\Framework\\v{ver[..ver.LastIndexOf('.')]}\\";
    }

    public static Type[] GetTypes(object[] objs) =>
        objs.Select(o => o?.GetType() ?? typeof(object)).ToArray();

    public static object CallMethod(Pair args, bool staticCall)
    {
        var objs = args.cdr?.cdr != null ? args.cdr.cdr.ToArray() : null;
        var types = objs != null ? GetTypes(objs) : Type.EmptyTypes;
        var type = staticCall ? GetType(args.car!.ToString()!) : args.car!.GetType();
        try
        {
            var method = type!.GetMethod(args.cdr!.car!.ToString()!, types);
            if (method != null)
                return method.Invoke(args.car, objs)!;

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
        Type? type = Type.GetType(tname);
        if (type != null) return type;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            if ((type = asm.GetType(tname)) != null) return type;

        var comp = tname.Split('@');
        comp[0] = comp[0].Replace("~", GAC);
        if (comp.Length == 2)
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
        var output = new StringBuilder("[").Append(title);
        foreach (object? o in args)
            output.Append(' ').Append(Dump(o));
        return output.Append(']').ToString();
    }

    private static readonly Symbol _sQuote = Symbol.Create("quote");

    private static string FormatDouble(double d)
    {
        if (double.IsNaN(d)) return "+nan.0";
        if (double.IsPositiveInfinity(d)) return "+inf.0";
        if (double.IsNegativeInfinity(d)) return "-inf.0";

        var s = d.ToString("R", CultureInfo.InvariantCulture);
        if (s.Contains('.') && !s.Contains('E') && !s.Contains('e'))
        {
            s = s.TrimEnd('0');
            if (s.EndsWith('.')) { }
        }

        if (!s.Contains('.') && !s.Contains('E') && !s.Contains('e'))
            s += ".";
        return s;
    }

    private static string FormatComplex(Complex z)
    {
        if (z.Imaginary == 0.0) return FormatDouble(z.Real);
        var re = z.Real == 0.0 ? "" : FormatDouble(z.Real);
        var im = z.Imaginary;
        string imStr = im == 1.0 ? "+i"
                     : im == -1.0 ? "-i"
                     : im > 0.0 ? "+" + FormatDouble(im) + "i"
                     : FormatDouble(im) + "i";
        return re + imStr;
    }

    public static string Dump(object? exp)
    {
        return exp switch
        {
            _ when Pair.IsNull(exp) => "()",
            string s => $"\"{s}\"",
            bool b => b ? "#t" : "#f",
            char c => $"#\\{c}",
            double d => FormatDouble(d),
            BigInteger bi => bi.ToString(),
            Rational r => r.ToString(),
            Complex z => FormatComplex(z),
            ErrorObject eo => eo.ToString(),
            Pair { car: Symbol quot } p when ReferenceEquals(quot, _sQuote) => $"'{Dump(p.cdr!.car)}",
            ICollection => FormatCollection(exp),
            _ => exp?.ToString() ?? "()",
        };
    }

    private static string FormatCollection(object? exp)
    {
        var sb = new StringBuilder("(");
        foreach (object? o in (ICollection)exp!) sb.Append(Dump(o)).Append(' ');
        if (sb.Length > 1) sb.Length--;
        sb.Append(')');
        return (exp is ArrayList ? "#" : "") + sb.ToString();
    }

    private static bool IsSymbolStopChar(char c) =>
        c is '(' or ')' or '\n' or '\r' or '\t' or ' ' or '#' or ',' or '\'' or '"';

    private static object? ParseComplexSuffix(string str, ref int pos, double realVal)
    {
        if (pos < str.Length && str[pos] == 'i' &&
            (pos + 1 >= str.Length || IsSymbolStopChar(str[pos + 1])))
        {
            pos++;
            return realVal == 0.0 ? (object)0.0 : new Complex(0.0, realVal);
        }

        if (pos < str.Length && (str[pos] == '+' || str[pos] == '-'))
        {
            int sepPos = pos;
            bool imagNeg = str[pos] == '-';
            pos++;
            int imagStart = pos;
            bool imagHasDot = false;
            bool imagHasExp = false;
            while (pos < str.Length && (char.IsAsciiDigit(str[pos]) || (!imagHasDot && str[pos] == '.')))
            {
                if (str[pos] == '.') imagHasDot = true;
                pos++;
            }
            if (pos < str.Length && (str[pos] == 'e' || str[pos] == 'E'))
            {
                int eS = pos;
                pos++;
                if (pos < str.Length && (str[pos] == '+' || str[pos] == '-')) pos++;
                if (pos < str.Length && char.IsAsciiDigit(str[pos]))
                {
                    while (pos < str.Length && char.IsAsciiDigit(str[pos])) pos++;
                    imagHasExp = true;
                }
                else pos = eS;
            }
            if (pos < str.Length && str[pos] == 'i' &&
                (pos + 1 >= str.Length || IsSymbolStopChar(str[pos + 1])))
            {
                pos++;
                double imag;
                if (pos - 1 == imagStart)
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
            pos = sepPos;
        }
        return null;
    }

    private static object? TryParseImaginary(string tok)
    {
        if (tok.Length < 2 || tok[^1] != 'i') return null;
        char first = tok[0];
        if (first != '+' && first != '-') return null;
        bool neg = first == '-';
        var numPart = tok.AsSpan(1, tok.Length - 2);
        if (numPart.Length == 0) return new Complex(0.0, neg ? -1.0 : 1.0);
        if (double.TryParse(numPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            return new Complex(0.0, neg ? -d : d);
        if (BigInteger.TryParse(numPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger bi))
            return new Complex(0.0, neg ? -(double)bi : (double)bi);
        return null;
    }

    public static object? Parse(string str, out string after)
    {
        int pos = 0;
        while (pos < str.Length && str[pos] is ' ' or '\t' or '\r' or '\n') pos++;
        var result = ParseAt(str, ref pos);
        while (pos < str.Length && str[pos] is ' ' or '\t' or '\r' or '\n') pos++;
        after = pos >= str.Length ? "" : str[pos..];
        return result;
    }

    private static object? ParseAt(string str, ref int pos)
    {
        while (pos < str.Length && str[pos] is ' ' or '\t' or '\r' or '\n') pos++;
        if (pos >= str.Length) return null;

        char ch = str[pos];

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
            if (pos < str.Length && (str[pos] == 'e' || str[pos] == 'E'))
            {
                int expStart = pos;
                pos++;
                if (pos < str.Length && (str[pos] == '+' || str[pos] == '-')) pos++;
                if (pos < str.Length && char.IsAsciiDigit(str[pos]))
                {
                    while (pos < str.Length && char.IsAsciiDigit(str[pos])) pos++;
                    hasExp = true;
                }
                else
                {
                    pos = expStart;
                }
            }
            var span = str.AsSpan(start, pos - start);
            if (hasDot || hasExp)
            {
                var rv = double.Parse(span, NumberStyles.Float, CultureInfo.InvariantCulture);
                return ParseComplexSuffix(str, ref pos, rv) ?? (object)rv;
            }
            if (pos < str.Length && str[pos] == '/' &&
                pos + 1 < str.Length && char.IsAsciiDigit(str[pos + 1]))
            {
                var numer = BigInteger.Parse(span, NumberStyles.Integer, CultureInfo.InvariantCulture);
                pos++;
                int dStart = pos;
                while (pos < str.Length && char.IsAsciiDigit(str[pos])) pos++;
                var denom = BigInteger.Parse(str.AsSpan(dStart, pos - dStart), NumberStyles.Integer, CultureInfo.InvariantCulture);
                if (denom.IsZero) throw new LispException("division by zero in rational literal");
                return new Rational(numer, denom).Normalize();
            }
            object numVal;
            if (int.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv))
                numVal = iv;
            else
                numVal = BigInteger.Parse(span, NumberStyles.Integer, CultureInfo.InvariantCulture);
            {
                double asD = numVal is int i2 ? i2 : (double)(BigInteger)numVal;
                return ParseComplexSuffix(str, ref pos, asD) ?? numVal;
            }
        }

        switch (ch)
        {
            case ';':
                while (pos < str.Length && str[pos] is not '\n' and not '\r') pos++;
                return ParseAt(str, ref pos);

            case ',':
                pos++;
                bool splicing = pos < str.Length && str[pos] == '@';
                if (splicing) pos++;
                return Pair.Cons(Symbol.Create(splicing ? ",@" : ","), new Pair(ParseAt(str, ref pos)));

            case '\'':
                pos++;
                return Pair.Cons(Symbol.Create("quote"), new Pair(ParseAt(str, ref pos)));

            case '`':
                pos++;
                return Pair.Cons(Symbol.Create("quote"), new Pair(ParseAt(str, ref pos)));

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
                                "newline" => (object)'\n',
                                "space" => (object)' ',
                                "tab" => (object)'\t',
                                "nul" or "null" => (object)'\0',
                                "return" => (object)'\r',
                                "escape" or "altmode" => (object)'\x1B',
                                "delete" or "rubout" => (object)'\x7F',
                                "backspace" => (object)'\b',
                                "alarm" => (object)'\a',
                                _ => throw new LispException($"Unknown character name: #\\{name}"),
                            };
                        }
                        return nameLen == 0 ? (object)str[pos++] : str[nameStart];
                    }
                    case '(':
                        pos--;
                        var vec = (Pair?)ParseAt(str, ref pos);
                        return new ArrayList(vec!.ToArray());
                    case 'b':
                    case 'B':
                    {
                        bool neg = pos < str.Length && str[pos] == '-';
                        if (neg || (pos < str.Length && str[pos] == '+')) pos++;
                        int start = pos;
                        while (pos < str.Length && (str[pos] == '0' || str[pos] == '1')) pos++;
                        if (pos == start) return Symbol.Create(neg ? "#b-" : "#b");
                        BigInteger bi = BigInteger.Zero;
                        for (int i = start; i < pos; i++) bi = (bi << 1) | (str[i] - '0');
                        if (neg) bi = -bi;
                        return bi >= int.MinValue && bi <= int.MaxValue ? (object)(int)bi : bi;
                    }
                    case 'o':
                    case 'O':
                    {
                        bool neg = pos < str.Length && str[pos] == '-';
                        if (neg || (pos < str.Length && str[pos] == '+')) pos++;
                        int start = pos;
                        while (pos < str.Length && str[pos] >= '0' && str[pos] <= '7') pos++;
                        if (pos == start) return Symbol.Create(neg ? "#o-" : "#o");
                        BigInteger bi = BigInteger.Zero;
                        for (int i = start; i < pos; i++) bi = (bi << 3) | (str[i] - '0');
                        if (neg) bi = -bi;
                        return bi >= int.MinValue && bi <= int.MaxValue ? (object)(int)bi : bi;
                    }
                    case 'x':
                    case 'X':
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
                        return bi >= int.MinValue && bi <= int.MaxValue ? (object)(int)bi : bi;
                    }
                    case 'd':
                    case 'D':
                        return ParseAt(str, ref pos);
                    default:
                        return str[pos - 1] == 't';
                }

            case '"':
            {
                pos++;
                var cVal = new StringBuilder();
                while (pos < str.Length && str[pos] != '"')
                {
                    if (str[pos] == '\\')
                    {
                        pos++;
                        if (str[pos] == 'n') cVal.Append('\n');
                        else cVal.Append(str[pos]);
                    }
                    else
                        cVal.Append(str[pos]);
                    pos++;
                }
                if (pos < str.Length) pos++;
                return cVal.ToString();
            }

            case '(':
            {
                pos++;
                Pair? retval = null;
                Pair? retvalTail = null;
                for (object? item; (item = ParseAt(str, ref pos)) != null;)
                {
                    var node = new Pair(item);
                    if (retvalTail == null) retval = retvalTail = node;
                    else { retvalTail.cdr = node; retvalTail = node; }
                }
                if (retval?.cdr == null && retval?.car is Pair lp && lp.car is string ls && ls == "LAMBDA")
                    return retval.car;
                return retval ?? Pair.Empty;
            }

            case ')':
                pos++;
                return null;

            case '\\':
            {
                pos++;
                int start = pos;
                while (pos < str.Length && str[pos] != '.') pos++;
                var paramStr = str[start..pos];
                pos++;
                Pair? vars = null;
                foreach (var id in paramStr.Split(','))
                    if (vars is null) vars = new Pair(Symbol.Create(id));
                    else vars.Append(Symbol.Create(id));
                return new Pair("LAMBDA", new Pair(vars, new Pair(ParseAt(str, ref pos))));
            }

            default:
            {
                int start = pos;
                while (pos < str.Length && !IsSymbolStopChar(str[pos])) pos++;
                var tok = str[start..pos];
                return tok switch
                {
                    "+inf.0" => (object)double.PositiveInfinity,
                    "-inf.0" => (object)double.NegativeInfinity,
                    "+nan.0" or "-nan.0" => (object)double.NaN,
                    _ => TryParseImaginary(tok) ?? (object)Symbol.Create(tok),
                };
            }
        }
    }
}
