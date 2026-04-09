namespace Lisp;

public static partial class Util
{
    public static string ParseRemainder
    {
        get => _parseRemainder ?? "";
        set => _parseRemainder = value;
    }

    [ThreadStatic] private static string? _parseRemainder;
    [ThreadStatic] private static string? _pendingDocComment;

    public static void SetPendingDocComment(string? comment) => _pendingDocComment = comment;

    public static string? ConsumePendingDocComment()
    {
        var c = _pendingDocComment;
        _pendingDocComment = null;
        return c;
    }

    /// <summary>
    /// Applies any pending doc-comment and debug name to a freshly defined value.
    /// If <paramref name="value"/> is a <see cref="Closure"/>, its <c>DebugName</c> is
    /// set to <paramref name="name"/> when not already assigned, and its <c>DocComment</c>
    /// is set from the pending doc-comment when not already set.
    /// In all cases the pending doc-comment is consumed so it is not reused.
    /// </summary>
    public static void ApplyDocComment(object? value, string name)
    {
        if (value is Closure closure)
        {
            if (string.IsNullOrEmpty(closure.DebugName))
                closure.DebugName = name;
            if (closure.DocComment == null)
                closure.DocComment = ConsumePendingDocComment();
            else
                ConsumePendingDocComment();
        }
        else
        {
            ConsumePendingDocComment();
        }
    }

    /// <summary>
    /// Scan <paramref name="text"/> and return the block of consecutive comment lines (;...) that
    /// immediately precede the first non-comment token, provided no blank line separates that
    /// comment block from the token.  Returns null if no such block exists.
    /// </summary>
    public static string? ExtractDocComment(string text)
    {
        int pos = 0;
        List<string>? pending = null;
        bool afterBlankOrStart = true;

        while (pos < text.Length)
        {
            // Skip horizontal whitespace on this line
            while (pos < text.Length && text[pos] is ' ' or '\t') pos++;

            if (pos >= text.Length) break;

            char ch = text[pos];

            if (ch is '\r' or '\n')
            {
                // Blank line: discard any comment block collected since the last blank line
                pending = null;
                afterBlankOrStart = true;
                if (ch is '\r' && pos + 1 < text.Length && text[pos + 1] is '\n') pos++;
                pos++;
            }
            else if (ch is ';')
            {
                int lineStart = pos;
                while (pos < text.Length && text[pos] is not '\r' and not '\n') pos++;
                string line = text[lineStart..pos];
                if (afterBlankOrStart)
                    pending = [line];
                else
                    (pending ??= []).Add(line);
                afterBlankOrStart = false;
                if (pos < text.Length && text[pos] is '\r') pos++;
                if (pos < text.Length && text[pos] is '\n') pos++;
            }
            else
            {
                // First non-whitespace, non-comment character: token starts here.
                // pending is the doc comment only if it was immediately before (no blank line between).
                return pending != null ? string.Join(Environment.NewLine, pending) : null;
            }
        }
        return null;
    }

    public static object? ParseOne(string content)
    {
        var result = Parse(content, out var after);
        ParseRemainder = after ?? "";
        return result;
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
        char dispatch = str[pos++];
        switch (dispatch)
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
            case 't':
            case 'T':
                if (pos < str.Length && !IsSymbolStopChar(str[pos]))
                    throw new LispException("Invalid boolean literal; expected #t or #f");
                return true;
            case 'f':
            case 'F':
                if (pos < str.Length && !IsSymbolStopChar(str[pos]))
                    throw new LispException("Invalid boolean literal; expected #t or #f");
                return false;
            default:
                throw new LispException($"Unknown reader dispatch: #{dispatch}");
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
                if (pos >= str.Length) break;
                switch (str[pos])
                {
                    case 'n': cVal.Append('\n'); break;
                    case 'r': cVal.Append('\r'); break;
                    case 't': cVal.Append('\t'); break;
                    case 'a': cVal.Append('\a'); break;
                    case 'b': cVal.Append('\b'); break;
                    case '0': cVal.Append('\0'); break;
                    case '"': cVal.Append('"'); break;
                    case '\\': cVal.Append('\\'); break;
                    case 'x':
                        // R7RS hex escape: \xHHHH; (hex digits terminated by semicolon)
                        pos++;
                        int hexStart = pos;
                        while (pos < str.Length && str[pos] != ';' && char.IsAsciiHexDigit(str[pos])) pos++;
                        if (pos < str.Length && str[pos] == ';' && pos > hexStart)
                        {
                            var codePoint = int.Parse(str.AsSpan(hexStart, pos - hexStart), NumberStyles.HexNumber);
                            cVal.Append(char.ConvertFromUtf32(codePoint));
                        }
                        else
                        {
                            // Malformed \x sequence — output literally and rewind to re-scan
                            cVal.Append('x');
                            pos = hexStart - 1;
                        }
                        break;
                    default: cVal.Append(str[pos]); break;
                }
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
            // Dotted pair notation: (a b . c) — the dot symbol between elements sets the cdr directly.
            if (item is Symbol dotSym && dotSym.ToString() == "." && retvalTail != null)
            {
                var tail = ParseAt(str, ref pos);
                retvalTail.cdr = tail;
                ParseAt(str, ref pos); // consume the closing ')'
                break;
            }
            var node = new Pair(item);
            if (retvalTail == null) retval = retvalTail = node;
            else { retvalTail.cdr = node; retvalTail = node; }
        }
        if (retval?.CdrPair == null && retval?.car is Pair lp && lp.car is string ls && ls == "LAMBDA")
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
