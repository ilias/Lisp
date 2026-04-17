namespace Lisp;

public static class BytecodeCompiler
{
    private static readonly Primitive _consPrim = Prim.TryGetPrimitive("cons", out var cons)
        ? cons
        : throw new InvalidOperationException("missing primitive 'cons'");
    private static readonly Primitive _tryPrim = TryThunkPrimitive;
    private static readonly Primitive _tryContPrim = TryContThunkPrimitive;

    internal static bool IsSimpleSectionExpr(Expression expr) => expr is Lit or Var;

    private static Expression? ChildSection(Expression parent, Expression child) =>
        IsSimpleSectionExpr(child) ? parent : null;

    private static void EmitInterpreted(Chunk chunk, Expression expr, bool tail, Expression? section)
    {
        var sectionExpr = section ?? expr;
        InterpreterContext.RecordInterpEmit(expr);
        chunk.Emit(OpCode.INTERP, chunk.AddAst(expr), sectionExpr);
        if (tail) chunk.Emit(OpCode.RETURN, source: sectionExpr);
    }

    private static int CompileArguments(Pair? args, Chunk chunk, Expression parentSection)
    {
        int argc = 0;
        if (args != null)
            foreach (object arg in args)
            {
                var argExpr = (Expression)arg;
                Compile(argExpr, chunk, tail: false, section: ChildSection(parentSection, argExpr));
                argc++;
            }
        return argc;
    }

    private static void CompileBodySequence(object[] bodyList, Chunk chunk)
    {
        for (int i = 0; i < bodyList.Length; i++)
        {
            bool isLast = i == bodyList.Length - 1;
            var bodyExpr = (Expression)bodyList[i];
            Compile(bodyExpr, chunk, tail: isLast, section: null);
            if (!isLast) chunk.Emit(OpCode.POP, source: bodyExpr);
        }

        if (bodyList.Length != 0) return;
        chunk.Emit(OpCode.LOAD_CONST, chunk.AddConst(Pair.Empty));
        chunk.Emit(OpCode.RETURN);
    }

    public static Chunk CompileTop(Expression expr)
    {
        var chunk = new Chunk { Params = null, Arity = 0, RootExpr = expr };
        Compile(expr, chunk, tail: false, section: null);
        chunk.Emit(OpCode.RETURN, source: expr);
        return chunk;
    }

    private static void Compile(Expression expr, Chunk chunk, bool tail, Expression? section)
    {
        var sectionExpr = section ?? expr;
        switch (expr)
        {
            case Lit lit:
                CompileLit(lit, chunk, sectionExpr);
                break;
            case Var v:
                chunk.Emit(OpCode.LOAD_VAR, chunk.AddSym(v.id), sectionExpr);
                break;
            case Define def:
                CompileDefine(def, chunk);
                break;
            case Assignment asgn:
                Compile(asgn.ValExpr, chunk, tail: false, section: asgn);
                chunk.Emit(OpCode.STORE_VAR, chunk.AddSym(asgn.Id), asgn);
                break;
            case Lambda lam:
                CompileLambda(lam, chunk, sectionExpr);
                break;
            case If ifExpr:
                CompileIf(ifExpr, chunk, tail);
                return;
            case Try tryExpr:
                CompileTry(tryExpr, chunk, tail, sectionExpr);
                return;
            case TryCont tryContExpr:
                CompileTryCont(tryContExpr, chunk, tail, sectionExpr);
                return;
            case LetSyntax letSyntaxExpr:
                CompileLetSyntax(letSyntaxExpr, chunk, tail, sectionExpr);
                return;
            case Evaluate evaluateExpr:
                CompileEvaluate(evaluateExpr, chunk, tail, sectionExpr);
                return;
            case App app:
                CompileApp(app, chunk, tail, section);
                return;
            case Prim prim:
                CompilePrim(prim, chunk, section);
                break;
            default:
                chunk.Emit(OpCode.INTERP, chunk.AddAst(expr), sectionExpr);
                break;
        }

        if (tail) chunk.Emit(OpCode.RETURN, source: sectionExpr);
    }

    private static void CompileLit(Lit lit, Chunk chunk, Expression section)
    {
        if (!HasComma(lit.Datum))
            chunk.Emit(OpCode.LOAD_CONST, chunk.AddConst(lit.Datum), section);
        else
            CompileLoweredQuasiquote(lit, chunk, section);
    }

    private static void CompileLoweredQuasiquote(Lit lit, Chunk chunk, Expression section)
    {
        var lowered = LowerQuasiquoteExpression(lit.Datum);
        Compile(lowered, chunk, tail: false, section);
    }

    private static Expression LowerQuasiquoteExpression(object? datum) => datum switch
    {
        Pair pair when !Pair.IsNull(pair) => LowerQuasiquoteList(pair),
        _ => new Lit(datum),
    };

    private static Expression LowerQuasiquoteList(Pair pair)
    {
        List<(Expression Expr, bool Splice)> segments = [];
        foreach (object? item in pair)
            segments.Add(LowerQuasiquoteSegment(item));

        if (segments.Count == 0)
            return new Lit(Pair.Empty);

        bool hasSplice = segments.Any(segment => segment.Splice);
        if (!hasSplice)
        {
            Expression listExpr = new Lit(Pair.Empty);
            for (int index = segments.Count - 1; index >= 0; index--)
                listExpr = MakeCons(segments[index].Expr, listExpr);
            return listExpr;
        }

        Pair? appendArgs = null;
        Pair? appendArgsTail = null;
        foreach (var segment in segments)
        {
            var segmentExpr = segment.Splice ? segment.Expr : MakeSingletonList(segment.Expr);
            Pair.AppendTail(ref appendArgs, ref appendArgsTail, segmentExpr);
        }

        return new Prim(QuasiAppend, appendArgs);
    }

    private static (Expression Expr, bool Splice) LowerQuasiquoteSegment(object? item)
    {
        if (item is Pair pair)
        {
            if (Symbol.IsEqual(",", pair.car))
                return (Expression.Parse(pair.CdrPair!.car), false);
            if (Symbol.IsEqual(",@", pair.car))
                return (Expression.Parse(pair.CdrPair!.car), true);
        }

        return (LowerQuasiquoteExpression(item), false);
    }

    private static Expression MakeCons(Expression carExpr, Expression cdrExpr)
        => new Prim(_consPrim, new Pair(carExpr, new Pair(cdrExpr)));

    private static Expression MakeSingletonList(Expression expr)
        => MakeCons(expr, new Lit(Pair.Empty));

    private static Expression LowerCallArgumentList(Pair? args)
    {
        if (args == null)
            return new Lit(Pair.Empty);

        Pair? appendArgs = null;
        Pair? appendArgsTail = null;
        bool hasSplice = false;
        foreach (object arg in args)
        {
            Expression segmentExpr;
            if (arg is CommaAt splice)
            {
                hasSplice = true;
                segmentExpr = LowerCommaAtExpression(splice);
            }
            else
            {
                segmentExpr = MakeSingletonList((Expression)arg);
            }
            Pair.AppendTail(ref appendArgs, ref appendArgsTail, segmentExpr);
        }

        if (!hasSplice)
        {
            Expression listExpr = new Lit(Pair.Empty);
            var exprs = args.ToArray();
            for (int index = exprs.Length - 1; index >= 0; index--)
                listExpr = MakeCons((Expression)exprs[index], listExpr);
            return listExpr;
        }

        return new Prim(QuasiAppend, appendArgs);
    }

    private static Expression LowerCommaAtExpression(CommaAt splice)
    {
        var spliceArgs = splice.Rands;
        if (spliceArgs is { car: Expression singleExpr } && spliceArgs.CdrPair == null)
            return singleExpr;

        return splice;
    }

    private static object QuasiAppend(Pair args)
    {
        Pair? head = null;
        Pair? tail = null;
        foreach (object? segment in args)
        {
            if (Pair.IsNull(segment))
                continue;

            if (segment is Pair pair)
            {
                foreach (object? item in pair)
                    Pair.AppendTail(ref head, ref tail, item);
                continue;
            }

            Pair.AppendTail(ref head, ref tail, segment);
        }

        return head ?? Pair.Empty;
    }

    private static bool HasComma(object? obj)
    {
        if (obj is not Pair p) return false;
        for (var cur = p; cur != null; cur = cur.CdrPair)
            if (cur.car is Pair inner && (Symbol.IsEqual(",", inner.car) || Symbol.IsEqual(",@", inner.car)))
                return true;
            else if (HasComma(cur.car))
                return true;
        return false;
    }

    private static void CompileDefine(Define def, Chunk chunk)
    {
        var sym = def.NameSym;
        var valExpr = def.ValExpr;
        Compile(valExpr, chunk, tail: false, section: def);
        chunk.Emit(OpCode.DEFINE_VAR, chunk.AddSym(sym), def);
    }

    private static void CompileLambda(Lambda lam, Chunk chunk, Expression section)
    {
        var proto = CompileLambdaChunk(lam);
        chunk.Emit(OpCode.MAKE_CLOSURE, chunk.AddProto(proto), section);
    }

    public static Chunk CompileLambdaChunk(Lambda lam)
    {
        Pair? rawBodyPair = lam.Body != null ? lam.RawBody : null;
        var proto = new Chunk
        {
            RootExpr = lam,
            Params = lam.Ids,
            Arity = lam.Ids?.Count ?? 0,
            SourceBody = rawBodyPair,
        };
        var bodyList = lam.Body?.ToArray() ?? [];
        CompileBodySequence(bodyList, proto);
        return proto;
    }

    private static void CompileIf(If ifExpr, Chunk chunk, bool tail)
    {
        Compile(ifExpr.Test, chunk, tail: false, section: null);
        int jumpFalse = chunk.Emit(OpCode.JUMP_IF_FALSE, 0, ChildSection(ifExpr, ifExpr.Test) ?? ifExpr.Test);
        Compile(ifExpr.ThenExpr, chunk, tail, section: null);
        int jumpEnd = chunk.Emit(OpCode.JUMP, 0, ifExpr.ThenExpr);
        chunk.Patch(jumpFalse, chunk.Code.Count);
        Compile(ifExpr.ElseExpr, chunk, tail, section: null);
        chunk.Patch(jumpEnd, chunk.Code.Count);
    }

    private static void CompileTry(Try tryExpr, Chunk chunk, bool tail, Expression section)
    {
        var lowered = new Prim(
            _tryPrim,
            new Pair(MakeThunk(tryExpr.TryExpr), new Pair(MakeThunk(tryExpr.CatchExpr))));
        Compile(lowered, chunk, tail, section);
    }

    private static void CompileTryCont(TryCont tryContExpr, Chunk chunk, bool tail, Expression section)
    {
        Pair args = new(MakeThunk(tryContExpr.TryExpr), new Pair(MakeThunk(tryContExpr.CatchExpr)));
        if (tryContExpr.TagExpr != null)
            args = new Pair(tryContExpr.TagExpr, args);
        var lowered = new Prim(_tryContPrim, args);
        Compile(lowered, chunk, tail, section);
    }

    private static void CompileLetSyntax(LetSyntax letSyntaxExpr, Chunk chunk, bool tail, Expression section)
    {
        var expandedBody = letSyntaxExpr.ExpandBodyExpressions();
        if (expandedBody.Length == 0)
        {
            chunk.Emit(OpCode.LOAD_CONST, chunk.AddConst(Pair.Empty), section);
            if (tail) chunk.Emit(OpCode.RETURN, source: section);
            return;
        }

        for (int i = 0; i < expandedBody.Length; i++)
        {
            bool isLast = i == expandedBody.Length - 1;
            var bodyExpr = expandedBody[i];
            Compile(bodyExpr, chunk, tail && isLast, section: null);
            if (!isLast) chunk.Emit(OpCode.POP, source: bodyExpr);
        }
    }

    private static void CompileEvaluate(Evaluate evaluateExpr, Chunk chunk, bool tail, Expression section)
    {
        Compile(evaluateExpr.DatumExpr, chunk, tail: false, section: ChildSection(section, evaluateExpr.DatumExpr));
        chunk.Emit(OpCode.EVAL, source: section);
        if (tail) chunk.Emit(OpCode.RETURN, source: section);
    }

    private static Lambda MakeThunk(Expression expr) => new(null, new Pair(expr));

    private static object CallThunkClosure(object? proc)
    {
        if (proc is not Closure closure)
            throw new LispException("internal try helper expected a closure");

        object result = closure.Eval(null);
        while (result is TailCall tc)
        {
            InterpreterContext.RecordTailCall();
            result = tc.Closure.Eval(tc.Args);
        }
        return result;
    }

    private static object TryThunkPrimitive(Pair args)
    {
        var tryThunk = args.car;
        var catchThunk = args.CdrPair?.car;
        try
        {
            return CallThunkClosure(tryThunk);
        }
        catch (ContinuationException)
        {
            throw;
        }
        catch (Exception ex) when (ExceptionDisplay.IsCatchableByTry(ex))
        {
            return CallThunkClosure(catchThunk);
        }
    }

    private static object TryContThunkPrimitive(Pair args)
    {
        object? tagValue;
        object? tryThunk;
        object? catchThunk;
        if (args.CdrPair?.CdrPair != null)
        {
            tagValue = args.car;
            tryThunk = args.CdrPair.car;
            catchThunk = args.CdrPair.CdrPair.car;
        }
        else
        {
            tagValue = null;
            tryThunk = args.car;
            catchThunk = args.CdrPair?.car;
        }

        try
        {
            return CallThunkClosure(tryThunk);
        }
        catch (ContinuationException ce)
        {
            if (tagValue == null || ReferenceEquals(ce.Tag, tagValue))
                return CallThunkClosure(catchThunk);
            throw;
        }
    }

    private static void CompileApp(App app, Chunk chunk, bool tail, Expression? section)
    {
        var sectionExpr = section ?? app;
        Compile(app.Rator, chunk, tail: false, section: ChildSection(sectionExpr, app.Rator));
        if (TryCompileListCallArguments(app.Rands, chunk, sectionExpr))
        {
            chunk.Emit(tail ? OpCode.TAIL_CALL_LIST : OpCode.CALL_LIST, source: sectionExpr);
            return;
        }

        int argc = CompileArguments(app.Rands, chunk, sectionExpr);
        chunk.Emit(tail ? OpCode.TAIL_CALL : OpCode.CALL, argc, sectionExpr);
    }

    private static void CompilePrim(Prim prim, Chunk chunk, Expression? section)
    {
        var sectionExpr = section ?? prim;
        if (TryCompileListCallArguments(prim.Rands, chunk, sectionExpr))
        {
            chunk.Emit(OpCode.PRIM_LIST, chunk.AddPrim(prim.PrimDelegate), sectionExpr);
            return;
        }

        int argc = CompileArguments(prim.Rands, chunk, sectionExpr);
        int primIdx = chunk.AddPrim(prim.PrimDelegate);
        chunk.Emit(OpCode.PRIM, Instruction.PackPrimOperand(primIdx, argc), sectionExpr);
    }

    private static bool TryCompileListCallArguments(Pair? args, Chunk chunk, Expression section)
    {
        if (!HasCommaAt(args))
            return false;

        var argListExpr = LowerCallArgumentList(args);
        Compile(argListExpr, chunk, tail: false, section: section);
        return true;
    }

    private static bool HasCommaAt(Pair? args)
    {
        if (args == null) return false;
        foreach (object arg in args)
            if (arg is CommaAt) return true;
        return false;
    }
}

