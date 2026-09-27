using System.Numerics;
using Jaqueca.Figures.Model;
using Jaqueca.Figures.Rig;
using Jaqueca.Sprites;
using static Jaqueca.Figures.Model.Figure;

namespace Jaqueca.Figures.Content;

/// <summary>
/// Lo que se ve de Ernesto en primera persona: las manos, las mangas del pijama a rayas, las armas y la
/// pierna de la patada con la pantufla. Cada pieza cuelga de un hueso que el juego ubica delante de la
/// cámara (no hay esqueleto: los huesos son lugares): <see cref="Bone.HandR"/> la mano del arma,
/// <see cref="Bone.HandL"/> la otra (la corredera de la escopeta), <see cref="Bone.FootR"/> la pierna.
/// En esas piezas +X es hacia adelante (hacia donde apunta el caño), +Y arriba y +Z a la derecha.
/// </summary>
public static class Ernesto
{
    public const uint Skin = 0xD4A07E, Pajama = 0x3C5A88, PajamaStripe = 0xD6DAE0, Nickel = 0x98A4B2, NickelDark = 0x4E5664,
        Pearl = 0xE8E1CE, RedPlastic = 0xB4271F, OrangeTip = 0xEE6A1A, Blued = 0x2E343E, Walnut = 0x7A4A2A, Tape = 0x1C1C1E,
        Brass = 0xC8A050, Slipper = 0x8A2C2C, SlipperDark = 0x3A1618, Sole = 0x2A2420;

    /// <summary>Dónde sale el tiro (la punta del caño) en la pieza del arma.</summary>
    public static readonly Vector3 RevolverMuzzle = V(3.55f, 0.25f, 0), ShotgunMuzzle = V(6.3f, 0.3f, 0);
    /// <summary>Dónde va la mano de la corredera (el hueso de la izquierda) en la pieza de la escopeta.</summary>
    public static readonly Vector3 ShotgunPump = V(3.1f, 0.02f, 0);

    /// <summary>Rayas a lo largo de un tubo de <paramref name="a"/> a <paramref name="b"/>: <paramref name="n"/> alrededor.</summary>
    private static Func<Vector3, int, int> Stripes(Vector3 a, Vector3 b, int n, int stripe)
    {
        var u = Vector3.Normalize(b - a);
        var v = Vector3.Normalize(Vector3.Cross(u, MathF.Abs(u.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY));
        var w = Vector3.Cross(u, v);
        return (p, m) =>
        {
            var q = p - a;
            float ang = MathF.Atan2(Vector3.Dot(q, w), Vector3.Dot(q, v));
            return MathF.Sin(ang * n) > 0.55f ? stripe : m;
        };
    }

    /// <summary>La mano derecha cerrada sobre un mango vertical que pasa por <paramref name="grip"/> (con el índice en el gatillo).</summary>
    private static void GripHand(Figure f, Bone bone, Vector3 grip, int skin, int part)
    {
        f.Ellipsoid(bone, grip + V(-0.08f, 0.02f, 0), V(0.46f, 0.6f, 0.4f), skin, part);                        // la palma
        for (int i = 0; i < 3; i++)                                                                                   // los dedos, al frente del mango
            f.Ellipsoid(bone, grip + V(0.3f, -0.12f - i * 0.24f, 0.04f), V(0.2f, 0.12f, 0.33f), skin, part);
        f.Cone(bone, grip + V(0.05f, 0.4f, 0.12f), 0.12f, grip + V(0.55f, 0.22f, 0.16f), 0.1f, skin, part);          // el índice, al gatillo
        f.Cone(bone, grip + V(-0.25f, 0.5f, -0.3f), 0.14f, grip + V(0.25f, 0.62f, -0.28f), 0.11f, skin, part);       // el pulgar, arriba
    }

    /// <summary>La muñeca y la manga que se van hacia atrás y abajo, fuera de la pantalla.</summary>
    private static void Sleeve(Figure f, Bone bone, Vector3 wrist, Vector3 elbow, int skin, int pajama, int stripe, int part)
    {
        f.Cone(bone, wrist, 0.34f, wrist + (elbow - wrist) * 0.12f, 0.36f, skin, part);
        var at = wrist + (elbow - wrist) * 0.1f;
        var cuff = f.Cone(bone, at, 0.5f, elbow, 0.72f, pajama, part);
        cuff.Paint = Stripes(at, elbow, 5, stripe);
        f.Cone(bone, at, 0.54f, at + Vector3.Normalize(elbow - wrist) * 0.25f, 0.54f, pajama, part);               // el puño de la manga
    }

    /// <summary>El revólver de cebita: niquelado, cachas de plástico blanco con el medallón rojo y la punta naranja del juguete.</summary>
    public static Figure Revolver()
    {
        var f = new Figure();
        int skin = f.AddMat("piel", MatChannel.Fixed, Skin, lift: 0.05f);
        int pajama = f.AddMat("pijama", MatChannel.Fixed, Pajama);
        int stripe = f.AddMat("raya", MatChannel.Fixed, PajamaStripe);
        int nickel = f.AddMat("níquel", MatChannel.Fixed, Nickel, shiny: true);
        int dark = f.AddMat("níquel oscuro", MatChannel.Fixed, NickelDark, shiny: true);
        int pearl = f.AddMat("cachas", MatChannel.Fixed, Pearl, shiny: true);
        int red = f.AddMat("plástico rojo", MatChannel.Fixed, RedPlastic, shiny: true);
        int orange = f.AddMat("punta naranja", MatChannel.Fixed, OrangeTip, lift: 0.1f);
        var b = Bone.HandR;
        int gun = f.Part();
        f.Cone(b, V(0.45f, 0.25f, 0), 0.19f, V(3.35f, 0.25f, 0), 0.17f, nickel, gun);                          // caño
        f.Box(b, V(1.9f, 0.43f, 0), V(1.45f, 0.05f, 0.07f), nickel, gun);                                       // la costilla de arriba
        f.Box(b, V(3.22f, 0.5f, 0), V(0.06f, 0.09f, 0.04f), dark, gun);                                         // mira
        f.Cone(b, V(3.33f, 0.25f, 0), 0.2f, V(3.55f, 0.25f, 0), 0.2f, orange, gun);                             // la punta naranja
        f.Cone(b, V(0.7f, 0.0f, 0), 0.07f, V(2.4f, 0.0f, 0), 0.07f, nickel, gun);                               // varilla
        var cyl = f.Cone(b, V(-0.05f, 0.17f, 0), 0.42f, V(0.62f, 0.17f, 0), 0.42f, nickel, gun);               // tambor
        cyl.Paint = (p, m) => MathF.Sin(MathF.Atan2(p.Z, p.Y - 0.17f) * 6) > 0.6f ? dark : m;
        f.Box(b, V(0.05f, 0.08f, 0), V(0.62f, 0.3f, 0.13f), nickel, gun);                                       // armazón
        f.Box(b, V(-0.5f, 0.5f, 0), V(0.14f, 0.13f, 0.06f), dark, gun);                                         // martillo
        f.Box(b, V(0.05f, -0.34f, 0), V(0.05f, 0.14f, 0.04f), dark, gun);                                       // gatillo
        f.Cone(b, V(-0.15f, -0.2f, 0), 0.05f, V(0.35f, -0.5f, 0), 0.05f, nickel, gun);                          // guardamonte
        f.Cone(b, V(0.35f, -0.5f, 0), 0.05f, V(0.45f, -0.18f, 0), 0.05f, nickel, gun);
        var grip = f.Box(b, V(-0.55f, -0.55f, 0), V(0.25f, 0.62f, 0.17f), pearl, gun);                          // cachas
        grip.Local = Matrix4x4.CreateTranslation(-V(-0.55f, -0.55f, 0)) * Matrix4x4.CreateRotationZ(-0.3f) * Matrix4x4.CreateTranslation(V(-0.55f, -0.55f, 0));
        foreach (int side in new[] { 1, -1 })
            f.Ellipsoid(b, V(-0.6f, -0.45f, side * 0.18f), V(0.12f, 0.14f, 0.03f), red, gun);
        int hand = f.Part();
        GripHand(f, b, V(-0.5f, -0.5f, 0), skin, hand);
        Sleeve(f, b, V(-1.0f, -0.85f, 0.05f), V(-4.6f, -2.6f, 0.9f), skin, pajama, stripe, hand);
        return f;
    }

    /// <summary>
    /// La escopeta del abuelo: de corredera, pavonada, la culata de nogal con cinta aisladora. La mano
    /// derecha en la empuñadura (<see cref="Bone.HandR"/>) y la izquierda en la corredera (<see cref="Bone.HandL"/>).
    /// </summary>
    public static Figure Shotgun()
    {
        var f = new Figure();
        int skin = f.AddMat("piel", MatChannel.Fixed, Skin, lift: 0.05f);
        int pajama = f.AddMat("pijama", MatChannel.Fixed, Pajama);
        int stripe = f.AddMat("raya", MatChannel.Fixed, PajamaStripe);
        int steel = f.AddMat("pavonado", MatChannel.Fixed, Blued, shiny: true);
        int wood = f.AddMat("nogal", MatChannel.Fixed, Walnut, tex: Grain);
        int tape = f.AddMat("cinta", MatChannel.Fixed, Tape, shiny: true);
        int brass = f.AddMat("bronce", MatChannel.Fixed, Brass, shiny: true);
        var b = Bone.HandR;
        int gun = f.Part();
        f.Box(b, V(0.1f, 0.1f, 0), V(0.95f, 0.34f, 0.19f), steel, gun);                                        // cajón
        f.Cone(b, V(0.95f, 0.3f, 0), 0.21f, V(6.3f, 0.3f, 0), 0.2f, steel, gun);                               // caño
        f.Cone(b, V(0.95f, 0.02f, 0), 0.17f, V(5.5f, 0.02f, 0), 0.17f, steel, gun);                            // tubo del cargador
        f.Ellipsoid(b, V(6.15f, 0.54f, 0), V(0.06f, 0.06f, 0.06f), brass, gun);                                 // la mira de bronce
        var stock = f.Box(b, V(-1.95f, -0.45f, 0), V(1.2f, 0.36f, 0.17f), wood, gun);                           // culata
        stock.Local = Matrix4x4.CreateTranslation(-V(-1.95f, -0.45f, 0)) * Matrix4x4.CreateRotationZ(0.2f) * Matrix4x4.CreateTranslation(V(-1.95f, -0.45f, 0));
        stock.Paint = (p, m) => MathF.Abs(p.X + 1.4f) < 0.22f || MathF.Abs(p.X + 2.0f) < 0.12f ? tape : m;      // la cinta aisladora
        var wrist = f.Box(b, V(-0.72f, -0.22f, 0), V(0.3f, 0.3f, 0.15f), wood, gun);
        wrist.Local = Matrix4x4.CreateTranslation(-V(-0.72f, -0.22f, 0)) * Matrix4x4.CreateRotationZ(-0.25f) * Matrix4x4.CreateTranslation(V(-0.72f, -0.22f, 0));
        f.Box(b, V(-0.1f, -0.3f, 0), V(0.05f, 0.14f, 0.04f), steel, gun);                                      // gatillo
        int hand = f.Part();
        GripHand(f, b, V(-0.65f, -0.45f, 0), skin, hand);
        Sleeve(f, b, V(-1.1f, -0.8f, 0.05f), V(-4.8f, -2.7f, 1.0f), skin, pajama, stripe, hand);

        // La corredera con la mano izquierda debajo, y el brazo que se va hacia la izquierda.
        var l = Bone.HandL;
        int pump = f.Part();
        var pb = f.Box(l, V(0, 0, 0), V(0.95f, 0.26f, 0.26f), wood, pump);
        pb.Paint = (p, m) => MathF.Sin(p.X * 14) > 0.7f ? tape : m;
        int lh = f.Part();
        f.Ellipsoid(l, V(-0.05f, -0.26f, 0), V(0.55f, 0.34f, 0.4f), skin, lh);
        for (int i = 0; i < 4; i++) f.Ellipsoid(l, V(-0.35f + i * 0.24f, 0.0f, 0.3f), V(0.12f, 0.2f, 0.12f), skin, lh);
        f.Cone(l, V(0.1f, 0.05f, -0.32f), 0.13f, V(0.55f, 0.12f, -0.3f), 0.11f, skin, lh);
        Sleeve(f, l, V(-0.3f, -0.45f, -0.1f), V(-3.2f, -3.2f, -2.4f), skin, pajama, stripe, lh);
        return f;
    }

    /// <summary>La pierna de la patada: el pantalón del pijama a rayas y la pantufla escocesa.</summary>
    public static Figure Leg()
    {
        var f = new Figure();
        int skin = f.AddMat("piel", MatChannel.Fixed, Skin, lift: 0.05f);
        int pajama = f.AddMat("pijama", MatChannel.Fixed, Pajama);
        int stripe = f.AddMat("raya", MatChannel.Fixed, PajamaStripe);
        int slipper = f.AddMat("pantufla", MatChannel.Fixed, Slipper, tex: Plaid);
        int dark = f.AddMat("escocés", MatChannel.Fixed, SlipperDark);
        int sole = f.AddMat("suela", MatChannel.Fixed, Sole);
        var b = Bone.FootR;
        int part = f.Part();
        var shoe = f.Ellipsoid(b, V(0.5f, 0, 0), V(1.7f, 0.55f, 0.7f), slipper, part);
        shoe.Paint = (p, m) => MathF.Sin(p.X * 6) > 0.85f || MathF.Sin(p.Z * 7) > 0.85f ? dark : m;
        f.Box(b, V(0.4f, -0.5f, 0), V(1.75f, 0.1f, 0.66f), sole, part);
        f.Ellipsoid(b, V(-0.7f, 0.45f, 0), V(0.5f, 0.45f, 0.45f), skin, part);                                 // el tobillo
        // La pierna viene de atrás y de abajo (de la cadera de Ernesto, debajo de la cámara) hacia el pie.
        var legA = V(-0.8f, 0.5f, 0); var legB = V(-5.5f, -7.5f, 2.8f);
        var leg = f.Cone(b, legA, 0.8f, legB, 1.1f, pajama, part);
        f.Cone(b, legA + V(0.1f, 0.05f, 0), 0.86f, legA + V(-0.35f, -0.45f, 0.18f), 0.86f, pajama, part);   // el ruedo del pantalón
        leg.Paint = Stripes(legA, legB, 6, stripe);
        return f;
    }

    private static int Grain(Vector3 p, int t)
    {
        float s = MathF.Sin(p.X * 9 + MathF.Sin(p.Y * 7) * 1.5f);
        return s > 0.75f && t > 1 ? t - 1 : t;
    }

    private static int Plaid(Vector3 p, int t) => t;
}
