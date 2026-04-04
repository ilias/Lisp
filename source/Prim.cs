using System.Xml;

namespace Lisp;

public class Prim(Primitive prim, Pair? rands) : Expression
{
    public Primitive PrimDelegate => prim;
    public Pair? Rands => rands;

    public override object Eval(Env env)
    {
        InterpreterContext.RecordPrimCall();
        return prim(Eval_Rands(rands, env)!);
    }

    public override string ToString() => Util.Dump("prim", prim, rands);

    public static readonly Dictionary<string, Primitive> list = new()
    {
        ["LESSTHAN"] = LessThan_prim,
        ["new"] = New_Prim,
        ["get"] = Get_Prim,
        ["set"] = Set_Prim,
        ["call"] = Call_Prim,
        ["call-static"] = Call_Static_Prim,
        ["env"] = Env_Prim,
        ["disasm"] = Disasm_Prim,
        ["doc"] = Doc_Prim,
        ["apropos"] = Apropos_Prim,
        ["car"] = Car_Prim,
        ["cdr"] = Cdr_Prim,
        ["null?"] = NullQ_Prim,
        ["pair?"] = PairQ_Prim,
        ["cons"] = Cons_Prim,
        ["not"] = Not_Prim,
        ["+"] = Add_Prim,
        ["-"] = Sub_Prim,
        ["*"] = Mul_Prim,
        ["/"] = Div_Prim,
        ["<"] = Lt_Prim,
        [">"] = Gt_Prim,
        ["<="] = Le_Prim,
        [">="] = Ge_Prim,
        ["zero?"] = ZeroQ_Prim,
        ["number?"] = NumberQ_Prim,
        ["eqv?"] = EqvQ_Prim,
        ["todouble"] = ToDouble_Prim,
        ["tointeger"] = ToInt_Prim,
        ["="] = Eq_Prim,
        ["equal?"] = EqualQ_Prim,
        ["escape-continuation"] = EscapeContinuation_Prim,
        ["escape-continuation/tag"] = EscapeContinuationTag_Prim,
        ["dynamic-wind-body"] = DynamicWindBody_Prim,
        ["call/cc-full"] = CallCCFull_Prim,
        ["%raise"] = Raise_Prim,
        ["%try-handler"] = TryHandler_Prim,
        ["%make-error-object"] = MakeErrorObject_Prim,
        ["error-object?"] = ErrorObjectQ_Prim,
        ["error-object-message"] = ErrorObjectMessage_Prim,
        ["error-object-irritants"] = ErrorObjectIrritants_Prim,
        ["exact?"] = ExactQ_Prim,
        ["inexact?"] = InexactQ_Prim,
        ["rational?"] = RationalQ_Prim,
        ["integer?"] = IntegerQ_Prim,
        ["real?"] = RealQ_Prim,
        ["complex?"] = ComplexQ_Prim,
        ["isPrime"] = IsPrime_Prim,
        ["exact->inexact"] = ExactToInexact_Prim,
        ["inexact->exact"] = InexactToExact_Prim,
        ["p-adic"] = PAdic_Prim,
        ["numerator"] = Numerator_Prim,
        ["denominator"] = Denominator_Prim,
        ["real-part"] = RealPart_Prim,
        ["imag-part"] = ImagPart_Prim,
        ["make-rectangular"] = MakeRect_Prim,
        ["make-polar"] = MakePolar_Prim,
        ["magnitude"] = Magnitude_Prim,
        ["angle"] = Angle_Prim,
        ["floor"] = Floor_Prim,
        ["ceiling"] = Ceiling_Prim,
        ["round"] = Round_Prim,
        ["truncate"] = Truncate_Prim,
        ["load"] = Load_Prim,
    };

    public static object New_Prim(Pair args)
    {
        var type = Util.GetType(args.car!.ToString()!) ?? throw new LispException($"Unknown type: {args.car}");
        try
        {
            if (Pair.IsNull(args.cdr))
                return Activator.CreateInstance(type)!;
            var ctorArgs = args.cdr!.ToArray();
            for (int ci = 0; ci < ctorArgs.Length; ci++)
                if (ctorArgs[ci] is Symbol) ctorArgs[ci] = ctorArgs[ci].ToString()!;
            return Activator.CreateInstance(type, ctorArgs)!;
        }
        catch (TargetInvocationException tie)
        {
            throw ExceptionDisplay.WrapHostException(tie.InnerException ?? tie, $"new {type.FullName}");
        }
        catch (Exception ex)
        {
            throw ExceptionDisplay.WrapHostException(ex, $"new {type.FullName}");
        }
    }

    public static object LessThan_prim(Pair args) => Arithmetic.LessThan(args.car!, args.cdr!.car!);

    private static void PrintClosureDefinition(Closure closure, string name)
    {
        bool pp = ConsoleOutput.PrettyPrint;

        if (!string.IsNullOrEmpty(closure.DocComment))
            Console.WriteLine(closure.DocComment);

        // Build the "(define (name args...)" head.
        var head = new StringBuilder("(define (");
        head.Append(name);
        if (closure.ids != null)
            foreach (object p in closure.ids)
            { head.Append(' '); head.Append(p); }
        head.Append(')');

        if (!pp || closure.rawBody == null)
        {
            // Flat output (original behaviour).
            if (closure.rawBody != null)
                foreach (object b in closure.rawBody)
                { head.Append(' '); head.Append(Util.Dump(b)); }
            head.Append(')');
            Console.WriteLine(head);
        }
        else
        {
            // Pretty-print each body form indented under the head.
            int bodyIndent = 2;
            string indent = new string(' ', bodyIndent);
            Console.WriteLine(head);
            var bodyItems = closure.rawBody.ToArray();
            for (int i = 0; i < bodyItems.Length; i++)
            {
                bool isLast = i == bodyItems.Length - 1;
                var ppBody = Util.PrettyPrint(bodyItems[i], bodyIndent);
                Console.Write(indent);
                Console.WriteLine(isLast ? ppBody + ")" : ppBody);
            }
            if (bodyItems.Length == 0) Console.WriteLine(")");
        }
    }

    private static string BuildSignature(Closure closure, string name)
    {
        var sb = new StringBuilder("(");
        sb.Append(name);
        if (closure.ids != null)
            foreach (object p in closure.ids)
            { sb.Append(' '); sb.Append(p); }
        sb.Append(')');
        return sb.ToString();
    }

    private static string FirstDocLine(string? doc)
    {
        if (string.IsNullOrEmpty(doc)) return "";
        foreach (var line in doc.Split('\n'))
        {
            var trimmed = line.TrimStart(';', ' ');
            if (trimmed.Length > 0) return trimmed;
        }
        return "";
    }

    public static object Doc_Prim(Pair? args)
    {
        var sym = args?.car ?? throw new LispException("doc: expected a symbol name");
        var name = sym.ToString()!;
        var globalEnv = Program.RequireCurrent().initEnv;
        var symObj = Symbol.Create(name);

        // Check user-defined closures
        if (globalEnv.table.TryGetValue(symObj, out var val) && val is Closure closure)
        {
            if (!string.IsNullOrEmpty(closure.DocComment))
                Console.WriteLine(closure.DocComment);
            Console.WriteLine(BuildSignature(closure, name));
            return Pair.Empty;
        }

        // Check macros
        var macroDoc = Macro.GetDocComment(symObj);
        if (Macro.CurrentDefinitions.ContainsKey(symObj))
        {
            if (!string.IsNullOrEmpty(macroDoc))
                Console.WriteLine(macroDoc);
            Console.WriteLine($"(macro {name})");
            return Pair.Empty;
        }

        // Check built-in primitives
        if (list.ContainsKey(name))
        {
            Console.WriteLine($"[built-in]  {name}");
            return Pair.Empty;
        }

        Console.WriteLine($"doc: '{name}' not defined");
        return Pair.Empty;
    }

    public static object Apropos_Prim(Pair? args)
    {
        var query = args?.car?.ToString() ?? throw new LispException("apropos: expected a string");
        var globalEnv = Program.RequireCurrent().initEnv;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void AddResult(string n, string desc)
        {
            if (n.Contains(query, StringComparison.OrdinalIgnoreCase) && seen.Add(n))
                results[n] = desc;
        }

        // User-defined closures
        foreach (var kv in globalEnv.table)
        {
            if (kv.Value is not Closure cl) continue;
            var n = kv.Key.ToString()!;
            var sig = BuildSignature(cl, n);
            var first = FirstDocLine(cl.DocComment);
            AddResult(n, string.IsNullOrEmpty(first) ? sig : $"{sig}  -- {first}");
        }

        // Built-in primitives
        foreach (var n in list.Keys)
            AddResult(n, $"[built-in]  {n}");

        // Macros
        foreach (var kv in Macro.CurrentDefinitions)
        {
            var n = kv.Key.ToString()!;
            var first = FirstDocLine(Macro.GetDocComment(kv.Key));
            AddResult(n, string.IsNullOrEmpty(first) ? $"(macro {n})" : $"(macro {n})  -- {first}");
        }

        if (results.Count == 0)
        {
            Console.WriteLine($"apropos: no matches for \"{query}\"");
            return Pair.Empty;
        }
        foreach (var desc in results.Values)
            Console.WriteLine(desc);
        return Pair.Empty;
    }

    public static object Env_Prim(Pair? args)
    {
        var globalEnv = Program.RequireCurrent().initEnv;
        string? filter = Pair.IsNull(args) ? null : args?.car?.ToString();

        // Parse wildcard patterns: prefix* or *contains* or exact match.
        bool isWildcard = filter?.Contains('*') == true;
        string? wcPrefix = (isWildcard && filter!.EndsWith('*') && !filter.StartsWith('*'))
            ? filter[..^1] : null;
        string? wcContains = (isWildcard && filter!.StartsWith('*') && filter.EndsWith('*') && filter.Length > 2)
            ? filter[1..^1] : null;

        bool Matches(string name)
        {
            if (filter == null) return true;
            if (!isWildcard) return name == filter;
            if (wcPrefix != null) return name.StartsWith(wcPrefix, StringComparison.OrdinalIgnoreCase);
            if (wcContains != null) return name.Contains(wcContains, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        // User-defined closures
        foreach (var kv in globalEnv.table.OrderBy(k => k.Key.ToString()))
        {
            var name = kv.Key.ToString()!;
            if (!Matches(name)) continue;
            if (kv.Value is Closure closure)
            {
                PrintClosureDefinition(closure, name);
                Console.WriteLine();
            }
        }

        // Built-in primitives
        foreach (var name in list.Keys.Order())
        {
            if (!Matches(name)) continue;
            Console.WriteLine($"[built-in]  {name}");
        }

        return Pair.Empty;
    }

    public static object Disasm_Prim(Pair? args)
    {
        var arg = args?.car;
        switch (arg)
        {
            case VmClosure vc:
            {
                string name = vc.DebugName ?? "lambda";
                PrintClosureDefinition(vc, name);
                Console.WriteLine();
                string paramStr = vc.Chunk.Params != null ? Util.Dump(vc.Chunk.Params) : "()";
                Vm.Disassemble(vc.Chunk, $"closure  {name}{paramStr}");
                break;
            }
            case Closure cl:
            {
                string name = cl.DebugName ?? "lambda";
                PrintClosureDefinition(cl, name);
                Console.WriteLine();
                ConsoleOutput.WriteDisassemblyHeader("(tree-walk closure - no bytecode available)");
                break;
            }
            case Primitive prim:
                ConsoleOutput.WriteDisassemblyHeader($"(built-in primitive: {prim.Method.Name})");
                break;
            default:
                ConsoleOutput.WriteDisassemblyHeader($"(not a procedure: {Util.Dump(arg)})");
                break;
        }
        return Pair.Empty;
    }

    public static object Call_Prim(Pair args) => Util.CallMethod(args, false);
    public static object Call_Static_Prim(Pair args) => Util.CallMethod(args, true);

    public static object Load_Prim(Pair? args)
    {
        var rawPath = args?.car?.ToString() ?? throw new LispException("load: expected a filename");
        // optional 2nd arg: echo? (#t = print input lines in gray, default #f)
        var arg2 = args?.cdr?.car;
        bool echo = arg2 switch { null => false, bool bv => bv, _ => true };
        string path;
        if (Path.IsPathRooted(rawPath))
        {
            path = rawPath;
        }
        else
        {
            // Prefer application base directory (where init.ss and lib/ live),
            // fall back to the current working directory for user scripts.
            var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rawPath);
            path = File.Exists(basePath) ? basePath : rawPath;
        }
        if (!File.Exists(path))
            throw new LispException($"load: file not found: {rawPath}");
        var ctx = InterpreterContext.RequireCurrent();
        bool prevShow = ctx.ShowInputLines;
        var prevColor = ctx.InputLineColor;
        if (echo)
        {
            ctx.ShowInputLines = true;
            ctx.InputLineColor = ConsoleColor.DarkGray;
        }
        try
        {
            Program.RequireCurrent().Eval(File.ReadAllText(path), path);
        }
        finally
        {
            ctx.ShowInputLines = prevShow;
            ctx.InputLineColor = prevColor;
        }
        return Pair.Empty;
    }

    public static object Get_Prim(Pair args) => SetGet(args, BindingFlags.GetField | BindingFlags.GetProperty);
    public static object Set_Prim(Pair args) => SetGet(args, BindingFlags.SetField | BindingFlags.SetProperty);

    public static object SetGet(Pair arg, BindingFlags flags)
    {
        object[] index = arg.cdr?.cdr != null ? arg.cdr.cdr.ToArray() : [];
        Type? t = arg.car is Symbol ? Util.GetType(arg.car!.ToString()!) : arg.car!.GetType();
        if (t == null)
            throw new LispException("Unknown type: " + arg.car);
        BindingFlags f = BindingFlags.Default | flags;
        string memberName = arg.cdr!.car!.ToString()!;
        try
        {
            return t.InvokeMember(memberName, f, null, arg.car, index)!;
        }
        catch (MissingMethodException)
        {
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
        catch (TargetInvocationException tie)
        {
            throw ExceptionDisplay.WrapHostException(tie.InnerException ?? tie, memberName);
        }
        catch (Exception ex)
        {
            throw ExceptionDisplay.WrapHostException(ex, memberName);
        }
    }

    public static object Car_Prim(Pair args) =>
        args?.car is Pair p && !Pair.IsNull(p) ? p.car! : throw new LispException($"car: not a pair: {Util.Dump(args?.car)}");

    public static object Cdr_Prim(Pair args)
    {
        if (args?.car is Pair p && !Pair.IsNull(p)) return p.cdr!;
        throw new LispException($"cdr: not a pair: {Util.Dump(args?.car)}");
    }

    public static object NullQ_Prim(Pair args) => Pair.IsNull(args?.car);
    public static object PairQ_Prim(Pair args) => args?.car is Pair p2 && !Pair.IsNull(p2);
    public static object Cons_Prim(Pair args) => Pair.Cons(args!.car!, args.cdr!.car!);
    public static object Not_Prim(Pair args) => args?.car is bool b && !b;

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

    private static bool AllAdjacentPairsMatch(Pair? args, Func<object?, object?, bool> predicate)
    {
        if (args?.cdr == null) return true;
        for (var pair = args; pair?.cdr != null; pair = pair.cdr)
            if (!predicate(pair.car, pair.cdr!.car)) return false;
        return true;
    }

    public static object Lt_Prim(Pair args) =>
        AllAdjacentPairsMatch(args, (left, right) => Arithmetic.LessThan(left!, right!));

    public static object Gt_Prim(Pair args) =>
        AllAdjacentPairsMatch(args, (left, right) => Arithmetic.LessThan(right!, left!));

    public static object Le_Prim(Pair args) =>
        AllAdjacentPairsMatch(args, (left, right) => !Arithmetic.LessThan(right!, left!));

    public static object Ge_Prim(Pair args) =>
        AllAdjacentPairsMatch(args, (left, right) => !Arithmetic.LessThan(left!, right!));

    public static object ZeroQ_Prim(Pair args) => args?.car switch
    {
        int i => i == 0,
        double d => d == 0.0,
        BigInteger bi => bi.IsZero,
        Rational r => r.Numer.IsZero,
        Complex z => z == Complex.Zero,
        _ => false,
    };

    public static object NumberQ_Prim(Pair args) => args?.car is int or double or BigInteger or Rational or Complex;
    public static object EqvQ_Prim(Pair args) => object.Equals(args?.car, args?.cdr?.car);

    public static object ToDouble_Prim(Pair args) => args?.car switch
    {
        BigInteger bi => (double)bi,
        Rational r => r.ToDouble(),
        Complex z => z.Real,
        var x => Convert.ToDouble(x ?? 0.0),
    };

    public static object ToInt_Prim(Pair args) => args?.car switch
    {
        BigInteger bi => (int)bi,
        Rational r => (int)(r.Numer / r.Denom),
        double d => (int)d,
        var x => Convert.ToInt32(x ?? 0),
    };

    public static object Eq_Prim(Pair? args) =>
        AllAdjacentPairsMatch(args, (left, right) => EqCS(left!, right!));

    public static object EqualQ_Prim(Pair? args) =>
        AllAdjacentPairsMatch(args, EqualCS);

    private static bool EqCS(object a, object b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is int ia && b is int ib) return ia == ib;
        if (a is Complex || b is Complex || a is Rational || b is Rational || a is BigInteger || b is BigInteger)
            return Arithmetic.IsNumericEqual(a, b);
        if (a is int or double && b is int or double)
            return Convert.ToDouble(a) == Convert.ToDouble(b);
        try { return Convert.ToDouble(a) == Convert.ToDouble(b); }
        catch { return object.Equals(a, b); }
    }

    private static bool EqualCS(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (Pair.IsNull(a) && Pair.IsNull(b)) return true;
        if (Pair.IsNull(a) || Pair.IsNull(b)) return false;
        if (a is BigInteger || b is BigInteger)
            return (a is int or double or BigInteger) && (b is int or double or BigInteger) && Arithmetic.IsNumericEqual(a!, b!);
        if (a is int or double)
            return b is int or double && Convert.ToDouble(a) == Convert.ToDouble(b);
        if (a is Pair pa && b is Pair pb)
            return EqualCS(pa.car, pb.car) && EqualCS(pa.cdr, pb.cdr);
        if (a is ArrayList al && b is ArrayList bl)
        {
            if (al.Count != bl.Count) return false;
            for (int i = 0; i < al.Count; i++)
                if (!EqualCS(al[i], bl[i])) return false;
            return true;
        }
        return object.Equals(a, b);
    }

    public static object CallCCFull_Prim(Pair args)
    {
        var f = args?.car as Closure ?? throw new LispException("call/cc: argument must be a procedure");
        var cont = new Continuation(f);
        return cont.Run();
    }

    private static readonly object _legacyTag = new();
    public static object EscapeContinuation_Prim(Pair args) => throw new ContinuationException(args?.car, _legacyTag);
    public static object EscapeContinuationTag_Prim(Pair args) => throw new ContinuationException(args?.car, args?.cdr?.car ?? _legacyTag);

    public static object DynamicWindBody_Prim(Pair args)
    {
        var thunk = args?.car as Closure ?? throw new LispException("dynamic-wind: thunk must be a closure");
        var after = args?.cdr?.car as Closure ?? throw new LispException("dynamic-wind: after must be a closure");
        Exception? exc = null;
        object result = Pair.Empty;
        try { result = CallClosure(thunk); }
        catch (Exception e) { exc = e; }
        CallClosure(after);
        if (exc != null) ExceptionDispatchInfo.Capture(exc).Throw();
        return result;
    }

    private static object CallClosure(Closure c, Pair? args = null)
    {
        object r = c.Eval(args);
        while (r is TailCall tc)
        {
            InterpreterContext.RecordTailCall();
            r = tc.Closure.Eval(tc.Args);
        }
        return r;
    }

    public static object Raise_Prim(Pair args) => throw new RaiseException(args?.car ?? Pair.Empty);

    public static object TryHandler_Prim(Pair args)
    {
        var handlerObj = args?.car ?? throw new LispException("%try-handler: handler must be a procedure");
        var thunk = args?.cdr?.car as Closure ?? throw new LispException("%try-handler: thunk must be a procedure");

        object InvokeHandler(object value)
        {
            var argPair = new Pair(value, Pair.Empty);
            return handlerObj switch
            {
                Closure c => CallClosure(c, argPair),
                Primitive p => p(argPair),
                _ => throw new LispException("%try-handler: handler must be a procedure"),
            };
        }

        try
        {
            return CallClosure(thunk);
        }
        catch (ContinuationException) { throw; }
        catch (RaiseException re)
        {
            return InvokeHandler(re.Value);
        }
        catch (Exception e)
        {
            var eo = new ErrorObject(e.Message, Pair.Empty);
            return InvokeHandler(eo);
        }
    }

    public static object MakeErrorObject_Prim(Pair args)
    {
        var msg = args?.car?.ToString() ?? "";
        var irritants = args?.cdr?.car ?? Pair.Empty;
        return new ErrorObject(msg, irritants);
    }

    public static object ErrorObjectQ_Prim(Pair args) => args?.car is ErrorObject;
    public static object ErrorObjectMessage_Prim(Pair args) =>
        (args?.car as ErrorObject ?? throw new LispException("error-object-message: not an error object")).Message;
    public static object ErrorObjectIrritants_Prim(Pair args) =>
        (args?.car as ErrorObject ?? throw new LispException("error-object-irritants: not an error object")).Irritants;

    public static object ExactQ_Prim(Pair args) => args?.car is int or BigInteger or Rational;
    public static object InexactQ_Prim(Pair args) => args?.car is double or Complex;
    public static object RationalQ_Prim(Pair args) =>
        args?.car is int or BigInteger or Rational ||
        (args?.car is double d1 && !double.IsNaN(d1) && !double.IsInfinity(d1));
    public static object IntegerQ_Prim(Pair args) =>
        args?.car is int or BigInteger ||
        (args?.car is Rational ri && ri.Denom.IsOne) ||
        (args?.car is double di && !double.IsNaN(di) && !double.IsInfinity(di) && di == Math.Floor(di));
    public static object ComplexQ_Prim(Pair args) => NumberQ_Prim(args);
    public static object RealQ_Prim(Pair args) =>
        args?.car is int or BigInteger or Rational or double ||
        (args?.car is Complex zr && zr.Imaginary == 0.0);

    public static object IsPrime_Prim(Pair args) => Util.IsPrimeInteger(args?.car);

    public static object ExactToInexact_Prim(Pair args) => args?.car switch
    {
        int i => (double)i,
        BigInteger bi => (double)bi,
        Rational r => r.ToDouble(),
        double d => d,
        Complex z => z,
        var x => Convert.ToDouble(x),
    };

    public static object InexactToExact_Prim(Pair args) => args?.car switch
    {
        int or BigInteger or Rational => args.car!,
        double d => Arithmetic.DoubleToExact(d),
        Complex z => z.Imaginary == 0.0 ? Arithmetic.DoubleToExact(z.Real) : throw new LispException("inexact->exact: cannot convert complex with nonzero imaginary"),
        var x => throw new LispException($"inexact->exact: not a number: {Util.Dump(x)}"),
    };

    public static object PAdic_Prim(Pair? args)
    {
        int radix = args?.car switch
        {
            int i => i,
            BigInteger bi when bi >= int.MinValue && bi <= int.MaxValue => (int)bi,
            _ => throw new LispException("p-adic: expected an exact integer base"),
        };

        int? precision = args?.cdr?.car switch
        {
            null => null,
            int i => i,
            BigInteger bi when bi >= int.MinValue && bi <= int.MaxValue => (int)bi,
            _ => throw new LispException("p-adic: precision must be a positive exact integer"),
        };

        if (args?.cdr?.cdr != null)
            throw new LispException("p-adic: expected (p-adic base [digits])");

        Util.SetNumericDisplay(radix, precision);
        return Pair.Empty;
    }

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
            if (double.IsNaN(d) || double.IsInfinity(d)) return 1.0;
            var (_, den) = Arithmetic.GetNumerDenom(Arithmetic.DoubleToExact(d));
            return (double)den;
        }
        var (_, denom) = Arithmetic.GetNumerDenom(x ?? throw new LispException("denominator: missing argument"));
        return Arithmetic.Normalize(denom);
    }

    public static object RealPart_Prim(Pair args) => args?.car switch
    {
        Complex z => z.Real,
        int i => (double)i,
        BigInteger bi => (double)bi,
        Rational r => r.ToDouble(),
        double d => d,
        var x => throw new LispException($"real-part: not a number: {Util.Dump(x)}"),
    };

    public static object ImagPart_Prim(Pair args) => args?.car switch
    {
        Complex z => z.Imaginary,
        int or BigInteger or Rational or double => 0.0,
        var x => throw new LispException($"imag-part: not a number: {Util.Dump(x)}"),
    };

    public static object MakeRect_Prim(Pair args)
    {
        var re = args?.car ?? throw new LispException("make-rectangular: missing real part");
        var im = args?.cdr?.car ?? throw new LispException("make-rectangular: missing imag part");
        if (im is int ii && ii == 0) return re;
        if (im is BigInteger bii && bii.IsZero) return re;
        if (im is Rational ri && ri.Numer.IsZero) return re;
        return new Complex(Arithmetic.D(re), Arithmetic.D(im));
    }

    public static object MakePolar_Prim(Pair args)
    {
        var mag = Arithmetic.D(args?.car ?? throw new LispException("make-polar: missing magnitude"));
        var angle = Arithmetic.D(args?.cdr?.car ?? throw new LispException("make-polar: missing angle"));
        return Complex.FromPolarCoordinates(mag, angle);
    }

    public static object Magnitude_Prim(Pair args) => args?.car switch
    {
        Complex z => Complex.Abs(z),
        int i => i < 0 ? (i == int.MinValue ? (object)(-(BigInteger)i) : -i) : i,
        BigInteger bi => BigInteger.Abs(bi),
        Rational r => r.Numer < 0 ? new Rational(-r.Numer, r.Denom) : r,
        double d => Math.Abs(d),
        var x => throw new LispException($"magnitude: not a number: {Util.Dump(x)}"),
    };

    public static object Angle_Prim(Pair args) => args?.car switch
    {
        Complex z => z.Phase,
        double d => d >= 0.0 ? 0.0 : Math.PI,
        int i => i >= 0 ? 0.0 : Math.PI,
        BigInteger bi => bi >= 0 ? 0.0 : Math.PI,
        Rational r => r.Numer >= 0 ? 0.0 : Math.PI,
        var x => throw new LispException($"angle: not a number: {Util.Dump(x)}"),
    };

    public static object Floor_Prim(Pair args) => Arithmetic.FloorObj(args?.car!);
    public static object Ceiling_Prim(Pair args) => Arithmetic.CeilingObj(args?.car!);
    public static object Round_Prim(Pair args) => Arithmetic.RoundObj(args?.car!);
    public static object Truncate_Prim(Pair args) => Arithmetic.TruncateObj(args?.car!);
}
