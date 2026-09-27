using System.Numerics;
using Jaqueca.Anim;
using Jaqueca.Look;

namespace Jaqueca.Sprites;

/// <summary>Resuelve el color de un material para una apariencia (rampas cacheadas).</summary>
public sealed class Palette
{
    private readonly Dictionary<(uint, bool), Ramp> _ramps = new();

    public Ramp Get(MatDef m, Appearance a)
    {
        uint rgb = m.Channel switch
        {
            MatChannel.Skin => Palettes.Skins[Math.Clamp(a.Skin, 0, Palettes.Skins.Length - 1)],
            MatChannel.Hair => a.HairColor,
            MatChannel.Eyes => Palettes.EyeColors[0],
            MatChannel.Accent => a.Accent,
            _ => m.Rgb,
        };
        return Get(rgb, m.Shiny);
    }

    public Ramp Get(uint rgb, bool shiny)
    {
        lock (_ramps)
        {
            if (!_ramps.TryGetValue((rgb, shiny), out var r)) _ramps[(rgb, shiny)] = r = Ramp.From(rgb, shiny);
            return r;
        }
    }
}

/// <summary>
/// Un frame ya compuesto y recortado. Color = el sprite horneado tal cual (hojas de revisión).
/// Para reiluminarlo en el juego, por píxel: normal (RGB, espacio de la cámara del horneado),
/// rampa y material, cuánto se corre el tono de la regla de luz (pliegues, mechones, líneas
/// entre prendas), profundidad y si es contorno.
/// </summary>
public sealed class ComposedFrame
{
    public int W, H;
    /// <summary>Pivote (los pies) dentro del recorte.</summary>
    public float PivotX, PivotY;
    public uint[] Color, Normal;
    public Ramp[] Ramp;
    public MatDef[] Mat;
    public sbyte[] Bias;
    public byte[] Depth;
    public bool[] Ink;
}

/// <summary>
/// Junta las capas del equipo en un frame. Cada píxel se lo queda la capa más cercana a la
/// cámara (a igual profundidad, la de slot más alto). Después agrega lo que depende de la
/// combinación y no se puede hornear por capa: la oclusión en los pliegues entre capas, el
/// contorno selectivo donde una capa pisa a otra y el contorno exterior sobre la silueta
/// final (así no hay contornos dobles entre prendas).
/// </summary>
public sealed class Composer
{
    public const uint Ink = 0xFF120C18;

    private readonly LayerLibrary _lib;
    public readonly Palette Palette = new();

    public Composer(LayerLibrary lib) { _lib = lib; }

    public ComposedFrame Compose(IReadOnlyList<LayerDef> layers, Appearance a, ClipId clip, Dir8 dir, int frame)
    {
        int fw = _lib.FrameW, fh = _lib.FrameH, n = fw * fh;
        var depth = new byte[n];
        var who = new sbyte[n];
        var px = new int[n];   // índice del píxel dentro del frame de su capa
        Array.Fill(who, (sbyte)-1);

        var hide = HairHide.None;
        foreach (var l in layers) if (l.Hide > hide) hide = l.Hide;

        var frames = new LayerFrame[layers.Count];
        for (int li = 0; li < layers.Count; li++)
        {
            var l = layers[li];
            if (l.Slot == GearSlot.Hair && hide == HairHide.All) continue;
            var f = l.Frame(clip, dir, frame);
            if (f == null || f.IsEmpty) continue;
            frames[li] = f;
            for (int y = 0; y < f.H; y++)
            for (int x = 0; x < f.W; x++)
            {
                int s = (y * f.W + x) * 4;
                byte d = f.Px[s + 1];
                if (d == 0) continue;
                if (hide == HairHide.Top && l.Mats[LayerFrame.Mat(f.Px[s])].HairTop) continue;
                int gx = f.X + x, gy = f.Y + y;
                if ((uint)gx >= (uint)fw || (uint)gy >= (uint)fh) continue;
                int i = gy * fw + gx;
                if (d < depth[i]) continue;
                depth[i] = d; who[i] = (sbyte)li; px[i] = s;
            }
        }

        // Tonos con oclusión y contorno selectivo entre capas.
        var color = new uint[n];
        var normal = new uint[n];
        var ramps = new Ramp[n];
        var mats = new MatDef[n];
        var bias = new sbyte[n];
        var ink = new bool[n];
        int minX = fw, minY = fh, maxX = -1, maxY = -1;
        for (int y = 0; y < fh; y++)
        for (int x = 0; x < fw; x++)
        {
            int i = y * fw + x;
            int li = who[i];
            if (li < 0) continue;
            var f = frames[li];
            byte b0 = f.Px[px[i]];
            var mat = layers[li].Mats[LayerFrame.Mat(b0)];
            int t = LayerFrame.Tone(b0);

            float occ = 0;
            bool line = false;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int qx = x + ox, qy = y + oy;
                if ((uint)qx >= (uint)fw || (uint)qy >= (uint)fh) continue;
                int q = qy * fw + qx;
                if (who[q] < 0 || who[q] == li) continue;
                float dz = (depth[q] - depth[i]) / LayerLibrary.DepthScale;
                int man = Math.Abs(ox) + Math.Abs(oy);
                occ = MathF.Max(occ, dz * (man <= 1 ? 1f : 0.7f));
                if (man == 1 && dz > 0.6f) line = true;
            }
            if (occ > 0.9f) t -= 1;
            if (line) t = Math.Min(t, 1) - 1;

            var ramp = Palette.Get(mat, a);
            t = Math.Max(0, t);
            ramps[i] = ramp;
            mats[i] = mat;
            color[i] = ramp[t];
            var nv = Oct.Decode(f.Px[px[i] + 2], f.Px[px[i] + 3]);
            int nr = (int)MathF.Round((nv.X * 0.5f + 0.5f) * 255), ng = (int)MathF.Round((nv.Y * 0.5f + 0.5f) * 255), nb = (int)MathF.Round((nv.Z * 0.5f + 0.5f) * 255);
            normal[i] = Col.Make(nr, ng, nb);
            // Corrimiento respecto de la regla de luz, con la misma normal de 8 bits que ve el shader:
            // con la luz del horneado, tono = regla + corrimiento da exactamente el sprite horneado.
            var nq = Vector3.Normalize(new Vector3(nr / 255f * 2 - 1, ng / 255f * 2 - 1, nb / 255f * 2 - 1));
            bias[i] = (sbyte)(t - Shading.BakeTone(nq, mat));
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
        }

        if (maxX < 0) return new ComposedFrame { W = 0, H = 0, Color = Array.Empty<uint>(), Normal = Array.Empty<uint>() };

        // Contorno exterior de 1 px (4 vecinos: sin esquinas dobles), teñido con la sombra del material vecino.
        minX = Math.Max(0, minX - 1); minY = Math.Max(0, minY - 1);
        maxX = Math.Min(fw - 1, maxX + 1); maxY = Math.Min(fh - 1, maxY + 1);
        var outline = new List<(int, uint, int)>();
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            int i = y * fw + x;
            if (who[i] >= 0) continue;
            Ramp nb = null;
            if (x > 0 && who[i - 1] >= 0) nb = ramps[i - 1];
            else if (x < fw - 1 && who[i + 1] >= 0) nb = ramps[i + 1];
            else if (y < fh - 1 && who[i + fw] >= 0) nb = ramps[i + fw];
            else if (y > 0 && who[i - fw] >= 0) nb = ramps[i - fw];
            int from = x > 0 && who[i - 1] >= 0 ? i - 1 : x < fw - 1 && who[i + 1] >= 0 ? i + 1 : y < fh - 1 && who[i + fw] >= 0 ? i + fw : i - fw;
            if (nb != null) outline.Add((i, Col.Mix(Ink, nb[0], 0.3f), from));
        }
        foreach (var (i, c, from) in outline)
        {
            color[i] = c;
            normal[i] = normal[from];
            ramps[i] = ramps[from];
            mats[i] = mats[from];
            depth[i] = depth[from];
            ink[i] = true;
        }

        int w = maxX - minX + 1, h = maxY - minY + 1;
        var res = new ComposedFrame
        {
            W = w, H = h, PivotX = _lib.PivotX - minX, PivotY = _lib.PivotY - minY,
            Color = new uint[w * h], Normal = new uint[w * h], Ramp = new Ramp[w * h], Mat = new MatDef[w * h],
            Bias = new sbyte[w * h], Depth = new byte[w * h], Ink = new bool[w * h],
        };
        for (int y = 0; y < h; y++)
        {
            int src = (minY + y) * fw + minX, dst = y * w;
            Array.Copy(color, src, res.Color, dst, w);
            Array.Copy(normal, src, res.Normal, dst, w);
            Array.Copy(ramps, src, res.Ramp, dst, w);
            Array.Copy(mats, src, res.Mat, dst, w);
            Array.Copy(bias, src, res.Bias, dst, w);
            Array.Copy(depth, src, res.Depth, dst, w);
            Array.Copy(ink, src, res.Ink, dst, w);
        }
        return res;
    }
}
