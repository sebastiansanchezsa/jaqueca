using System.Numerics;
using Jaqueca.Figures.Rig;
using Jaqueca.Sprites;

namespace Jaqueca.Figures.Model;

/// <summary>Material de una capa: lo que va al binario (<see cref="MatDef"/>) más cómo se trabaja al hornear.</summary>
public sealed class Mat
{
    public MatDef Def;
    /// <summary>Modula el tono según la posición en el hueso (punto, tono) → tono. Pegado al modelo: no "hierve" entre frames.</summary>
    public Func<Vector3, int, int> Tex;
}

/// <summary>
/// Detalle de 1-3 píxeles estampado sobre una superficie (ojos, cejas). A esta escala una
/// forma 3D tan chica parpadea entre frames: el estampado cae siempre en el píxel exacto.
/// </summary>
public sealed class Stamp
{
    public Bone Bone;
    /// <summary>Punto y normal en el espacio del hueso.</summary>
    public Vector3 At, Normal;
    /// <summary>Píxeles relativos al punto proyectado: (dx, dy, material, tono).</summary>
    public (int dx, int dy, int mat, int tone)[] Pixels;
    /// <summary>Qué tan de frente tiene que estar la superficie para que se vea (coseno).</summary>
    public float MinFacing = 0.2f;
    /// <summary>
    /// Eje del hueso hacia "afuera" (p. ej. la sien de ese lado). Si está, los dx se espejan
    /// según hacia qué lado de la pantalla cae: así pestañas y cejas apuntan siempre afuera.
    /// </summary>
    public Vector3 Outward;
    /// <summary>Píxeles con el ojo cerrado (parpadeo); null = no parpadea.</summary>
    public (int dx, int dy, int mat, int tone)[] Closed;
}

/// <summary>Una capa del paper-doll lista para hornear: formas, materiales y estampados.</summary>
public sealed partial class Figure
{
    public readonly List<Shape> Shapes = new();
    public readonly List<Mat> Mats = new();
    public readonly List<Stamp> Stamps = new();
    /// <summary>Lo que tapa a los estampados al hornear la capa sola (para los ojos: la cabeza del cuerpo).</summary>
    public Figure Occluder;
    /// <summary>En vivo: partes sobre las que caen los estampados (la cara).</summary>
    public readonly HashSet<int> StampParts = new();
    /// <summary>Hay un sombrero de ala: el pelo marcado como "arriba" no se dibuja.</summary>
    public bool HideHairTop;
    private int _nextPart = 1;

    public int Part() => _nextPart++;

    public int AddMat(string name, MatChannel ch, uint rgb = 0, bool shiny = false, Func<Vector3, int, int> tex = null, float flat = 0, float lift = 0, bool hairTop = false)
    {
        Mats.Add(new Mat { Def = new MatDef { Name = name, Channel = ch, Rgb = rgb, Shiny = shiny, HairTop = hairTop, Flat = flat, Lift = lift }, Tex = tex });
        return Mats.Count - 1;
    }

    private T Add<T>(T s, Bone bone, int mat, int part) where T : Shape
    {
        s.Bone = bone; s.Mat = mat; s.Part = part == 0 ? Part() : part;
        Shapes.Add(s);
        return s;
    }

    public Ellipsoid Ellipsoid(Bone bone, Vector3 c, Vector3 r, int mat, int part = 0)
        => Add(new Ellipsoid { C = c, R = r }, bone, mat, part);

    public RoundCone Cone(Bone bone, Vector3 a, float ra, Vector3 b, float rb, int mat, int part = 0)
        => Add(new RoundCone { A = a, B = b, Ra = ra, Rb = rb }, bone, mat, part);

    /// <summary>Una cuerda de radio <paramref name="r"/> de un punto de <paramref name="bone"/> a uno de <paramref name="to"/>.</summary>
    public RoundCone Cord(Bone bone, Vector3 from, Bone to, Vector3 at, float r, int mat, int part = 0)
    {
        var c = Add(new RoundCone { A = Vector3.Zero, B = Vector3.UnitY, Ra = r, Rb = r }, bone, mat, part);
        c.CordBone = to;
        c.CordFrom = from;
        c.CordTo = at;
        c.Thin = true;
        return c;
    }

    public Box Box(Bone bone, Vector3 c, Vector3 half, int mat, int part = 0)
        => Add(new Box { C = c, Half = half }, bone, mat, part);

    public static Vector3 V(float x, float y, float z) => new(x, y, z);
}
