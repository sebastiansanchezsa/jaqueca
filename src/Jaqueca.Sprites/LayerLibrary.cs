using System.IO.Compression;
using System.Numerics;
using Jaqueca.Anim;
using Jaqueca.Look;

namespace Jaqueca.Sprites;

/// <summary>De dónde sale el color de un material: fijo o elegido por el jugador.</summary>
public enum MatChannel : byte { Fixed, Skin, Hair, Eyes, Accent }

public sealed class MatDef
{
    public string Name;
    public MatChannel Channel;
    /// <summary>Color base (0xRRGGBB) cuando el canal es Fixed.</summary>
    public uint Rgb;
    public bool Shiny;
    /// <summary>Parte de arriba del pelo: se esconde bajo tocados con <see cref="HairHide.Top"/>.</summary>
    public bool HairTop;
    /// <summary>Corre la luz hacia arriba (piel de la cara) y aplana el sombreado (0 = completo, 1 = plano).</summary>
    public float Lift, Flat;
}

/// <summary>
/// Un frame de una capa: cada píxel son 4 bytes (material·8 + tono, profundidad, normal en
/// octaedro X, Y). Sin colores finales: el compositor los resuelve con la paleta del jugador.
/// X, Y es la esquina del recorte dentro del frame completo de la biblioteca.
/// </summary>
public sealed class LayerFrame
{
    public static readonly LayerFrame Empty = new() { Px = Array.Empty<byte>() };

    public short X, Y, W, H;
    public byte[] Px;

    public bool IsEmpty => W == 0 || H == 0;

    public static byte Pack(int mat, int tone) => (byte)((mat << 3) | Math.Clamp(tone, 0, 7));
    public static int Mat(byte b) => b >> 3;
    public static int Tone(byte b) => b & 7;
}

/// <summary>Una capa del paper-doll (un cuerpo, un peinado, una armadura, un arma...).</summary>
public sealed class LayerDef
{
    public string Id;
    public GearSlot Slot;
    public WeaponFamily Family;
    public HairHide Hide;
    public MatDef[] Mats;
    /// <summary>Por clip: [dirección][frame].</summary>
    public readonly Dictionary<ClipId, LayerFrame[][]> Clips = new();

    public LayerFrame Frame(ClipId clip, Dir8 dir, int frame)
    {
        if (!Clips.TryGetValue(clip, out var byDir)) return null;
        var frames = byDir[(int)dir];
        return frames[Math.Clamp(frame, 0, frames.Length - 1)];
    }
}

/// <summary>
/// Todas las capas horneadas por el generador (assets/sprites/chars.bin). Los frames de todas
/// las capas comparten tamaño y pivote (los pies), así se superponen sin ajustes.
/// </summary>
public sealed class LayerLibrary
{
    public const float DepthScale = 6f;   // pasos de profundidad por unidad de mundo
    public const int DepthZero = 128;

    public int FrameW, FrameH;
    public float PivotX, PivotY;
    /// <summary>Píxeles por unidad de mundo y elevación de la cámara del horneado (grados).</summary>
    public float K, Elevation;
    public readonly Dictionary<string, LayerDef> Layers = new();

    public static byte QuantDepth(float d) => (byte)Math.Clamp((int)MathF.Round(DepthZero + d * DepthScale), 1, 255);
    public static float Depth(byte q) => (q - DepthZero) / DepthScale;

    public void Add(LayerDef l)
    {
        if (!Layers.TryAdd(l.Id, l)) throw new InvalidOperationException("Capa duplicada: " + l.Id);
    }

    /// <summary>Busca la variante de una capa para la complexión ("torso/placas.robust"), si no la genérica.</summary>
    public LayerDef Find(string id, Build build)
    {
        if (id == null) return null;
        if (Layers.TryGetValue(id + "." + BuildName(build), out var l)) return l;
        return Layers.TryGetValue(id, out l) ? l : null;
    }

    public static string BuildName(Build b) => b.ToString().ToLowerInvariant();

    /// <summary>Las capas que lleva una apariencia, en orden de slot.</summary>
    public List<LayerDef> LayersFor(Appearance a)
    {
        var list = new List<LayerDef>();
        void Add(string id) { var l = Find(id, a.Build); if (l != null) list.Add(l); }
        Add("body");
        Add("eyes/" + a.Eyes);
        if (a.Beard > 0) Add("beard/" + a.Beard);
        Add("hair/" + a.HairStyle);
        Add(a.Torso);
        Add(a.Head);
        Add(a.Back);
        Add(a.OffHand);
        Add(a.MainHand);
        list.Sort((x, y) => x.Slot.CompareTo(y.Slot));
        return list;
    }

    // ------------------------------------------------------------------ binario

    private const uint Magic = 0x4C514E49; // "INQL"
    private const int Version = 2;

    public void Write(Stream s)
    {
        using var z = new ZLibStream(s, CompressionLevel.Optimal, leaveOpen: true);
        using var w = new BinaryWriter(z);
        w.Write(Magic); w.Write(Version);
        w.Write(FrameW); w.Write(FrameH); w.Write(PivotX); w.Write(PivotY); w.Write(K); w.Write(Elevation);
        w.Write(Layers.Count);
        foreach (var l in Layers.Values.OrderBy(l => l.Id, StringComparer.Ordinal))
        {
            w.Write(l.Id); w.Write((byte)l.Slot); w.Write((byte)l.Family); w.Write((byte)l.Hide);
            w.Write(l.Mats.Length);
            foreach (var m in l.Mats)
            {
                w.Write(m.Name ?? ""); w.Write((byte)m.Channel); w.Write(m.Rgb); w.Write(m.Shiny); w.Write(m.HairTop); w.Write(m.Lift); w.Write(m.Flat);
            }
            w.Write(l.Clips.Count);
            foreach (var (clip, byDir) in l.Clips.OrderBy(c => c.Key))
            {
                w.Write((byte)clip);
                w.Write(byDir.Length);
                foreach (var frames in byDir)
                {
                    w.Write(frames.Length);
                    foreach (var f in frames)
                    {
                        w.Write(f.X); w.Write(f.Y); w.Write(f.W); w.Write(f.H);
                        w.Write(f.Px);
                    }
                }
            }
        }
    }

    public static LayerLibrary Read(Stream s)
    {
        using var z = new ZLibStream(s, CompressionMode.Decompress, leaveOpen: true);
        using var r = new BinaryReader(z);
        if (r.ReadUInt32() != Magic) throw new InvalidDataException("chars.bin no es una biblioteca de capas");
        int ver = r.ReadInt32();
        if (ver != Version) throw new InvalidDataException($"chars.bin versión {ver}, se esperaba {Version}: regenerá el arte");
        var lib = new LayerLibrary
        {
            FrameW = r.ReadInt32(), FrameH = r.ReadInt32(), PivotX = r.ReadSingle(), PivotY = r.ReadSingle(),
            K = r.ReadSingle(), Elevation = r.ReadSingle(),
        };
        int n = r.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            var l = new LayerDef { Id = r.ReadString(), Slot = (GearSlot)r.ReadByte(), Family = (WeaponFamily)r.ReadByte(), Hide = (HairHide)r.ReadByte() };
            l.Mats = new MatDef[r.ReadInt32()];
            for (int m = 0; m < l.Mats.Length; m++)
                l.Mats[m] = new MatDef { Name = r.ReadString(), Channel = (MatChannel)r.ReadByte(), Rgb = r.ReadUInt32(), Shiny = r.ReadBoolean(), HairTop = r.ReadBoolean(), Lift = r.ReadSingle(), Flat = r.ReadSingle() };
            int clips = r.ReadInt32();
            for (int c = 0; c < clips; c++)
            {
                var id = (ClipId)r.ReadByte();
                var byDir = new LayerFrame[r.ReadInt32()][];
                for (int d = 0; d < byDir.Length; d++)
                {
                    byDir[d] = new LayerFrame[r.ReadInt32()];
                    for (int f = 0; f < byDir[d].Length; f++)
                    {
                        var fr = new LayerFrame { X = r.ReadInt16(), Y = r.ReadInt16(), W = r.ReadInt16(), H = r.ReadInt16() };
                        fr.Px = r.ReadBytes(fr.W * fr.H * 4);
                        byDir[d][f] = fr;
                    }
                }
                l.Clips[id] = byDir;
            }
            lib.Add(l);
        }
        return lib;
    }

    public static LayerLibrary Load(string path)
    {
        using var fs = File.OpenRead(path);
        return Read(fs);
    }
}

/// <summary>Normales empaquetadas en 2 bytes (mapeo octaédrico).</summary>
public static class Oct
{
    public static (byte, byte) Encode(Vector3 n)
    {
        n /= MathF.Abs(n.X) + MathF.Abs(n.Y) + MathF.Abs(n.Z);
        float x = n.X, y = n.Y;
        if (n.Z < 0)
        {
            x = (1 - MathF.Abs(n.Y)) * (n.X >= 0 ? 1 : -1);
            y = (1 - MathF.Abs(n.X)) * (n.Y >= 0 ? 1 : -1);
        }
        return ((byte)Math.Clamp((int)MathF.Round((x * 0.5f + 0.5f) * 255), 0, 255),
                (byte)Math.Clamp((int)MathF.Round((y * 0.5f + 0.5f) * 255), 0, 255));
    }

    public static Vector3 Decode(byte bx, byte by)
    {
        float x = bx / 255f * 2 - 1, y = by / 255f * 2 - 1;
        float z = 1 - MathF.Abs(x) - MathF.Abs(y);
        if (z < 0)
        {
            float ox = x;
            x = (1 - MathF.Abs(y)) * (ox >= 0 ? 1 : -1);
            y = (1 - MathF.Abs(ox)) * (y >= 0 ? 1 : -1);
        }
        return Vector3.Normalize(new Vector3(x, y, z));
    }
}
