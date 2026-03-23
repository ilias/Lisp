namespace Lisp;

public enum OpCode : byte
{
    LOAD_CONST,
    LOAD_VAR,
    STORE_VAR,
    DEFINE_VAR,
    POP,
    JUMP,
    JUMP_IF_FALSE,
    RETURN,
    MAKE_CLOSURE,
    CALL,
    TAIL_CALL,
    PRIM,
    INTERP,
}

public readonly struct Instruction(OpCode op, int operand = 0)
{
    public readonly OpCode Op = op;
    public readonly int Operand = operand;
    public override string ToString() => Operand == 0 ? Op.ToString() : $"{Op} {Operand}";
}

public sealed class Chunk
{
    public readonly List<Instruction> Code = [];
    public readonly List<object?> Constants = [];
    public readonly List<Symbol> Symbols = [];
    public readonly List<Chunk> Prototypes = [];
    public readonly List<Primitive> Primitives = [];
    public readonly List<Expression> AstNodes = [];
    public Pair? Params;
    public int Arity;
    public Pair? SourceBody;

    public int Emit(OpCode op, int operand = 0) { Code.Add(new Instruction(op, operand)); return Code.Count - 1; }
    public void Patch(int at, int operand) { Code[at] = new Instruction(Code[at].Op, operand); }
    public int AddConst(object? v) { Constants.Add(v); return Constants.Count - 1; }
    public int AddSym(Symbol s) { Symbols.Add(s); return Symbols.Count - 1; }
    public int AddProto(Chunk c) { Prototypes.Add(c); return Prototypes.Count - 1; }
    public int AddPrim(Primitive p) { Primitives.Add(p); return Primitives.Count - 1; }
    public int AddAst(Expression e) { AstNodes.Add(e); return AstNodes.Count - 1; }
}

public sealed class VmClosure : Closure
{
    public Chunk Chunk { get; }

    public VmClosure(Chunk chunk, Env capturedEnv)
        : base(chunk.Params, chunk.SourceBody, capturedEnv, chunk.SourceBody)
    {
        Chunk = chunk;
    }

    public override object Eval(Pair? args)
    {
        if (Program.Stats) Program.Iterations++;
        var callEnv = env.Extend(Chunk.Params, args, Chunk.Arity);
        return Vm.Execute(Chunk, callEnv);
    }

    public override string ToString() => "#<vm-closure>";
}

public static class BytecodeCompiler
{
    private static void EmitInterpreted(Chunk chunk, Expression expr, bool tail)
    {
        chunk.Emit(OpCode.INTERP, chunk.AddAst(expr));
        if (tail) chunk.Emit(OpCode.RETURN);
    }

    private static int CompileArguments(Pair? args, Chunk chunk)
    {
        int argc = 0;
        if (args != null)
            foreach (object arg in args)
            {
                Compile((Expression)arg, chunk, tail: false);
                argc++;
            }
        return argc;
    }

    private static void CompileBodySequence(object[] bodyList, Chunk chunk)
    {
        for (int i = 0; i < bodyList.Length; i++)
        {
            bool isLast = i == bodyList.Length - 1;
            Compile((Expression)bodyList[i], chunk, tail: isLast);
            if (!isLast) chunk.Emit(OpCode.POP);
        }

        if (bodyList.Length != 0) return;
        chunk.Emit(OpCode.LOAD_CONST, chunk.AddConst(Pair.Empty));
        chunk.Emit(OpCode.RETURN);
    }

    public static Chunk CompileTop(Expression expr)
    {
        var chunk = new Chunk { Params = null, Arity = 0 };
        Compile(expr, chunk, tail: false);
        chunk.Emit(OpCode.RETURN);
        return chunk;
    }

    private static void Compile(Expression expr, Chunk chunk, bool tail)
    {
        switch (expr)
        {
            case Lit lit:
                CompileLit(lit, chunk);
                break;
            case Var v:
                chunk.Emit(OpCode.LOAD_VAR, chunk.AddSym(v.id));
                break;
            case Define def:
                CompileDefine(def, chunk);
                break;
            case Assignment asgn:
                Compile(asgn.ValExpr, chunk, tail: false);
                chunk.Emit(OpCode.STORE_VAR, chunk.AddSym(asgn.Id));
                break;
            case Lambda lam:
                CompileLambda(lam, chunk);
                break;
            case If ifExpr:
                CompileIf(ifExpr, chunk, tail);
                return;
            case App app:
                CompileApp(app, chunk, tail);
                return;
            case Prim prim:
                CompilePrim(prim, chunk);
                break;
            default:
                chunk.Emit(OpCode.INTERP, chunk.AddAst(expr));
                break;
        }

        if (tail) chunk.Emit(OpCode.RETURN);
    }

    private static void CompileLit(Lit lit, Chunk chunk)
    {
        if (!HasComma(lit.Datum))
            chunk.Emit(OpCode.LOAD_CONST, chunk.AddConst(lit.Datum));
        else
            EmitInterpreted(chunk, lit, tail: false);
    }

    private static bool HasComma(object? obj)
    {
        if (obj is not Pair p) return false;
        for (var cur = p; cur != null; cur = cur.cdr)
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
        Compile(valExpr, chunk, tail: false);
        chunk.Emit(OpCode.DEFINE_VAR, chunk.AddSym(sym));
    }

    private static void CompileLambda(Lambda lam, Chunk chunk)
    {
        Pair? rawBodyPair = lam.Body != null ? lam.RawBody : null;
        var proto = new Chunk
        {
            Params = lam.Ids,
            Arity = lam.Ids?.Count ?? 0,
            SourceBody = rawBodyPair,
        };
        var bodyList = lam.Body?.ToArray() ?? [];
        CompileBodySequence(bodyList, proto);
        chunk.Emit(OpCode.MAKE_CLOSURE, chunk.AddProto(proto));
    }

    private static void CompileIf(If ifExpr, Chunk chunk, bool tail)
    {
        Compile(ifExpr.Test, chunk, tail: false);
        int jumpFalse = chunk.Emit(OpCode.JUMP_IF_FALSE, 0);
        Compile(ifExpr.ThenExpr, chunk, tail);
        int jumpEnd = chunk.Emit(OpCode.JUMP, 0);
        chunk.Patch(jumpFalse, chunk.Code.Count);
        Compile(ifExpr.ElseExpr, chunk, tail);
        chunk.Patch(jumpEnd, chunk.Code.Count);
    }

    private static void CompileApp(App app, Chunk chunk, bool tail)
    {
        if (HasCommaAt(app.Rands))
        {
            EmitInterpreted(chunk, app, tail);
            return;
        }

        Compile(app.Rator, chunk, tail: false);
        int argc = CompileArguments(app.Rands, chunk);
        chunk.Emit(tail ? OpCode.TAIL_CALL : OpCode.CALL, argc);
    }

    private static void CompilePrim(Prim prim, Chunk chunk)
    {
        if (HasCommaAt(prim.Rands))
        {
            EmitInterpreted(chunk, prim, tail: false);
            return;
        }

        int argc = CompileArguments(prim.Rands, chunk);
        int primIdx = chunk.AddPrim(prim.PrimDelegate);
        chunk.Emit(OpCode.PRIM, (primIdx << 16) | argc);
    }

    private static bool HasCommaAt(Pair? rands)
    {
        if (rands == null) return false;
        foreach (object rand in rands)
            if (rand is CommaAt) return true;
        return false;
    }
}

internal sealed class CallFrame(Chunk chunk, Env env, int stackBase)
{
    public Chunk Chunk { get; set; } = chunk;
    public int Pc { get; set; }
    public Env Env { get; set; } = env;
    public int StackBase { get; set; } = stackBase;
}

public static class Vm
{
    private const int MaxFrames = 10_000;

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
            if (Program.Stats) Program.TailCalls++;
            result = tc.Closure.Eval(tc.Args);
        }
        return result;
    }

    private static void ReturnToCaller(ref object?[] stack, ref int sp, ref int frameCount, CallFrame frame)
    {
        var retVal = sp > frame.StackBase ? stack[--sp] : Pair.Empty;
        sp = frame.StackBase;
        frameCount--;
        Push(ref stack, ref sp, retVal);
    }

    public static object Execute(Chunk chunk, Env env)
    {
        var stack = new object?[256];
        int sp = 0;
        var frames = new CallFrame[MaxFrames];
        int frameCount = 0;
        frames[frameCount++] = new CallFrame(chunk, env, 0);

        while (frameCount > 0)
        {
            var frame = frames[frameCount - 1];
            var code = frame.Chunk.Code;

            while (true)
            {
                if (frame.Pc >= code.Count)
                {
                    ReturnToCaller(ref stack, ref sp, ref frameCount, frame);
                    if (frameCount > 0)
                    {
                        frame = frames[frameCount - 1];
                        code = frame.Chunk.Code;
                    }
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
                        frame.Env.table[sym] = stack[--sp]!;
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
                        ReturnToCaller(ref stack, ref sp, ref frameCount, frame);
                        goto NextFrame;
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
                        switch (proc)
                        {
                            case VmClosure vmClosure:
                            {
                                var callEnv = vmClosure.env.Extend(vmClosure.Chunk.Params, BuildArgPair(stack, sp, argc), vmClosure.Chunk.Arity);
                                if (instr.Op == OpCode.TAIL_CALL)
                                {
                                    sp = procIdx;
                                    frame.Chunk = vmClosure.Chunk;
                                    frame.Pc = 0;
                                    frame.Env = callEnv;
                                    frame.StackBase = sp;
                                    code = frame.Chunk.Code;
                                    if (Program.Stats) Program.TailCalls++;
                                }
                                else
                                {
                                    sp = procIdx;
                                    if (frameCount >= MaxFrames)
                                        throw new LispException($"VM: call stack overflow (depth {MaxFrames})");
                                    frames[frameCount++] = new CallFrame(vmClosure.Chunk, callEnv, sp);
                                    frame = frames[frameCount - 1];
                                    code = frame.Chunk.Code;
                                }
                                if (Program.Stats) Program.Iterations++;
                                break;
                            }
                            case Closure treeWalkClosure:
                            {
                                var callArgs = BuildArgPair(stack, sp, argc);
                                sp = procIdx;
                                Push(ref stack, ref sp, ResolveTailCalls(treeWalkClosure.Eval(callArgs)));
                                break;
                            }
                            case Primitive prim:
                            {
                                var callArgs = BuildArgPair(stack, sp, argc);
                                sp = procIdx;
                                if (Program.Stats) Program.PrimCalls++;
                                Push(ref stack, ref sp, prim(callArgs!));
                                break;
                            }
                            default:
                                throw new LispException($"VM: not a procedure: {Util.Dump(proc)}");
                        }
                        break;
                    }
                    case OpCode.PRIM:
                    {
                        int primIdx = instr.Operand >> 16;
                        int argc = instr.Operand & 0xFFFF;
                        var prim = frame.Chunk.Primitives[primIdx];
                        var args = BuildArgPair(stack, sp, argc);
                        sp -= argc;
                        if (Program.Stats) Program.PrimCalls++;
                        Push(ref stack, ref sp, prim(args!));
                        break;
                    }
                    case OpCode.INTERP:
                    {
                        var astExpr = frame.Chunk.AstNodes[instr.Operand];
                        Push(ref stack, ref sp, ResolveTailCalls(astExpr.Eval(frame.Env)));
                        break;
                    }
                }
                continue;

            NextFrame:
                if (frameCount > 0)
                {
                    frame = frames[frameCount - 1];
                    code = frame.Chunk.Code;
                }
                break;
            }
        }

        return sp > 0 ? stack[sp - 1]! : Pair.Empty;
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

    public static void Disassemble(Chunk chunk, string name = "top-level", string indent = "")
    {
        ConsoleOutput.WriteDisassemblyHeader(indent, name, chunk.Code.Count);
        for (int i = 0; i < chunk.Code.Count; i++)
        {
            var instr = chunk.Code[i];
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
                    segments.Add(new(Util.Dump(chunk.Constants[instr.Operand]), ConsoleColor.White));
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
                    segments.Add(new(paramStr, ConsoleColor.White));
                    break;
                }
                case OpCode.CALL:
                    segments.Add(new("  argc=", ConsoleColor.DarkGray));
                    segments.Add(new(instr.Operand.ToString(), ConsoleColor.Yellow));
                    break;
                case OpCode.TAIL_CALL:
                    segments.Add(new("  argc=", ConsoleColor.DarkGray));
                    segments.Add(new(instr.Operand.ToString(), ConsoleColor.Yellow));
                    segments.Add(new("  (tail)", ConsoleColor.DarkYellow));
                    break;
                case OpCode.PRIM:
                {
                    int primIdx = instr.Operand >> 16;
                    int argc = instr.Operand & 0xFFFF;
                    string mname = chunk.Primitives[primIdx].Method.Name;
                    segments.Add(new("  "));
                    segments.Add(new(mname, ConsoleColor.Blue));
                    segments.Add(new("  argc=", ConsoleColor.DarkGray));
                    segments.Add(new(argc.ToString(), ConsoleColor.Yellow));
                    break;
                }
                case OpCode.INTERP:
                    segments.Add(new("  (interp)", ConsoleColor.DarkYellow));
                    segments.Add(new(" "));
                    segments.Add(new(chunk.AstNodes[instr.Operand].ToString()!, ConsoleColor.White));
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
