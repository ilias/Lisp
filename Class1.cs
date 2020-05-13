/* [build 2 - Ilias H. Mavreas - reports@midstate.com ]
 *	add - ((if #t + *) 3 4) ==> 5, all forms of 'define' including internal
 *	add - 'get' and 'set' now support index properties.
 *	add - , ,@ let map begin apply cond list number? #( vector... try throw eval
 *	add - let* letrec (macro support) (trace support)
 */

using System;
using System.IO;
using System.Collections;
using System.Reflection;
using System.Text;
using Tachy.Environment;
using Tachy.Expressions;
using Tachy.Programs;
using Tachy.Macros;

namespace Tachy
{
    public class Util
    {
        static public string GAC = null;
        static Util()
        {
            GAC = System.Environment.GetEnvironmentVariable("systemroot");
            string ver = System.Environment.Version.ToString();
            GAC += "\\Microsoft.NET\\Framework\\v" + ver.Substring(0, ver.LastIndexOf(".")) + "\\";
        }
        static public Type[] GetTypes(object[] objs)
        {
            Type[] retval = new Type[objs.Length];
            for (int i = 0; i < objs.Length; i++)
                retval[i] = (objs[i] == null) ? typeof(object) : objs[i].GetType();
            return retval;
        }
        public static object Call_Method(Pair args, bool staticCall)
        {
            object[] objs = args.cdr.cdr != null ? args.cdr.cdr.ToArray() : null;
            Type[] types = args.cdr.cdr != null ? GetTypes(objs) : new Type[0];
            Type type = staticCall ? Util.GetType(args.car.ToString()) : args.car.GetType();
            return type.GetMethod(args.cdr.car.ToString(), types).Invoke(args.car, objs);
        }
        public static Type GetType(string tname)
        {
            Type type = Assembly.LoadFrom(GAC + "system.dll").GetType(tname);
            if (type != null)
                return type;
            foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                if ((type = assembly.GetType(tname)) != null)
                    return type;
            string[] comp = tname.Split(new char[] { '@' }); // 'file@class or '~file@class
            comp[0] = comp[0].Replace("~", GAC);  // replace ~ with the GAC directory
            if (comp.Length == 2) // 'file@class or 'path\file@class
                if ((type = Assembly.LoadFrom(comp[0]).GetType(comp[1])) != null)
                    return type;
            return null;
        }
        static public void Throw(string message)
        {
            throw new Exception(message);
        }
        static public string Dump(string title, params object[] args)
        {
            StringBuilder output = new StringBuilder("[" + title + " ");
            foreach (object o in args)
                output.Append(Dump(o) + " ");
            return output.Append("]").ToString();
        }
        static public string Dump(object exp)
        {
            if (Pair.IsNull(exp)) return "()";
            if (exp is string) return '"' + exp.ToString() + '"';
            if (exp is bool) return ((bool)exp) ? "#t" : "#f";
            if (exp is char) return "#\\" + (char)exp;
            if (exp is Pair && (exp as Pair).car is Symbol
                && ((exp as Pair).car as Symbol).ToString() == "quote")
                return "\'" + Dump((exp as Pair).cdr.car);
            if (!(exp is ICollection)) return exp.ToString();
            StringBuilder retVal = new StringBuilder("( ");
            foreach (object o in exp as ICollection)
                retVal.Append(Dump(o) + " ");
            return (exp is ArrayList ? "#" : "") + retVal.Append(")").ToString();
        }
        static public object Parse(string str, out string after)
        {
            object retval = null;
            int pos = 0;
            char[] trim = new char[] { ' ', '\n', '\r', '\t' };
            StringBuilder cVal = new StringBuilder();

            str = str.TrimStart(trim).TrimEnd(trim);
            after = str;
            if (str.Length == 0) return null;
            if (char.IsDigit(str[pos]) || (str[pos] == '-' && char.IsDigit(str[pos + 1])))// Number 
            {
                cVal.Append(str[pos++]); // prime due to possible minus
                while (pos < str.Length && "01234567890.".IndexOf(str[pos]) >= 0)
                    cVal.Append(str[pos++]);
                after = str.Substring(pos);
                if (cVal.ToString().IndexOf('.') == -1) // cannot use ?: because return 'Single'
                    return int.Parse(cVal.ToString());
                return float.Parse(cVal.ToString());
            }
            switch (str[pos++])
            {
                case ';':  // Comment
                    for (pos--; pos < str.Length && "\n\r".IndexOf(str[pos]) == -1; pos++) ;
                    return Parse(str.Substring(pos), out after);
                case ',': // , and ,@
                    Symbol sym = Symbol.Create(str[pos] == '@' ? ",@" : ",");
                    string act = str.Substring(str[pos] == '@' ? ++pos : pos);
                    return Pair.Cons(sym, new Pair(Parse(act, out after)));
                case '\'': // Quote
                    object qItem = Parse(str.Substring(pos), out after);
                    return Pair.Cons(Symbol.Create("quote"), new Pair(qItem));
                case '#':  // Boolean, Character or Vector
                    switch (str[pos++])
                    {
                        case '\\': // character
                            retval = str[pos++];
                            break;
                        case '(':  // vector
                            Pair body = Parse(str.Substring(pos - 1), out after) as Pair;
                            return new ArrayList(body.ToArray());
                        default:   // boolean
                            retval = (str[pos - 1] == 't') ? true : false;
                            break;
                    }
                    break;
                case '"':  // string
                    for (; pos < str.Length && str[pos] != '"'; pos++)
                        if (str[pos] == '\\')
                            switch (str[++pos])
                            {
                                case 'n': cVal.Append("\n"); break;
                                default: cVal.Append(str[pos]); break;
                            }
                        else
                            cVal.Append(str[pos]);
                    pos++;
                    retval = cVal.ToString();
                    break;
                case '(':  // Pair
                    str = str.Substring(pos);
                    for (object cItem = 1; (cItem = Parse(str, out after)) != null; str = after)
                        if (cItem != null)
                            retval = Pair.Append(retval as Pair, cItem);
                    return retval == null ? new Pair(null) : retval;
                case ')':   // End of Pair
                    retval = null;
                    break;
                case '\\':  // \<vars>.<expression> == (LAMBDA (<var>) <expression>) where vars = v1,v2,...
                    for (; pos < str.Length && str[pos] != '.'; pos++)
                        cVal.Append(str[pos]);
                    Pair vars = null;
                    foreach (string id in cVal.ToString().Split(new char[] { ',' }))
                        if (vars == null)
                            vars = new Pair(Symbol.Create(id));
                        else
                            vars.Append(Symbol.Create(id));
                    return new Pair("LAMBDA", new Pair(vars, new Pair(Parse(str.Substring(++pos), out after))));
                default:   // Symbol
                    for (pos--; pos < str.Length && "()\n\r\t #,\'\"".IndexOf(str[pos]) == -1; )
                        cVal.Append(str[pos++]);
                    retval = Symbol.Create(cVal.ToString());
                    break;
            }
            after = str.Substring(pos);
            return retval;
        }
    }

    public class Symbol
    {
        static public Hashtable syms = new Hashtable();
        static public int symNum = 1000;
        string val;

        private Symbol(string val) { this.val = val; }
        static public Symbol GenSym()
        {
            return Create("_sym_" + (symNum++).ToString());
        }
        static public Symbol Create(string symName)
        {
            if (syms[symName] == null)
                syms.Add(symName, new Symbol(symName));
            return (Symbol)syms[symName];
        }
        static public bool IsEqual(string id, object obj)
        {
            return obj is Symbol && id == (obj as Symbol).val;
        }
        override public string ToString() { return val.ToString(); }
    }

    public class Closure
    {
        public Pair ids, body;
        public Env env, inEnv;

        public Closure(Pair ids, Pair body, Env env) { this.ids = ids; this.body = body; this.env = env; }
        public object Eval(Pair args)
        {
            if (Expression.IsTraceOn(Symbol.Create("closure")))
                Console.WriteLine(Util.Dump("closure: ", ids, body, args));
            object retval = null;
            inEnv = env.Extend(ids, args);
            foreach (Expression exp in body)
                retval = exp.Eval(inEnv);
            return retval;
        }
        override public string ToString() { return Util.Dump("closure", ids, body); }
    }

    public class Pair : ICollection
    {
        public static Pair Append(Pair link, object obj)
        {
            if (link == null) return new Pair(obj);
            link.Append(obj);
            return link;
        }
        public static bool IsNull(object obj)
        {
            if (obj == null) return true;
            if (obj is Pair) return (obj as Pair).car == null && (obj as Pair).cdr == null;
            return false;
        }
        public static Pair Cons(object obj, object p)
        {
            Pair newPair = new Pair(obj);
            if (Pair.IsNull(p)) return newPair;
            newPair.cdr = (p == null || p is Pair) ? (Pair)p : new Pair(p);
            return newPair;
        }

        public void Append(object obj)
        {
            Pair curr = this;
            while (curr.cdr != null)
                curr = curr.cdr;
            curr.cdr = new Pair(obj);
        }
        public int Count
        {
            get
            {
                int len = 0;
                foreach (object obj in this) len++;
                return len;
            }
        }
        public void CopyTo(Array array, int index)
        {
            if (array.Length < (this.Count + index))
                throw new ArgumentException();
            foreach (object obj in this)
                array.SetValue(obj, index++);
        }
        public IEnumerator GetEnumerator()
        {
            return new PairEnumerator(this);
        }

        class PairEnumerator : IEnumerator
        {
            Pair pair, current;
            public PairEnumerator(Pair pair) { this.pair = pair; this.current = null; }
            public object Current { get { return current.car; } }
            public bool MoveNext()
            {
                if (current == null)
                    current = pair;
                else if (current.cdr != null)
                    current = current.cdr;
                else
                    return false;
                return true;
            }
            public void Reset() { current = pair; }
        }

        public bool IsSynchronized { get { return false; } }
        public object SyncRoot { get { return this; } }
        public object[] ToArray()
        {
            object[] retval = new object[Count];
            CopyTo(retval, 0);
            return retval;
        }
        public object car;
        public Pair cdr;
        public Pair(object car) : this(car, null) { }
        public Pair(object car, Pair cdr) { this.car = car; this.cdr = cdr; }
        override public string ToString() { return Util.Dump(this); }
    }

    namespace Macros
    {
        public class Macro
        {
            static public Hashtable macros = new Hashtable();
            static public Hashtable vars = new Hashtable();
            static public Hashtable cons = new Hashtable();
            static public Hashtable temp = new Hashtable();
            static public void Add(Pair obj)
            {
                macros[obj.car] = obj.cdr;
            }
            static public object Variable(object var, object val, bool all)
            {
                var = Symbol.Create(all ? (var.ToString() + "...") : var.ToString());
                return (vars[var] = all ? Pair.Append(vars[var] as Pair, val) : val);
            }
            static public bool IsMatch(Pair obj, Pair pat, bool all)
            {
                for (; pat != null; pat = pat.cdr)
                    if (Pair.IsNull(pat.car) && Pair.IsNull((obj as Pair).car))
                        obj = obj.cdr;
                    else if (Pair.IsNull(pat.car) && !Pair.IsNull((obj as Pair).car))
                        return false;
                    else if (pat.car is Symbol && pat.car.ToString().IndexOf("...") > 0)
                    { // is the last item (variable containing ... in name takes rest 
                        Variable(pat.car, obj, all);
                        return true;
                    }
                    else if (obj == null) return false;
                    else if (pat.car is Symbol && cons.ContainsKey(pat.car)) // is a constant
                    {
                        if (obj.car != cons[pat.car]) return false;
                        obj = obj.cdr;
                    }
                    else if (pat.cdr != null && pat.cdr.car.ToString() == "...")
                    {
                        foreach (object x in obj)
                            if (!IsMatch(x as Pair, pat.car as Pair, true))
                                return false;
                        return true;
                    }
                    else if (pat.car is Symbol) // variable
                    {
                        Variable(pat.car, obj.car, all);
                        obj = obj.cdr;
                    }
                    else if (IsMatch(obj.car as Pair, pat.car as Pair, all)) // first element
                        obj = obj.cdr;
                    else
                        return false;
                return obj == null;
            }
            static public bool more = false;
            static public object Transform(object obj, bool repeat)
            {
                if (!(obj is Pair))
                    return vars.ContainsKey(obj) ? vars[obj] : obj;  // var or val or name
                Pair retval = null;
                for (; obj != null; obj = (obj as Pair).cdr)
                {
                    object o = (obj as Pair).car;
                    Pair next = (obj as Pair).cdr;
                    // if not repeating variable
                    if (o is Symbol && vars.ContainsKey(o) && o.ToString().IndexOf("...") == -1)
                        retval = Pair.Append(retval, vars[o]);
                    else if (o is Symbol && vars.ContainsKey(o) && !repeat)
                    {
                        if (vars[o] != null)
                            foreach (object x in vars[o] as Pair)
                                retval = Pair.Append(retval, x);
                    }
                    else if (o is Symbol && vars.ContainsKey(o))
                    { // add only the car of the variable
                        if (!temp.ContainsKey(o))
                            temp[o] = vars[o];
                        retval = Pair.Append(retval, (temp[o] as Pair).car);
                        //repeat if more values
                        more = more && temp[o] != null && (temp[o] as Pair).cdr != null;
                    }
                    else if (o is Symbol && o.ToString()[0] == '?')
                        retval = Pair.Append(retval, Symbol.Create(o.ToString() + symbol));
                    else if (next != null && next.car.ToString() == "...")
                    { // (any) ... => repeat (any) until empty variable data - using car
                        more = true;
                        temp = new Hashtable();
                        while (more)
                        {
                            retval = Pair.Append(retval, Transform(o, true));
                            foreach (object xx in vars.Keys)
                                if (temp[xx] != null && temp[xx] is Pair)
                                    temp[xx] = (temp[xx] as Pair).cdr;
                        }
                        temp = new Hashtable();
                        obj = next;
                    }
                    else if (!(o is Pair)) // is contant value
                        retval = Pair.Append(retval, o);
                    else // is pair so transform every element
                        retval = Pair.Append(retval, Transform(o, repeat));
                }
                return retval;
            }
            static public object Check(object obj)
            {
                if (!(obj is Pair)) return obj;
                if ((obj as Pair).car is Symbol && macros.ContainsKey((obj as Pair).car))
                    foreach (object o in (macros[(obj as Pair).car] as Pair).cdr)
                    {
                        symbol++; // increment ?value symbol
                        vars = new Hashtable();
                        cons = new Hashtable();
                        cons[Symbol.Create("_")] = (obj as Pair).car; // macro name
                        if ((macros[(obj as Pair).car] as Pair).car != null) // constants
                            foreach (object x in (macros[(obj as Pair).car] as Pair).car as Pair)
                                if (x != null) cons[x] = x; // value of const is const
                        if (IsMatch(obj as Pair, (o as Pair).car as Pair, false))
                        {
                            if (Expression.IsTraceOn(Symbol.Create("match")))
                                Console.WriteLine("MATCH {0}: {1} ==> {2}",
                                    (obj as Pair).car,
                                    (o as Pair).car,
                                    (o as Pair).cdr.car);
                            obj = Check(Transform((o as Pair).cdr.car, false)); // macro in macro
                            break;
                        }
                    }
                if (!(obj is Pair)) return obj;
                Pair retval = null;
                foreach (object o in obj as Pair)
                    retval = Pair.Append(retval, Check(o));
                return retval;
            }
            static int symbol = 0;
        }
    }

    namespace Programs
    {
        public class Program
        {
            static public bool lastValue = true;
            static public Program current = null;
            public Env initEnv;
            public Program()
            {
                current = this;
                this.initEnv = new Extended_Env(null, null, new Env(), false);
            }
            public object Eval(Expression exp)
            {
                return exp.Eval(initEnv);
            }
            public object Eval(string exp)
            {
                string after;
                object parsedObj = Util.Parse(exp, out after);
                if (parsedObj is Pair && (parsedObj as Pair).car.ToString() == "macro")
                {
                    Macro.Add((parsedObj as Pair).cdr);
                    return (after == "") ? (parsedObj as Pair).cdr.car : Eval(after);
                }
                parsedObj = Macro.Check(parsedObj);
                if (Expression.IsTraceOn(Symbol.Create("macro")))
                    Console.WriteLine(Util.Dump("macro:   ", parsedObj));
                if (parsedObj is Pair && (parsedObj as Pair).car.ToString() == "DEFINE")
                {
                    Define def = new Define(parsedObj as Pair);
                    object name = def.Eval(initEnv);
                    return (after == "") ? name : Eval(after);
                }
                object answer = Eval(Expression.Parse(parsedObj));
                if (answer is Pair && (answer as Pair).car is Var)
                { // evaluate again if the first (car) is an unevaluated variable
                    Pair pagain = answer as Pair;
                    pagain.car = (pagain.car as Var).GetName();
                    answer = Eval(Expression.Parse(pagain));
                }
                if (after != "" && !lastValue) Console.WriteLine(Util.Dump(answer));
                return (after == "") ? answer : Eval(after);
            }
        }
    }
    namespace Environment
    {
        public class Env
        {
            public Hashtable table = new Hashtable();
            public Env Extend(Pair syms, Pair vals)
            {
                if (Pair.IsNull(syms)) return this;
                return new Extended_Env(syms, vals, this, true);
            }
            virtual public object Bind(Symbol id, object val)
            {
                throw new Exception("Unbound variable " + id);
            }
            virtual public object Apply(Symbol id)
            {
                throw new Exception("Unbound variable " + id);
            }
        }

        public class Extended_Env : Env
        {
            Env env = null;
            override public string ToString() { return Util.Dump("env", table, env); }
            public Extended_Env(Pair inSyms, Pair inVals, Env inEnv, bool eval)
            {
                env = inEnv;
                for (; inSyms != null; inSyms = inSyms.cdr)
                {
                    Symbol currSym = inSyms.car as Symbol;
                    if (Symbol.IsEqual(".", currSym))// multiple values passed in (R5RS 4.1.4)
                    {
                        table.Add(inSyms.cdr.car as Symbol, inVals);
                        break;
                    }
                    table.Add(currSym, inVals.car);
                    inVals = inVals.cdr;
                }
            }
            override public object Bind(Symbol id, object val)
            {
                if (!table.ContainsKey(id)) return env.Bind(id, val);
                table[id] = val;
                return id;
            }
            override public object Apply(Symbol id)
            {
                if (table.ContainsKey(id))
                    return table[id];
                return env.Apply(id);
            }
        }
    }

    namespace Expressions
    {
        public abstract class Expression
        {
            static public bool Trace = false;  // use (trace on) or (trace off)
            static public Hashtable traceHash = new Hashtable();
            static public bool IsTraceOn(Symbol s)
            {
                return Trace && (traceHash.ContainsKey(s) ||
                                 traceHash.ContainsKey(Symbol.Create("_all_")));
            }
            public abstract object Eval(Env env);
            static public Pair Eval_Rands(Pair rands, Env env)
            {
                if (rands == null) return null;
                Pair retval = null;
                foreach (object obj in rands)
                {
                    object o = (obj as Expression).Eval(env);
                    if (obj is CommaAt && o is Pair)
                        foreach (object oo in (o as Pair))
                            retval = Pair.Append(retval, oo);
                    else
                        retval = Pair.Append(retval, o);
                }
                return retval;
            }
            static public Expression Parse(object a)
            {
                if (a is Symbol) return new Var(a as Symbol);
                if (!(a is Pair)) return new Lit(a);
                Pair pair = a as Pair;
                Pair args = pair.cdr;
                Pair body = null;
                switch (pair.car.ToString())
                {
                    case "IF":     // (if test then else)
                        return new If(Parse(args.car), Parse(args.cdr.car), Parse(args.cdr.cdr.car));
                    case "DEFINE": // (define name <body>)
                        return new Define(pair);
                    case "EVAL":   // (eval ((if #f '* '+) 2 3))
                        return new Evaluate(Parse(args.car));
                    case "LAMBDA": // (lambda () body), (lambda (x ...) body) 
                        foreach (object obj in args.cdr)
                            body = Pair.Append(body, Parse(obj));
                        return new Lambda(args.car as Pair, body);
                    case "quote":  // (quote <body>) or '<body>
                        return new Lit(args.car);
                    case "set!":
                        return new Assignment(args.car as Symbol, Parse(args.cdr.car));
                    case "TRY":    // (try exp1 catch-exp)
                        return new Try(Parse(args.car), Parse(args.cdr.car));
                    default:
                        if (args != null)
                            foreach (object obj in args)
                                body = Pair.Append(body, Parse(obj));
                        if (pair.car.ToString() == ",@") return new CommaAt(body);
                        if (Prim.list[pair.car.ToString()] != null)
                            return new Prim(Prim.list[pair.car.ToString()] as Primitive, body);
                        return new App(Parse(pair.car), body);
                }
            }
        }

        public class Lit : Expression
        {
            object datum;
            public Lit(object datum) { this.datum = datum; }
            public override object Eval(Env env)
            {
                if (datum is Pair) return Comma(datum as Pair, env); //eval ',' and ',@'
                return datum;
            }
            public Pair Comma(Pair o, Env env)
            {
                Pair retVal = null;
                foreach (object car in o)
                    if (!(car is Pair))
                        retVal = Pair.Append(retVal, car);
                    else if (Symbol.IsEqual(",", (car as Pair).car))
                        retVal = Pair.Append(retVal, Parse((car as Pair).cdr.car).Eval(env));
                    else if (Symbol.IsEqual(",@", (car as Pair).car))
                    {
                        object ev = Parse((car as Pair).cdr.car).Eval(env);
                        if (ev != null && ev is Pair) // ,@( ... )
                            foreach (object oo in ev as Pair)
                                retVal = Pair.Append(retVal, oo);
                        else // ,@ <item>      - not a pair -  maybe not standard
                            retVal = ev == null ? retVal : Pair.Append(retVal, ev);
                    }
                    else // all levels
                        retVal = Pair.Append(retVal, Comma(car as Pair, env));
                return retVal;
            }
            public override string ToString() { return Util.Dump("lit", datum); }
            public string GetName() { return datum.ToString(); }
        }

        public class Evaluate : Expression
        {
            Expression datum;
            public Evaluate(Expression datum) { this.datum = datum; }
            public override object Eval(Env env)
            {
                object o = datum.Eval(env);
                if (o == null) return o;
                if (o is string) return Parse(Program.current.Eval(o as string)).Eval(env);
                return Parse(o).Eval(env);
            }
            public override string ToString() { return Util.Dump("EVAL", datum); }
        }

        public class Var : Expression
        {
            public Symbol id;
            public Var(Symbol id) { this.id = id; }
            public override object Eval(Env env)
            {
                return env.Apply(id);
            }
            public string GetName() { return id.ToString(); }
            override public string ToString() { return Util.Dump("var", id); }
        }

        public class Lambda : Expression
        {
            Pair ids;
            Pair body;
            public Lambda(Pair ids, Pair body) { this.ids = ids; this.body = body; }
            public override object Eval(Env env)
            {
                if (IsTraceOn(Symbol.Create("lambda")))
                    Console.WriteLine(Util.Dump("lambda: ", ids, body));
                return new Closure(ids, body, env);
            }
            override public string ToString() { return Util.Dump("LAMBDA", ids, body); }
        }

        public class Define : Expression
        {
            Pair datum;
            public Define(Pair datum) { this.datum = datum; }
            public override object Eval(Env env)
            {
                string name = datum.cdr.car.ToString();
                object body = datum.cdr.cdr.car;
                env.table[Symbol.Create(name)] = Parse(body).Eval(env);
                return Symbol.Create(name);
            }
            override public string ToString() { return Util.Dump("DEFINE", datum); }
        }

        public delegate object Primitive(Pair args);

        public class Prim : Expression
        {
            Primitive prim;
            Pair rands;
            public Prim(Primitive prim, Pair rands) { this.prim = prim; this.rands = rands; }
            public override object Eval(Env env)
            {
                return prim(Eval_Rands(rands, env));
            }
            override public string ToString() { return Util.Dump("prim", prim, rands); }

            static public Hashtable list = new Hashtable();
            static Prim()
            {
                list["LESSTHAN"] = new Primitive(LessThan_prim);    // < x y
                list["new"] = new Primitive(New_Prim);         // new object
                list["get"] = new Primitive(Get_Prim);         // get Property or Index
                list["set"] = new Primitive(Set_Prim);         // set Property or Index
                list["call"] = new Primitive(Call_Prim);        // call Method or object
                list["call-static"] = new Primitive(Call_Static_Prim); // call static Method
            }
            public static object New_Prim(Pair args)
            {
                Type type = Util.GetType(args.car.ToString());
                if (Pair.IsNull(args.cdr))
                    return Activator.CreateInstance(type);
                return Activator.CreateInstance(type, args.cdr.ToArray(), null);
            }
            public static object LessThan_prim(Pair args)
            {
                return double.Parse(args.car.ToString()) < double.Parse(args.cdr.car.ToString());
            }
            public static object Call_Prim(Pair args)
            {
                return Util.Call_Method(args, false);
            }
            public static object Call_Static_Prim(Pair args)
            {
                return Util.Call_Method(args, true);
            }
            public static object Get_Prim(Pair args)
            {
                return SetGet(args, BindingFlags.GetField | BindingFlags.GetProperty);
            }
            public static object Set_Prim(Pair args)
            {
                return SetGet(args, BindingFlags.SetField | BindingFlags.SetProperty);
            }
            public static object SetGet(Pair arg, BindingFlags flags)
            {
                object[] index = arg.cdr.cdr != null ? arg.cdr.cdr.ToArray() : new object[] { };
                Type t = arg.car is Symbol ? Type.GetType(arg.car.ToString()) : arg.car.GetType();
                BindingFlags f = BindingFlags.Default | flags;
                return t.InvokeMember(arg.cdr.car.ToString(), f, null, arg.car, index);
            }
        }

        public class If : Expression
        {
            Expression test, tX, eX;
            public If(Expression e1, Expression e2, Expression e3) { test = e1; tX = e2; eX = e3; }
            public override object Eval(Env env)
            {
                bool res = true;
                try
                {
                    res = (bool)test.Eval(env);
                }
                catch (Exception ex)
                { // if an Exception was thrown by user then throw again.
                    if (ex.ToString().IndexOf("Tachy.Util.Throw") > 0) throw ex;
                }
                try
                {
                    return res ? tX.Eval(env) : eX.Eval(env);
                }
                catch (Exception ex)
                { // if an Exception was thrown by user then throw again.
                    if (ex.ToString().IndexOf("Tachy.Util.Throw") > 0) throw ex;
                    return res ? tX : eX; // ((if #f * +) 2 3) ==> 5
                }
            }
            override public string ToString() { return Util.Dump("IF", test, tX, eX); }
        }

        public class Try : Expression
        {
            Expression tryX, catchX;
            public Try(Expression xtry, Expression xcatch) { tryX = xtry; catchX = xcatch; }
            public override object Eval(Env env)
            {
                try
                {
                    return tryX.Eval(env);
                }
                catch { return catchX.Eval(env); }
            }
            override public string ToString() { return Util.Dump("TRY", tryX, catchX); }
        }

        public class Assignment : Expression
        {
            Symbol id;
            Expression val;
            public Assignment(Symbol id, Expression val) { this.id = id; this.val = val; }
            public override object Eval(Env env)
            {
                return env.Bind(id, val.Eval(env));
            }
            override public string ToString() { return Util.Dump("set!", id, val); }
        }

        public class CommaAt : Expression
        {
            Pair rands;
            public CommaAt(Pair rands) { this.rands = rands; }
            public override object Eval(Env env)
            {
                Pair o = (rands == null) ? null : Eval_Rands(rands, env);
                return o.Count == 1 ? o.car : o; // (1 ,@(2 4) 3) ==> (1 2 4 3)
            }
            override public string ToString() { return Util.Dump(",@", rands); }
        }

        public class App : Expression
        {
            public static bool CarryOn = false; // (\x.\y.y 1 2) == ((LAMBDA (x) (LAMBDA (y) y)) 1 2)
            Expression rator;
            Pair rands;
            public App(Expression rator, Pair rands) { this.rator = rator; this.rands = rands; }
            public override object Eval(Env env)
            {
                if (rator is Var && IsTraceOn((rator as Var).id))
                    Console.WriteLine(Util.Dump("call: ", (rator as Var).id, rands));
                object proc = rator.Eval(env);
                if (proc is Closure)
                {
                    object result = (proc as Closure).Eval(Eval_Rands(rands, env));
                    if (CarryOn && (proc as Closure).ids.Count < rands.Count)
                    {
                        for (int i = 0; i < (proc as Closure).ids.Count; i++)
                            rands = rands.cdr;
                        return (result as Closure).Eval(Eval_Rands(rands, env)); ;
                    }
                    return result;
                }
                if (proc is Var) // allow ((if #f + *) 2 3) ==> 6
                    return Parse(new Pair((proc as Var).GetName(), rands)).Eval(env);
                if (proc is Pair && (proc as Pair).car is Closure) // somewhere is an extra () ?
                    return ((proc as Pair).car as Closure).Eval(Eval_Rands(rands, env));
                throw new Exception("invalid operator " + proc.GetType() + " " + proc);
            }
            override public string ToString() { return Util.Dump("app", rator, rands); }
        }
    }

    public class Interpreter
    {
        public static bool EndProgram = false;
        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("*** Tachy ver {0} - Copyright (c) 2003 by Ilias H. Mavreas ***\n", Assembly.GetEntryAssembly().GetName().Version);
            Program prog = new Program();
            try
            {
                Console.Write("Initializing: loading 'init.ss'...");
                prog.Eval(File.OpenText("init.ss").ReadToEnd());
            }
            catch { }
            while (true)
                try
                {
                    if (EndProgram) return;
                    Console.Write("tachy> ");
                    StringBuilder val = new StringBuilder();
                    for (string line; (line = Console.ReadLine()) != ""; val.Append(line + "\n"))
                        Console.Write("...    ");
                    Console.WriteLine(Util.Dump(prog.Eval(val.ToString())));
                }
                catch (Exception e) { Console.WriteLine("error: " + e.Message); }
        }
    }
}