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
    CALL_LIST,
    TAIL_CALL,
    TAIL_CALL_LIST,
    PRIM,
    PRIM_LIST,
    DEFINE_LIBRARY,
    EVAL,
    INTERP,
}

public readonly struct Instruction(OpCode op, int operand = 0)
{
    public readonly OpCode Op = op;
    public readonly int Operand = operand;
    public override string ToString() => Operand == 0 ? Op.ToString() : $"{Op} {Operand}";

    // PRIM operand encodes both the primitive index and the argument count in one int:
    // high 16 bits = primitive index into Chunk.Primitives, low 16 bits = argc.
    public static int PackPrimOperand(int primIdx, int argc) => (primIdx << 16) | argc;
    public static (int PrimIdx, int Argc) UnpackPrimOperand(int operand) => (operand >> 16, operand & 0xFFFF);
}

public sealed class Chunk
{
    public readonly List<Instruction> Code = [];
    public readonly List<Expression?> SourceMap = [];
    public readonly List<object?> Constants = [];
    public readonly List<Symbol> Symbols = [];
    public readonly List<Chunk> Prototypes = [];
    public readonly List<Primitive> Primitives = [];
    public readonly List<Expression> AstNodes = [];
    public Expression? RootExpr;
    public Pair? Params;
    public int Arity;
    public Pair? SourceBody;

    public int Emit(OpCode op, int operand = 0, Expression? source = null)
    {
        Code.Add(new Instruction(op, operand));
        SourceMap.Add(source);
        return Code.Count - 1;
    }
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
        InterpreterContext.RecordIteration();
        var callEnv = env.Extend(Chunk.Params, args, Chunk.Arity);
        return Vm.Execute(Chunk, callEnv);
    }

    public override string ToString() => "#<vm-closure>";
}

