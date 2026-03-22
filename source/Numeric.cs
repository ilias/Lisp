namespace Lisp;

public readonly struct Rational : IEquatable<Rational>, IComparable<Rational>
{
    public readonly BigInteger Numer;
    public readonly BigInteger Denom;

    public Rational(BigInteger n, BigInteger d)
    {
        if (d.IsZero) throw new LispException("division by zero");
        if (d < BigInteger.Zero) { n = -n; d = -d; }
        var g = BigInteger.GreatestCommonDivisor(n < 0 ? -n : n, d);
        Numer = n / g;
        Denom = d / g;
    }

    public object Normalize() =>
        Denom.IsOne
            ? (Numer >= int.MinValue && Numer <= int.MaxValue ? (object)(int)Numer : Numer)
            : this;

    public double ToDouble() => (double)Numer / (double)Denom;
    public static Rational operator +(Rational a, Rational b) => new(a.Numer * b.Denom + b.Numer * a.Denom, a.Denom * b.Denom);
    public static Rational operator -(Rational a, Rational b) => new(a.Numer * b.Denom - b.Numer * a.Denom, a.Denom * b.Denom);
    public static Rational operator *(Rational a, Rational b) => new(a.Numer * b.Numer, a.Denom * b.Denom);
    public static Rational operator /(Rational a, Rational b)
    {
        if (b.Numer.IsZero) throw new LispException("division by zero");
        return new(a.Numer * b.Denom, a.Denom * b.Numer);
    }
    public static Rational operator -(Rational a) => new(-a.Numer, a.Denom);
    public static bool operator <(Rational a, Rational b) => a.Numer * b.Denom < b.Numer * a.Denom;
    public static bool operator >(Rational a, Rational b) => b < a;
    public static bool operator <=(Rational a, Rational b) => !(a > b);
    public static bool operator >=(Rational a, Rational b) => !(a < b);
    public static bool operator ==(Rational a, Rational b) => a.Numer == b.Numer && a.Denom == b.Denom;
    public static bool operator !=(Rational a, Rational b) => !(a == b);
    public int CompareTo(Rational other) => this < other ? -1 : this > other ? 1 : 0;
    public bool Equals(Rational other) => this == other;
    public override bool Equals(object? obj) => obj is Rational r && this == r;
    public override int GetHashCode() => HashCode.Combine(Numer, Denom);
    public override string ToString() => $"{Numer}/{Denom}";
}

public static class Arithmetic
{
    public static double D(object a) => a switch
    {
        BigInteger bi => (double)bi,
        Rational r => r.ToDouble(),
        Complex z => z.Real,
        _ => Convert.ToDouble(a),
    };

    static int I(object a) => a is BigInteger bi ? (int)bi : Convert.ToInt32(a);

    static BigInteger BI(object a) => a switch
    {
        BigInteger bi => bi,
        int i => i,
        double d => (BigInteger)d,
        Rational r => r.Numer / r.Denom,
        _ => (BigInteger)Convert.ToInt64(a),
    };

    static Rational ToRational(object a) => a switch
    {
        Rational r => r,
        int i => new Rational(i, 1),
        BigInteger bi => new Rational(bi, 1),
        _ => throw new LispException($"cannot convert {a?.GetType().Name ?? "null"} to exact rational"),
    };

    static Complex ToComplex(object a) => a switch
    {
        Complex z => z,
        double d => new Complex(d, 0.0),
        Rational r => new Complex(r.ToDouble(), 0.0),
        int i => new Complex(i, 0.0),
        BigInteger bi => new Complex((double)bi, 0.0),
        _ => new Complex(Convert.ToDouble(a), 0.0),
    };

    public static object Normalize(BigInteger v) =>
        v >= int.MinValue && v <= int.MaxValue ? (object)(int)v : v;

    public static object AddObj(object a, object b)
    {
        if (a is Complex || b is Complex) return Complex.Add(ToComplex(a), ToComplex(b));
        if (a is double || b is double) return D(a) + D(b);
        if (a is Rational || b is Rational) return (ToRational(a) + ToRational(b)).Normalize();
        if (a is int ia && b is int ib)
        {
            try { return checked(ia + ib); }
            catch (OverflowException) { return Normalize((BigInteger)ia + ib); }
        }
        return Normalize(BI(a) + BI(b));
    }

    public static object SubObj(object a, object b)
    {
        if (a is Complex || b is Complex) return Complex.Subtract(ToComplex(a), ToComplex(b));
        if (a is double || b is double) return D(a) - D(b);
        if (a is Rational || b is Rational) return (ToRational(a) - ToRational(b)).Normalize();
        if (a is int ia && b is int ib)
        {
            try { return checked(ia - ib); }
            catch (OverflowException) { return Normalize((BigInteger)ia - ib); }
        }
        return Normalize(BI(a) - BI(b));
    }

    public static object MulObj(object a, object b)
    {
        if (a is Complex || b is Complex) return Complex.Multiply(ToComplex(a), ToComplex(b));
        if (a is double || b is double) return D(a) * D(b);
        if (a is Rational || b is Rational) return (ToRational(a) * ToRational(b)).Normalize();
        if (a is int ia && b is int ib)
        {
            try { return checked(ia * ib); }
            catch (OverflowException) { return Normalize((BigInteger)ia * ib); }
        }
        return Normalize(BI(a) * BI(b));
    }

    public static object DivObj(object a, object b)
    {
        if (a is Complex || b is Complex) return Complex.Divide(ToComplex(a), ToComplex(b));
        if (a is double || b is double) return D(a) / D(b);
        if (a is Rational || b is Rational) return (ToRational(a) / ToRational(b)).Normalize();
        var bn = BI(a);
        var bd = BI(b);
        if (bd.IsZero) throw new LispException("division by zero");
        return new Rational(bn, bd).Normalize();
    }

    public static object NegObj(object a) => a switch
    {
        double d => -d,
        int i => i == int.MinValue ? (object)(-(BigInteger)i) : -i,
        BigInteger bi => Normalize(-bi),
        Rational r => new Rational(-r.Numer, r.Denom).Normalize(),
        Complex z => Complex.Negate(z),
        _ => -I(a),
    };

    public static object IDivObj(object a, object b) =>
        a is BigInteger || b is BigInteger ? Normalize(BI(a) / BI(b)) : I(a) / I(b);

    public static object ModObj(object a, object b) =>
        a is BigInteger || b is BigInteger ? Normalize(BI(a) % BI(b)) : I(a) % I(b);

    public static object PowObj(object a, object b)
    {
        if (a is Complex || b is Complex) return Complex.Pow(ToComplex(a), ToComplex(b));
        if (b is int iexp && (a is int || a is BigInteger || a is Rational))
        {
            var r = a is Rational ra ? ra : new Rational(BI(a), BigInteger.One);
            if (iexp == 0) return 1;
            if (r.Numer.IsZero)
            {
                if (iexp < 0) throw new LispException("expt: zero base with negative exponent");
                return 0;
            }
            int n = iexp < 0 ? -iexp : iexp;
            bool negSign = r.Numer < BigInteger.Zero && (n % 2 == 1);
            var absBase = r.Numer < BigInteger.Zero ? -r.Numer : r.Numer;
            var numPow = BigInteger.Pow(absBase, n);
            var denPow = BigInteger.Pow(r.Denom, n);
            var numRes = negSign ? -numPow : numPow;
            return iexp < 0 ? new Rational(denPow, numRes).Normalize() : new Rational(numRes, denPow).Normalize();
        }
        return Math.Pow(D(a), D(b));
    }

    public static bool LessThan(object a, object b)
    {
        if (a is Complex || b is Complex)
            throw new LispException("<: comparison not defined for complex numbers");
        if ((a is Rational || b is Rational) && a is not double && b is not double)
            return ToRational(a) < ToRational(b);
        if (a is int ia && b is int ib) return ia < ib;
        if (a is BigInteger || b is BigInteger) return BI(a) < BI(b);
        return D(a) < D(b);
    }

    public static bool IsNumericEqual(object a, object b)
    {
        if (a is Complex || b is Complex) return ToComplex(a) == ToComplex(b);
        if (a is double || b is double) return D(a) == D(b);
        if (a is Rational || b is Rational) return ToRational(a) == ToRational(b);
        return BI(a) == BI(b);
    }

    public static (BigInteger n, BigInteger d) GetNumerDenom(object x) => x switch
    {
        int i => ((BigInteger)i, BigInteger.One),
        BigInteger bi => (bi, BigInteger.One),
        Rational r => (r.Numer, r.Denom),
        _ => throw new LispException($"not an exact rational: {Util.Dump(x)}"),
    };

    public static object DoubleToExact(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d))
            throw new LispException($"inexact->exact: no exact representation for {d}");
        if (d == 0.0) return 0;
        long bits = BitConverter.DoubleToInt64Bits(d);
        bool neg = bits < 0;
        int rawExp = (int)((bits >> 52) & 0x7FF);
        long rawMant = bits & 0x000FFFFFFFFFFFFFL;
        BigInteger mant = rawExp == 0 ? rawMant : ((BigInteger)rawMant | 0x0010000000000000L);
        int exp2 = rawExp == 0 ? -1022 - 52 : rawExp - 1023 - 52;
        if (neg) mant = -mant;
        if (exp2 >= 0) return Normalize(mant << exp2);
        return new Rational(mant, BigInteger.Pow(2, -exp2)).Normalize();
    }

    private static BigInteger FloorBI(BigInteger numer, BigInteger denom)
    {
        var (q, r) = BigInteger.DivRem(numer, denom);
        if (!r.IsZero && numer < BigInteger.Zero) q--;
        return q;
    }

    public static object FloorObj(object x) => x switch
    {
        int or BigInteger => x,
        Rational r => Normalize(FloorBI(r.Numer, r.Denom)),
        double d => Math.Floor(d),
        _ => Math.Floor(D(x)),
    };

    public static object CeilingObj(object x) => x switch
    {
        int or BigInteger => x,
        Rational r => Normalize(-FloorBI(-r.Numer, r.Denom)),
        double d => Math.Ceiling(d),
        _ => Math.Ceiling(D(x)),
    };

    public static object TruncateObj(object x) => x switch
    {
        int or BigInteger => x,
        Rational r => Normalize(r.Numer / r.Denom),
        double d => Math.Truncate(d),
        _ => Math.Truncate(D(x)),
    };

    public static object RoundObj(object x) => x switch
    {
        int or BigInteger => x,
        Rational r => RoundRational(r),
        double d => Math.Round(d, MidpointRounding.ToEven),
        _ => Math.Round(D(x), MidpointRounding.ToEven),
    };

    private static object RoundRational(Rational r)
    {
        var f = FloorBI(r.Numer, r.Denom);
        var fracN = r.Numer - f * r.Denom;
        var twice = fracN * 2;
        if (twice < r.Denom) return Normalize(f);
        if (twice > r.Denom) return Normalize(f + 1);
        return Normalize(f % 2 == 0 ? f : f + 1);
    }

    public static object BitAndObj(object a, object b) =>
        a is BigInteger || b is BigInteger ? Normalize(BI(a) & BI(b)) : I(a) & I(b);
    public static object BitOrObj(object a, object b) =>
        a is BigInteger || b is BigInteger ? Normalize(BI(a) | BI(b)) : I(a) | I(b);
    public static object BitXorObj(object a, object b) =>
        a is BigInteger || b is BigInteger ? Normalize(BI(a) ^ BI(b)) : I(a) ^ I(b);
    public static object XorObj(object a, object b) => BitXorObj(a, b);
}
