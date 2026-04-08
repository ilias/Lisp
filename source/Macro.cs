namespace Lisp;

public static class Macro
{
    private static readonly Symbol _sQuote = Symbol.Create("quote");

    private static LispException SyntaxRulesError(object? sourceObj, string message) =>
        new LispException(message).AttachSchemeContext(Util.GetSource(sourceObj), null);

    private static void ValidateLiteralIdentifierList(Pair? literals, object? sourceObj, string owner)
    {
        if (literals == null)
            throw SyntaxRulesError(sourceObj, $"{owner}: expected a literal identifier list");

        if (Pair.IsNull(literals))
            return;

        foreach (object? literal in literals)
            if (literal is not Symbol symbol)
                throw SyntaxRulesError(literals, $"{owner}: literal identifiers must be symbols");
            else if (symbol.ToString() is "..." or "_")
                throw SyntaxRulesError(literals, $"{owner}: literal identifiers cannot include reserved pattern markers");
    }

    private static void ValidateEllipsisUsage(object? form, bool inPattern, object? sourceObj)
    {
        if (form is not Pair pair || Pair.IsNull(pair))
            return;

        object? previous = null;
        foreach (object? item in pair)
        {
            if (item is Symbol symbol && symbol.ToString() == "...")
            {
                if (previous == null)
                    throw SyntaxRulesError(sourceObj, inPattern
                        ? "syntax-rules: ellipsis must follow a pattern element"
                        : "syntax-rules: ellipsis must follow a template element");

                if (previous is Symbol prevSymbol && prevSymbol.ToString() == "...")
                    throw SyntaxRulesError(sourceObj, inPattern
                        ? "syntax-rules: duplicate ellipsis is not allowed in patterns"
                        : "syntax-rules: duplicate ellipsis is not allowed in templates");

                previous = item;
                continue;
            }

            ValidateEllipsisUsage(item, inPattern, sourceObj);
            previous = item;
        }
    }

    private static void ValidateSyntaxRulesClauses(Pair? rawClauses, object? sourceObj, string owner)
    {
        if (rawClauses == null || Pair.IsNull(rawClauses))
            throw SyntaxRulesError(sourceObj, $"{owner}: expected at least one syntax-rules clause");

        foreach (object? rawClause in rawClauses)
        {
            if (rawClause is not Pair clause)
                throw SyntaxRulesError(rawClauses, $"{owner}: each syntax-rules clause must be a list");

            if (clause.car is not Pair)
                throw SyntaxRulesError(clause, $"{owner}: each syntax-rules clause must start with a pattern list");

            if (clause?.Count != 2)
                throw SyntaxRulesError(clause, $"{owner}: each syntax-rules clause must contain exactly a pattern and template");

            ValidateEllipsisUsage(clause.car, inPattern: true, clause);
            ValidateEllipsisUsage(clause.CdrPair?.car, inPattern: false, clause.CdrPair?.car ?? clause);
        }
    }

    private static LispException NoMatchingClauseError(Symbol macroName, Pair invocation, Pair macroEntry)
    {
        var patterns = new List<string>();
        foreach (object? rawClause in macroEntry.CdrPair ?? Pair.Empty)
            if (rawClause is Pair clause)
                patterns.Add(Util.Dump(clause.car));

        string patternSummary = patterns.Count == 0 ? "<none>" : string.Join(" | ", patterns);
        return SyntaxRulesError(
            invocation,
            $"syntax-rules: macro '{macroName}' had no matching clause for {Util.Dump(invocation)}; patterns: {patternSummary}");
    }

    internal static Dictionary<object, object?> macros => InterpreterContext.RequireCurrent().Macros;
    public static IReadOnlyDictionary<object, object?> CurrentDefinitions => InterpreterContext.RequireCurrent().Macros;

    private static Dictionary<object, object?> CurrentMacros => InterpreterContext.RequireCurrent().Macros;

    private static Dictionary<object, string> CurrentDocComments => InterpreterContext.RequireCurrent().MacroDocComments;
    public static IReadOnlyDictionary<object, string> DocComments => CurrentDocComments;

    private static int NextExpansionId() => ++InterpreterContext.RequireCurrent().MacroSymbolCounter;

    private static int NextWildcardCounter() => InterpreterContext.RequireCurrent().MacroWildcardCounter++;

    public static void SetDocComment(object name, string? comment)
    {
        if (!string.IsNullOrEmpty(comment))
            CurrentDocComments[name] = comment;
    }

    public static string GetDocComment(object name) =>
        CurrentDocComments.TryGetValue(name, out var c) ? c : "";

    public static Dictionary<object, object?> Snapshot() => new(CurrentMacros);

    public static void Restore(Dictionary<object, object?> snapshot)
    {
        CurrentMacros.Clear();
        foreach (var kv in snapshot)
            CurrentMacros[kv.Key] = kv.Value;
    }

    public static void Clear()
    {
        CurrentMacros.Clear();
        CurrentDocComments.Clear();
    }

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
        CurrentMacros[obj.car!] = obj.cdr;
    }

    public static void Set(object name, object? value)
    {
        CurrentMacros[name] = value;
    }

    public static Pair? TranslateDefineSyntax(Pair ds)
    {
        if (ds.cdr is not Pair np)
            throw SyntaxRulesError(ds, "define-syntax: expected a name and syntax-rules transformer");

        var name = np.car;
        if (np.cdr is not Pair srCell || srCell.car is not Pair sr || sr.car?.ToString() != "syntax-rules")
            throw SyntaxRulesError(ds, "define-syntax: expected a syntax-rules transformer");

        ValidateLiteralIdentifierList(sr.CdrPair?.car as Pair, sr, "syntax-rules");
        ValidateSyntaxRulesClauses(sr.CdrPair?.CdrPair, sr, "syntax-rules");
        return TranslateSyntaxRules(name, sr.CdrPair?.car as Pair, sr.CdrPair?.CdrPair);
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
                var tmpl = clause.CdrPair?.car;
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
            // Save non-Pair (dotted) cdr before advancing so we can attach it at the end
            var pendingDottedCdr = p.cdr is not Pair && p.cdr != null ? p.cdr : null;
            p = p.CdrPair;
            object? toAppend;
            if (first) { toAppend = Symbol.Create("_"); first = false; }
            else if (elem is Symbol ws && ws.ToString() == "_")
                toAppend = Symbol.Create($"?wc{NextWildcardCounter()}");
            else if (elem is Symbol sym && !sym.ToString().Contains("...") && p?.car?.ToString() == "...")
            { toAppend = Symbol.Create(sym + "..."); p = p.CdrPair; }
            else
            {
                if (elem is Pair sub) elem = MergeEllipsis(sub, replaceHead: false);
                toAppend = elem;
            }
            AppendNode(ref result, ref resultTail, toAppend);
            // If this was the last node and had a dotted non-Pair cdr, attach translated tail
            if (p == null && pendingDottedCdr != null && resultTail != null)
            {
                object translated = pendingDottedCdr is Symbol ds && ds.ToString() == "_"
                    ? Symbol.Create($"?wc{NextWildcardCounter()}")
                    : pendingDottedCdr;
                resultTail.cdr = translated;
            }
        }
        return result;
    }

    public static object? Check(object? obj)
    {
        if (CurrentMacros.Count == 0) return obj;
        var result = new MacroExpander().Expand(obj, shadowed: null);
        Symbol.ClearGensyms();
        return result;
    }

    private class MacroExpander
    {
        private static readonly Symbol _sLambda = Symbol.Create("LAMBDA");
        Dictionary<object, object?> vars = [];
        Dictionary<object, object?> cons = [];
        // Tracks the advancing cursor into each captured ellipsis list during template expansion.
        Dictionary<object, object?> _ellipsisCursors = [];
        // Set to false once all ellipsis cursors are exhausted, ending the expansion loop.
        bool _hasMoreEllipsis;
        int expansionId;

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

            for (var current = pair; current != null; current = current.CdrPair)
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

                // Yield dotted rest symbol (e.g. (a . rest) -> yield rest)
                if (current.cdr is Symbol dottedRest && !Symbol.IsEqual(".", dottedRest))
                    yield return dottedRest;
            }
        }

        private static bool IsShadowed(HashSet<Symbol>? shadowed, object? identifier) =>
            identifier is Symbol symbol && shadowed?.Contains(symbol) == true;

        object? Variable(object? v, object? val, bool all)
        {
            var sym = Symbol.Create(all ? (v!.ToString()! + "...") : v!.ToString()!);
            return vars[sym] = all ? Pair.Append(vars.GetValueOrDefault(sym) as Pair, val) : val!;
        }

        // Expands an ellipsis repetition: evaluates 'template' with repeat=true until all
        // captured ellipsis variables are exhausted. Uses _ellipsisCursors to track progress.
        void ExpandRepeatTemplate(object? template, Action<object?> append)
        {
            _hasMoreEllipsis = true;
            _ellipsisCursors = [];
            while (_hasMoreEllipsis)
            {
                append(Transform(template, true));
                // Advance each cursor to the next element, or to the dotted tail if last.
                foreach (object key in vars.Keys)
                    if (_ellipsisCursors.TryGetValue(key, out var cursor) && cursor is Pair cp)
                        _ellipsisCursors[key] = (cp.CdrPair == null && cp.cdr != null) ? cp.cdr : cp.CdrPair;
            }
            _ellipsisCursors = [];
        }

        // Matches input list 'obj' against syntax-rules pattern 'pat' (R7RS §7.1.2).
        // Pattern symbols in 'cons' (the literal-identifier list) match by value;
        // all other symbols are pattern variables that capture the corresponding input.
        // Symbols whose name ends in "..." capture zero or more remaining elements.
        // Returns true on a successful match; captured bindings are stored in 'vars'.
        bool IsMatch(Pair? obj, Pair? pat, bool all, HashSet<Symbol>? shadowed)
        {
            // dottedCdrSym records a non-Pair cdr seen on the input side so we can
            // distinguish a proper list tail (null) from a dotted tail (symbolX).
            Symbol? dottedCdrSym = null;
            for (; pat != null; pat = pat.CdrPair)
            {
                switch (pat.car, obj?.car)
                {
                    case (_, _) when Pair.IsNull(pat.car) && Pair.IsNull(obj?.car):
                        dottedCdrSym = null;
                        obj = obj?.CdrPair;
                        break;
                    case (_, _) when Pair.IsNull(pat.car) && !Pair.IsNull(obj?.car):
                        return false;
                    case (Symbol patSym, _) when patSym.ToString().Contains("..."):
                    {
                        // Capture remaining obj, or re-encode a dangling dotted cdr as (. sym).
                        object? capture = obj;
                        if (obj == null && dottedCdrSym != null)
                            capture = new Pair(Symbol.Create("."), new Pair(dottedCdrSym));
                        Variable(pat.car, capture, all);
                        dottedCdrSym = null;
                        return true;
                    }
                    case (_, _) when obj == null:
                        return false;
                    case (not Symbol and not Pair, _) when Equals(pat.car, obj.car):
                        dottedCdrSym = obj.cdr is Symbol s0 ? s0 : null;
                        obj = obj.CdrPair;
                        break;
                    case (Symbol, _) when cons.TryGetValue(pat.car, out _) && IsShadowed(shadowed, obj.car):
                        return false;
                    case (Symbol, _) when cons.TryGetValue(pat.car, out var constVal) && obj.car != constVal:
                        return false;
                    case (Symbol, _) when cons.TryGetValue(pat.car, out _):
                        dottedCdrSym = obj.cdr is Symbol s1 ? s1 : null;
                        obj = obj.CdrPair;
                        break;
                    case (_, _) when pat.CdrPair?.car?.ToString() == "...":
                        foreach (object x in obj!)
                            if (!IsMatch(x as Pair, pat.car as Pair, true, shadowed))
                                return false;
                        return true;
                    case (Symbol, _):
                        Variable(pat.car, obj.car, all);
                        dottedCdrSym = obj.cdr is Symbol s2 ? s2 : null;
                        obj = obj.CdrPair;
                        break;
                    case (Pair, _) when IsMatch(obj.car as Pair, pat.car as Pair, all, shadowed):
                        dottedCdrSym = obj.cdr is Symbol s3 ? s3 : null;
                        obj = obj.CdrPair;
                        break;
                    default:
                        return false;
                }
                // After successfully matching pat.car, check whether the pattern itself has a
                // dotted-rest tail (e.g. (a b . rest)). If so, capture all remaining input.
                if (pat.cdr is Symbol dottedPatVar)
                {
                    Variable(dottedPatVar, obj, all);
                    return true;
                }
            }
            // Succeed only if both obj and any dotted-cdr are exhausted
            return obj == null && dottedCdrSym == null;
        }

        // Substitutes pattern variables into template 'obj' (R7RS §7.1.3).
        // 'repeat' is true when this call is inside an ellipsis expansion loop —
        // in that case variable values are consumed one element at a time via _ellipsisCursors.
        object? Transform(object? obj, bool repeat)
        {
            if (obj == null) return null;
            if (obj is not Pair)
                return vars.TryGetValue(obj, out var v) ? v : obj;
            Pair? retval = null;
            Pair? retvalTail = null;
            void AppendNode(object? val) => Pair.AppendTail(ref retval, ref retvalTail, val);
            for (; obj != null; obj = (obj as Pair)?.CdrPair)
            {
                var current = (Pair)obj;
                object? o = current.car;
                Pair? next = current.CdrPair;
                if (o is Symbol sym && vars.TryGetValue(sym, out var symVal))
                {
                    if (!sym.ToString().Contains("..."))
                        AppendNode(symVal);
                    else if (!repeat)
                    {
                        if (symVal is Pair symPair)
                        {
                            foreach (object x in symPair)
                                AppendNode(x);
                            // Propagate any dotted (non-Pair) cdr from the captured list
                            var lastNode = symPair;
                            while (lastNode.CdrPair != null) lastNode = lastNode.CdrPair;
                            if (lastNode.cdr is { } dottedTail && lastNode.cdr is not Pair && retvalTail != null)
                                retvalTail.cdr = dottedTail;
                        }
                    }
                    else
                    {
                        // Inside a repeat context: initialise the cursor on first encounter,
                        // then advance it and check whether more elements remain.
                        _ellipsisCursors.TryAdd(sym, symVal);
                        AppendNode((_ellipsisCursors[sym] as Pair)!.car);
                        _hasMoreEllipsis = _hasMoreEllipsis
                            && _ellipsisCursors[sym] != null
                            && (_ellipsisCursors[sym] as Pair)!.CdrPair != null;
                    }
                }
                else if (o is Symbol genSym && genSym.ToString()[0] == '?')
                    AppendNode(Symbol.CreateGensym(genSym.ToString() + expansionId));
                else if (next?.car?.ToString() == "...")
                {
                    // Expand the ellipsis template element, iterating until all captured
                    // lists are consumed.  The inner Transform calls update _ellipsisCursors.
                    ExpandRepeatTemplate(o!, AppendNode);
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
            if (ReferenceEquals(objPair.car, _sQuote)) return objPair;
            if (objPair.car is Symbol head && !IsShadowed(shadowed, head) && CurrentMacros.TryGetValue(head, out var macroVal))
            {
                var macroEntry = (Pair)macroVal!;
                bool matched = false;
                foreach (object o in macroEntry.CdrPair!)
                {
                    expansionId = NextExpansionId();
                    vars = [];
                    cons = [];
                    cons[Symbol.Create("_")] = objPair.car;
                    AddLiteralConstants(cons, macroEntry.car as Pair);
                    var clause = (Pair)o;
                    if (IsMatch(objPair, (Pair)clause.car!, false, shadowed))
                    {
                        matched = true;
                        if (Expression.IsTraceOn(Symbol.Create("match")))
                            ConsoleOutput.WriteTrace($"MATCH {objPair.car}: {clause.car} ==> {clause.CdrPair!.car}");
                        obj = Expand(Transform(clause.CdrPair!.car, false), shadowed);
                        break;
                    }
                }

                if (!matched)
                    throw NoMatchingClauseError(head, objPair, macroEntry);
            }
            if (obj is not Pair resultPair) return obj;

            if (ReferenceEquals(resultPair.car, _sLambda))
                return ExpandLambda(resultPair, shadowed);

            Pair? retval = null;
            Pair? retvalTail = null;
            for (var cur = resultPair; cur != null; cur = cur.CdrPair)
            {
                Pair.AppendTail(ref retval, ref retvalTail, Expand(cur.car, shadowed));
                // Preserve dotted tail (non-Pair, non-null cdr)
                if (cur.cdr != null && cur.CdrPair == null)
                {
                    retvalTail!.cdr = Expand(cur.cdr, shadowed);
                    break;
                }
            }
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
                if (index == 0)
                {
                    expandedItem = Expand(item, shadowed);
                }
                else if (index == 1)
                {
                    expandedItem = item;
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
