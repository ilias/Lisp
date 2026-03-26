namespace Lisp;

public static class Macro
{
    public static Dictionary<object, object?> macros = [];
    private static int _symbol;
    private static int _wcCounter;

    private static void AppendNode(ref Pair? head, ref Pair? tail, object? value) =>
        Pair.AppendTail(ref head, ref tail, value);

    private static void AddLiteralConstants(Dictionary<object, object?> constants, Pair? literals)
    {
        if (literals == null) return;
        foreach (object literal in literals)
            if (literal != null) constants[literal] = literal;
    }

    public static void Add(Pair obj)
    {
        macros[obj.car!] = obj.cdr;
    }

    public static Pair? TranslateDefineSyntax(Pair ds)
    {
        if (ds.cdr is not Pair np) return null;
        var name = np.car;
        if (np.cdr is not Pair srCell || srCell.car is not Pair sr || sr.car?.ToString() != "syntax-rules")
            return null;
        return TranslateSyntaxRules(name, sr.cdr?.car as Pair, sr.cdr?.cdr);
    }

    public static Pair? TranslateSyntaxRules(object? name, Pair? lits, Pair? rawClauses)
    {
        if (name == null) return null;
        Pair? clauses = null;
        Pair? clausesTail = null;
        if (rawClauses != null)
            foreach (object rawClause in rawClauses)
            {
                if (rawClause is not Pair clause) continue;
                var origPat = clause.car as Pair;
                var tmpl = clause.cdr?.car;
                if (origPat == null || tmpl == null) continue;
                var tPat = MergeEllipsis(origPat, replaceHead: true);
                var tTmpl = tmpl is Pair tp ? (object?)MergeEllipsis(tp, replaceHead: false) : tmpl;
                AppendNode(ref clauses, ref clausesTail, new Pair(tPat, new Pair(tTmpl, null)));
            }
        return new Pair(name, new Pair(lits, clauses));
    }

    private static Pair? MergeEllipsis(Pair? p, bool replaceHead)
    {
        if (p == null) return null;
        Pair? result = null;
        Pair? resultTail = null;
        bool first = replaceHead;
        while (p != null)
        {
            var elem = p.car;
            p = p.cdr;
            object? toAppend;
            if (first) { toAppend = Symbol.Create("_"); first = false; }
            else if (elem is Symbol ws && ws.ToString() == "_")
                toAppend = Symbol.Create($"?wc{_wcCounter++}");
            else if (elem is Symbol sym && !sym.ToString().Contains("...") && p?.car?.ToString() == "...")
            { toAppend = Symbol.Create(sym + "..."); p = p.cdr; }
            else
            {
                if (elem is Pair sub) elem = MergeEllipsis(sub, replaceHead: false);
                toAppend = elem;
            }
            AppendNode(ref result, ref resultTail, toAppend);
        }
        return result;
    }

    public static object? Check(object? obj)
    {
        if (macros.Count == 0) return obj;
        var result = new MacroExpander().Expand(obj, shadowed: null);
        Symbol.ClearGensyms();
        return result;
    }

    private class MacroExpander
    {
        private static readonly Symbol _sLambda = Symbol.Create("LAMBDA");
        Dictionary<object, object?> vars = [];
        Dictionary<object, object?> cons = [];
        Dictionary<object, object?> temp = [];
        bool more;

        private static HashSet<Symbol>? ExtendShadowed(HashSet<Symbol>? shadowed, IEnumerable<Symbol> names)
        {
            HashSet<Symbol>? updated = shadowed;
            foreach (var name in names)
            {
                updated ??= [];
                updated.Add(name);
            }
            return updated;
        }

        private static IEnumerable<Symbol> GetBoundSymbols(object? formals)
        {
            if (formals is Symbol single)
            {
                if (!Symbol.IsEqual(".", single))
                    yield return single;
                yield break;
            }

            if (formals is not Pair pair || Pair.IsNull(pair))
                yield break;

            for (var current = pair; current != null; current = current.cdr)
            {
                if (current.car is Symbol symbol)
                {
                    if (!Symbol.IsEqual(".", symbol))
                        yield return symbol;
                    continue;
                }

                if (current.car is Pair nested)
                {
                    foreach (var nestedSymbol in GetBoundSymbols(nested))
                        yield return nestedSymbol;
                }
            }
        }

        private static bool IsShadowed(HashSet<Symbol>? shadowed, object? identifier) =>
            identifier is Symbol symbol && shadowed?.Contains(symbol) == true;

        object? Variable(object? v, object? val, bool all)
        {
            var sym = Symbol.Create(all ? (v!.ToString()! + "...") : v!.ToString()!);
            return vars[sym] = all ? Pair.Append(vars.GetValueOrDefault(sym) as Pair, val) : val!;
        }

        bool IsMatch(Pair? obj, Pair? pat, bool all, HashSet<Symbol>? shadowed)
        {
            for (; pat != null; pat = pat.cdr)
            {
                switch (pat.car, obj?.car)
                {
                    case (_, _) when Pair.IsNull(pat.car) && Pair.IsNull(obj?.car):
                        obj = obj?.cdr;
                        break;
                    case (_, _) when Pair.IsNull(pat.car) && !Pair.IsNull(obj?.car):
                        return false;
                    case (Symbol patSym, _) when patSym.ToString().Contains("..."):
                        Variable(pat.car, obj, all);
                        return true;
                    case (_, _) when obj == null:
                        return false;
                    case (not Symbol and not Pair, _) when Equals(pat.car, obj.car):
                        obj = obj.cdr;
                        break;
                    case (Symbol, _) when cons.TryGetValue(pat.car, out _) && IsShadowed(shadowed, obj.car):
                        return false;
                    case (Symbol, _) when cons.TryGetValue(pat.car, out var constVal) && obj.car != constVal:
                        return false;
                    case (Symbol, _) when cons.TryGetValue(pat.car, out _):
                        obj = obj.cdr;
                        break;
                    case (_, _) when pat.cdr?.car?.ToString() == "...":
                        foreach (object x in obj!)
                            if (!IsMatch(x as Pair, pat.car as Pair, true, shadowed))
                                return false;
                        return true;
                    case (Symbol, _):
                        Variable(pat.car, obj.car, all);
                        obj = obj.cdr;
                        break;
                    case (Pair, _) when IsMatch(obj.car as Pair, pat.car as Pair, all, shadowed):
                        obj = obj.cdr;
                        break;
                    default:
                        return false;
                }
            }
            return obj == null;
        }

        object? Transform(object? obj, bool repeat)
        {
            if (obj == null) return null;
            if (obj is not Pair)
                return vars.TryGetValue(obj, out var v) ? v : obj;
            Pair? retval = null;
            Pair? retvalTail = null;
            void AppendNode(object? val) => Pair.AppendTail(ref retval, ref retvalTail, val);
            for (; obj != null; obj = (obj as Pair)?.cdr)
            {
                var current = (Pair)obj;
                object? o = current.car;
                Pair? next = current.cdr;
                if (o is Symbol sym && vars.TryGetValue(sym, out var symVal))
                {
                    if (!sym.ToString().Contains("..."))
                        AppendNode(symVal);
                    else if (!repeat)
                    {
                        if (symVal != null)
                            foreach (object x in (Pair)symVal!)
                                AppendNode(x);
                    }
                    else
                    {
                        temp.TryAdd(sym, symVal);
                        AppendNode((temp[sym] as Pair)!.car);
                        more = more && temp[sym] != null && (temp[sym] as Pair)!.cdr != null;
                    }
                }
                else if (o is Symbol genSym && genSym.ToString()[0] == '?')
                    AppendNode(Symbol.CreateGensym(genSym.ToString() + _symbol));
                else if (next?.car?.ToString() == "...")
                {
                    more = true;
                    temp = [];
                    while (more)
                    {
                        AppendNode(Transform(o!, true));
                        foreach (object xx in vars.Keys)
                            if (temp.TryGetValue(xx, out var tv) && tv is Pair tp)
                                temp[xx] = tp.cdr;
                    }
                    temp = [];
                    obj = next;
                }
                else if (o is not Pair)
                    AppendNode(o);
                else
                    AppendNode(Transform(o!, repeat));
            }
            return retval;
        }

        public object? Expand(object? obj, HashSet<Symbol>? shadowed)
        {
            if (obj is not Pair objPair) return obj;
            if (Pair.IsNull(objPair)) return objPair;
            if (objPair.car is Symbol head && !IsShadowed(shadowed, head) && macros.TryGetValue(head, out var macroVal))
            {
                var macroEntry = (Pair)macroVal!;
                foreach (object o in macroEntry.cdr!)
                {
                    _symbol++;
                    vars = [];
                    cons = [];
                    cons[Symbol.Create("_")] = objPair.car;
                    AddLiteralConstants(cons, macroEntry.car as Pair);
                    var clause = (Pair)o;
                    if (IsMatch(objPair, (Pair)clause.car!, false, shadowed))
                    {
                        if (Expression.IsTraceOn(Symbol.Create("match")))
                            ConsoleOutput.WriteTrace($"MATCH {objPair.car}: {clause.car} ==> {clause.cdr!.car}");
                        obj = Expand(Transform(clause.cdr!.car, false), shadowed);
                        break;
                    }
                }
            }
            if (obj is not Pair resultPair) return obj;

            if (ReferenceEquals(resultPair.car, _sLambda))
                return ExpandLambda(resultPair, shadowed);

            Pair? retval = null;
            Pair? retvalTail = null;
            foreach (object o in resultPair)
                Pair.AppendTail(ref retval, ref retvalTail, Expand(o, shadowed));
            return retval;
        }

        private object? ExpandLambda(Pair lambdaForm, HashSet<Symbol>? shadowed)
        {
            Pair? expanded = null;
            Pair? expandedTail = null;
            int index = 0;
            var lambdaShadowed = shadowed;

            foreach (object item in lambdaForm)
            {
                object? expandedItem;
                if (index < 2)
                {
                    expandedItem = Expand(item, shadowed);
                    if (index == 1)
                        lambdaShadowed = ExtendShadowed(shadowed, GetBoundSymbols(item));
                }
                else
                {
                    expandedItem = Expand(item, lambdaShadowed);
                }

                Pair.AppendTail(ref expanded, ref expandedTail, expandedItem);
                index++;
            }

            return expanded;
        }
    }
}
