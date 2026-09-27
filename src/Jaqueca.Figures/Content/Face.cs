using System.Numerics;
using Jaqueca.Figures.Model;
using Jaqueca.Figures.Rig;
using Jaqueca.Look;
using Jaqueca.Sprites;
using static Jaqueca.Figures.Model.Figure;

namespace Jaqueca.Figures.Content;

/// <summary>
/// Ojos y cejas como estampados sobre la cabeza del cuerpo. La cara mide ~6 píxeles de ancho:
/// un ojo es un píxel oscuro con la ceja (o la cuenca en sombra) encima; estampado, cae
/// siempre justo y no parpadea al girar.
/// </summary>
public static class Face
{
    public const int Styles = 4;
    public static readonly string[] Names = { "normales", "hundidos", "severos", "pestañas" };

    /// <summary>A qué altura y qué tan separados van los ojos (en el hueso de la cabeza).</summary>
    public const float EyeY = 1.8f, EyeZ = 0.32f;

    public static Figure Eyes(int style)
    {
        var f = new Figure { Occluder = HeadOnly() };
        int eye = f.AddMat("ojos", MatChannel.Eyes);
        int brow = f.AddMat("cejas", MatChannel.Hair);

        foreach (int side in new[] { 1, -1 })
        {
            var at = OnHead(EyeY, side * EyeZ, out var n);
            // dx positivo = hacia la sien (se espeja según el lado de la pantalla).
            var px = style switch
            {
                1 => new[] { (0, 0, eye, 1), (0, -1, eye, 0) },                                   // hundidos: la cuenca en sombra
                2 => new[] { (0, 0, eye, 2), (0, -1, brow, 0), (1, -1, brow, 0) },                // severos: ceja gruesa y baja
                3 => new[] { (0, 0, eye, 2), (1, 0, eye, 1) },                                    // pestañas: el ojo se estira hacia afuera
                _ => new[] { (0, 0, eye, 2), (0, -1, brow, 1) },
            };
            f.Stamps.Add(new Stamp
            {
                Bone = Bone.Head, At = at, Normal = n, Pixels = px, MinFacing = 0.3f,
                Outward = V(0, 0, side),
            });
        }
        return f;
    }

    /// <summary>
    /// Punto de la superficie de la cara a la altura y y al costado z (espacio del hueso), con
    /// su normal: el más adelantado entre el cráneo y la cara.
    /// </summary>
    public static Vector3 OnHead(float y, float z, out Vector3 normal)
    {
        var a = OnEllipsoid(Body.HeadC, Body.HeadR, y, z, out var na);
        var b = OnEllipsoid(FaceC, FaceR, y, z, out var nb);
        if (a.X >= b.X) { normal = na; return a; }
        normal = nb;
        return b;
    }

    /// <summary>La parte de abajo de la cara (mentón y mejillas), la misma que arma <see cref="Body"/>.</summary>
    public static readonly Vector3 FaceC = V(0.38f, 1.42f, 0), FaceR = V(0.66f, 0.78f, 0.6f);

    private static Vector3 OnEllipsoid(Vector3 c, Vector3 r, float y, float z, out Vector3 normal)
    {
        float dy = (y - c.Y) / r.Y, dz = z / r.Z;
        float x = c.X + r.X * MathF.Sqrt(MathF.Max(0, 1 - dy * dy - dz * dz));
        var p = V(x, y, z);
        normal = Vector3.Normalize((p - c) / (r * r));
        return p;
    }

    /// <summary>La cabeza sola (con nariz y orejas): lo que tapa a los ojos.</summary>
    private static Figure HeadOnly()
    {
        var body = Body.Build(Build.Slim);
        var f = new Figure();
        f.Mats.AddRange(body.Mats);
        f.Shapes.AddRange(body.Shapes.Where(s => s.Bone == Bone.Head));
        return f;
    }
}
