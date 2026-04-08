namespace Lisp;

internal struct CallFrame(Chunk chunk, Env env, int stackBase)
{
    public Chunk Chunk = chunk;
    public int Pc;
    public Env Env = env;
    public int StackBase = stackBase;
    public Expression? CallSite;
    public string? ProcedureName;
}

public static class Vm
{
    private const int MaxFrames = 10_000;
    public static bool DisassemblyVerbose { get; set; }

    private static void Push(ref object?[] stack, ref int sp, object? value)
    {
        EnsureStack(ref stack, sp);
        stack[sp++] = value;
    }

    private static object ResolveTailCalls(object value)
    {
        var result = value;
        while (result is TailCall tc)
        {
            InterpreterContext.RecordTailCall();
            result = tc.Closure.Eval(tc.Args);
        }
        return result;
    }

    private static void ReturnToCaller(ref object?[] stack, ref int sp, ref int frameCount, in CallFrame frame)
    {
        var retVal = sp > frame.StackBase ? stack[--sp] : Pair.Empty;
        sp = frame.StackBase;
        frameCount--;
        Push(ref stack, ref sp, retVal);
    }

    private static Pair? NormalizeArgList(object? rawArgs) => rawArgs switch
    {
        null => null,
        Pair pair when Pair.IsNull(pair) => null,
        Pair pair => pair,
        _ => new Pair(rawArgs),
    };

    private static void InvokeVmClosure(
        VmClosure vmClosure,
        Pair? args,
        bool tail,
        Expression? callSite,
        ref object?[] stack,
        ref int sp,
        ref CallFrame[] frames,
        ref int frameCount,
        ref CallFrame frame,
        ref List<Instruction> code,
        int procIdx)
    {
        var callEnv = vmClosure.env.Extend(vmClosure.Chunk.Params, args, vmClosure.Chunk.Arity);
        if (tail)
        {
            sp = procIdx;
            frame.Chunk = vmClosure.Chunk;
            frame.Pc = 0;
            frame.Env = callEnv;
            frame.StackBase = sp;
            frame.CallSite = callSite;
            frame.ProcedureName = GetProcedureName(vmClosure, vmClosure.Chunk);
            code = frame.Chunk.Code;
            InterpreterContext.RecordTailCall();
        }
        else
        {
            sp = procIdx;
            if (frameCount >= MaxFrames)
                throw new LispException($"VM: call stack overflow (depth {MaxFrames})");
            frames[frameCount++] = new CallFrame(vmClosure.Chunk, callEnv, sp)
            {
                CallSite = callSite,
                ProcedureName = GetProcedureName(vmClosure, vmClosure.Chunk),
            };
        }
        InterpreterContext.RecordIteration();
    }

    // Returns true when a new VM frame was pushed (non-tail VmClosure call), signalling
    // the dispatch loop to break and let the outer loop rebind the frame ref.
    private static bool InvokeProcedure(
        object? proc,
        Pair? callArgs,
        bool tail,
        Expression? callSite,
        ref object?[] stack,
        ref int sp,
        ref CallFrame[] frames,
        ref int frameCount,
        ref CallFrame frame,
        ref List<Instruction> code,
        int procIdx)
    {
        switch (proc)
        {
            case VmClosure vmClosure:
                InvokeVmClosure(vmClosure, callArgs, tail, callSite, ref stack, ref sp, ref frames, ref frameCount, ref frame, ref code, procIdx);
                return !tail;
            case Closure treeWalkClosure:
                sp = procIdx;
                Push(ref stack, ref sp, ResolveTailCalls(treeWalkClosure.Eval(callArgs)));
                return false;
            case Primitive prim:
                sp = procIdx;
                InterpreterContext.RecordPrimCall();
                Push(ref stack, ref sp, prim(callArgs ?? Pair.Empty));
                return false;
            default:
                throw new LispException($"VM: not a procedure: {Util.Dump(proc)}");
        }
    }

    public static object Execute(Chunk chunk, Env env)
    {
        var stack = new object?[256];
        int sp = 0;
        var frames = new CallFrame[MaxFrames];
        int frameCount = 0;
        frames[frameCount++] = new CallFrame(chunk, env, 0)
        {
            CallSite = chunk.RootExpr,
            ProcedureName = "<top-level>",
        };

        try
        {
            while (frameCount > 0)
            {
                ref var frame = ref frames[frameCount - 1];
                var code = frame.Chunk.Code;

                while (true)
                {
                    bool stayInCurrentFrame = true;

                    if (frame.Pc >= code.Count)
                    {
                        ReturnToCaller(ref stack, ref sp, ref frameCount, in frame);
                        stayInCurrentFrame = false;
                        break;
                    }

                    var instr = code[frame.Pc++];
                    switch (instr.Op)
                    {
                    case OpCode.LOAD_CONST:
                        Push(ref stack, ref sp, frame.Chunk.Constants[instr.Operand]);
                        break;
                    case OpCode.LOAD_VAR:
                    {
                        var sym = frame.Chunk.Symbols[instr.Operand];
                        Push(ref stack, ref sp, frame.Env.Apply(sym));
                        break;
                    }
                    case OpCode.STORE_VAR:
                    {
                        var sym = frame.Chunk.Symbols[instr.Operand];
                        frame.Env.Bind(sym, stack[--sp]!);
                        Push(ref stack, ref sp, sym);
                        break;
                    }
                    case OpCode.DEFINE_VAR:
                    {
                        var sym = frame.Chunk.Symbols[instr.Operand];
                        var value = stack[--sp]!;
                        Util.ApplyDocComment(value, sym.ToString());
                        frame.Env.table[sym] = value;
                        Push(ref stack, ref sp, sym);
                        break;
                    }
                    case OpCode.POP:
                        sp--;
                        break;
                    case OpCode.JUMP:
                        frame.Pc = instr.Operand;
                        break;
                    case OpCode.JUMP_IF_FALSE:
                    {
                        var v = stack[--sp];
                        if (v is bool b && !b)
                            frame.Pc = instr.Operand;
                        break;
                    }
                    case OpCode.RETURN:
                    {
                        ReturnToCaller(ref stack, ref sp, ref frameCount, in frame);
                        stayInCurrentFrame = false;
                        break;
                    }
                    case OpCode.MAKE_CLOSURE:
                    {
                        var proto = frame.Chunk.Prototypes[instr.Operand];
                        Push(ref stack, ref sp, new VmClosure(proto, frame.Env));
                        break;
                    }
                    case OpCode.CALL:
                    case OpCode.TAIL_CALL:
                    {
                        int argc = instr.Operand;
                        int procIdx = sp - argc - 1;
                        var proc = stack[procIdx];
                        var callArgs = BuildArgPair(stack, sp, argc);
                        if (InvokeProcedure(proc, callArgs, instr.Op == OpCode.TAIL_CALL, GetCurrentSource(frame), ref stack, ref sp, ref frames, ref frameCount, ref frame, ref code, procIdx))
                            stayInCurrentFrame = false;
                        break;
                    }
                    case OpCode.CALL_LIST:
                    case OpCode.TAIL_CALL_LIST:
                    {
                        var rawArgs = stack[--sp];
                        int procIdx = sp - 1;
                        var proc = stack[procIdx];
                        var callArgs = NormalizeArgList(rawArgs);
                        if (InvokeProcedure(proc, callArgs, instr.Op == OpCode.TAIL_CALL_LIST, GetCurrentSource(frame), ref stack, ref sp, ref frames, ref frameCount, ref frame, ref code, procIdx))
                            stayInCurrentFrame = false;
                        break;
                    }
                    case OpCode.PRIM:
                    {
                        var (primIdx, argc) = Instruction.UnpackPrimOperand(instr.Operand);
                        var prim = frame.Chunk.Primitives[primIdx];
                        var args = BuildArgPair(stack, sp, argc);
                        sp -= argc;
                        InterpreterContext.RecordPrimCall();
                        Push(ref stack, ref sp, prim(args!));
                        break;
                    }
                    case OpCode.PRIM_LIST:
                    {
                        var prim = frame.Chunk.Primitives[instr.Operand];
                        var args = NormalizeArgList(stack[--sp]);
                        InterpreterContext.RecordPrimCall();
                        Push(ref stack, ref sp, prim(args ?? Pair.Empty));
                        break;
                    }
                    case OpCode.INTERP:
                    {
                        var astExpr = frame.Chunk.AstNodes[instr.Operand];
                        InterpreterContext.RecordInterpExec(astExpr);
                        Push(ref stack, ref sp, ResolveTailCalls(astExpr.Eval(frame.Env)));
                        break;
                    }
                    }
                    if (stayInCurrentFrame)
                        continue;

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            var currentSource = frameCount > 0 ? GetCurrentSource(frames[frameCount - 1]) : chunk.RootExpr;
            var source = currentSource?.Source
                ?? (frameCount > 0 ? frames[frameCount - 1].Chunk.RootExpr?.Source : null)
                ?? chunk.RootExpr?.Source;
            throw ExceptionDisplay.Attach(ex, source, BuildSchemeStack(frames, frameCount, currentSource));
        }

        return sp > 0 ? stack[sp - 1]! : Pair.Empty;
    }

    private static string GetProcedureName(Closure closure, Chunk chunk)
    {
        if (!string.IsNullOrWhiteSpace(closure.DebugName))
            return closure.DebugName!;

        return chunk.RootExpr is Lambda lambda
            ? $"lambda {Util.Dump(lambda.Ids)}"
            : "<procedure>";
    }

    private static Expression? GetCurrentSource(CallFrame frame)
    {
        int sourceIndex = frame.Pc - 1;
        if (sourceIndex >= 0 && sourceIndex < frame.Chunk.SourceMap.Count)
            return frame.Chunk.SourceMap[sourceIndex] ?? frame.CallSite ?? frame.Chunk.RootExpr;

        return frame.CallSite ?? frame.Chunk.RootExpr;
    }

    private static IReadOnlyList<SchemeStackFrame> BuildSchemeStack(CallFrame[] frames, int frameCount, Expression? currentSource)
    {
        List<SchemeStackFrame> stack = [];
        for (int index = frameCount - 1; index >= 0; index--)
        {
            var frame = frames[index];
            var source = index == frameCount - 1
                ? currentSource ?? frame.CallSite ?? frame.Chunk.RootExpr
                : frame.CallSite ?? frame.Chunk.RootExpr;

            if (source == null && string.IsNullOrWhiteSpace(frame.ProcedureName))
                continue;

            stack.Add(new SchemeStackFrame(
                frame.ProcedureName ?? "<procedure>",
                source != null ? FormatSource(source) : string.Empty,
                source?.Source ?? frame.CallSite?.Source ?? frame.Chunk.RootExpr?.Source));
        }

        return stack;
    }

    private static Pair? BuildArgPair(object?[] stack, int sp, int argc)
    {
        if (argc == 0) return null;
        Pair? head = null;
        Pair? tail = null;
        for (int i = sp - argc; i < sp; i++)
            Pair.AppendTail(ref head, ref tail, stack[i]);
        return head;
    }

    private static void EnsureStack(ref object?[] stack, int sp)
    {
        if (sp >= stack.Length)
            Array.Resize(ref stack, stack.Length * 2);
    }

    private static string FormatSource(Expression expr)
    {
        static string JoinForms(IEnumerable<string> forms) => string.Join(" ", forms.Where(form => form.Length > 0));

        static IEnumerable<string> FormatExprList(Pair? exprs)
        {
            if (exprs == null) yield break;
            foreach (object expr in exprs)
                yield return expr is Expression e ? FormatSource(e) : Util.Dump(expr);
        }

        return expr switch
        {
            Lit lit => Util.Dump(lit.Datum),
            Var v => v.id.ToString() ?? "<?>",
            Define def => $"(define {def.NameSym} {FormatSource(def.ValExpr)})",
            Assignment asgn => $"(set! {asgn.Id} {FormatSource(asgn.ValExpr)})",
            Lambda lam => $"(lambda {Util.Dump(lam.Ids)}{(lam.RawBody != null || lam.Body != null ? " " : "")}{JoinForms(FormatExprList(lam.RawBody ?? lam.Body))})",
            If ifExpr => $"(if {FormatSource(ifExpr.Test)} {FormatSource(ifExpr.ThenExpr)} {FormatSource(ifExpr.ElseExpr)})",
            App app => $"({FormatSource(app.Rator)}{(app.Rands != null ? " " : "")}{JoinForms(FormatExprList(app.Rands))})",
            Prim prim => $"({FormatPrimitiveName(prim.PrimDelegate)}{(prim.Rands != null ? " " : "")}{JoinForms(FormatExprList(prim.Rands))})",
            _ => expr.ToString() ?? Util.Dump(expr),
        };
    }

    private static string FormatPrimitiveName(Primitive primitive)
    {
        foreach (var kv in Prim.list)
            if (kv.Value == primitive)
                return kv.Key;

        const string suffix = "_Prim";
        string name = primitive.Method.Name;
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;
    }

    private static string IndentText(int indent) => new(' ', Math.Max(0, indent));

    private static void AppendClosingParen(List<string> lines)
    {
        if (lines.Count == 0)
        {
            lines.Add(")");
            return;
        }

        lines[^1] += ")";
    }

    private static void AppendIndentedLines(List<string> lines, IEnumerable<string> childLines)
    {
        foreach (string line in childLines)
            lines.Add(line);
    }

    private static IEnumerable<Expression> GetLambdaBodyForms(Lambda lam)
    {
        if (lam.Body != null)
        {
            foreach (object bodyExpr in lam.Body)
                if (bodyExpr is Expression expr)
                    yield return expr;
            yield break;
        }

        if (lam.RawBody != null)
            foreach (object bodyExpr in lam.RawBody)
                yield return bodyExpr is Expression expr ? expr : Expression.Parse(bodyExpr);
    }

    private static bool IsAtomicSourceExpression(Expression expr) => expr is Lit or Var;

    private static int CountExpressionItems(Pair? exprs)
    {
        int count = 0;
        if (exprs == null) return count;
        foreach (object _ in exprs)
            count++;
        return count;
    }

    private static bool AllAtomicSourceExpressions(Pair? exprs)
    {
        if (exprs == null) return true;
        foreach (object expr in exprs)
            if (expr is not Expression sourceExpr || !IsAtomicSourceExpression(sourceExpr))
                return false;
        return true;
    }

    private static bool HasCompactLambdaBody(Lambda lam)
    {
        int bodyCount = 0;
        foreach (Expression bodyExpr in GetLambdaBodyForms(lam))
        {
            bodyCount++;
            if (!IsAtomicSourceExpression(bodyExpr) || bodyCount > 1)
                return false;
        }

        return true;
    }

    private static bool IsCompactInlineSource(Expression expr)
        => expr switch
        {
            Lit or Var => true,
            Define def => IsAtomicSourceExpression(def.ValExpr),
            Assignment asgn => IsAtomicSourceExpression(asgn.ValExpr),
            Lambda lam => HasCompactLambdaBody(lam),
            If ifExpr => IsAtomicSourceExpression(ifExpr.Test)
                && IsAtomicSourceExpression(ifExpr.ThenExpr)
                && IsAtomicSourceExpression(ifExpr.ElseExpr),
            App app => app.Rator is Var && CountExpressionItems(app.Rands) <= 2 && AllAtomicSourceExpressions(app.Rands),
            Prim prim => CountExpressionItems(prim.Rands) <= 2 && AllAtomicSourceExpressions(prim.Rands),
            _ => false,
        };

    private static int GetSoftInlineWidth(Expression expr)
        => expr switch
        {
            Lit or Var => int.MaxValue,
            App or Prim => 44,
            If => 48,
            Lambda => 52,
            Define or Assignment => 56,
            _ => 60,
        };

    private static bool ShouldInlineSource(Expression expr, int width, int indent, string inline)
    {
        if (indent + inline.Length > width)
            return false;

        if (IsCompactInlineSource(expr))
            return true;

        return inline.Length <= GetSoftInlineWidth(expr);
    }

    private static List<string> PrettyFormatSource(Expression expr, int width, int indent = 0)
    {
        string inline = FormatSource(expr);
        if (ShouldInlineSource(expr, width, indent, inline))
            return [IndentText(indent) + inline];

        switch (expr)
        {
            case Lit or Var:
                return [IndentText(indent) + inline];

            case Define def:
            {
                List<string> lines = [IndentText(indent) + $"(define {def.NameSym}"];
                AppendIndentedLines(lines, PrettyFormatSource(def.ValExpr, width, indent + 2));
                AppendClosingParen(lines);
                return lines;
            }

            case Assignment asgn:
            {
                List<string> lines = [IndentText(indent) + $"(set! {asgn.Id}"];
                AppendIndentedLines(lines, PrettyFormatSource(asgn.ValExpr, width, indent + 2));
                AppendClosingParen(lines);
                return lines;
            }

            case Lambda lam:
            {
                List<string> lines = [IndentText(indent) + $"(lambda {Util.Dump(lam.Ids)}"];
                foreach (Expression bodyExpr in GetLambdaBodyForms(lam))
                    AppendIndentedLines(lines, PrettyFormatSource(bodyExpr, width, indent + 2));
                AppendClosingParen(lines);
                return lines;
            }

            case If ifExpr:
            {
                List<string> lines = [IndentText(indent) + "(if"];
                AppendIndentedLines(lines, PrettyFormatSource(ifExpr.Test, width, indent + 2));
                AppendIndentedLines(lines, PrettyFormatSource(ifExpr.ThenExpr, width, indent + 2));
                AppendIndentedLines(lines, PrettyFormatSource(ifExpr.ElseExpr, width, indent + 2));
                AppendClosingParen(lines);
                return lines;
            }

            case App app:
            {
                bool inlineRator = app.Rator is Var or Lit;
                List<string> lines;

                if (inlineRator)
                {
                    lines = [IndentText(indent) + $"({FormatSource(app.Rator)}"];
                }
                else
                {
                    lines = PrettyFormatSource(app.Rator, width, indent + 1);
                    if (lines.Count == 0)
                        lines = [IndentText(indent) + "("];
                    else
                        lines[0] = IndentText(indent) + "(" + lines[0].TrimStart();
                }

                if (app.Rands != null)
                    foreach (object arg in app.Rands)
                        if (arg is Expression argExpr)
                            AppendIndentedLines(lines, PrettyFormatSource(argExpr, width, indent + 2));
                        else
                            lines.Add(IndentText(indent + 2) + Util.Dump(arg));
                AppendClosingParen(lines);
                return lines;
            }

            case Prim prim:
            {
                List<string> lines = [IndentText(indent) + $"({FormatPrimitiveName(prim.PrimDelegate)}"];
                if (prim.Rands != null)
                    foreach (object arg in prim.Rands)
                        if (arg is Expression argExpr)
                            AppendIndentedLines(lines, PrettyFormatSource(argExpr, width, indent + 2));
                        else
                            lines.Add(IndentText(indent + 2) + Util.Dump(arg));
                AppendClosingParen(lines);
                return lines;
            }

            default:
                return [IndentText(indent) + inline];
        }
    }

    private static IEnumerable<Expression> GetChildExpressions(Expression expr)
    {
        switch (expr)
        {
            case Define def:
                yield return def.ValExpr;
                yield break;
            case Assignment asgn:
                yield return asgn.ValExpr;
                yield break;
            case Lambda lam when lam.Body != null:
                foreach (object bodyExpr in lam.Body)
                    if (bodyExpr is Expression body)
                        yield return body;
                yield break;
            case If ifExpr:
                yield return ifExpr.Test;
                yield return ifExpr.ThenExpr;
                yield return ifExpr.ElseExpr;
                yield break;
            case App app:
                yield return app.Rator;
                if (app.Rands != null)
                    foreach (object arg in app.Rands)
                        if (arg is Expression argExpr)
                            yield return argExpr;
                yield break;
            case Prim prim when prim.Rands != null:
                foreach (object arg in prim.Rands)
                    if (arg is Expression argExpr)
                        yield return argExpr;
                yield break;
        }
    }

    private static bool TryGetSourceDepth(Expression current, Expression target, int depth, out int foundDepth)
    {
        if (ReferenceEquals(current, target))
        {
            foundDepth = depth;
            return true;
        }

        foreach (var child in GetChildExpressions(current))
            if (TryGetSourceDepth(child, target, depth + 1, out foundDepth))
                return true;

        foundDepth = 0;
        return false;
    }

    private static bool ContainsExpression(Expression current, Expression target)
    {
        if (ReferenceEquals(current, target)) return true;
        foreach (var child in GetChildExpressions(current))
            if (ContainsExpression(child, target))
                return true;
        return false;
    }

    private static bool IsAncestorSection(Expression candidateAncestor, Expression current)
        => !ReferenceEquals(candidateAncestor, current) && ContainsExpression(candidateAncestor, current);

    private static int GetSourceDepth(Chunk chunk, Expression source)
    {
        if (chunk.RootExpr == null) return 0;
        return TryGetSourceDepth(chunk.RootExpr, source, 0, out int depth) ? depth : 0;
    }

    private static bool ShouldDisplaySource(Expression source)
        => DisassemblyVerbose || !BytecodeCompiler.IsSimpleSectionExpr(source);

    public static void Disassemble(Chunk chunk, string name = "top-level", string indent = "")
    {
        ConsoleOutput.WriteDisassemblyHeader(indent, name, chunk.Code.Count);
        Expression? currentSource = null;
        for (int i = 0; i < chunk.Code.Count; i++)
        {
            var instr = chunk.Code[i];
            var source = i < chunk.SourceMap.Count ? chunk.SourceMap[i] : null;
            if (source != null && !ReferenceEquals(source, currentSource))
            {
                if (ShouldDisplaySource(source) && (currentSource == null || !IsAncestorSection(source, currentSource)))
                {
                    int sourceDepth = GetSourceDepth(chunk, source);
                    int width = ConsoleOutput.GetDisassemblySourceWidth(indent, sourceDepth);
                    List<string> sourceLines = PrettyFormatSource(source, width);
                    if (sourceLines.Count > 0) // Indent the first line to align with the instruction lines "source"
                        sourceLines[0] = " " + sourceLines[0].TrimStart();
                    ConsoleOutput.WriteDisassemblySource(indent, sourceDepth, sourceLines);
                }
                currentSource = source;
            }
            List<ConsoleOutput.Segment> segments =
            [
                new(indent + "  "),
                new($"{i,4}", ConsoleColor.DarkGray),
                new(": ", ConsoleColor.DarkGray),
                new($"{instr.Op,-16}", ConsoleColor.Cyan),
            ];
            switch (instr.Op)
            {
                case OpCode.LOAD_CONST:
                    segments.Add(new("  #", ConsoleColor.DarkGray));
                    segments.Add(new(instr.Operand.ToString(), ConsoleColor.Yellow));
                    segments.Add(new("  "));
                    segments.Add(new(Util.Dump(chunk.Constants[instr.Operand]), ConsoleColor.Gray));
                    break;
                case OpCode.LOAD_VAR:
                case OpCode.STORE_VAR:
                case OpCode.DEFINE_VAR:
                    segments.Add(new("  #", ConsoleColor.DarkGray));
                    segments.Add(new(instr.Operand.ToString(), ConsoleColor.Yellow));
                    segments.Add(new("  "));
                    segments.Add(new(chunk.Symbols[instr.Operand].ToString()!, ConsoleColor.Magenta));
                    break;
                case OpCode.JUMP:
                case OpCode.JUMP_IF_FALSE:
                    segments.Add(new("  -> ", ConsoleColor.DarkGray));
                    segments.Add(new(instr.Operand.ToString(), ConsoleColor.Yellow));
                    break;
                case OpCode.MAKE_CLOSURE:
                {
                    var proto = chunk.Prototypes[instr.Operand];
                    string paramStr = proto.Params != null ? Util.Dump(proto.Params) : "()";
                    segments.Add(new("  proto #", ConsoleColor.DarkGray));
                    segments.Add(new(instr.Operand.ToString(), ConsoleColor.Yellow));
                    segments.Add(new("  params=", ConsoleColor.DarkGray));
                    segments.Add(new(paramStr, ConsoleColor.Gray));
                    break;
                }
                case OpCode.CALL:
                    segments.Add(new("  argc=", ConsoleColor.DarkGray));
                    segments.Add(new(instr.Operand.ToString(), ConsoleColor.Yellow));
                    break;
                case OpCode.CALL_LIST:
                    segments.Add(new("  arglist", ConsoleColor.DarkGray));
                    break;
                case OpCode.TAIL_CALL:
                    segments.Add(new("  argc=", ConsoleColor.DarkGray));
                    segments.Add(new(instr.Operand.ToString(), ConsoleColor.Yellow));
                    segments.Add(new("  (tail)", ConsoleColor.DarkYellow));
                    break;
                case OpCode.TAIL_CALL_LIST:
                    segments.Add(new("  arglist", ConsoleColor.DarkGray));
                    segments.Add(new("  (tail)", ConsoleColor.DarkYellow));
                    break;
                case OpCode.PRIM:
                {
                    var (primIdx, argc) = Instruction.UnpackPrimOperand(instr.Operand);
                    string mname = chunk.Primitives[primIdx].Method.Name;
                    segments.Add(new("  "));
                    segments.Add(new(mname, ConsoleColor.Green));
                    segments.Add(new("  argc=", ConsoleColor.DarkGray));
                    segments.Add(new(argc.ToString(), ConsoleColor.Yellow));
                    break;
                }
                case OpCode.PRIM_LIST:
                {
                    string mname = chunk.Primitives[instr.Operand].Method.Name;
                    segments.Add(new("  "));
                    segments.Add(new(mname, ConsoleColor.Green));
                    segments.Add(new("  arglist", ConsoleColor.DarkGray));
                    break;
                }
                case OpCode.INTERP:
                    segments.Add(new("  (interp)", ConsoleColor.DarkYellow));
                    segments.Add(new(" "));
                    segments.Add(new(chunk.AstNodes[instr.Operand].ToString()!, ConsoleColor.Gray));
                    break;
                default:
                    if (instr.Operand != 0)
                    {
                        segments.Add(new("  "));
                        segments.Add(new(instr.Operand.ToString(), ConsoleColor.Yellow));
                    }
                    break;
            }
            ConsoleOutput.WriteLineSegments(segments);
        }
        for (int p = 0; p < chunk.Prototypes.Count; p++)
        {
            var proto = chunk.Prototypes[p];
            string paramStr = proto.Params != null ? Util.Dump(proto.Params) : "()";
            Disassemble(proto, $"proto #{p}  lambda{paramStr}", indent + "  ");
        }
    }
}
