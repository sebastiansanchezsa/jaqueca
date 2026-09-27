using System.Numerics;

namespace Jaqueca.Mathx;

/// <summary>RNG determinista (SplitMix64). Misma semilla = misma partida, clave para red y repeticiones.</summary>
public sealed class Rng
{
    private ulong _s;

    public Rng(ulong seed) { _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }

    public ulong NextU64()
    {
        _s += 0x9E3779B97F4A7C15UL;
        ulong z = _s;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>[0, 1)</summary>
    public float Float() => (NextU64() >> 40) * (1f / (1 << 24));
    public float Range(float a, float b) => a + (b - a) * Float();
    public int Int(int n) => n <= 0 ? 0 : (int)(NextU64() % (ulong)n);
    public int Range(int a, int bExclusive) => a + Int(bExclusive - a);
    public bool Chance(float p) => Float() < p;
    public float Angle() => Float() * M.Tau;
    public float Sign() => (NextU64() & 1) == 0 ? -1f : 1f;
    public float Spread(float amount) => (Float() * 2 - 1) * amount;
    public T Pick<T>(IReadOnlyList<T> list) => list[Int(list.Count)];

    public Vector2 InCircle(float r)
    {
        float a = Angle();
        float d = MathF.Sqrt(Float()) * r;
        return new Vector2(MathF.Cos(a) * d, MathF.Sin(a) * d);
    }

    public Vector2 OnCircle(float r) => M.Dir(Angle()) * r;
}
