namespace Lisp;

public static class Util
{
    public static readonly string GAC;
    private sealed class SourceHolder(SourceSpan span) { public SourceSpan Span { get; } = span; }
    public sealed class SourceDocument
    {
        private readonly int[] _lineStarts;

        public string Text { get; }
        public string? SourceName { get; }

        public SourceDocument(string text, string? sourceName)
        {
            Text = text;
            SourceName = sourceName;

            List<int> lineStarts = [0];
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] == '\r')
                {
                    if (index + 1 < text.Length && text[index + 1] == '\n') index++;
                    lineStarts.Add(index + 1);
                }
                else if (text[index] == '\n')
                {
                    lineStarts.Add(index + 1);
                }
            }
            _lineStarts = [.. lineStarts];
        }

        public SourceSpan GetSpan(int startOffset, int endOffset)
        {
            startOffset = Math.Clamp(startOffset, 0, Text.Length);
            endOffset = Math.Clamp(Math.Max(startOffset, endOffset), 0, Text.Length);
            var (startLine, startColumn) = GetLineColumn(startOffset);
            var (endLine, endColumn) = GetLineColumn(endOffset);
            return new SourceSpan(SourceName, startLine, startColumn, endLine, endColumn);
        }

        private (int Line, int Column) GetLineColumn(int offset)
        {
            int index = Array.BinarySearch(_lineStarts, offset);
            if (index < 0) index = ~index - 1;
            index = Math.Clamp(index, 0, _lineStarts.Length - 1);
            int lineStart = _lineStarts[index];
            return (index + 1, (offset - lineStart) + 1);
        }
    }

    private sealed class ParseContext(SourceDocument document, int baseOffset, ParseContext? previous)
    {
        public SourceDocument Document { get; } = document;
        public int BaseOffset { get; } = baseOffset;
        public ParseContext? Previous { get; } = previous;
    }

    private sealed class ParseScope(ParseContext? previous) : IDisposable
    {
        public void Dispose() => _parseContext = previous;
    }

    private static readonly ConditionalWeakTable<Pair, SourceHolder> _pairSources = new();

    static Util()
    {
        var root = Environment.GetEnvironmentVariable("systemroot");
        var ver = Environment.Version.ToString();
        GAC = $"{root}\\Microsoft.NET\\Framework\\v{ver[..ver.LastIndexOf('.')]}\\";
    }

    public static Type[] GetTypes(object[] objs) =>
        objs.Select(o => o?.GetType() ?? typeof(object)).ToArray();

    [ThreadStatic] private static ParseContext? _parseContext;

    public static IDisposable PushSourceContext(SourceDocument document, int baseOffset)
    {
        var previous = _parseContext;
        _parseContext = new ParseContext(document, baseOffset, previous);
        return new ParseScope(previous);
    }

    public static SourceSpan? GetSource(object? obj) =>
        obj is Pair pair && _pairSources.TryGetValue(pair, out var holder) ? holder.Span : null;

    public static void PropagateSource(object? from, object? to)
    {
        if (to is not Pair pair || GetSource(pair) != null || GetSource(from) is not { } source)
            return;

        _pairSources.Remove(pair);
        _pairSources.Add(pair, new SourceHolder(source));
    }

    public static void PropagateSourceDeep(object? from, object? to)
    {
        if (GetSource(from) is not { } source)
            return;

        HashSet<Pair> visited = new(ReferenceEqualityComparer.Instance);
        ApplySourceDeep(to, source, visited);
    }

    public static SourceSpan? GetCurrentSourceSpan(int localStartOffset, int localEndOffset)
    {
        if (_parseContext == null)
            return null;

        return _parseContext.Document.GetSpan(_parseContext.BaseOffset + localStartOffset, _parseContext.BaseOffset + localEndOffset);
    }

    private static void RegisterPairSource(Pair pair, int localStartOffset, int localEndOffset)
    {
        if (GetCurrentSourceSpan(localStartOffset, localEndOffset) is not { } source)
            return;

        _pairSources.Remove(pair);
        _pairSources.Add(pair, new SourceHolder(source));
    }

    private static void ApplySourceDeep(object? obj, SourceSpan source, HashSet<Pair> visited)
    {
        if (obj is not Pair pair || !visited.Add(pair))
            return;

        if (GetSource(pair) == null)
        {
            _pairSources.Remove(pair);
            _pairSources.Add(pair, new SourceHolder(source));
        }

        ApplySourceDeep(pair.car, source, visited);
        ApplySourceDeep(pair.cdr, source, visited);
    }

    public static object CallMethod(Pair args, bool staticCall)
    {
        var objs = args.cdr?.cdr != null ? args.cdr.cdr.ToArray() : null;
        var types = objs != null ? GetTypes(objs) : Type.EmptyTypes;
        var type = staticCall ? GetType(args.car!.ToString()!) : args.car!.GetType();
        string methodName = args.cdr!.car!.ToString()!;
        if (type == null)
            throw new LispException($"Unknown type: {args.car}");
        try
        {
            var method = type.GetMethod(methodName, types);
            if (method != null)
                return method.Invoke(args.car, objs)!;

            var flags = BindingFlags.InvokeMethod
                      | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            return type.InvokeMember(methodName, flags, null, args.car, objs)!;
        }
        catch (TargetInvocationException tie)
        {
            throw ExceptionDisplay.WrapHostException(tie.InnerException ?? tie, methodName);
        }
        catch (Exception ex)
        {
            throw ExceptionDisplay.WrapHostException(ex, methodName);
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
    private const int DefaultPAdicDisplayDigits = 16;

    public static int NumericDisplayBase { get; private set; } = 10;
    public static int NumericDisplayPrecision { get; private set; } = DefaultPAdicDisplayDigits;

    public static void SetNumericDisplay(int radix, int? precision = null)
    {
        if (precision.HasValue)
        {
            if (precision.Value <= 0)
                throw new LispException("p-adic: precision must be a positive exact integer");
            NumericDisplayPrecision = precision.Value;
        }

        if (radix == 10)
        {
            NumericDisplayBase = 10;
            return;
        }

        if (radix < 2 || !IsPrimeInteger(radix))
            throw new LispException("p-adic: base must be a prime integer, or 10 to disable p-adic display");

        NumericDisplayBase = radix;
    }

    public static bool IsPrimeInteger(object? value) => value switch
    {
        int i => IsPrimeBigInteger(i),
        BigInteger bi => IsPrimeBigInteger(bi),
        Rational r when r.Denom.IsOne => IsPrimeBigInteger(r.Numer),
        _ => false,
    };

    private static bool IsPrimeBigInteger(BigInteger value)
    {
        if (value < 2) return false;
        if (value == 2) return true;
        if (value.IsEven) return false;
        for (BigInteger divisor = 3; divisor <= value / divisor; divisor += 2)
            if ((value % divisor).IsZero) return false;
        return true;
    }

    private static bool TryGetFiniteNonNegativeInteger(object value, out BigInteger integer)
    {
        switch (value)
        {
            case int i when i >= 0:
                integer = i;
                return true;
            case BigInteger bi when bi.Sign >= 0:
                integer = bi;
                return true;
            case Rational r when r.Denom.IsOne && r.Numer.Sign >= 0:
                integer = r.Numer;
                return true;
            default:
                integer = BigInteger.Zero;
                return false;
        }
    }

    private static void GetExactNumeratorDenominator(object value, out BigInteger numer, out BigInteger denom)
    {
        switch (value)
        {
            case int i:
                numer = i;
                denom = BigInteger.One;
                break;
            case BigInteger bi:
                numer = bi;
                denom = BigInteger.One;
                break;
            case Rational r:
                numer = r.Numer;
                denom = r.Denom;
                break;
            default:
                throw new LispException($"p-adic: not an exact number: {value}");
        }
    }

    private static int FactorOutRadix(ref BigInteger value, int radix)
    {
        if (value.IsZero) return 0;

        int count = 0;
        BigInteger bigRadix = radix;
        while ((value % bigRadix).IsZero)
        {
            value /= bigRadix;
            count++;
        }
        return count;
    }

    private static int PositiveMod(BigInteger value, int modulus)
    {
        int result = (int)(value % modulus);
        return result < 0 ? result + modulus : result;
    }

    private static int ModInverse(int value, int modulus)
    {
        int t = 0;
        int newT = 1;
        int r = modulus;
        int newR = value % modulus;

        while (newR != 0)
        {
            int quotient = r / newR;
            (t, newT) = (newT, t - quotient * newT);
            (r, newR) = (newR, r - quotient * newR);
        }

        if (r != 1)
            throw new LispException($"p-adic: denominator is not invertible modulo {modulus}");

        return t < 0 ? t + modulus : t;
    }

    private static List<int> GetPAdicUnitDigits(BigInteger numer, BigInteger denom, int radix, int digits)
    {
        List<int> result = new(Math.Max(1, digits));
        int inverseDenom = ModInverse(PositiveMod(denom, radix), radix);

        for (int index = 0; index < digits; index++)
        {
            int digit = PositiveMod((BigInteger)PositiveMod(numer, radix) * inverseDenom, radix);
            result.Add(digit);
            numer = (numer - ((BigInteger)digit * denom)) / radix;
        }

        return result;
    }

    private static char DigitToChar(int digit) =>
        digit < 10 ? (char)('0' + digit) : (char)('a' + (digit - 10));

    private static string DigitsToString(IReadOnlyList<int> digits)
    {
        var sb = new StringBuilder(digits.Count);
        for (int index = digits.Count - 1; index >= 0; index--)
            sb.Append(DigitToChar(digits[index]));
        return sb.ToString();
    }

    private static string FormatIntegerBase(BigInteger value, int radix)
    {
        if (value.IsZero) return "0";

        bool negative = value.Sign < 0;
        if (negative) value = BigInteger.Abs(value);

        var sb = new StringBuilder();
        BigInteger bigRadix = radix;
        while (value > BigInteger.Zero)
        {
            value = BigInteger.DivRem(value, bigRadix, out var remainder);
            sb.Append(DigitToChar((int)remainder));
        }

        if (negative) sb.Append('-');
        for (int left = 0, right = sb.Length - 1; left < right; left++, right--)
            (sb[left], sb[right]) = (sb[right], sb[left]);
        return sb.ToString();
    }

    private static string FormatFinitePAdicInteger(BigInteger value, int radix)
    {
        string digits = FormatIntegerBase(value, radix);
        if (digits.Length <= NumericDisplayPrecision)
            return $"{digits}_{radix.ToString(CultureInfo.InvariantCulture)}";

        string suffix = digits[^NumericDisplayPrecision..];
        return $"...{suffix}_{radix.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string FormatPAdicExact(object value)
    {
        int radix = NumericDisplayBase;
        if (TryGetFiniteNonNegativeInteger(value, out var finiteInteger))
            return FormatFinitePAdicInteger(finiteInteger, radix);

        GetExactNumeratorDenominator(value, out var numer, out var denom);
        if (numer.IsZero) return $"0_{radix.ToString(CultureInfo.InvariantCulture)}";

        var unitNumer = numer;
        var unitDenom = denom;
        int valuation = FactorOutRadix(ref unitNumer, radix) - FactorOutRadix(ref unitDenom, radix);

        if (valuation >= 0)
        {
            List<int> digits = new(Math.Max(NumericDisplayPrecision, valuation + 1));
            for (int index = 0; index < valuation; index++)
                digits.Add(0);

            int remainingDigits = Math.Max(1, NumericDisplayPrecision - valuation);
            digits.AddRange(GetPAdicUnitDigits(unitNumer, unitDenom, radix, remainingDigits));
            return $"...{DigitsToString(digits)}_{radix.ToString(CultureInfo.InvariantCulture)}";
        }

        string unitDigits = TryGetFiniteNonNegativeInteger(new Rational(unitNumer, unitDenom).Normalize(), out var unitInteger)
            ? FormatIntegerBase(unitInteger, radix)
            : $"...{DigitsToString(GetPAdicUnitDigits(unitNumer, unitDenom, radix, NumericDisplayPrecision))}";
        return $"{unitDigits}_{radix.ToString(CultureInfo.InvariantCulture)}*{radix.ToString(CultureInfo.InvariantCulture)}^{valuation.ToString(CultureInfo.InvariantCulture)}";
    }

    public static string NumberToString(object? value)
    {
        return value switch
        {
            int i => NumericDisplayBase == 10 ? i.ToString(CultureInfo.InvariantCulture) : FormatPAdicExact(i),
            BigInteger bi => NumericDisplayBase == 10 ? bi.ToString(CultureInfo.InvariantCulture) : FormatPAdicExact(bi),
            Rational r => NumericDisplayBase == 10 ? r.ToString() : FormatPAdicExact(r),
            double d => FormatDouble(d),
            Complex z => FormatComplex(z),
            _ => value?.ToString() ?? "()",
        };
    }

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
            int i => NumberToString(i),
            double d => NumberToString(d),
            BigInteger bi => NumberToString(bi),
            Rational r => NumberToString(r),
            Complex z => NumberToString(z),
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

    private static void SkipWhitespace(string str, ref int pos)
    {
        while (pos < str.Length && str[pos] is ' ' or '\t' or '\r' or '\n') pos++;
    }

    private static object ParseQuoteLike(string symbolName, string str, ref int pos)
    {
        int start = pos;
        pos++;
        var pair = Pair.Cons(Symbol.Create(symbolName), new Pair(ParseAt(str, ref pos)));
        RegisterPairSource(pair, start, pos);
        return pair;
    }

    private static object? ParseCharacterLiteral(string str, ref int pos)
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

    private static object ParseRadixInteger(string str, ref int pos, int radix, Func<char, bool> isDigit, Func<char, int> digitValue, string prefix)
    {
        bool neg = pos < str.Length && str[pos] == '-';
        if (neg || (pos < str.Length && str[pos] == '+')) pos++;
        int start = pos;
        while (pos < str.Length && isDigit(str[pos])) pos++;
        if (pos == start) return Symbol.Create(neg ? prefix + "-" : prefix);
        BigInteger bi = BigInteger.Zero;
        for (int i = start; i < pos; i++)
            bi = (bi * radix) + digitValue(str[i]);
        if (neg) bi = -bi;
        return bi >= int.MinValue && bi <= int.MaxValue ? (object)(int)bi : bi;
    }

    private static object? ParseHashDispatch(string str, ref int pos)
    {
        pos++;
        if (pos >= str.Length) return null;
        switch (str[pos++])
        {
            case '\\':
                return ParseCharacterLiteral(str, ref pos);
            case '(':
                pos--;
                var vec = (Pair?)ParseAt(str, ref pos);
                return new ArrayList(vec!.ToArray());
            case 'b':
            case 'B':
                return ParseRadixInteger(str, ref pos, 2, ch => ch is '0' or '1', ch => ch - '0', "#b");
            case 'o':
            case 'O':
                return ParseRadixInteger(str, ref pos, 8, ch => ch >= '0' && ch <= '7', ch => ch - '0', "#o");
            case 'x':
            case 'X':
                return ParseRadixInteger(
                    str,
                    ref pos,
                    16,
                    char.IsAsciiHexDigit,
                    ch => ch >= '0' && ch <= '9' ? ch - '0'
                        : ch >= 'a' && ch <= 'f' ? ch - 'a' + 10
                        : ch - 'A' + 10,
                    "#x");
            case 'd':
            case 'D':
                return ParseAt(str, ref pos);
            default:
                return str[pos - 1] == 't';
        }
    }

    private static string ParseStringLiteral(string str, ref int pos)
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
            {
                cVal.Append(str[pos]);
            }
            pos++;
        }
        if (pos < str.Length) pos++;
        return cVal.ToString();
    }

    private static object ParseListLiteral(string str, ref int pos)
    {
        int start = pos;
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
        {
            RegisterPairSource(lp, start, pos);
            return retval.car;
        }
        if (retval != null)
            RegisterPairSource(retval, start, pos);
        return retval ?? Pair.Empty;
    }

    private static object ParseLambdaShorthand(string str, ref int pos)
    {
        int sourceStart = pos;
        pos++;
        int paramStart = pos;
        while (pos < str.Length && str[pos] != '.') pos++;
        var paramStr = str[paramStart..pos];
        pos++;
        Pair? vars = null;
        foreach (var id in paramStr.Split(','))
            if (vars is null) vars = new Pair(Symbol.Create(id));
            else vars.Append(Symbol.Create(id));
        var lambda = new Pair("LAMBDA", new Pair(vars, new Pair(ParseAt(str, ref pos))));
        RegisterPairSource(lambda, sourceStart, pos);
        return lambda;
    }

    private static void ConsumeUnsignedDecimal(string str, ref int pos, ref bool hasDot)
    {
        while (pos < str.Length && (char.IsAsciiDigit(str[pos]) || (!hasDot && str[pos] == '.')))
        {
            if (str[pos] == '.') hasDot = true;
            pos++;
        }
    }

    private static bool ConsumeExponent(string str, ref int pos)
    {
        if (pos >= str.Length || (str[pos] != 'e' && str[pos] != 'E')) return false;
        int expStart = pos;
        pos++;
        if (pos < str.Length && (str[pos] == '+' || str[pos] == '-')) pos++;
        if (pos < str.Length && char.IsAsciiDigit(str[pos]))
        {
            while (pos < str.Length && char.IsAsciiDigit(str[pos])) pos++;
            return true;
        }
        pos = expStart;
        return false;
    }

    private static object ParseIntegerSpan(ReadOnlySpan<char> span) =>
        int.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue)
            ? (object)intValue
            : BigInteger.Parse(span, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static bool TryParseRationalLiteral(string str, ref int pos, ReadOnlySpan<char> numeratorSpan, out object value)
    {
        value = null!;
        if (pos >= str.Length || str[pos] != '/' || pos + 1 >= str.Length || !char.IsAsciiDigit(str[pos + 1]))
            return false;

        var numer = BigInteger.Parse(numeratorSpan, NumberStyles.Integer, CultureInfo.InvariantCulture);
        pos++;
        int denominatorStart = pos;
        while (pos < str.Length && char.IsAsciiDigit(str[pos])) pos++;
        var denom = BigInteger.Parse(str.AsSpan(denominatorStart, pos - denominatorStart), NumberStyles.Integer, CultureInfo.InvariantCulture);
        if (denom.IsZero) throw new LispException("division by zero in rational literal");
        value = new Rational(numer, denom).Normalize();
        return true;
    }

    private static object ParseNumericLiteral(string str, ref int pos)
    {
        int start = pos++;
        bool hasDot = false;
        ConsumeUnsignedDecimal(str, ref pos, ref hasDot);
        bool hasExp = ConsumeExponent(str, ref pos);

        var span = str.AsSpan(start, pos - start);
        if (hasDot || hasExp)
        {
            var realValue = double.Parse(span, NumberStyles.Float, CultureInfo.InvariantCulture);
            return ParseComplexSuffix(str, ref pos, realValue) ?? (object)realValue;
        }

        if (TryParseRationalLiteral(str, ref pos, span, out var rationalValue))
            return rationalValue;

        var integerValue = ParseIntegerSpan(span);
        double asDouble = integerValue is int intValue ? intValue : (double)(BigInteger)integerValue;
        return ParseComplexSuffix(str, ref pos, asDouble) ?? integerValue;
    }

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
            ConsumeUnsignedDecimal(str, ref pos, ref imagHasDot);
            bool imagHasExp = ConsumeExponent(str, ref pos);
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
        try
        {
            SkipWhitespace(str, ref pos);
            var result = ParseAt(str, ref pos);
            SkipWhitespace(str, ref pos);
            after = pos >= str.Length ? "" : str[pos..];
            return result;
        }
        catch (Exception ex)
        {
            after = "";
            throw ExceptionDisplay.Attach(ex, GetCurrentSourceSpan(pos, pos), null);
        }
    }

    private static object? ParseAt(string str, ref int pos)
    {
        SkipWhitespace(str, ref pos);
        if (pos >= str.Length) return null;

        char ch = str[pos];

        if (char.IsDigit(ch) || (ch == '-' && pos + 1 < str.Length && char.IsDigit(str[pos + 1])))
            return ParseNumericLiteral(str, ref pos);

        switch (ch)
        {
            case ';':
                while (pos < str.Length && str[pos] is not '\n' and not '\r') pos++;
                return ParseAt(str, ref pos);

            case ',':
                int start = pos;
                pos++;
                bool splicing = pos < str.Length && str[pos] == '@';
                if (splicing) pos++;
                var commaPair = Pair.Cons(Symbol.Create(splicing ? ",@" : ","), new Pair(ParseAt(str, ref pos)));
                RegisterPairSource(commaPair, start, pos);
                return commaPair;

            case '\'':
                return ParseQuoteLike("quote", str, ref pos);

            case '`':
                return ParseQuoteLike("quote", str, ref pos);

            case '#':
                return ParseHashDispatch(str, ref pos);

            case '"':
                return ParseStringLiteral(str, ref pos);

            case '(':
                return ParseListLiteral(str, ref pos);

            case ')':
                pos++;
                return null;

            case '\\':
                return ParseLambdaShorthand(str, ref pos);

            default:
            {
                int tokenStart = pos;
                while (pos < str.Length && !IsSymbolStopChar(str[pos])) pos++;
                var tok = str[tokenStart..pos];
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
