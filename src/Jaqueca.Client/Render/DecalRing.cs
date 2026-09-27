using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Jaqueca.Client.Render;

/// <summary>
/// Las manchas pegadas en el mundo (la sangre en el parquet, en las paredes, en el sillón): cuadrados
/// planos sobre la superficie. Hay un tope: cuando se llena, la mancha nueva pisa a la más vieja (sin
/// pedir memoria nueva nunca).
/// </summary>
public sealed class DecalRing
{
    public const int Max = 1800;
    /// <summary>Cada mancha es un abanico: el centro y <see cref="Rim"/> puntas alrededor (con radios distintos: un manchón, no un cuadrado).</summary>
    private const int Rim = 6, Verts = Rim + 1, Tris = Rim;
    private readonly WorldVertex[] _v = new WorldVertex[Max * Verts];
    private readonly short[] _i = new short[Max * Tris * 3];
    private uint _rng = 12345;
    private int _next, _count;

    public DecalRing()
    {
        for (int k = 0; k < Max; k++)
        {
            int v = k * Verts, i = k * Tris * 3;
            for (int t = 0; t < Rim; t++)
            {
                _i[i + t * 3] = (short)v;
                _i[i + t * 3 + 1] = (short)(v + 1 + t);
                _i[i + t * 3 + 2] = (short)(v + 1 + (t + 1) % Rim);
            }
        }
    }

    /// <summary>
    /// Una mancha en <paramref name="at"/> sobre la superficie de normal <paramref name="n"/>, de radio
    /// <paramref name="size"/>, girada <paramref name="spin"/> y estirada <paramref name="stretch"/> (una
    /// gota que llegó de costado deja una mancha larga).
    /// </summary>
    public void Add(Vector3 at, Vector3 n, float size, float spin, Color col, float stretch = 1)
    {
        var t = Vector3.Cross(n, MathF.Abs(n.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY);
        t.Normalize();
        var b = Vector3.Cross(n, t);
        float c = MathF.Cos(spin), s = MathF.Sin(spin);
        var u = (t * c + b * s) * size * stretch;
        var w = (b * c - t * s) * size;
        var p = at + n * 0.08f;
        int k = _next * Verts;
        var d = new Vector4(0, 0, 0, 1);
        _v[k] = new WorldVertex(p, n, col, d);
        for (int j = 0; j < Rim; j++)
        {
            float a = j * MathF.Tau / Rim;
            float r = 0.55f + 0.6f * Next();
            _v[k + 1 + j] = new WorldVertex(p + (u * MathF.Cos(a) + w * MathF.Sin(a)) * r, n, col, d);
        }
        _next = (_next + 1) % Max;
        _count = Math.Min(Max, _count + 1);
    }

    public void Clear() { _next = 0; _count = 0; }

    private float Next()
    {
        _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
        return (_rng & 0xFFFF) / 65536f;
    }

    public void Draw(GraphicsDevice gd)
    {
        if (_count == 0) return;
        gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _v, 0, _count * Verts, _i, 0, _count * Tris);
    }
}
