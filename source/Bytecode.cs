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
            chunk.Emit(OpCode.INTERP, chunk.AddAst(lit));
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
        for (int i = 0; i < bodyList.Length; i++)
        {
            bool lastForm = i == bodyList.Length - 1;
            if (lastForm)
                Compile((Expression)bodyList[i], proto, tail: true);
            else
            {
                Compile((Expression)bodyList[i], proto, tail: false);
                proto.Emit(OpCode.POP);
            }
        }
        if (bodyList.Length == 0)
            proto.Emit(OpCode.LOAD_CONST, proto.AddConst(Pair.Empty));
        if (bodyList.Length == 0)
            proto.Emit(OpCode.RETURN);
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
            chunk.Emit(OpCode.INTERP, chunk.AddAst(app));
            if (tail) chunk.Emit(OpCode.RETURN);
            return;
        }

        Compile(app.Rator, chunk, tail: false);
        int argc = 0;
        if (app.Rands != null)
            foreach (object rand in app.Rands)
            {
                Compile((Expression)rand, chunk, tail: false);
                argc++;
            }
        chunk.Emit(tail ? OpCode.TAIL_CALL : OpCode.CALL, argc);
    }

    private static void CompilePrim(Prim prim, Chunk chunk)
    {
        if (HasCommaAt(prim.Rands))
        {
            chunk.Emit(OpCode.INTERP, chunk.AddAst(prim));
            return;
        }

        int argc = 0;
        if (prim.Rands != null)
            foreach (object rand in prim.Rands)
            {
                Compile((Expression)rand, chunk, tail: false);
                argc++;
            }
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
                    var retVal = sp > frame.StackBase ? stack[--sp] : Pair.Empty;
                    sp = frame.StackBase;
                    frameCount--;
                    EnsureStack(ref stack, sp);
                    stack[sp++] = retVal;
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
                        EnsureStack(ref stack, sp);
                        stack[sp++] = frame.Chunk.Constants[instr.Operand];
                        break;
                    case OpCode.LOAD_VAR:
                    {
                        var sym = frame.Chunk.Symbols[instr.Operand];
                        EnsureStack(ref stack, sp);
                        stack[sp++] = frame.Env.Apply(sym);
                        break;
                    }
                    case OpCode.STORE_VAR:
                    {
                        var sym = frame.Chunk.Symbols[instr.Operand];
                        frame.Env.Bind(sym, stack[--sp]!);
                        EnsureStack(ref stack, sp);
                        stack[sp++] = sym;
                        break;
                    }
                    case OpCode.DEFINE_VAR:
                    {
                        var sym = frame.Chunk.Symbols[instr.Operand];
                        frame.Env.table[sym] = stack[--sp]!;
                        EnsureStack(ref stack, sp);
                        stack[sp++] = sym;
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
                        var retVal = sp > frame.StackBase ? stack[--sp] : Pair.Empty;
                        sp = frame.StackBase;
                        frameCount--;
                        EnsureStack(ref stack, sp);
                        stack[sp++] = retVal;
                        goto NextFrame;
                    }
                    case OpCode.MAKE_CLOSURE:
                    {
                        var proto = frame.Chunk.Prototypes[instr.Operand];
                        EnsureStack(ref stack, sp);
                        stack[sp++] = new VmClosure(proto, frame.Env);
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
                                object r = treeWalkClosure.Eval(callArgs);
                                while (r is TailCall tc)
                                {
                                    if (Program.Stats) Program.TailCalls++;
                                    r = tc.Closure.Eval(tc.Args);
                                }
                                EnsureStack(ref stack, sp);
                                stack[sp++] = r;
                                break;
                            }
                            case Primitive prim:
                            {
                                var callArgs = BuildArgPair(stack, sp, argc);
                                sp = procIdx;
                                if (Program.Stats) Program.PrimCalls++;
                                EnsureStack(ref stack, sp);
                                stack[sp++] = prim(callArgs!);
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
                        EnsureStack(ref stack, sp);
                        stack[sp++] = prim(args!);
                        break;
                    }
                    case OpCode.INTERP:
                    {
                        var astExpr = frame.Chunk.AstNodes[instr.Operand];
                        object interped = astExpr.Eval(frame.Env);
                        while (interped is TailCall tc)
                        {
                            if (Program.Stats) Program.TailCalls++;
                            interped = tc.Closure.Eval(tc.Args);
                        }
                        EnsureStack(ref stack, sp);
                        stack[sp++] = interped;
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
        Console.WriteLine($"{indent}=== {name}  ({chunk.Code.Count} instructions) ===");
        for (int i = 0; i < chunk.Code.Count; i++)
        {
            var instr = chunk.Code[i];
            var sb = new StringBuilder();
            sb.Append($"{indent}  {i,4}: {instr.Op,-16}");
            switch (instr.Op)
            {
                case OpCode.LOAD_CONST:
                    sb.Append($"  #{instr.Operand}  {Util.Dump(chunk.Constants[instr.Operand])}");
                    break;
                case OpCode.LOAD_VAR:
                case OpCode.STORE_VAR:
                case OpCode.DEFINE_VAR:
                    sb.Append($"  #{instr.Operand}  {chunk.Symbols[instr.Operand]}");
                    break;
                case OpCode.JUMP:
                case OpCode.JUMP_IF_FALSE:
                    sb.Append($"  -> {instr.Operand}");
                    break;
                case OpCode.MAKE_CLOSURE:
                {
                    var proto = chunk.Prototypes[instr.Operand];
                    string paramStr = proto.Params != null ? Util.Dump(proto.Params) : "()";
                    sb.Append($"  proto #{instr.Operand}  params={paramStr}");
                    break;
                }
                case OpCode.CALL:
                    sb.Append($"  argc={instr.Operand}");
                    break;
                case OpCode.TAIL_CALL:
                    sb.Append($"  argc={instr.Operand}  (tail)");
                    break;
                case OpCode.PRIM:
                {
                    int primIdx = instr.Operand >> 16;
                    int argc = instr.Operand & 0xFFFF;
                    string mname = chunk.Primitives[primIdx].Method.Name;
                    sb.Append($"  {mname}  argc={argc}");
                    break;
                }
                case OpCode.INTERP:
                    sb.Append($"  (interp) {chunk.AstNodes[instr.Operand]}");
                    break;
                default:
                    if (instr.Operand != 0) sb.Append($"  {instr.Operand}");
                    break;
            }
            Console.WriteLine(sb.ToString());
        }
        for (int p = 0; p < chunk.Prototypes.Count; p++)
        {
            var proto = chunk.Prototypes[p];
            string paramStr = proto.Params != null ? Util.Dump(proto.Params) : "()";
            Disassemble(proto, $"proto #{p}  lambda{paramStr}", indent + "  ");
        }
    }
}
