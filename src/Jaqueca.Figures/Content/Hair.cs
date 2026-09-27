using System.Numerics;
using Jaqueca.Figures.Model;
using Jaqueca.Figures.Rig;
using Jaqueca.Sprites;
using static Jaqueca.Figures.Model.Figure;

namespace Jaqueca.Figures.Content;

/// <summary>
/// Peinados y barbas del creador. Todos cuelgan del hueso de la cabeza, que es igual en las
/// dos complexiones, así cada uno se hornea una sola vez. Lo que queda por encima de
/// <see cref="TopY"/> es "arriba": lo esconden los sombreros de ala.
/// </summary>
public static class Hair
{
    public const int Styles = 6;
    public static readonly string[] Names = { "corto", "largo", "rapado", "coleta", "rizos", "calvo" };

    public const int Beards = 4;
    public static readonly string[] BeardNames = { "sin barba", "candado", "tupida", "larga" };

    /// <summary>Altura (en el hueso de la cabeza) desde la que el pelo cuenta como "arriba".</summary>
    public const float TopY = 2.55f;

    private static readonly Dims Human = new();

    public static Figure Build(int style)
    {
        var f = new Figure();
        int hair = f.AddMat("pelo", MatChannel.Hair, tex: Strands);
        int top = f.AddMat("pelo arriba", MatChannel.Hair, tex: Strands, hairTop: true);
        int part = f.Part();
        Func<Vector3, int, int> paint = (p, m) => p.Y > TopY ? top : m;

        Ellipsoid Blob(Vector3 c, Vector3 r)
        {
            var e = f.Ellipsoid(Bone.Head, c, r, hair, part);
            e.Paint = paint;
            return e;
        }
        RoundCone Lock(Vector3 a, float ra, Vector3 b, float rb)
        {
            var c = f.Cone(Bone.Head, a, ra, b, rb, hair, part);
            c.Paint = paint;
            return c;
        }
        // Casquete: cubre el cráneo por encima de la línea del pelo (baja hacia la nuca).
        Ellipsoid Cap(float grow, float front = 2.38f, float nape = 1.35f)
        {
            var e = Blob(Body.HeadC + V(-0.04f, 0.05f, 0), Body.HeadR + V(grow, grow * 0.9f, grow));
            e.Mask = p => p.Y > Hairline(p.X, front, nape);
            return e;
        }

        switch (style)
        {
            case 0: // corto, con flequillo de lado y un remolino arriba
                Cap(0.1f);
                Blob(V(0.7f, 2.42f, -0.28f), V(0.3f, 0.2f, 0.48f));
                Blob(V(-0.1f, 2.95f, 0.15f), V(0.36f, 0.14f, 0.3f));
                break;
            case 1: // largo hasta los hombros, con mechones al costado de la cara
                // Raya al medio: el pelo cae a los dos lados de la frente.
                Cap(0.13f, front: 2.42f, nape: 1.1f);
                Blob(V(0.62f, 2.42f, -0.4f), V(0.3f, 0.2f, 0.36f));
                Blob(V(0.6f, 2.42f, 0.42f), V(0.3f, 0.2f, 0.34f));
                // La melena cuelga de dos huesos con inercia; en reposo queda igual que un solo mechón.
                var b1 = Human.HairBackRoot;
                var b2 = b1 - new Vector3(0, Human.HairBackSeg, 0);
                var mid = b1 + V(-0.12f, -Human.HairBackSeg, 0);
                var end = mid + V(-0.1f, -Human.HairBackSeg * 0.95f, 0);
                foreach (var (bone, a, ra, e, re, at) in new[] { (Bone.HairBack1, b1, 0.82f, mid, 0.72f, b1), (Bone.HairBack2, mid, 0.72f, end, 0.6f, b2) })
                {
                    // Achatada de adelante hacia atrás (alrededor del enganche del hueso).
                    var c = f.Cone(bone, a, ra, e, re, hair, part);
                    c.Local = Matrix4x4.CreateTranslation(-at) * Matrix4x4.CreateScale(0.75f, 1, 1.12f);
                    c.Origin = at;
                    c.Mask = p => p.X < 0.25f;
                }
                foreach (int side in new[] { 1, -1 })
                    Lock(V(0.3f, 2.3f, side * 0.72f), 0.24f, V(0.12f, 1.05f, side * 0.72f), 0.18f);
                break;
            case 2: // rapado: una capa fina pegada al cráneo
                Cap(0.05f, front: 2.42f, nape: 1.5f);
                break;
            case 3: // coleta atada atrás
                Cap(0.09f, front: 2.36f);
                Blob(V(0.72f, 2.38f, 0.32f), V(0.3f, 0.24f, 0.4f));
                Blob(V(-1.0f, 2.2f, 0), V(0.26f, 0.26f, 0.26f));
                // La coleta: dos tramos con inercia.
                var t1 = Human.TailRoot;
                var t2 = t1 - new Vector3(0, Human.TailSeg, 0);
                var tm = t1 + V(-0.15f, -Human.TailSeg, 0);
                var te = tm + V(-0.1f, -Human.TailSeg, 0);
                var c1 = f.Cone(Bone.Tail1, V(0, 0, 0), 0.24f, tm - t1, 0.2f, hair, part);
                c1.Origin = t1;
                var c2 = f.Cone(Bone.Tail2, tm - t2, 0.2f, te - t2, 0.13f, hair, part);
                c2.Origin = t2;
                break;
            case 4: // rizos: volumen redondo de bucles
                Cap(0.18f, front: 2.32f, nape: 1.2f);
                var rng = new Random(7);
                for (int i = 0; i < 16; i++)
                {
                    float a = i / 16f * MathF.Tau, h = 1.75f + 1.15f * (float)rng.NextDouble();
                    float rr = 0.98f * MathF.Sqrt(MathF.Max(0.2f, 1 - MathF.Pow((h - 1.95f) / 1.2f, 2)));
                    var c = V(-0.05f + MathF.Cos(a) * rr, h, MathF.Sin(a) * rr * 0.85f);
                    if (c.X > 0.35f && h < 2.35f) continue; // no taparle la cara
                    Blob(c, V(0.32f, 0.3f, 0.32f));
                }
                break;
            default: // calvo
                break;
        }
        return f;
    }

    /// <summary>
    /// Barbas: siguen la mandíbula (el mentón y los costados de la cara) sin taparle los ojos.
    /// Son del color del pelo. null = sin barba.
    /// </summary>
    public static Figure Beard(int style)
    {
        if (style <= 0) return null;
        var f = new Figure();
        int hair = f.AddMat("barba", MatChannel.Hair, tex: Strands);
        int part = f.Part();
        var jaw = Face.FaceC;
        switch (style)
        {
            case 1: // candado: bigote y mentón
                f.Ellipsoid(Bone.Head, V(0.92f, 1.3f, 0), V(0.16f, 0.12f, 0.34f), hair, part);
                f.Ellipsoid(Bone.Head, V(0.78f, 0.82f, 0), V(0.3f, 0.3f, 0.3f), hair, part);
                break;
            case 2: // tupida y corta: toda la mandíbula
            {
                var e = f.Ellipsoid(Bone.Head, jaw + V(0.06f, -0.04f, 0), FaceR + V(0.08f, 0.08f, 0.1f), hair, part);
                e.Mask = p => p.Y < 1.4f - 0.2f * MathF.Max(0, p.X - 0.7f) && p.X > -0.05f;
                f.Ellipsoid(Bone.Head, V(0.92f, 1.32f, 0), V(0.18f, 0.12f, 0.36f), hair, part);
                break;
            }
            default: // larga: baja sobre el pecho
            {
                var e = f.Ellipsoid(Bone.Head, jaw + V(0.06f, -0.04f, 0), FaceR + V(0.09f, 0.08f, 0.1f), hair, part);
                e.Mask = p => p.Y < 1.42f - 0.2f * MathF.Max(0, p.X - 0.7f) && p.X > -0.05f;
                f.Cone(Bone.Head, V(0.62f, 0.85f, 0), 0.5f, V(0.72f, -0.35f, 0), 0.2f, hair, part);
                f.Ellipsoid(Bone.Head, V(0.92f, 1.32f, 0), V(0.18f, 0.13f, 0.38f), hair, part);
                break;
            }
        }
        return f;
    }

    private static readonly Vector3 FaceR = Face.FaceR;

    /// <summary>Altura de la línea del pelo según qué tan adelante está el punto.</summary>
    private static float Hairline(float x, float front, float nape)
    {
        float t = Math.Clamp((0.62f - x) / 1.2f, 0, 1);
        return front + (nape - front) * t;
    }

    /// <summary>Mechones: bandas que siguen la vuelta de la cabeza.</summary>
    private static int Strands(Vector3 p, int t)
    {
        float a = MathF.Atan2(p.Z, p.X - 0.1f);
        float s = MathF.Sin(a * 8 + p.Y * 2.5f);
        return s > 0.8f && t > 1 ? t - 1 : t;
    }
}
