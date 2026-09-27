using System.Numerics;
using Jaqueca.Figures.Model;
using Jaqueca.Figures.Rig;
using Jaqueca.Sprites;
using static Jaqueca.Figures.Model.Figure;

namespace Jaqueca.Figures.Content;

/// <summary>
/// Lo que se tiene un momento en la mano izquierda (ver Look.Appearance.Prop): no es un arma, no pega y no se
/// guarda en la figura de todos los días (sólo mientras se usa: así no ocupa materiales el resto del tiempo).
/// Se agarra como un arma, con el eje X del hueso de la mano pasando por el puño.
/// </summary>
public static class Props
{
    /// <summary>La calabaza del brebaje de Ossa.</summary>
    public const string Gourd = "calabaza";

    /// <summary>Dónde está la boca de la calabaza en el hueso de la mano (la escena la lleva a los labios).</summary>
    public static readonly Vector3 GourdMouth = V(1.55f, WeaponModels.Fist.Y, 0);

    /// <summary>El modelo de <paramref name="id"/> en la mano izquierda (null si no hay).</summary>
    public static Figure Build(string id) => id switch
    {
        Gourd => GourdModel(),
        _ => null,
    };

    /// <summary>
    /// Una calabacita de peregrino: el bulbo grande en el puño, la cintura atada con un tiento, el bulbo chico
    /// arriba y el cuello tapado con un corcho sellado con cera. La boca apunta hacia adelante del puño (+X).
    /// </summary>
    private static Figure GourdModel()
    {
        var f = new Figure();
        int skin = f.AddMat("calabaza seca", MatChannel.Fixed, 0xC0894A, tex: Speckle);
        int cork = f.AddMat("corcho y cera", MatChannel.Fixed, 0x5E3E26);
        int part = f.Part();
        var hand = Skeleton.Arm(-1, 2);
        float y = WeaponModels.Fist.Y;
        f.Ellipsoid(hand, V(0.3f, y, 0), V(0.62f, 0.5f, 0.5f), skin, part);
        f.Cone(hand, V(0.72f, y, 0), 0.26f, V(0.86f, y, 0), 0.26f, cork, part);
        f.Ellipsoid(hand, V(1.08f, y, 0), V(0.34f, 0.3f, 0.3f), skin, part);
        f.Cone(hand, V(1.3f, y, 0), 0.14f, V(1.46f, y, 0), 0.13f, skin, part);
        f.Cone(hand, V(1.46f, y, 0), 0.16f, V(1.6f, y, 0), 0.15f, cork, part);
        return f;
    }

    /// <summary>La piel de la calabaza: pecas oscuras del secado (un tono más abajo).</summary>
    private static int Speckle(Vector3 p, int t) => MathF.Sin(p.X * 23.1f + p.Y * 7.3f) * MathF.Sin(p.Z * 19.7f - p.X * 5.1f) > 0.8f && t > 0 ? t - 1 : t;
}
