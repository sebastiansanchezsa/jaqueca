using System.Numerics;
using Jaqueca.Figures.Rig;

namespace Jaqueca.Figures.Model;

/// <summary>
/// Una prenda de tela que cuelga (una falda, una túnica, un faldón, una capa): una grilla de puntos
/// que en reposo tiene la forma que se le dio (<see cref="Rest"/>, en el espacio de su hueso, a la medida
/// de referencia) y en el juego se mueve sola (ver Physics.ClothSim): la fila de arriba va cosida al hueso,
/// el resto cae con su peso, se hamaca con el cuerpo, vuelve de a poco a su forma y las piernas la empujan
/// en vez de atravesarla. Se dibuja con triángulos (<see cref="ClothTri"/>) que pintan igual que las formas
/// de siempre: el dibujo de la tela (franjas, mugre, trama) sale del punto de reposo, así queda pegado a la tela.
/// </summary>
public sealed class ClothDef
{
    /// <summary>El hueso del que cuelga (la cadera para una falda, la espalda para una capa).</summary>
    public Bone Anchor;
    /// <summary>Puntos por fila (alrededor o a lo ancho) y filas de tela debajo de la cosida.</summary>
    public int Cols, Rows;
    /// <summary>Cerrada (una falda: la última columna se une con la primera) o abierta (un paño).</summary>
    public bool Ring;
    /// <summary>(Rows + 1) × Cols puntos en el espacio del hueso, fila por fila desde la de arriba.</summary>
    public Vector3[] Rest;
    /// <summary>Qué tan firme vuelve a su forma (0: blanda, se hamaca mucho; 1: casi rígida; arriba, cerca de la costura, es más firme).</summary>
    public float Stiff = 0.14f;
    /// <summary>Cuánto se frena al hamacarse (1: se detiene sin pasarse; menos, se hamaca un poco antes de quedarse).</summary>
    public float Damp = 0.6f;
    /// <summary>Cuánto pesa (1: lo de siempre): más pesada, se queda más atrás cuando el cuerpo se mueve y cae más.</summary>
    public float Weight = 1;
    /// <summary>Radio de las piernas contra las que choca (a la medida de referencia) y la distancia que guarda.</summary>
    public float LegR = 1.1f, Pad = 0.3f;
    /// <summary>Si choca también contra el cuerpo (radio del tronco, a la medida de referencia; 0 = no): para las capas.</summary>
    public float BodyR;
    /// <summary>Los triángulos que la dibujan.</summary>
    public readonly List<ClothTri> Tris = new();

    /// <summary>Recorta la tela (punto de reposo, en el espacio del hueso): el ruedo deshilachado, un tajo.</summary>
    public Func<Vector3, bool> Mask { set { foreach (var t in Tris) t.Mask = value; } }
    /// <summary>Repinta la tela según el punto de reposo (franjas, mugre, sangre).</summary>
    public Func<Vector3, int, int> Paint { set { foreach (var t in Tris) t.Paint = value; } }

    public int Index(int col, int row) => row * Cols + col;
    public int Count => Cols * (Rows + 1);
}

/// <summary>
/// Un triángulo de una tela (ver <see cref="ClothDef"/>). No se traza como las demás formas: el
/// rasterizador lo dibuja con los puntos de la tela de este cuadro (simulados, o en reposo si no hay
/// simulación). <see cref="Shape.Paint"/>, <see cref="Shape.Mask"/> y la textura del material reciben el
/// punto de reposo (en el espacio del hueso), como una forma rígida.
/// </summary>
public sealed class ClothTri : Shape
{
    public ClothDef Cloth;
    public int A, B, C;

    public override Vector3 BoundCenter => (Cloth.Rest[A] + Cloth.Rest[B] + Cloth.Rest[C]) / 3;
    public override float BoundRadius => MathF.Max(Vector3.Distance(BoundCenter, Cloth.Rest[A]), MathF.Max(Vector3.Distance(BoundCenter, Cloth.Rest[B]), Vector3.Distance(BoundCenter, Cloth.Rest[C])));

    public override bool Hit(Vector3 o, Vector3 d, out float t0, out Vector3 n0, out float t1, out Vector3 n1)
    {
        t0 = t1 = 0;
        n0 = n1 = Vector3.UnitY;
        return false;
    }
}

public sealed partial class Figure
{
    /// <summary>Las telas de la figura (cada una con sus triángulos en <see cref="Shapes"/>).</summary>
    public readonly List<ClothDef> Cloths = new();

    /// <summary>
    /// Una tela que cuelga de <paramref name="anchor"/>: <paramref name="cols"/> puntos por fila y
    /// <paramref name="rows"/> filas debajo de la costura, en reposo donde dice <paramref name="surface"/>
    /// (u de 0 a 1 alrededor o a lo ancho, v de 0 en la costura a 1 en el ruedo; en el espacio del hueso).
    /// </summary>
    public ClothDef Cloth(Bone anchor, int cols, int rows, bool ring, Func<float, float, Vector3> surface, int mat, int part = 0,
        Func<Vector3, int, int> paint = null, Func<Vector3, bool> mask = null)
    {
        var c = new ClothDef { Anchor = anchor, Cols = cols, Rows = rows, Ring = ring, Rest = new Vector3[cols * (rows + 1)] };
        for (int r = 0; r <= rows; r++)
        for (int k = 0; k < cols; k++)
            c.Rest[c.Index(k, r)] = surface(ring ? k / (float)cols : k / (float)(cols - 1), r / (float)rows);
        if (part == 0) part = Part();
        int spans = ring ? cols : cols - 1;
        for (int r = 0; r < rows; r++)
        for (int k = 0; k < spans; k++)
        {
            int k1 = (k + 1) % cols;
            int a = c.Index(k, r), b = c.Index(k1, r), d = c.Index(k, r + 1), e = c.Index(k1, r + 1);
            foreach (var (x, y, z) in new[] { (a, b, e), (a, e, d) })
            {
                var t = new ClothTri { Cloth = c, A = x, B = y, C = z, Bone = anchor, Mat = mat, Part = part, Paint = paint, Mask = mask };
                c.Tris.Add(t);
                Shapes.Add(t);
            }
        }
        Cloths.Add(c);
        return c;
    }

    /// <summary>
    /// Una tela con forma de cono (como <see cref="Cone"/> de <paramref name="a"/> a <paramref name="b"/>, sin tapas):
    /// una falda, un faldón o una túnica. <paramref name="squashX"/> la achata adelante y atrás (como el
    /// torso) y <paramref name="squashZ"/> a los costados; u da la vuelta desde adelante (+X).
    /// </summary>
    public ClothDef ConeCloth(Bone bone, Vector3 a, float ra, Vector3 b, float rb, int mat, int part = 0, float squashX = 1, float squashZ = 1,
        int cols = 16, int rows = 4, Func<Vector3, int, int> paint = null, Func<Vector3, bool> mask = null)
    {
        return Cloth(bone, cols, rows, true, (u, v) =>
        {
            float ang = u * MathF.Tau, r = ra + (rb - ra) * v;
            return Vector3.Lerp(a, b, v) + new Vector3(MathF.Cos(ang) * r * squashX, 0, MathF.Sin(ang) * r * squashZ);
        }, mat, part, paint, mask);
    }

    /// <summary>
    /// Un paño que cuelga (un faldón de adelante, una capa): de la costura <paramref name="left"/>..<paramref name="right"/>
    /// cae hasta <paramref name="leftHem"/>..<paramref name="rightHem"/>, con <paramref name="bulge"/> de panza hacia
    /// <paramref name="outward"/> en el medio (no queda plano contra el cuerpo).
    /// </summary>
    public ClothDef PanelCloth(Bone bone, Vector3 left, Vector3 right, Vector3 leftHem, Vector3 rightHem, Vector3 outward, float bulge, int mat, int part = 0,
        int cols = 6, int rows = 4, Func<Vector3, int, int> paint = null, Func<Vector3, bool> mask = null)
    {
        return Cloth(bone, cols, rows, false, (u, v) =>
        {
            var top = Vector3.Lerp(left, right, u);
            var bot = Vector3.Lerp(leftHem, rightHem, u);
            return Vector3.Lerp(top, bot, v) + outward * (bulge * MathF.Sin(u * MathF.PI) * (0.4f + 0.6f * v));
        }, mat, part, paint, mask);
    }
}
