using Jaqueca.Sprites;

namespace Jaqueca.ArtGen;

/// <summary>Lienzo de píxeles con primitivas para dibujar sprites.</summary>
public sealed class Canvas
{
    public readonly int W, H;
    public readonly uint[] Px;
    public float PivotX, PivotY;

    public Canvas(int w, int h)
    {
        W = w; H = h;
        Px = new uint[w * h];
        PivotX = w / 2f; PivotY = h / 2f;
    }

    public Canvas Pivot(float x, float y) { PivotX = x; PivotY = y; return this; }

    public bool In(int x, int y) => (uint)x < (uint)W && (uint)y < (uint)H;
    public uint Get(int x, int y) => In(x, y) ? Px[y * W + x] : 0;

    public void Set(int x, int y, uint c)
    {
        if (In(x, y)) Px[y * W + x] = c;
    }

    /// <summary>Pinta con mezcla alfa sobre lo existente.</summary>
    public void Blend(int x, int y, uint c)
    {
        if (!In(x, y)) return;
        byte a = Col.A(c);
        if (a == 255) { Px[y * W + x] = c; return; }
        if (a == 0) return;
        uint d = Px[y * W + x];
        float fa = a / 255f, da = Col.A(d) / 255f;
        float oa = fa + da * (1 - fa);
        if (oa <= 0) return;
        int r = (int)((Col.R(c) * fa + Col.R(d) * da * (1 - fa)) / oa);
        int g = (int)((Col.G(c) * fa + Col.G(d) * da * (1 - fa)) / oa);
        int b = (int)((Col.B(c) * fa + Col.B(d) * da * (1 - fa)) / oa);
        Px[y * W + x] = Col.Make(r, g, b, (int)(oa * 255));
    }

    public bool Filled(int x, int y) => Col.A(Get(x, y)) > 0;

    public void Rect(int x, int y, int w, int h, uint c)
    {
        for (int j = y; j < y + h; j++)
        for (int i = x; i < x + w; i++) Set(i, j, c);
    }

    public void Line(float x0, float y0, float x1, float y1, uint c, float thickness = 1)
    {
        float dx = x1 - x0, dy = y1 - y0;
        int steps = (int)MathF.Ceiling(MathF.Max(MathF.Abs(dx), MathF.Abs(dy)) * 2) + 1;
        for (int s = 0; s <= steps; s++)
        {
            float t = s / (float)steps;
            float x = x0 + dx * t, y = y0 + dy * t;
            if (thickness <= 1) Set((int)MathF.Round(x), (int)MathF.Round(y), c);
            else Disc(x, y, thickness / 2f, c);
        }
    }

    public void Disc(float cx, float cy, float r, uint c)
    {
        for (int y = (int)MathF.Floor(cy - r - 1); y <= (int)MathF.Ceiling(cy + r + 1); y++)
        for (int x = (int)MathF.Floor(cx - r - 1); x <= (int)MathF.Ceiling(cx + r + 1); x++)
        {
            float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
            if (dx * dx + dy * dy <= r * r) Set(x, y, c);
        }
    }

    public void Ellipse(float cx, float cy, float rx, float ry, uint c)
    {
        for (int y = (int)MathF.Floor(cy - ry - 1); y <= (int)MathF.Ceiling(cy + ry + 1); y++)
        for (int x = (int)MathF.Floor(cx - rx - 1); x <= (int)MathF.Ceiling(cx + rx + 1); x++)
        {
            float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
            if (dx * dx + dy * dy <= 1) Set(x, y, c);
        }
    }

    /// <summary>Elipse con sombreado en 3 tonos, luz desde arriba a la izquierda.</summary>
    public void ShadedEllipse(float cx, float cy, float rx, float ry, uint baseCol, uint shadeCol, float highlight = 1.18f)
    {
        uint light = Col.Shade(baseCol, highlight);
        for (int y = (int)MathF.Floor(cy - ry - 1); y <= (int)MathF.Ceiling(cy + ry + 1); y++)
        for (int x = (int)MathF.Floor(cx - rx - 1); x <= (int)MathF.Ceiling(cx + rx + 1); x++)
        {
            float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
            float d = nx * nx + ny * ny;
            if (d > 1) continue;
            float l = -(nx * 0.55f + ny * 0.8f); // dot con la luz
            uint c = l > 0.45f ? light : (l < -0.35f || d > 0.82f && l < 0.1f) ? shadeCol : baseCol;
            Set(x, y, c);
        }
    }

    /// <summary>Contorno de 1 px alrededor de todo lo pintado.</summary>
    public void Outline(uint c, bool diagonal = false)
    {
        var add = new List<(int, int)>();
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            if (Filled(x, y)) continue;
            bool n = Filled(x - 1, y) || Filled(x + 1, y) || Filled(x, y - 1) || Filled(x, y + 1);
            if (!n && diagonal) n = Filled(x - 1, y - 1) || Filled(x + 1, y - 1) || Filled(x - 1, y + 1) || Filled(x + 1, y + 1);
            if (n) add.Add((x, y));
        }
        foreach (var (x, y) in add) Set(x, y, c);
    }

    /// <summary>Oscurece los píxeles del borde interior (da volumen).</summary>
    public void InnerEdge(float factor)
    {
        var copy = (uint[])Px.Clone();
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            if (Col.A(copy[y * W + x]) == 0) continue;
            bool edge = x == 0 || y == 0 || x == W - 1 || y == H - 1
                || Col.A(copy[y * W + x - 1]) == 0 || Col.A(copy[y * W + x + 1]) == 0
                || Col.A(copy[(y - 1) * W + x]) == 0 || Col.A(copy[(y + 1) * W + x]) == 0;
            if (edge) Px[y * W + x] = Col.Shade(copy[y * W + x], factor);
        }
    }

    public void Blit(Canvas src, int ox, int oy)
    {
        for (int y = 0; y < src.H; y++)
        for (int x = 0; x < src.W; x++)
        {
            uint c = src.Px[y * src.W + x];
            if (Col.A(c) > 0) Blend(ox + x, oy + y, c);
        }
    }

    public void Replace(Func<int, int, uint, uint> f)
    {
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
            Px[y * W + x] = f(x, y, Px[y * W + x]);
    }

    public Canvas Scaled(int s)
    {
        var c = new Canvas(W * s, H * s);
        for (int y = 0; y < c.H; y++)
        for (int x = 0; x < c.W; x++) c.Px[y * c.W + x] = Px[(y / s) * W + x / s];
        return c;
    }
}

/// <summary>Ruido determinista para texturas.</summary>
public static class Noise
{
    public static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
    }

    public static float Rand(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFFFF) / (float)0x1000000;

    /// <summary>Value noise tileable con período p.</summary>
    public static float Value(float x, float y, int period, int seed)
    {
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        int Wrap(int v) => ((v % period) + period) % period;
        float a = Rand(Wrap(x0), Wrap(y0), seed), b = Rand(Wrap(x0 + 1), Wrap(y0), seed);
        float c = Rand(Wrap(x0), Wrap(y0 + 1), seed), d = Rand(Wrap(x0 + 1), Wrap(y0 + 1), seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    public static float Fbm(float x, float y, int period, int seed, int octaves = 3)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (int o = 0; o < octaves; o++)
        {
            sum += Value(x, y, period, seed + o * 17) * amp;
            norm += amp;
            x *= 2; y *= 2; period *= 2; amp *= 0.5f;
        }
        return sum / norm;
    }
}

public sealed class Seeded
{
    private Random _r;
    public Seeded(int seed) { _r = new Random(seed); }
    public float F() => (float)_r.NextDouble();
    public float Range(float a, float b) => a + (b - a) * F();
    public int Int(int a, int b) => _r.Next(a, b);
    public bool Chance(float p) => F() < p;
}
