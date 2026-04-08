namespace Lisp;

public static partial class Util
{
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
            Pair { car: Symbol quot } p when ReferenceEquals(quot, _sQuote) => $"'{Dump(p.CdrPair!.car)}",
            ICollection => FormatCollection(exp),
            _ => exp?.ToString() ?? "()",
        };
    }

    private static string FormatCollection(object? exp)
    {
        if (exp is ArrayList al)
        {
            var sb = new StringBuilder("#(");
            foreach (object? o in al) sb.Append(Dump(o)).Append(' ');
            if (sb.Length > 2) sb.Length--;
            sb.Append(')');
            return sb.ToString();
        }
        if (exp is Pair pair)
        {
            var sb = new StringBuilder("(");
            var current = pair;
            bool first = true;
            while (current != null && !Pair.IsNull(current))
            {
                if (!first) sb.Append(' ');
                first = false;
                sb.Append(Dump(current.car));
                if (current.cdr == null) break;
                if (current.CdrPair == null)
                {
                    // Dotted pair tail
                    sb.Append(" . ");
                    sb.Append(Dump(current.cdr));
                    break;
                }
                current = current.CdrPair;
            }
            sb.Append(')');
            return sb.ToString();
        }
        // Fallback for other ICollection implementations.
        {
            var sb = new StringBuilder("(");
            foreach (object? o in (ICollection)exp!) sb.Append(Dump(o)).Append(' ');
            if (sb.Length > 1) sb.Length--;
            sb.Append(')');
            return sb.ToString();
        }
    }

    // -----------------------------------------------------------------------
    // Pretty printing
    // -----------------------------------------------------------------------

    // Column budget used by PrettyPrint; 0 = auto-detect terminal width.
    private static int _prettyPrintWidth = 0;
    public static int PrettyPrintWidth
    {
        get
        {
            if (_prettyPrintWidth > 0) return _prettyPrintWidth;
            try { int w = Console.WindowWidth; return w > 20 ? w - 1 : 80; }
            catch { return 80; }
        }
        set => _prettyPrintWidth = value;
    }

    /// <summary>Format <paramref name="exp"/> as indented s-expression text.</summary>
    public static string PrettyPrint(object? exp)
    {
        var sb = new StringBuilder();
        PrettyPrintTo(sb, exp, indent: 0, column: 0);
        return sb.ToString();
    }

    /// <summary>
    /// Format <paramref name="exp"/> as indented s-expression text, treating
    /// <paramref name="startIndent"/> as the current column (for line-budget checks).
    /// </summary>
    public static string PrettyPrint(object? exp, int startIndent)
    {
        var sb = new StringBuilder();
        PrettyPrintTo(sb, exp, indent: startIndent, column: startIndent);
        return sb.ToString();
    }

    private static int PrettyPrintTo(StringBuilder sb, object? exp, int indent, int column)
    {
        if (Pair.IsNull(exp) || exp is not Pair pair)
        {
            var atom = Dump(exp);
            sb.Append(atom);
            return column + atom.Length;
        }

        // Check if the whole list fits on the remaining part of the current line.
        var flat = Dump(exp);
        if (column + flat.Length <= PrettyPrintWidth)
        {
            sb.Append(flat);
            return column + flat.Length;
        }

        // Special indent rules for common special forms and function calls.
        // Head: print inline, then pick indentation for remaining items.
        string prefix = exp is ArrayList ? "#(" : "(";
        sb.Append(prefix);
        int col = column + prefix.Length;

        // Print the head.
        var head = pair.car;
        bool isSymHead = head is Symbol;
        var headStr = Dump(head);
        sb.Append(headStr);
        col += headStr.Length;

        // Determine the body indent: aligned after head for short symbol heads,
        // otherwise standard 2-space indent.
        int bodyIndent = isSymHead && headStr.Length <= 12
            ? indent + prefix.Length + headStr.Length + 1
            : indent + 2;

        var rest = pair.cdr;
        bool first = true;
        while (!Pair.IsNull(rest) && rest is Pair rp)
        {
            if (first)
            {
                // Try to put first argument on the same line.
                var firstFlat = Dump(rp.car);
                if (col + 1 + firstFlat.Length <= PrettyPrintWidth)
                {
                    sb.Append(' ');
                    col = PrettyPrintTo(sb, rp.car, bodyIndent, col + 1);
                    first = false;
                    rest = rp.cdr;
                    continue;
                }
            }

            // New line for each subsequent element.
            sb.AppendLine();
            sb.Append(' ', bodyIndent);
            col = bodyIndent;
            col = PrettyPrintTo(sb, rp.car, bodyIndent, col);
            first = false;
            rest = rp.cdr;
        }

        // Dotted tail?
        if (!Pair.IsNull(rest) && rest != null)
        {
            sb.AppendLine();
            sb.Append(' ', bodyIndent);
            sb.Append(".");
            sb.AppendLine();
            sb.Append(' ', bodyIndent);
            PrettyPrintTo(sb, rest, bodyIndent, bodyIndent);
        }

        sb.Append(')');
        return col + 1;
    }

}
