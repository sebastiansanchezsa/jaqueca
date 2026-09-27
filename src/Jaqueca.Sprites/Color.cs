namespace Jaqueca.Sprites;

/// <summary>Colores empaquetados como 0xAARRGGBB.</summary>
public static class Col
{
    public const uint Clear = 0x00000000;

    public static uint Rgb(uint rgb, byte a = 255) => ((uint)a << 24) | (rgb & 0xFFFFFF);
    public static byte A(uint c) => (byte)(c >> 24);
    public static byte R(uint c) => (byte)(c >> 16);
    public static byte G(uint c) => (byte)(c >> 8);
    public static byte B(uint c) => (byte)c;
    public static uint Make(int r, int g, int b, int a = 255)
        => ((uint)Math.Clamp(a, 0, 255) << 24) | ((uint)Math.Clamp(r, 0, 255) << 16) | ((uint)Math.Clamp(g, 0, 255) << 8) | (uint)Math.Clamp(b, 0, 255);

    /// <summary>Multiplica el brillo (f &lt; 1 oscurece, f &gt; 1 aclara hacia blanco).</summary>
    public static uint Shade(uint c, float f)
    {
        if (f <= 1) return Make((int)(R(c) * f), (int)(G(c) * f), (int)(B(c) * f), A(c));
        float t = f - 1;
        return Make((int)(R(c) + (255 - R(c)) * t), (int)(G(c) + (255 - G(c)) * t), (int)(B(c) + (255 - B(c)) * t), A(c));
    }

    public static uint Mix(uint a, uint b, float t)
        => Make((int)(R(a) + (R(b) - R(a)) * t), (int)(G(a) + (G(b) - G(a)) * t), (int)(B(a) + (B(b) - B(a)) * t), (int)(A(a) + (A(b) - A(a)) * t));

    public static uint WithAlpha(uint c, byte a) => (c & 0xFFFFFF) | ((uint)a << 24);
    public static uint Gray(int v, int a = 255) => Make(v, v, v, a);
}

/// <summary>
/// Rampa de 5 tonos de pixel art (sombra profunda → brillo) con corrimiento de matiz, en
/// clave oscura: las sombras caen hondo y se van hacia el violeta, los brillos se quedan cerca
/// del color y tiran al amarillo (luz de vela, no de estudio).
/// </summary>
public sealed class Ramp
{
    public readonly uint[] T = new uint[5];
    /// <summary>Brillo especular marcado (metal, cuero, gemas).</summary>
    public bool Shiny;

    public uint this[int i] => T[Math.Clamp(i, 0, 4)];

    public static Ramp From(uint rgb, bool shiny = false)
    {
        var r = new Ramp { Shiny = shiny };
        Hsv.To(rgb, out float h, out float s, out float v);
        bool gray = s < 0.1f;
        r.T[0] = Hsv.From(gray ? 268 : Hsv.Toward(h, 275, 30), gray ? 0.28f : MathF.Min(0.9f, s * 1.1f + 0.2f), v * 0.2f + 0.025f);
        r.T[1] = Hsv.From(gray ? 262 : Hsv.Toward(h, 268, 15), gray ? 0.14f : MathF.Min(0.9f, s * 1.08f + 0.1f), v * 0.5f + 0.012f);
        r.T[2] = Col.Rgb(rgb);
        r.T[3] = Hsv.From(gray ? 45 : Hsv.Toward(h, 50, 5), gray ? 0.05f : s * 0.9f, MathF.Min(1, v * 1.13f + 0.04f));
        r.T[4] = Hsv.From(gray ? 45 : Hsv.Toward(h, 50, 11), gray ? 0.07f : s * 0.72f, MathF.Min(1, v * 1.3f + 0.11f));
        if (shiny) r.T[4] = Col.Mix(r.T[4], 0xFFFFFFFF, 0.35f);
        return r;
    }
}

public static class Hsv
{
    public static void To(uint rgb, out float h, out float s, out float v)
    {
        float r = ((rgb >> 16) & 255) / 255f, g = ((rgb >> 8) & 255) / 255f, b = (rgb & 255) / 255f;
        float max = MathF.Max(r, MathF.Max(g, b)), min = MathF.Min(r, MathF.Min(g, b)), d = max - min;
        v = max;
        s = max <= 0 ? 0 : d / max;
        if (d <= 1e-5f) { h = 0; return; }
        if (max == r) h = 60 * (((g - b) / d) % 6);
        else if (max == g) h = 60 * ((b - r) / d + 2);
        else h = 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
    }

    public static uint From(float h, float s, float v)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1); v = Math.Clamp(v, 0, 1);
        float c = v * s, x = c * (1 - MathF.Abs((h / 60) % 2 - 1)), m = v - c;
        float r, g, b;
        if (h < 60) (r, g, b) = (c, x, 0);
        else if (h < 120) (r, g, b) = (x, c, 0);
        else if (h < 180) (r, g, b) = (0, c, x);
        else if (h < 240) (r, g, b) = (0, x, c);
        else if (h < 300) (r, g, b) = (x, 0, c);
        else (r, g, b) = (c, 0, x);
        return Col.Make((int)MathF.Round((r + m) * 255), (int)MathF.Round((g + m) * 255), (int)MathF.Round((b + m) * 255));
    }

    /// <summary>Mueve el matiz h hacia target como mucho deg grados por el camino corto.</summary>
    public static float Toward(float h, float target, float deg)
    {
        float d = ((target - h + 540) % 360) - 180;
        return h + Math.Clamp(d, -deg, deg);
    }
}
