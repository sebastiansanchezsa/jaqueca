using System.Numerics;
using Jaqueca.Figures.Model;
using Jaqueca.Figures.Rig;
using Jaqueca.Look;
using Jaqueca.Sprites;
using static Jaqueca.Figures.Model.Figure;

namespace Jaqueca.Figures.Content;

/// <summary>
/// Cuerpo base con la ropa de peregrino que llevan todos debajo del equipo: camisa de lino
/// gastada con las mangas arremangadas, capelina con capucha y el borde deshilachado, faja
/// del color del jugador con las puntas colgando, correa cruzada con morral, pantalón oscuro,
/// vendas en las canillas y zapatos de cuero. Proporciones realistas (~7 cabezas).
/// </summary>
public static class Body
{
    public const uint Linen = 0xA79B80, Capelet = 0x5B4838, Trousers = 0x3F3139, Wraps = 0x8E846E, Shoes = 0x4A3024,
        Leather = 0x4C3223, Buckle = 0xA08A55;

    /// <summary>El cráneo del humano en el hueso de la cabeza (igual en las dos complexiones).</summary>
    public static readonly Vector3 HeadC = new Dims().HeadC, HeadR = new Dims().HeadR;

    /// <summary>El cuerpo con la ropa de una clase (o la de peregrino).</summary>
    public static Figure Build(Build build, Outfit outfit) => outfit == Outfit.Pilgrim ? Build(build) : Outfits.Build(build, outfit);

    public static Figure Build(Build build)
    {
        var d = Dims.Of(build);
        float L = d.Limb;
        float thick = 0.9f + 0.1f * L;
        var f = new Figure();
        int skin = f.AddMat("piel", MatChannel.Skin, lift: 0.06f);
        int linen = f.AddMat("lino", MatChannel.Fixed, Linen, tex: LinenFolds);
        int cape = f.AddMat("capelina", MatChannel.Fixed, Capelet, tex: CapeFolds);
        int pants = f.AddMat("pantalón", MatChannel.Fixed, Trousers);
        int wraps = f.AddMat("vendas", MatChannel.Fixed, Wraps, tex: WrapBands);
        int shoes = f.AddMat("zapatos", MatChannel.Fixed, Shoes);
        int leather = f.AddMat("cuero", MatChannel.Fixed, Leather);
        int buckle = f.AddMat("hebilla", MatChannel.Fixed, Buckle, shiny: true);
        int sash = f.AddMat("faja", MatChannel.Accent, tex: SashFolds);

        // ---- cadera y faldón de la camisa (cuelgan de la pelvis: giran con las piernas)
        int hips = f.Part();
        f.Ellipsoid(Bone.Pelvis, V(0, -0.05f, 0), V(0.82f * L, 0.85f, d.HipW + 0.42f * L), pants, hips);
        int tail = f.Part();
        // El faldón es tela: se hamaca con el paso y el muslo lo levanta.
        var skirt = f.ConeCloth(Bone.Pelvis, V(0.02f, 0.5f, 0), d.WaistS * 0.9f, V(0.05f, -0.95f, 0), d.HipW + 0.8f * L, linen, tail, (d.WaistF + 0.08f) / d.WaistS, 1, 16, 2);
        skirt.LegR = 0.72f * L;
        skirt.Pad = 0.18f;
        // Ruedo desparejo, más largo atrás.
        skirt.Mask = p => p.Y < 0.55f && p.Y > -0.75f - 0.25f * Smooth(0.2f, -0.8f, p.X) + 0.1f * MathF.Sin(p.Z * 7 + p.X * 3);

        // Faja: varias vueltas a la cintura, el nudo al costado izquierdo y las puntas colgando (con inercia).
        int band = f.Part();
        var sashRing = f.Ellipsoid(Bone.Pelvis, V(0.02f, 0.62f, 0), V(d.WaistF + 0.24f, 0.6f, d.WaistS + 0.22f), sash, band);
        sashRing.Mask = p => p.Y > 0.3f + 0.06f * MathF.Sin(p.Z * 5) && p.Y < 0.95f;
        var knot = d.ScarfRoot;
        f.Ellipsoid(Bone.Pelvis, knot + V(0.05f, 0.05f, 0), V(0.26f, 0.24f, 0.24f) * thick, sash, band);
        foreach (var (bone, from, to, w) in new[] { (Bone.Scarf1, V(0, 0, 0), V(0, -d.ScarfSeg, 0), 0.24f), (Bone.Scarf2, V(0, 0, 0), V(0.02f, -0.95f, 0), 0.2f) })
        {
            var root = bone == Bone.Scarf1 ? knot : knot - V(0, d.ScarfSeg, 0);
            var strip = f.Box(bone, (from + to) / 2, V(0.07f, (from.Y - to.Y) / 2 + 0.05f, w), sash, band);
            strip.Origin = root;
            // La punta cortada en diagonal.
            if (bone == Bone.Scarf2) strip.Mask = p => p.Y - root.Y > -0.95f + 0.35f * ((p.Z - root.Z) / 0.2f * 0.5f + 0.5f);
        }

        // ---- torso: cintura, pecho, trapecios y hombros son una sola parte (la camisa)
        int torso = f.Part();
        f.Ellipsoid(Bone.Spine, V(0.02f, 0.25f, 0), V(d.WaistF, 1.05f, d.WaistS), linen, torso);
        f.Ellipsoid(Bone.Spine, V(0.02f, 1.95f, 0), V(d.ChestF, 1.4f, d.ChestS), linen, torso);
        f.Ellipsoid(Bone.Spine, V(0.35f, 2.35f, 0), V(0.72f * thick, 0.7f, d.ChestS * 0.8f), linen, torso);   // pecho
        foreach (int side in new[] { 1, -1 })
        {
            // Trapecio: del cuello al hombro, en pendiente.
            f.Cone(Bone.Spine, V(-0.1f, 3.35f, side * 0.35f), 0.45f * thick, V(-0.05f, 3.1f, side * (d.ShoulderW - 0.25f)), 0.42f * L, linen, torso);
            f.Ellipsoid(Bone.Spine, V(-0.02f, 3.02f, side * (d.ShoulderW - 0.12f)), V(0.5f, 0.46f, 0.5f) * L, linen, torso);
        }
        // Correa cruzada del hombro derecho a la cadera izquierda, con el morral atrás.
        int strapPart = f.Part();
        var strapN = Vector3.Normalize(V(0, 0.62f, -0.78f));
        var strap = f.Ellipsoid(Bone.Spine, V(0.02f, 1.85f, 0), V(d.ChestF + 0.07f, 1.75f, d.ChestS + 0.07f), leather, strapPart);
        strap.Mask = p => MathF.Abs(Vector3.Dot(p - V(0, 1.75f, 0), strapN)) < 0.13f && p.Y > 0.2f;
        strap.Paint = (p, m) => p.X > d.ChestF * 0.75f && p.Y > 1.35f && p.Y < 1.62f ? buckle : m;   // hebilla al frente
        int bag = f.Part();
        f.Ellipsoid(Bone.Pelvis, V(-0.6f, 0.05f, -(d.HipW + 0.75f * L)), V(0.5f, 0.45f, 0.26f), leather, bag);
        f.Ellipsoid(Bone.Pelvis, V(-0.55f, 0.32f, -(d.HipW + 0.85f * L)), V(0.46f, 0.2f, 0.22f), shoes, bag);   // tapa

        // ---- capelina: cubre hombros y pecho, deshilachada; la capucha caída en la nuca
        int capePart = f.Part();
        // Una cúpula sobre los hombros con el agujero del cuello, cortada abajo en el ruedo.
        var cap = f.Ellipsoid(Bone.Spine, V(-0.05f, 2.55f, 0), V(d.ChestF + 0.4f, 1.38f, d.ShoulderW + 0.64f * L), cape, capePart);
        cap.Mask = p => p.Y > 1.95f + 0.25f * Smooth(0.9f, -0.6f, p.X) + 0.16f * Tatter(p.X * 2.3f + p.Z * 4.1f)
                        && (p.X + 0.05f) * (p.X + 0.05f) + p.Z * p.Z > 0.5f * 0.5f * thick * thick;
        var collar = f.Cone(Bone.Spine, V(-0.05f, 3.55f, 0), 0.66f * thick, V(0, 3.85f, 0), 0.56f * thick, cape, capePart);
        collar.Mask = p => p.Y < 3.9f + 0.1f * p.X;
        // Capucha caída sobre la espalda.
        f.Ellipsoid(Bone.Spine, V(-0.95f, 3.45f, 0), V(0.52f, 0.55f, 0.75f) * thick, cape, capePart);

        // ---- cabeza y cuello
        int head = f.Part();
        f.Cone(Bone.Head, V(0.0f, -0.45f, 0), 0.46f * thick, V(0.12f, 1.0f, 0), 0.4f * thick, skin);
        f.Ellipsoid(Bone.Head, HeadC, HeadR, skin, head);                                   // cráneo
        f.Ellipsoid(Bone.Head, V(0.38f, 1.42f, 0), V(0.66f, 0.78f, 0.6f), skin, head);      // cara y mentón
        f.Ellipsoid(Bone.Head, V(0.05f, 1.25f, 0), V(0.56f, 0.5f, 0.66f), skin, head);      // mandíbula
        f.Ellipsoid(Bone.Head, V(0.8f, 2.02f, 0), V(0.28f, 0.14f, 0.6f), skin, head);       // arco de las cejas
        f.Ellipsoid(Bone.Head, V(1.02f, 1.62f, 0), V(0.2f, 0.3f, 0.15f), skin, head);       // nariz
        foreach (int side in new[] { 1, -1 })
            f.Ellipsoid(Bone.Head, V(-0.05f, 1.7f, side * 0.78f), V(0.17f, 0.3f, 0.1f), skin, head); // orejas

        foreach (int side in new[] { 1, -1 })
        {
            // ---- brazo: manga hasta el codo, arremangada; antebrazo con venda en la muñeca y mano
            int sleeve = f.Part();
            f.Cone(Skeleton.Arm(side, 0), V(0, -0.2f, 0), 0.45f * L, V(0, -2.35f, 0), 0.42f * L, linen, sleeve);
            f.Ellipsoid(Skeleton.Arm(side, 0), V(0, -0.6f, 0), V(0.52f, 0.72f, 0.54f) * L, linen, sleeve);
            f.Ellipsoid(Skeleton.Arm(side, 1), V(0, -0.12f, 0), V(0.45f, 0.3f, 0.45f) * L, linen, sleeve);    // lo arremangado
            int arm = f.Part();
            var fore = f.Cone(Skeleton.Arm(side, 1), V(0, 0, 0), 0.38f * L, V(0, -2.35f, 0), 0.26f * L, skin, arm);
            fore.Paint = (p, m) => p.Y < -1.75f ? wraps : m;
            f.Ellipsoid(Skeleton.Arm(side, 1), V(0.04f, -0.75f, 0), V(0.4f, 0.8f, 0.4f) * L, skin, arm);
            // Mano: palma, dedos recogidos y pulgar (el puño cierra alrededor de WeaponModels.Fist).
            int hand = f.Part();
            f.Ellipsoid(Skeleton.Arm(side, 2), V(0.02f, -0.42f, 0), V(0.34f, 0.46f, 0.2f) * thick, skin, hand);
            f.Ellipsoid(Skeleton.Arm(side, 2), V(0.1f, -0.85f, 0), V(0.3f, 0.3f, 0.2f) * thick, skin, hand);
            f.Cone(Skeleton.Arm(side, 2), V(0.28f, -0.25f, 0), 0.12f, V(0.4f, -0.7f, 0), 0.1f, skin, hand);

            // ---- pierna: muslo, rodilla, canilla vendada y zapato
            int thigh = f.Part();
            f.Cone(Skeleton.Leg(side, 0), V(0, 0.15f, 0), 0.7f * L, V(0, -3.55f, 0), 0.48f * L, pants, thigh);
            f.Ellipsoid(Skeleton.Leg(side, 0), V(0.12f, -1.35f, 0), V(0.66f, 1.55f, 0.64f) * L, pants, thigh);
            int shin = f.Part();
            f.Ellipsoid(Skeleton.Leg(side, 1), V(0.1f, 0.02f, 0), V(0.47f, 0.44f, 0.45f) * L, pants, shin);
            var calf = f.Cone(Skeleton.Leg(side, 1), V(0, -0.1f, 0), 0.45f * L, V(0, -3.55f, 0), 0.29f * L, pants, shin);
            calf.Paint = (p, m) => p.Y < -1.2f ? wraps : m;
            f.Ellipsoid(Skeleton.Leg(side, 1), V(-0.1f, -1.1f, 0), V(0.47f, 1.05f, 0.43f) * L, pants, shin).Paint = (p, m) => p.Y < -1.2f ? wraps : m;
            int foot = f.Part();
            f.Ellipsoid(Skeleton.Leg(side, 2), V(0.62f, -0.4f, 0), V(1.1f, 0.33f, 0.4f) * thick, shoes, foot);
            f.Ellipsoid(Skeleton.Leg(side, 2), V(-0.05f, -0.35f, 0), V(0.38f, 0.38f, 0.36f) * thick, shoes, foot);
            f.Cone(Skeleton.Leg(side, 2), V(0, 0.1f, 0), 0.33f * L, V(0.05f, -0.3f, 0), 0.36f * thick, shoes, foot);
        }
        return f;
    }

    private static float Smooth(float e0, float e1, float x)
    {
        float t = Math.Clamp((x - e0) / (e1 - e0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>Borde deshilachado: dientes irregulares entre -1 y 1.</summary>
    private static float Tatter(float x)
    {
        float s = MathF.Sin(x) * 0.6f + MathF.Sin(x * 2.7f + 1.3f) * 0.4f;
        return s > 0.35f ? 1 : s;
    }

    /// <summary>Pliegues del lino: se junta arrugado sobre la faja y en las axilas.</summary>
    private static int LinenFolds(Vector3 p, int t)
    {
        float s = MathF.Sin(p.Z * 9 + p.X * 4);
        return s > 0.8f && t > 1 && t < 4 ? t - 1 : t;
    }

    private static int CapeFolds(Vector3 p, int t)
    {
        float s = MathF.Sin(MathF.Atan2(p.Z, p.X) * 7);
        return s > 0.75f && t > 1 ? t - 1 : t;
    }

    private static int SashFolds(Vector3 p, int t)
    {
        float s = MathF.Sin(p.Y * 22 + p.Z * 3);
        return s > 0.6f && t > 1 ? t - 1 : t;
    }

    /// <summary>Vendas: bandas en diagonal alrededor de la canilla y la muñeca.</summary>
    private static int WrapBands(Vector3 p, int t)
    {
        float a = MathF.Atan2(p.Z, p.X);
        float s = MathF.Sin(p.Y * 9 + a * 1.2f);
        return s > 0.55f && t > 1 ? t - 1 : t;
    }
}
