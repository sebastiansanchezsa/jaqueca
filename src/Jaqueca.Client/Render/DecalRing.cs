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
    private readonly WorldVertex[] _v = new WorldVertex[Max * 4];
    private readonly short[] _i = new short[Max * 6];
    private int _next, _count;

    public DecalRing()
    {
        for (int k = 0; k < Max; k++)
        {
            int v = k * 4, i = k * 6;
            _i[i] = (short)v; _i[i + 1] = (short)(v + 1); _i[i + 2] = (short)(v + 2);
            _i[i + 3] = (short)v; _i[i + 4] = (short)(v + 2); _i[i + 5] = (short)(v + 3);
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
        int k = _next * 4;
        var d = new Vector4(0, 0, 0, 1);
        _v[k] = new WorldVertex(p - u - w, n, col, d);
        _v[k + 1] = new WorldVertex(p + u - w, n, col, d);
        _v[k + 2] = new WorldVertex(p + u + w, n, col, d);
        _v[k + 3] = new WorldVertex(p - u + w, n, col, d);
        _next = (_next + 1) % Max;
        _count = Math.Min(Max, _count + 1);
    }

    public void Clear() { _next = 0; _count = 0; }

    public void Draw(GraphicsDevice gd)
    {
        if (_count == 0) return;
        gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _v, 0, _count * 4, _i, 0, _count * 2);
    }
}
