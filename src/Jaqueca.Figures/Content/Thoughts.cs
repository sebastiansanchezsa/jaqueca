using System.Numerics;
using Jaqueca.Figures.Model;
using Jaqueca.Figures.Rig;
using Jaqueca.Look;
using Jaqueca.Sprites;
using static Jaqueca.Figures.Model.Figure;

namespace Jaqueca.Figures.Content;

/// <summary>
/// Los pensamientos intrusivos de Ernesto: la gente que le arruinó la vida, como la recuerda (más
/// grande, más fea, más ruidosa). Se arman con las mismas primitivas que los personajes de
/// Inquisition, a la medida de referencia (un hombre mide 16), sobre los huesos de siempre.
/// No hablan: gruñen, chistan, hacen ruido.
/// </summary>
public static class Thoughts
{
    // ------------------------------------------------------------------ el Vecino del Taladro

    public const uint NeighborSkin = 0xC98E6E, NeighborHair = 0x2E221C, Undershirt = 0xD9D2BC, Stain = 0xB8A46A,
        Bermuda = 0x55624E, Flipflop = 0x2C68A0, DrillBody = 0xD9A21E, DrillBlack = 0x26221F, Steel = 0xA9AEB3;

    /// <summary>
    /// El cuerpo del vecino: grandote, con panza (la musculosa no le llega a tapar el ombligo), los
    /// brazos peludos y quemados de sol, pelado arriba con la herradura de pelo, bigote tupido.
    /// </summary>
    public static readonly Dims NeighborDims = new()
    {
        HipW = 0.95f, ShoulderW = 1.9f, ChestF = 1.25f, ChestS = 1.6f, WaistF = 1.5f, WaistS = 1.6f, Limb = 1.2f,
    };

    public static Figure Neighbor()
    {
        var d = NeighborDims;
        float L = d.Limb;
        var f = new Figure();
        int skin = f.AddMat("piel quemada", MatChannel.Fixed, NeighborSkin, lift: 0.05f, tex: Hairy);
        int face = f.AddMat("cara", MatChannel.Fixed, NeighborSkin, lift: 0.08f);
        int hair = f.AddMat("pelo", MatChannel.Fixed, NeighborHair);
        int shirt = f.AddMat("musculosa", MatChannel.Fixed, Undershirt, tex: Ribbed);
        int stain = f.AddMat("mancha", MatChannel.Fixed, Stain);
        int pants = f.AddMat("bermuda", MatChannel.Fixed, Bermuda, tex: Plaid);
        int sole = f.AddMat("ojotas", MatChannel.Fixed, Flipflop, shiny: true);
        int dark = f.AddMat("sombra", MatChannel.Fixed, 0x1A1412);
        int eyeW = f.AddMat("ojo", MatChannel.Fixed, 0xE8E0D0, flat: 0.8f);

        // ---- cadera con la bermuda (le queda baja: asoma la panza)
        int hips = f.Part();
        f.Ellipsoid(Bone.Pelvis, V(0, 0, 0), V(0.95f * L, 0.95f, d.HipW + 0.5f * L), pants, hips);

        // ---- panza y pecho: una sola parte (la musculosa, con la panza que asoma abajo)
        int torso = f.Part();
        var belly = f.Ellipsoid(Bone.Spine, V(0.55f, 0.55f, 0), V(1.75f, 1.5f, 1.65f), shirt, torso);
        belly.Paint = (p, m) => p.Y < -0.2f + 0.08f * MathF.Sin(p.Z * 5) ? skin : Stains(p, m, stain);
        f.Ellipsoid(Bone.Spine, V(0.1f, 2.0f, 0), V(d.ChestF, 1.35f, d.ChestS), shirt, torso).Paint = (p, m) => Stains(p, m, stain);
        foreach (int side in new[] { 1, -1 })
        {
            // Los hombros al aire (la musculosa es de tiras): el trapecio y el hombro son piel.
            f.Cone(Bone.Spine, V(-0.1f, 3.3f, side * 0.4f), 0.55f, V(-0.05f, 3.05f, side * (d.ShoulderW - 0.3f)), 0.55f * L, skin, torso);
            f.Ellipsoid(Bone.Spine, V(-0.02f, 2.95f, side * (d.ShoulderW - 0.12f)), V(0.6f, 0.55f, 0.6f) * L, skin, torso);
            // La tira de la musculosa sobre el hombro.
            f.Box(Bone.Spine, V(0.05f, 3.2f, side * 0.95f), V(0.62f, 0.12f, 0.18f), shirt, torso);
        }
        // El ombligo.
        f.Ellipsoid(Bone.Spine, V(2.2f, -0.35f, 0), V(0.08f, 0.1f, 0.08f), dark, torso);

        // ---- cabeza: pelado, la herradura de pelo, el bigote, papada
        int head = f.Part();
        f.Cone(Bone.Head, V(0, -0.5f, 0), 0.62f, V(0.12f, 0.9f, 0), 0.52f, skin);                  // cogote grueso
        f.Ellipsoid(Bone.Head, d.HeadC + V(0, 0, 0), d.HeadR * 1.04f, face, head);
        f.Ellipsoid(Bone.Head, V(0.38f, 1.38f, 0), V(0.7f, 0.8f, 0.66f), face, head);             // cara
        f.Ellipsoid(Bone.Head, V(0.3f, 0.95f, 0), V(0.62f, 0.45f, 0.7f), face, head);             // papada
        f.Ellipsoid(Bone.Head, V(0.8f, 2.02f, 0), V(0.3f, 0.16f, 0.62f), face, head);             // ceño
        f.Ellipsoid(Bone.Head, V(1.02f, 1.6f, 0), V(0.26f, 0.34f, 0.2f), face, head);             // nariz
        foreach (int side in new[] { 1, -1 })
            f.Ellipsoid(Bone.Head, V(-0.05f, 1.7f, side * 0.84f), V(0.18f, 0.32f, 0.12f), face, head);
        // La herradura: pelo corto a los costados y atrás, nada arriba.
        var horse = f.Ellipsoid(Bone.Head, d.HeadC + V(-0.05f, -0.02f, 0), d.HeadR * 1.09f, hair, head);
        // Sólo atrás y sobre las orejas (adelante de las orejas, nada: si no parecen anteojos).
        horse.Mask = p => p.Y < 2.3f && p.Y > 1.3f - 0.3f * MathF.Max(0, -p.X) && (p.X < -0.15f || (p.X < 0.2f && p.Y > 1.75f && MathF.Abs(p.Z) > 0.75f));
        // El bigote: tupido, cae sobre la boca.
        f.Ellipsoid(Bone.Head, V(1.02f, 1.3f, 0), V(0.18f, 0.16f, 0.46f), hair, head);
        foreach (int side in new[] { 1, -1 })
            f.Ellipsoid(Bone.Head, V(0.98f, 1.2f, side * 0.34f), V(0.14f, 0.18f, 0.16f), hair, head);
        // Los ojos chiquitos y enojados bajo el ceño (con la ceja gruesa).
        foreach (int side in new[] { 1, -1 })
        {
            var at = Face.OnHead(1.82f, side * 0.34f, out var n);
            f.Stamps.Add(new Stamp
            {
                Bone = Bone.Head, At = at + V(0.06f, 0, 0), Normal = n, MinFacing = 0.3f, Outward = V(0, 0, side),
                Pixels = new[] { (0, 0, dark, 0), (0, -2, hair, 0), (1, -2, hair, 0) },
            });
        }
        f.StampParts.Add(head);

        foreach (int side in new[] { 1, -1 })
        {
            // ---- brazos gruesos y peludos, manos grandes
            int arm = f.Part();
            f.Cone(Skeleton.Arm(side, 0), V(0, -0.1f, 0), 0.62f * L, V(0, -2.4f, 0), 0.46f * L, skin, arm);
            f.Ellipsoid(Skeleton.Arm(side, 0), V(0, -0.8f, 0), V(0.62f, 0.9f, 0.62f) * L, skin, arm);
            f.Cone(Skeleton.Arm(side, 1), V(0, 0, 0), 0.46f * L, V(0, -2.3f, 0), 0.32f * L, skin, arm);
            f.Ellipsoid(Skeleton.Arm(side, 1), V(0.05f, -0.7f, 0), V(0.46f, 0.85f, 0.46f) * L, skin, arm);
            int hand = f.Part();
            f.Ellipsoid(Skeleton.Arm(side, 2), V(0.02f, -0.45f, 0), V(0.42f, 0.52f, 0.26f), skin, hand);
            f.Ellipsoid(Skeleton.Arm(side, 2), V(0.12f, -0.9f, 0), V(0.36f, 0.34f, 0.26f), skin, hand);
            f.Cone(Skeleton.Arm(side, 2), V(0.3f, -0.25f, 0), 0.15f, V(0.45f, -0.72f, 0), 0.13f, skin, hand);

            // ---- piernas: bermuda hasta la rodilla, canillas peludas, ojotas
            int thigh = f.Part();
            f.Cone(Skeleton.Leg(side, 0), V(0, 0.15f, 0), 0.82f * L, V(0, -3.0f, 0), 0.66f * L, pants, thigh);
            int shin = f.Part();
            f.Ellipsoid(Skeleton.Leg(side, 1), V(0.1f, 0.02f, 0), V(0.46f, 0.44f, 0.46f) * L, skin, shin);
            f.Cone(Skeleton.Leg(side, 1), V(0, -0.1f, 0), 0.46f * L, V(0, -3.55f, 0), 0.3f * L, skin, shin);
            f.Ellipsoid(Skeleton.Leg(side, 1), V(-0.12f, -1.1f, 0), V(0.5f, 1.05f, 0.46f) * L, skin, shin);
            int foot = f.Part();
            f.Ellipsoid(Skeleton.Leg(side, 2), V(0.55f, -0.36f, 0), V(1.0f, 0.3f, 0.42f), skin, foot);
            f.Ellipsoid(Skeleton.Leg(side, 2), V(-0.05f, -0.32f, 0), V(0.38f, 0.36f, 0.36f), skin, foot);
            // La ojota: la suela chata y la tira en V.
            f.Box(Skeleton.Leg(side, 2), V(0.5f, -0.68f, 0), V(1.25f, 0.08f, 0.52f), sole, foot);
            f.Box(Skeleton.Leg(side, 2), V(0.75f, -0.42f, 0), V(0.35f, 0.08f, 0.4f), sole, foot);
        }

        // ---- el taladro: amarillo y negro, con la mecha larga (en la mano derecha, como una pistola)
        Drill(f, dark);
        return f;
    }

    /// <summary>
    /// El taladro en la mano derecha. El mango pasa por el puño a lo largo del eje X de la mano (como el
    /// mango de una espada) y el cuerpo sale del lado del índice hacia adelante del brazo (−Y), con la
    /// mecha. El gatillo bajo el índice, la batería abajo.
    /// </summary>
    private static void Drill(Figure f, int dark)
    {
        int body = f.AddMat("taladro", MatChannel.Fixed, DrillBody, shiny: true);
        int black = f.AddMat("goma", MatChannel.Fixed, DrillBlack);
        int steel = f.AddMat("mecha", MatChannel.Fixed, Steel, shiny: true);
        int part = f.Part();
        var fist = WeaponModels.Fist;
        Shape W(Shape s) { s.Weapon = true; return s; }
        W(f.Box(Bone.HandR, fist + V(0, 0, 0), V(0.62f, 0.26f, 0.22f), black, part));                      // mango
        W(f.Box(Bone.HandR, fist + V(-0.85f, 0.05f, 0), V(0.28f, 0.5f, 0.38f), body, part));              // batería
        // El cuerpo del motor: del lado del índice, a lo largo del brazo.
        var motor = W(f.Cone(Bone.HandR, fist + V(0.85f, 0.75f, 0), 0.5f, fist + V(0.85f, -1.2f, 0), 0.42f, body, part));
        motor.Paint = (p, m) => MathF.Abs(p.Y - (fist.Y + 0.3f)) < 0.14f ? black : m;
        W(f.Cone(Bone.HandR, fist + V(0.85f, -1.2f, 0), 0.34f, fist + V(0.85f, -1.75f, 0), 0.22f, black, part));   // mandril
        W(f.Cone(Bone.HandR, fist + V(0.85f, -1.75f, 0), 0.08f, fist + V(0.85f, -3.9f, 0), 0.04f, steel, part));   // mecha
        W(f.Box(Bone.HandR, fist + V(0.55f, -0.2f, 0), V(0.14f, 0.2f, 0.1f), dark, part));                          // gatillo
    }

    /// <summary>La punta de la mecha en el hueso de la mano (de ahí sale la sangre y el ruido).</summary>
    public static Vector3 DrillTip => WeaponModels.Fist + V(0.85f, -3.9f, 0);

    private static int Stains(Vector3 p, int m, int stain)
    {
        // Manchas de grasa y de tuco: dos o tres manchones al frente.
        float s = MathF.Sin(p.Y * 3.1f + p.Z * 2.3f) * MathF.Sin(p.Z * 4.7f - p.Y * 1.3f);
        return p.X > 0.4f && s > 0.55f ? stain : m;
    }

    private static int Hairy(Vector3 p, int t)
    {
        float s = MathF.Sin(p.X * 23.1f + p.Y * 7.3f) * MathF.Sin(p.Y * 19.7f - p.Z * 13.9f) * MathF.Sin(p.Z * 21.3f + p.X * 5.1f);
        return s > 0.45f && t > 1 ? t - 1 : t;
    }

    private static int Ribbed(Vector3 p, int t)
    {
        // La musculosa acanalada: rayitas verticales.
        float a = MathF.Atan2(p.Z, p.X);
        return MathF.Sin(a * 26) > 0.7f && t > 1 && t < 4 ? t - 1 : t;
    }

    private static int Plaid(Vector3 p, int t)
    {
        bool a = MathF.Sin(p.Y * 6) > 0.8f, b = MathF.Sin(MathF.Atan2(p.Z, p.X) * 8) > 0.8f;
        return (a || b) && t > 1 ? t - 1 : t;
    }

    // ------------------------------------------------------------------ la Maestra

    public const uint TeacherSkin = 0xE0BCA0, TeacherHair = 0x6E5E54, Cardigan = 0xA8906A, Blouse = 0xE6E1D6, Skirt = 0x4E4642,
        Stockings = 0xB89880, Shoes = 0x241E1C, Frames = 0x2A2220, Chalk = 0xF2EEE4;

    public static readonly Dims TeacherDims = new()
    {
        HipW = 0.8f, ShoulderW = 1.5f, ChestF = 1.0f, ChestS = 1.25f, WaistF = 0.86f, WaistS = 1.12f, Limb = 1.08f,
    };

    /// <summary>
    /// La maestra de tercer grado: flaca y derecha, rodete tirante, anteojos, saquito de lana sobre la
    /// blusa con el broche, pollera tableada debajo de la rodilla, medias y zapatos bajos; la tiza en la mano.
    /// </summary>
    public static Figure Teacher()
    {
        var d = TeacherDims;
        float L = d.Limb;
        var f = new Figure();
        int skin = f.AddMat("piel", MatChannel.Fixed, TeacherSkin, lift: 0.06f);
        int hair = f.AddMat("pelo", MatChannel.Fixed, TeacherHair, tex: Combed);
        int wool = f.AddMat("saquito", MatChannel.Fixed, Cardigan, tex: Knit);
        int blouse = f.AddMat("blusa", MatChannel.Fixed, Blouse);
        int skirt = f.AddMat("pollera", MatChannel.Fixed, Skirt, tex: Pleats);
        int hose = f.AddMat("medias", MatChannel.Fixed, Stockings);
        int shoes = f.AddMat("zapatos", MatChannel.Fixed, Shoes, shiny: true);
        int frames = f.AddMat("anteojos", MatChannel.Fixed, Frames, shiny: true);
        int brooch = f.AddMat("broche", MatChannel.Fixed, 0xC8A050, shiny: true);
        int dark = f.AddMat("sombra", MatChannel.Fixed, 0x1C1616);
        int chalk = f.AddMat("tiza", MatChannel.Fixed, Chalk, flat: 0.4f);
        int lips = f.AddMat("labios", MatChannel.Fixed, 0x9A4A4A);

        // ---- la pollera tableada: un cono ancho de la cintura a debajo de la rodilla
        int hips = f.Part();
        f.Ellipsoid(Bone.Pelvis, V(0, 0.1f, 0), V(0.82f, 0.9f, d.HipW + 0.4f), skirt, hips);
        var sk = f.Cone(Bone.Pelvis, V(0.02f, 0.6f, 0), 1.05f, V(0.1f, -4.9f, 0), 2.0f, skirt, hips);
        sk.Mask = p => p.Y > -4.6f + 0.1f * MathF.Sin(MathF.Atan2(p.Z, p.X) * 14);

        // ---- torso: la blusa y el saquito abierto adelante, el broche
        int torso = f.Part();
        f.Ellipsoid(Bone.Spine, V(0.02f, 0.4f, 0), V(d.WaistF, 1.05f, d.WaistS), blouse, torso);
        var chest = f.Ellipsoid(Bone.Spine, V(0.05f, 1.95f, 0), V(d.ChestF, 1.4f, d.ChestS), blouse, torso);
        var coat = f.Ellipsoid(Bone.Spine, V(0.0f, 1.5f, 0), V(d.ChestF + 0.12f, 2.0f, d.ChestS + 0.12f), wool, torso);
        // El saquito abierto: al frente, una franja del medio sin saco (se ve la blusa).
        coat.Mask = p => !(p.X > 0.4f && MathF.Abs(p.Z) < 0.38f + 0.12f * (2.2f - p.Y)) && p.Y > -0.45f;
        f.Ellipsoid(Bone.Spine, V(0.98f, 2.6f, 0.1f), V(0.08f, 0.14f, 0.12f), brooch, torso);
        foreach (int side in new[] { 1, -1 })
        {
            f.Cone(Bone.Spine, V(-0.1f, 3.3f, side * 0.3f), 0.38f, V(-0.05f, 3.05f, side * (d.ShoulderW - 0.25f)), 0.38f * L, wool, torso);
            f.Ellipsoid(Bone.Spine, V(-0.02f, 2.98f, side * (d.ShoulderW - 0.12f)), V(0.46f, 0.44f, 0.46f) * L, wool, torso);
        }
        // El cuello de la blusa.
        f.Cone(Bone.Spine, V(0.05f, 3.4f, 0), 0.52f, V(0.1f, 3.75f, 0), 0.44f, blouse, torso);

        // ---- cabeza: la cara flaca y seria, anteojos, rodete
        int head = f.Part();
        f.Cone(Bone.Head, V(0, -0.45f, 0), 0.36f, V(0.12f, 1.0f, 0), 0.34f, skin);
        f.Ellipsoid(Bone.Head, d.HeadC, d.HeadR * V(0.96f, 1.02f, 0.94f), skin, head);
        f.Ellipsoid(Bone.Head, V(0.36f, 1.4f, 0), V(0.62f, 0.78f, 0.54f), skin, head);
        f.Ellipsoid(Bone.Head, V(0.8f, 2.02f, 0), V(0.24f, 0.12f, 0.56f), skin, head);
        f.Cone(Bone.Head, V(0.9f, 1.95f, 0), 0.12f, V(1.1f, 1.5f, 0), 0.14f, skin, head);            // nariz afilada
        foreach (int side in new[] { 1, -1 })
            f.Ellipsoid(Bone.Head, V(-0.05f, 1.72f, side * 0.76f), V(0.15f, 0.28f, 0.09f), skin, head);
        // El pelo tirante hacia atrás y el rodete en la nuca.
        var cap = f.Ellipsoid(Bone.Head, d.HeadC + V(-0.06f, 0.06f, 0), d.HeadR * 1.07f, hair, head);
        cap.Mask = p => p.X < 0.55f - 0.2f * MathF.Max(0, 1.6f - p.Y) && p.Y > 1.25f;
        f.Ellipsoid(Bone.Head, V(-0.95f, 2.45f, 0), V(0.5f, 0.5f, 0.52f), hair, head);
        f.Ellipsoid(Bone.Head, V(-1.12f, 2.55f, 0), V(0.28f, 0.3f, 0.3f), hair, head);
        // Los anteojos: dos aros (cajitas finas) y el puente.
        int glasses = f.Part();
        foreach (int side in new[] { 1, -1 })
        {
            var at = Face.OnHead(1.8f, side * 0.33f, out _);
            f.Box(Bone.Head, at + V(0.12f, 0, 0), V(0.04f, 0.2f, 0.25f), frames, glasses).Mask = p => MathF.Abs(p.Y - 1.8f) > 0.12f || MathF.Abs(MathF.Abs(p.Z) - 0.33f) > 0.17f;
            f.Box(Bone.Head, V(0.5f, 1.85f, side * 0.62f), V(0.4f, 0.03f, 0.03f), frames, glasses);
        }
        f.Box(Bone.Head, V(1.0f, 1.86f, 0), V(0.04f, 0.03f, 0.1f), frames, glasses);
        foreach (int side in new[] { 1, -1 })
        {
            var at = Face.OnHead(1.8f, side * 0.33f, out var n);
            f.Stamps.Add(new Stamp
            {
                Bone = Bone.Head, At = at + V(0.02f, 0, 0), Normal = n, MinFacing = 0.3f, Outward = V(0, 0, side),
                Pixels = new[] { (0, 0, dark, 0), (0, -1, hair, 0), (1, -1, hair, 0) },
            });
        }
        var mouth = Face.OnHead(1.25f, 0, out var mn);
        f.Stamps.Add(new Stamp { Bone = Bone.Head, At = mouth + V(0.05f, 0, 0), Normal = mn, MinFacing = 0.35f, Pixels = new[] { (-1, 0, lips, 1), (0, 0, lips, 1), (1, 0, lips, 1) } });
        f.StampParts.Add(head);

        foreach (int side in new[] { 1, -1 })
        {
            int arm = f.Part();
            f.Cone(Skeleton.Arm(side, 0), V(0, -0.1f, 0), 0.44f * L, V(0, -2.4f, 0), 0.36f * L, wool, arm);
            f.Cone(Skeleton.Arm(side, 1), V(0, 0, 0), 0.36f * L, V(0, -2.1f, 0), 0.3f * L, wool, arm);
            f.Ellipsoid(Skeleton.Arm(side, 1), V(0, -2.1f, 0), V(0.34f, 0.18f, 0.34f) * L, wool, arm);     // el puño del saco
            int hand = f.Part();
            f.Ellipsoid(Skeleton.Arm(side, 2), V(0.02f, -0.42f, 0), V(0.3f, 0.44f, 0.17f), skin, hand);
            f.Ellipsoid(Skeleton.Arm(side, 2), V(0.08f, -0.84f, 0), V(0.26f, 0.28f, 0.17f), skin, hand);

            int shin = f.Part();
            f.Cone(Skeleton.Leg(side, 0), V(0, 0.15f, 0), 0.62f * L, V(0, -3.5f, 0), 0.44f * L, hose, shin);
            f.Cone(Skeleton.Leg(side, 1), V(0, -0.1f, 0), 0.4f * L, V(0, -3.55f, 0), 0.24f * L, hose, shin);
            f.Ellipsoid(Skeleton.Leg(side, 1), V(-0.08f, -1.1f, 0), V(0.4f, 0.95f, 0.38f) * L, hose, shin);
            int foot = f.Part();
            f.Ellipsoid(Skeleton.Leg(side, 2), V(0.55f, -0.38f, 0), V(0.95f, 0.28f, 0.34f), shoes, foot);
            f.Ellipsoid(Skeleton.Leg(side, 2), V(-0.08f, -0.35f, 0), V(0.32f, 0.32f, 0.3f), shoes, foot);
            f.Box(Skeleton.Leg(side, 2), V(-0.25f, -0.62f, 0), V(0.18f, 0.12f, 0.2f), shoes, foot);       // el taco bajo
        }

        // ---- la tiza en la mano derecha (la tira: se ve en el puño antes de salir volando)
        int ch = f.Part();
        var tz = f.Cone(Bone.HandR, WeaponModels.Fist + V(0.4f, 0, 0), 0.12f, WeaponModels.Fist + V(1.1f, 0, 0), 0.11f, chalk, ch);
        tz.Weapon = true;
        return f;
    }

    private static int Knit(Vector3 p, int t)
    {
        // El tejido: puntos en V que se repiten.
        float s = MathF.Sin(p.Y * 16) * MathF.Sin(MathF.Atan2(p.Z, p.X) * 20 + MathF.Abs(MathF.Sin(p.Y * 8)) * 2);
        return s > 0.6f && t > 1 ? t - 1 : t;
    }

    private static int Pleats(Vector3 p, int t)
    {
        // Las tablas: franjas verticales alrededor (una clara y una en sombra).
        float a = MathF.Atan2(p.Z, p.X);
        float s = MathF.Sin(a * 12);
        return s > 0.35f ? Math.Min(4, t + (t < 4 ? 0 : 0)) : (t > 1 ? t - 1 : t);
    }

    private static int Combed(Vector3 p, int t)
    {
        float s = MathF.Sin(p.Z * 14 + p.Y * 2);
        return s > 0.7f && t > 1 ? t - 1 : t;
    }
}
