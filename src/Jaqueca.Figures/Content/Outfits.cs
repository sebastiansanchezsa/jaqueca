using System.Numerics;
using Jaqueca.Figures.Model;
using Jaqueca.Figures.Rig;
using Jaqueca.Look;
using Jaqueca.Sprites;
using static Jaqueca.Figures.Model.Figure;

namespace Jaqueca.Figures.Content;

/// <summary>
/// La ropa de cada clase (el cuerpo de peregrino está en <see cref="Body"/>): la misma anatomía
/// (cabeza, manos, piernas) con otra ropa encima. Todas llevan la faja del color del jugador, con
/// las puntas colgando: en el cooperativo, así se sabe quién es quién.
/// <list type="bullet">
/// <item><b>Soldado</b>: gambesón acolchado hasta medio muslo, hombrera de acero en el hombro
/// izquierdo, brazales, guantes y botas altas; la vaina de la espada al costado.</item>
/// <item><b>Cazador</b>: capucha y esclavina verde oscuro, jubón de cuero sobre la camisa, carcaj a
/// la espalda (las flechas asoman por el hombro izquierdo, el de la mano que tira), brazal en el
/// brazo del arco, mitones, vendas y botas bajas.</item>
/// <item><b>Blasfemo</b>: el torso desnudo bajo un sambenito roto (el amarillo con el aspa roja y las
/// llamas para arriba de los que iban a la hoguera: lo guardó), la argolla de hierro del cuello,
/// cuerda a la cintura, vendas de cáñamo en los antebrazos y descalzo.</item>
/// <item><b>Curandera</b>: blusa arremangada, corpiño, falda larga, delantal, chal y pañuelo en la
/// cabeza (el pelo asoma atrás); saquitos, un manojo de hierbas y un frasco de barro en la cintura.</item>
/// <item><b>Pícaro</b>: todo oscuro: capucha, máscara que tapa nariz y boca, jubón de cuero con una
/// bandolera de cuchillos arrojadizos, brazales, mitones y botas blandas.</item>
/// <item><b>Ocultista</b>: túnica larga negra violácea con capucha honda y mangas de campana, la
/// estola de hueso con sigilos rojos, un collar de huesitos y el grimorio encadenado a la cadera.</item>
/// </list>
/// Las faldas largas (curandera, ocultista) no son un cono en la cadera, que las piernas
/// atravesarían al caminar: arriba cuelgan de la cadera y abajo cada pierna lleva su paño.
/// </summary>
public static partial class Outfits
{
    /// <summary>Cuánto pelo tapa la ropa: las capuchas, todo; el pañuelo, lo de arriba.</summary>
    public static HairHide Hides(Outfit o) => o switch
    {
        Outfit.Hunter or Outfit.Rogue or Outfit.Occultist => HairHide.All,
        Outfit.Healer => HairHide.Top,
        _ => HairHide.None,
    };

    public static Figure Build(Build build, Outfit outfit) => outfit switch
    {
        Outfit.Soldier => Soldier(new Tailor(build)),
        Outfit.Hunter => Hunter(new Tailor(build)),
        Outfit.Blasphemer => Blasphemer(new Tailor(build)),
        Outfit.Healer => Healer(new Tailor(build)),
        Outfit.Rogue => Rogue(new Tailor(build)),
        Outfit.Occultist => Occultist(new Tailor(build)),
        _ => Body.Build(build),
    };

    // ------------------------------------------------------------------ soldado

    private static Figure Soldier(Tailor t)
    {
        var d = t.D;
        int gamb = t.Mat("gambesón", 0x7A5E40, Quilt);
        int leather = t.Mat("cuero", 0x4A3022);
        int steel = t.Mat("acero", 0x8C969E, shiny: true);
        int pants = t.Mat("calzas", 0x3A3036);
        int boots = t.Mat("botas", 0x3A2820);
        int buckle = t.Mat("hebilla", 0xA08A55, shiny: true);
        int sash = t.Accent("faja", SashFolds);

        t.Hips(pants);
        int body = t.Torso(gamb, 0.14f);
        // El faldón del gambesón hasta medio muslo, parejo (lo acolchado no se deshilacha).
        var skirt = t.Skirt(gamb, 0.55f, -2.1f, d.WaistS + 0.12f, d.HipW + 1.05f * t.L, body);
        skirt.Mask = p => p.Y > -2.0f - 0.15f * Smooth(0.2f, -0.8f, p.X);
        // Cuello alto acolchado.
        t.F.Cone(Bone.Spine, V(-0.05f, 3.35f, 0), 0.7f * t.Thick, V(0, 3.9f, 0), 0.6f * t.Thick, gamb, body);
        t.Belt(leather, buckle, 0.55f, 0.2f);
        t.Sash(sash, 0.85f, 0.26f);
        // La vaina, colgada del cinto a la izquierda, inclinada hacia atrás.
        int scab = t.F.Part();
        t.F.Cone(Bone.Pelvis, V(0.25f, 0.45f, -(d.HipW + 0.95f * t.L)), 0.2f, V(-1.7f, -3.1f, -(d.HipW + 1.15f * t.L)), 0.15f, leather, scab);
        t.F.Ellipsoid(Bone.Pelvis, V(-1.72f, -3.15f, -(d.HipW + 1.15f * t.L)), V(0.18f, 0.18f, 0.18f), steel, scab);

        foreach (int side in new[] { 1, -1 })
        {
            t.UpperArm(side, gamb, 0.12f);
            t.Forearm(side, gamb, 0.08f);
            // Brazal de acero del codo a la muñeca, y guante.
            int bracer = t.F.Part();
            t.F.Cone(Skeleton.Arm(side, 1), V(0.02f, -0.55f, 0), 0.47f * t.L, V(0, -2.05f, 0), 0.36f * t.L, steel, bracer);
            t.Hand(side, leather);
            t.Thigh(side, pants);
            t.Shin(side, boots, 0.06f);
            // La caña de la bota, abierta arriba bajo la rodilla.
            t.F.Ellipsoid(Skeleton.Leg(side, 1), V(0.05f, -0.45f, 0), V(0.58f, 0.28f, 0.56f) * t.L, boots);
            t.Foot(side, boots);
        }
        // Hombrera del lado izquierdo: dos láminas de acero sobre el hombro, que se mueven con el brazo.
        int pauldron = t.F.Part();
        var top = t.F.Ellipsoid(Skeleton.Arm(-1, 0), V(0, -0.3f, 0), V(0.78f, 0.66f, 0.8f) * t.L, steel, pauldron);
        top.Mask = p => p.Y > -0.75f;
        var lame = t.F.Ellipsoid(Skeleton.Arm(-1, 0), V(0, -0.95f, 0), V(0.66f, 0.34f, 0.7f) * t.L, steel, pauldron);
        lame.Mask = p => p.Y > -1.15f;
        // La correa que la sujeta, cruzando el pecho hacia la axila derecha.
        var strapN = Vector3.Normalize(V(0, 0.75f, 0.66f));
        var strap = t.F.Ellipsoid(Bone.Spine, V(0.02f, 2.1f, 0), V(d.ChestF + 0.22f, 1.5f, d.ChestS + 0.22f), leather);
        strap.Mask = p => MathF.Abs(Vector3.Dot(p - V(0, 2.45f, 0), strapN)) < 0.1f && p.Y > 1.4f;
        t.Head();
        return t.F;
    }

    // ------------------------------------------------------------------ cazador

    private static Figure Hunter(Tailor t)
    {
        var d = t.D;
        int shirt = t.Mat("camisa", 0x8A7E66, LinenFolds);
        int jerkin = t.Mat("jubón", 0x55522F, Seams);
        int hood = t.Mat("capucha", 0x2F3A2A, CapeFolds);
        int leather = t.Mat("cuero", 0x5A3A24);
        int pants = t.Mat("calzas", 0x3D3A30);
        int wraps = t.Mat("vendas", 0x7E7460, WrapBands);
        int boots = t.Mat("botas", 0x4A3326);
        int fletch = t.Mat("plumas", 0xC9C0A8, flat: 0.2f);
        int red = t.Mat("plumas rojas", 0x8E3A2A, flat: 0.2f);
        int buckle = t.Mat("hebilla", 0x9A8A60, shiny: true);
        int sash = t.Accent("faja", SashFolds);

        t.Hips(pants);
        t.Torso(shirt);
        // Jubón de cuero sin mangas sobre la camisa, con un faldón corto.
        int vest = t.Torso(jerkin, 0.1f, arms: false);
        var skirt = t.Skirt(jerkin, 0.55f, -1.25f, d.WaistS + 0.1f, d.HipW + 0.85f * t.L, vest);
        skirt.Mask = p => p.Y > -1.2f - 0.1f * Smooth(0.2f, -0.8f, p.X);
        t.Sash(sash, 0.62f, 0.2f);
        // Correa del carcaj: del hombro izquierdo a la cadera derecha.
        t.Strap(leather, buckle, Vector3.Normalize(V(0, 0.62f, 0.78f)), 0.12f);
        // El carcaj a la espalda: de la cadera derecha al hombro izquierdo; las flechas asoman arriba.
        int quiver = t.F.Part();
        int quiverLeather = t.Mat("carcaj", 0x7E4E2C, Seams);
        var qa = V(-(d.ChestF + 0.6f), 0.45f, 0.62f);
        var qb = V(-(d.ChestF + 0.5f), 3.35f, -0.72f);
        t.F.Cone(Bone.Spine, qa, 0.4f, qb, 0.47f, quiverLeather, quiver);
        t.F.Ellipsoid(Bone.Spine, qa, V(0.42f, 0.22f, 0.42f), boots, quiver);
        var up = Vector3.Normalize(qb - qa);
        for (int i = 0; i < 5; i++)
        {
            var off = V(0.1f * MathF.Sin(i * 2.1f), 0, 0.22f * (i - 2) * 0.5f);
            var a = qb + off;
            var b = a + up * (1.3f + 0.15f * (i % 2));
            t.F.Cone(Bone.Spine, a, 0.06f, b, 0.06f, boots, quiver).Thin = true;
            t.F.Cone(Bone.Spine, b - up * 0.5f, 0.17f, b + up * 0.05f, 0.06f, i == 2 ? red : fletch, quiver).Thin = true;
        }

        foreach (int side in new[] { 1, -1 })
        {
            t.UpperArm(side, shirt);
            t.Cuff(side, shirt);
            t.Forearm(side, t.Skin);
            // Brazal de cuero en el brazo del arco (el derecho); vendas en la muñeca del otro.
            if (side > 0) t.F.Cone(Skeleton.Arm(side, 1), V(0.02f, -0.5f, 0), 0.44f * t.L, V(0, -2.1f, 0), 0.33f * t.L, leather);
            else t.F.Cone(Skeleton.Arm(side, 1), V(0, -1.7f, 0), 0.3f * t.L, V(0, -2.2f, 0), 0.28f * t.L, wraps);
            t.Hand(side, t.Skin, palm: leather);
            t.Thigh(side, pants);
            t.Shin(side, pants, paint: p => p.Y < -1.0f ? wraps : pants);
            t.Foot(side, boots, ankle: true);
        }
        t.Head();
        t.HoodUp(hood, cowl: true, ragged: 0.16f);
        return t.F;
    }

    // ------------------------------------------------------------------ blasfemo

    private static Figure Blasphemer(Tailor t)
    {
        var d = t.D;
        int yellow = t.Mat("sambenito", 0xAE8F3E, Weave);
        int red = t.Mat("pintura roja", 0x8A2A1E);
        int pants = t.Mat("calzas remendadas", 0x4A3A30, Patches);
        int rope = t.Mat("cuerda", 0x8A7650, Twist);
        int hemp = t.Mat("vendas de cáñamo", 0x8E846E, WrapBands);
        int iron = t.Mat("hierro", 0x5A5E62, shiny: true);
        int sash = t.Accent("trapo", SashFolds);

        t.Hips(pants);
        t.Torso(t.Skin, 0, muscles: true);
        // El sambenito: un paño adelante y otro atrás, abierto a los costados (los brazos y los
        // flancos al aire), roto abajo. Adelante el aspa; abajo, las llamas que suben.
        int cloth = t.F.Part();
        float halfW = d.ChestS * 0.62f;
        Func<Vector3, int, int> paintTop = (p, m) =>
        {
            if (p.X > 0)
            {
                float y = p.Y - 1.95f;
                bool cross = MathF.Abs(p.Z) < 1.0f && MathF.Abs(y) < 0.9f && (MathF.Abs(y - 0.9f * p.Z) < 0.26f || MathF.Abs(y + 0.9f * p.Z) < 0.26f);
                if (cross) return red;
            }
            return m;
        };
        // Un escapulario: pasa por arriba de los hombros (con el agujero de la cabeza) y cae adelante y atrás.
        var panel = t.F.Ellipsoid(Bone.Spine, V(0.02f, 1.55f, 0), V(d.ChestF + 0.18f, 2.35f, d.ChestS + 0.16f), yellow, cloth);
        panel.Mask = p => MathF.Abs(p.Z) < halfW && (p.X - 0.02f) * (p.X - 0.02f) + p.Z * p.Z > 0.58f * 0.58f * t.Thick * t.Thick;
        panel.Paint = paintTop;
        var lower = t.F.ConeCloth(Bone.Pelvis, V(0.02f, 0.7f, 0), d.WaistS + 0.14f, V(0.05f, -2.5f, 0), d.HipW + 0.95f * t.L, yellow, cloth, (d.WaistF + 0.16f) / d.WaistS, 1, 18, 3);
        lower.LegR = 0.72f * t.L;
        lower.Pad = 0.22f;
        lower.Mask = p => MathF.Abs(p.X) > 0.25f && MathF.Abs(p.Z) < halfW + 0.15f && p.Y > -2.35f + 0.3f * Tatter(p.Z * 3.1f + p.X);
        lower.Paint = (p, m) => p.Y < -1.1f + 0.55f * MathF.Abs(MathF.Sin(p.Z * 4.2f + (p.X > 0 ? 0.4f : 1.9f))) ? red : m;
        // Cuerda a la cintura (sobre el sambenito) y el trapo del color del jugador anudado debajo.
        t.Sash(sash, 0.6f, 0.2f);
        int ropePart = t.F.Part();
        var ring = t.F.Ellipsoid(Bone.Pelvis, V(0.02f, 0.85f, 0), V(d.WaistF + 0.3f, 0.16f, d.WaistS + 0.3f), rope, ropePart);
        ring.Mask = p => p.Y > 0.72f;
        t.F.Cone(Bone.Pelvis, V(0.9f, 0.8f, 0.45f), 0.1f, V(1.05f, -0.6f, 0.55f), 0.08f, rope, ropePart);
        // La argolla de hierro del cuello, con un eslabón roto colgando.
        int collar = t.F.Part();
        var neck = t.F.Ellipsoid(Bone.Spine, V(0.02f, 3.62f, 0), V(0.66f * t.Thick, 0.15f, 0.66f * t.Thick), iron, collar);
        neck.Mask = p => (p.X - 0.02f) * (p.X - 0.02f) + p.Z * p.Z > 0.3f * 0.3f;
        t.F.Ellipsoid(Bone.Spine, V(0.72f * t.Thick, 3.3f, 0), V(0.1f, 0.2f, 0.06f), iron, collar);

        foreach (int side in new[] { 1, -1 })
        {
            t.UpperArm(side, t.Skin, 0.04f);
            t.Forearm(side, t.Skin, 0.05f);
            // Vendas de cáñamo de los nudillos a medio antebrazo.
            t.F.Cone(Skeleton.Arm(side, 1), V(0.02f, -1.0f, 0), 0.4f * t.L, V(0, -2.35f, 0), 0.3f * t.L, hemp);
            t.Hand(side, hemp, fingers: t.Skin);
            t.Thigh(side, pants);
            t.Shin(side, pants, paint: p => p.Y < -2.4f ? t.Skin : pants);
            t.Foot(side, t.Skin, bare: true);
        }
        t.Head();
        return t.F;
    }

    // ------------------------------------------------------------------ curandera

    private static Figure Healer(Tailor t)
    {
        var d = t.D;
        int blouse = t.Mat("blusa", 0xB0A488, LinenFolds);
        int bodice = t.Mat("corpiño", 0x4A3326, Seams);
        int skirt = t.Mat("falda", 0x3E4A32, Folds);
        int apron = t.Mat("delantal", 0x958A70, LinenFolds);
        int shawl = t.Mat("chal", 0x5E2F35, CapeFolds);
        int leather = t.Mat("cuero", 0x5A3A26);
        int herb = t.Mat("hierbas", 0x5E7A3A);
        int flower = t.Mat("lavanda", 0x7A6A9A);
        int clay = t.Mat("barro", 0x9A5A3A);
        int shoes = t.Mat("zapatos", 0x4A3024);
        int sash = t.Accent("faja", SashFolds);

        t.Hips(skirt);
        int torso = t.Torso(blouse);
        // Corpiño ajustado de la cintura al pecho.
        var bod = t.F.Ellipsoid(Bone.Spine, V(0.02f, 0.75f, 0), V(d.WaistF + 0.08f, 1.45f, d.WaistS + 0.08f), bodice);
        bod.Mask = p => p.Y < 1.9f - 0.3f * MathF.Max(0, p.X);
        t.Robe(skirt, 2.3f, 0.35f);
        // Delantal corto adelante, con un bolsillo.
        int apr = t.F.Part();
        var ap = t.F.ConeCloth(Bone.Pelvis, V(0.1f, 0.6f, 0), d.WaistS + 0.2f, V(0.4f, -2.8f, 0), d.HipW + 1.15f * t.L, apron, apr, (d.WaistF + 0.2f) / d.WaistS, 1, 20, 3);
        ap.LegR = 0.72f * t.L;
        ap.Pad = 0.34f;
        ap.Mask = p => p.X > 0.35f && MathF.Abs(p.Z) < 0.95f && p.Y > -2.7f + 0.12f * MathF.Sin(p.Z * 6);
        ap.Paint = (p, m) => p.Y < -0.9f && p.Y > -1.6f && p.Z > 0.05f && p.Z < 0.6f ? leather : m;
        t.Sash(sash, 0.7f, 0.24f);
        // Saquitos, el manojo de hierbas y el frasco de barro, colgados a los costados.
        int bits = t.F.Part();
        t.F.Ellipsoid(Bone.Pelvis, V(-0.2f, -0.35f, d.HipW + 0.95f * t.L), V(0.32f, 0.42f, 0.24f), leather, bits);
        t.F.Ellipsoid(Bone.Pelvis, V(0.35f, -0.2f, -(d.HipW + 0.95f * t.L)), V(0.26f, 0.36f, 0.24f), clay, bits);
        t.F.Cone(Bone.Pelvis, V(0.35f, 0.15f, -(d.HipW + 0.95f * t.L)), 0.1f, V(0.35f, 0.32f, -(d.HipW + 0.95f * t.L)), 0.08f, leather, bits);
        for (int i = 0; i < 4; i++)
        {
            var at = V(-0.75f + 0.12f * i, 0.4f, -(d.HipW + 0.85f * t.L) - 0.08f * i);
            t.F.Cone(Bone.Pelvis, at, 0.07f, at + V(0.1f * (i - 1.5f), -1.3f, -0.05f), 0.13f, i % 2 == 0 ? herb : flower, bits).Thin = true;
        }
        // El chal sobre los hombros, en punta atrás hasta la cintura.
        int sh = t.F.Part();
        var cap = t.F.Ellipsoid(Bone.Spine, V(-0.05f, 2.6f, 0), V(d.ChestF + 0.36f, 1.25f, d.ShoulderW + 0.58f * t.L), shawl, sh);
        cap.Mask = p => p.Y > 2.15f + 0.35f * Smooth(0.9f, -0.2f, p.X) && (p.X + 0.05f) * (p.X + 0.05f) + p.Z * p.Z > 0.5f * 0.5f * t.Thick * t.Thick;
        var tail = t.F.Cone(Bone.Spine, V(-(d.ChestF + 0.2f), 2.8f, 0), 1.1f, V(-(d.ChestF + 0.05f), 0.5f, 0), 0.12f, shawl, sh);
        tail.Local = Matrix4x4.CreateScale(0.35f, 1, 1);

        foreach (int side in new[] { 1, -1 })
        {
            t.UpperArm(side, blouse, 0.1f);
            t.Cuff(side, blouse, 0.08f);
            t.Forearm(side, t.Skin);
            t.Hand(side, t.Skin);
            t.Thigh(side, skirt);
            t.Shin(side, t.Skin, 0, paint: p => p.Y < -3.0f ? shoes : t.Skin);
            t.Foot(side, shoes, ankle: true);
        }
        t.Head();
        t.Kerchief(shawl);
        return t.F;
    }

    // ------------------------------------------------------------------ pícaro

    private static Figure Rogue(Tailor t)
    {
        var d = t.D;
        int shirt = t.Mat("camisa oscura", 0x5E554A, LinenFolds);
        int jerkin = t.Mat("jubón", 0x3A2E2A, Seams);
        int cloth = t.Mat("paño", 0x2E2C33, CapeFolds);
        int leather = t.Mat("correas", 0x4A3226);
        int pants = t.Mat("calzas", 0x33292E);
        int boots = t.Mat("botas blandas", 0x2E221C, WrapBands);
        int steel = t.Mat("acero", 0x9AA3AA, shiny: true);
        int sash = t.Accent("faja", SashFolds);

        t.Hips(pants);
        t.Torso(shirt);
        int vest = t.Torso(jerkin, 0.08f, arms: false);
        var skirt = t.Skirt(jerkin, 0.55f, -1.4f, d.WaistS + 0.08f, d.HipW + 0.8f * t.L, vest);
        skirt.Mask = p => p.Y > -1.35f + 0.25f * Smooth(-0.2f, 0.8f, p.X) + 0.12f * Tatter(p.Z * 5 + p.X * 2);
        t.Sash(sash, 0.6f, 0.16f);
        // La bandolera del hombro derecho a la cadera izquierda, con cuatro cuchillos arrojadizos.
        var n = Vector3.Normalize(V(0, 0.62f, -0.78f));
        t.Strap(leather, leather, n, 0.14f);
        int knives = t.F.Part();
        for (int i = 0; i < 4; i++)
        {
            // Sobre la banda, en el pecho: cada cuchillo apunta hacia el hombro.
            float y = 1.5f + 0.42f * i;
            float z = -(y - 1.75f) * 0.62f / 0.78f;
            var at = V(d.ChestF + 0.22f + 0.12f * MathF.Sin(i), y, z);
            t.F.Cone(Bone.Spine, at - V(0, 0.28f, -0.2f), 0.07f, at + V(0.02f, 0.3f, 0.23f), 0.05f, steel, knives).Thin = true;
            t.F.Cone(Bone.Spine, at - V(0, 0.28f, -0.2f), 0.09f, at - V(0, 0.55f, -0.38f), 0.09f, leather, knives).Thin = true;
        }

        foreach (int side in new[] { 1, -1 })
        {
            t.UpperArm(side, shirt);
            t.Forearm(side, shirt, 0.02f);
            t.F.Cone(Skeleton.Arm(side, 1), V(0.02f, -0.6f, 0), 0.43f * t.L, V(0, -2.1f, 0), 0.32f * t.L, leather);
            t.Hand(side, t.Skin, palm: leather);
            t.Thigh(side, pants, -0.04f);
            t.Shin(side, pants, paint: p => p.Y < -1.2f ? boots : pants);
            t.F.Ellipsoid(Skeleton.Leg(side, 1), V(0.05f, -1.2f, 0), V(0.52f, 0.2f, 0.5f) * t.L, boots);
            t.Foot(side, boots, ankle: true);
        }
        t.Head();
        // La máscara: tapa nariz, boca y mentón, anudada atrás.
        int mask = t.F.Part();
        var m1 = t.F.Ellipsoid(Bone.Head, V(0.42f, 1.35f, 0), V(0.72f, 0.62f, 0.72f), cloth, mask);
        m1.Mask = p => p.Y < 1.72f - 0.1f * MathF.Abs(p.Z);
        t.HoodUp(cloth, cowl: true, ragged: 0.22f);
        return t.F;
    }

    // ------------------------------------------------------------------ ocultista

    private static Figure Occultist(Tailor t)
    {
        var d = t.D;
        int robe = t.Mat("túnica", 0x2A2330, Folds);
        int stole = t.Mat("estola", 0xC8BFAE);
        int sigil = t.Mat("sigilos", 0x6A1E24);
        int bone = t.Mat("hueso", 0xC8BFAE);
        int book = t.Mat("grimorio", 0x4A2E2A);
        int brass = t.Mat("latón", 0x9A8050, shiny: true);
        int iron = t.Mat("cadena", 0x5A5E62, shiny: true);
        int shoes = t.Mat("zapatos", 0x221C22);
        int sash = t.Accent("cordón", SashFolds);

        t.Hips(robe);
        int body = t.Torso(robe, 0.1f);
        t.Robe(robe, 3.35f, 0.5f, body);
        t.F.Cone(Bone.Spine, V(-0.05f, 3.35f, 0), 0.72f * t.Thick, V(0, 3.9f, 0), 0.62f * t.Thick, robe, body);
        t.Sash(sash, 0.6f, 0.2f, thin: true);
        // La estola: dos bandas de hueso del cuello a medio muslo, con sigilos.
        int st = t.F.Part();
        Func<Vector3, int, int> glyphs = (p, m) => MathF.Sin(p.Y * 9.5f) > 0.55f && MathF.Sin(p.Y * 3.1f + p.Z * 20) > -0.2f ? sigil : m;
        foreach (int side in new[] { 1, -1 })
        {
            var up = t.F.Box(Bone.Spine, V(d.ChestF + 0.22f, 1.75f, side * 0.6f), V(0.06f, 1.65f, 0.14f), stole, st);
            up.Paint = glyphs;
            // La parte de abajo es tela: se mueve con la túnica.
            float x = d.WaistF + 0.34f, z = side * 0.6f;
            var lo = t.F.PanelCloth(Bone.Pelvis, V(x, 0.4f, z - 0.14f), V(x, 0.4f, z + 0.14f), V(x + 0.15f, -2.6f, z - 0.14f), V(x + 0.15f, -2.6f, z + 0.14f),
                Vector3.UnitX, 0.05f, stole, st, 2, 4, glyphs);
            lo.LegR = 0.72f * t.L;
            lo.Pad = 0.32f;
        }
        // Collar de huesitos.
        int beads = t.F.Part();
        for (int i = 0; i < 9; i++)
        {
            float a = (i - 4) * 0.33f;
            t.F.Ellipsoid(Bone.Spine, V(0.1f + 0.62f * MathF.Cos(a), 3.2f - 0.25f * MathF.Cos(a * 1.4f), 0.75f * MathF.Sin(a)), V(0.1f, 0.14f, 0.1f), bone, beads);
        }
        // El grimorio en la cadera derecha, encadenado al cordón.
        int bk = t.F.Part();
        var at = V(0.2f, -1.1f, d.HipW + 1.2f * t.L);
        t.F.Box(Bone.Pelvis, at, V(0.55f, 0.72f, 0.2f), book, bk);
        t.F.Box(Bone.Pelvis, at + V(0, 0, 0.21f), V(0.2f, 0.2f, 0.02f), brass, bk);
        foreach (var c in new[] { V(0.5f, 0.66f, 0), V(-0.5f, 0.66f, 0), V(0.5f, -0.66f, 0), V(-0.5f, -0.66f, 0) })
            t.F.Ellipsoid(Bone.Pelvis, at + c + V(0, 0, 0.1f), V(0.1f, 0.1f, 0.14f), brass, bk);
        t.F.Cone(Bone.Pelvis, V(0.3f, 0.55f, d.HipW + 1.0f * t.L), 0.07f, at + V(0.1f, 0.72f, 0), 0.07f, iron, bk).Thin = true;

        foreach (int side in new[] { 1, -1 })
        {
            // Mangas de campana hasta la muñeca: se asoman las manos.
            int sleeve = t.F.Part();
            t.F.Cone(Skeleton.Arm(side, 0), V(0, -0.1f, 0), 0.5f * t.L, V(0, -2.4f, 0), 0.52f * t.L, robe, sleeve);
            var bell = t.F.Cone(Skeleton.Arm(side, 1), V(0, 0, 0), 0.5f * t.L, V(0, -2.1f, 0), 0.86f * t.L, robe, sleeve);
            bell.Mask = p => p.Y > -2.05f;
            t.Forearm(side, t.Skin);
            t.Hand(side, t.Skin);
            t.Thigh(side, robe);
            t.Shin(side, robe);
            t.Foot(side, shoes);
        }
        t.Head();
        t.HoodUp(robe, cowl: true, ragged: 0.1f, deep: true);
        return t.F;
    }

    // ------------------------------------------------------------------ el taller

    /// <summary>
    /// Arma una ropa: la anatomía de siempre (medidas de <see cref="Body"/>) con los materiales que
    /// se le pidan en cada parte, y las prendas que se repiten entre clases (faja, cinto, correa,
    /// capucha, pañuelo, faldas).
    /// </summary>
    private sealed partial class Tailor
    {
        public readonly Figure F = new();
        public readonly Dims D;
        public readonly float L, Thick;
        public readonly int Skin;

        public Tailor(Build b)
        {
            D = Dims.Of(b);
            L = D.Limb;
            Thick = 0.9f + 0.1f * L;
            Skin = F.AddMat("piel", MatChannel.Skin, lift: 0.06f);
        }

        public int Mat(string name, uint rgb, Func<Vector3, int, int> tex = null, bool shiny = false, float flat = 0, float lift = 0) =>
            F.AddMat(name, MatChannel.Fixed, rgb, shiny, tex, flat, lift);

        public int Accent(string name, Func<Vector3, int, int> tex = null) => F.AddMat(name, MatChannel.Accent, tex: tex);

        /// <summary>Cabeza y cuello (piel), como el cuerpo de siempre: los ojos caen en estas partes.</summary>
        public void Head()
        {
            int head = F.Part();
            int neck = F.Part();
            F.Cone(Bone.Head, V(0.0f, -0.45f, 0), 0.46f * Thick, V(0.12f, 1.0f, 0), 0.4f * Thick, Skin, neck);
            F.Ellipsoid(Bone.Head, Body.HeadC, Body.HeadR, Skin, head);
            F.Ellipsoid(Bone.Head, V(0.38f, 1.42f, 0), V(0.66f, 0.78f, 0.6f), Skin, head);
            F.Ellipsoid(Bone.Head, V(0.05f, 1.25f, 0), V(0.56f, 0.5f, 0.66f), Skin, head);
            F.Ellipsoid(Bone.Head, V(0.8f, 2.02f, 0), V(0.28f, 0.14f, 0.6f), Skin, head);
            F.Ellipsoid(Bone.Head, V(1.02f, 1.62f, 0), V(0.2f, 0.3f, 0.15f), Skin, head);
            foreach (int side in new[] { 1, -1 })
                F.Ellipsoid(Bone.Head, V(-0.05f, 1.7f, side * 0.78f), V(0.17f, 0.3f, 0.1f), Skin, head);
            F.StampParts.Add(head);
            F.StampParts.Add(neck);
        }

        /// <summary>Cintura, pecho, trapecios y hombros en una sola parte (más gruesos si la ropa abulta).</summary>
        public int Torso(int m, float grow = 0, bool arms = true, bool muscles = false)
        {
            int part = F.Part();
            float g = grow;
            F.Ellipsoid(Bone.Spine, V(0.02f, 0.25f, 0), V(D.WaistF + g, 1.05f + 0.5f * g, D.WaistS + g), m, part);
            F.Ellipsoid(Bone.Spine, V(0.02f, 1.95f, 0), V(D.ChestF + g, 1.4f + 0.5f * g, D.ChestS + g), m, part);
            F.Ellipsoid(Bone.Spine, V(0.35f, 2.35f, 0), V(0.72f * Thick + g, 0.7f, D.ChestS * 0.8f + g), m, part);
            if (muscles)
            {
                // Pecho y panza marcados (el blasfemo pelea con el torso al aire).
                foreach (int side in new[] { 1, -1 })
                    F.Ellipsoid(Bone.Spine, V(0.55f, 2.3f, side * 0.5f), V(0.5f * Thick, 0.48f, 0.6f), m, part);
                F.Ellipsoid(Bone.Spine, V(0.45f, 0.9f, 0), V(0.5f, 0.85f, 0.7f), m, part);
            }
            if (!arms) return part;
            foreach (int side in new[] { 1, -1 })
            {
                F.Cone(Bone.Spine, V(-0.1f, 3.35f, side * 0.35f), 0.45f * Thick + g, V(-0.05f, 3.1f, side * (D.ShoulderW - 0.25f)), 0.42f * L + g, m, part);
                F.Ellipsoid(Bone.Spine, V(-0.02f, 3.02f, side * (D.ShoulderW - 0.12f)), V(0.5f, 0.46f, 0.5f) * L + V(g, g, g), m, part);
            }
            return part;
        }

        public void Hips(int m) => F.Ellipsoid(Bone.Pelvis, V(0, -0.05f, 0), V(0.82f * L, 0.85f, D.HipW + 0.42f * L), m);

        /// <summary>Un faldón de tela que cuelga de la cadera (achatado adelante y atrás, como el torso): se hamaca y lo empujan las piernas.</summary>
        public ClothDef Skirt(int m, float top, float bottom, float rTop, float rBottom, int part = 0)
        {
            var c = F.ConeCloth(Bone.Pelvis, V(0.02f, top, 0), rTop, V(0.05f, bottom, 0), rBottom, m, part, (D.WaistF + 0.1f) / D.WaistS, 1, 16, 3);
            Legs(c);
            return c;
        }

        /// <summary>Contra qué choca la tela: las piernas de la gente (a la medida de referencia).</summary>
        private void Legs(ClothDef c)
        {
            c.LegR = 0.72f * L;
            c.Pad = 0.2f;
        }

        /// <summary>
        /// Falda larga que deja caminar: de la cadera a medio muslo cuelga de la pelvis, y de ahí
        /// cada pierna lleva su paño (el del muslo y el de la canilla) hasta el ruedo, a
        /// <paramref name="belowKnee"/> de la rodilla. Todo es una sola parte: se lee como una prenda.
        /// </summary>
        public ClothDef Robe(int m, float belowKnee, float flare, int part = 0)
        {
            if (part == 0) part = F.Part();
            // Línea A: angosta en la cintura y cada vez más ancha hasta el ruedo. Es tela: al caminar la
            // rodilla la empuja adelante y el ruedo se hamaca (ya no se parte en un paño por pierna).
            float hem = -(D.HipDown + D.Thigh) - belowKnee;
            var c = F.ConeCloth(Bone.Pelvis, V(0.02f, 0.6f, 0), D.WaistS + 0.02f, V(0.1f, hem, 0), D.HipW + 1.25f * L + 0.7f * flare, m, part,
                (D.WaistF + 0.35f) / D.WaistS, 1, 18, 6, mask: p => p.Y > hem + 0.15f + 0.12f * MathF.Abs(MathF.Sin(MathF.Atan2(p.Z, p.X) * 6)));
            Legs(c);
            return c;
        }

        /// <summary>La faja del jugador: vueltas a la cintura, el nudo al costado y las puntas colgando (con inercia).</summary>
        public void Sash(int m, float y, float grow, bool thin = false)
        {
            int band = F.Part();
            var ring = F.Ellipsoid(Bone.Pelvis, V(0.02f, y, 0), V(D.WaistF + grow, thin ? 0.3f : 0.6f, D.WaistS + grow), m, band);
            float h = thin ? 0.14f : 0.3f;
            ring.Mask = p => p.Y > y - h + 0.06f * MathF.Sin(p.Z * 5) && p.Y < y + h + 0.33f * (thin ? 0 : 1);
            var knot = D.ScarfRoot + V(0, y - 0.55f, -(grow - 0.22f));
            F.Ellipsoid(Bone.Pelvis, knot + V(0.05f, 0.05f, 0), V(0.26f, 0.24f, 0.24f) * Thick * (thin ? 0.7f : 1), m, band);
            foreach (var (bone, len, w) in new[] { (Bone.Scarf1, D.ScarfSeg, thin ? 0.12f : 0.24f), (Bone.Scarf2, 0.95f, thin ? 0.1f : 0.2f) })
            {
                var root = bone == Bone.Scarf1 ? D.ScarfRoot : D.ScarfRoot - V(0, D.ScarfSeg, 0);
                var strip = F.Box(bone, V(0, -len / 2, 0), V(0.07f, len / 2 + 0.05f, w), m, band);
                strip.Origin = root;
            }
        }

        public void Belt(int leather, int buckle, float y, float grow)
        {
            int part = F.Part();
            var ring = F.Ellipsoid(Bone.Pelvis, V(0.02f, y, 0), V(D.WaistF + grow, 0.4f, D.WaistS + grow), leather, part);
            ring.Mask = p => MathF.Abs(p.Y - y) < 0.17f;
            ring.Paint = (p, m) => p.X > D.WaistF * 0.85f && MathF.Abs(p.Z) < 0.2f ? buckle : m;
        }

        /// <summary>Una correa cruzada sobre el pecho (el plano de <paramref name="n"/>), con hebilla adelante.</summary>
        public void Strap(int leather, int buckle, Vector3 n, float grow)
        {
            int part = F.Part();
            var s = F.Ellipsoid(Bone.Spine, V(0.02f, 1.85f, 0), V(D.ChestF + grow, 1.75f, D.ChestS + grow), leather, part);
            s.Mask = p => MathF.Abs(Vector3.Dot(p - V(0, 1.75f, 0), n)) < 0.13f && p.Y > 0.2f;
            s.Paint = (p, m) => p.X > D.ChestF * 0.75f && p.Y > 1.35f && p.Y < 1.62f ? buckle : m;
        }

        public void UpperArm(int side, int m, float grow = 0)
        {
            int part = F.Part();
            F.Cone(Skeleton.Arm(side, 0), V(0, -0.2f, 0), 0.45f * L + grow, V(0, -2.35f, 0), 0.42f * L + grow, m, part);
            F.Ellipsoid(Skeleton.Arm(side, 0), V(0, -0.6f, 0), V(0.52f, 0.72f, 0.54f) * L + V(grow, grow, grow), m, part);
        }

        /// <summary>Lo arremangado sobre el codo.</summary>
        public void Cuff(int side, int m, float grow = 0) =>
            F.Ellipsoid(Skeleton.Arm(side, 1), V(0, -0.12f, 0), V(0.45f, 0.3f, 0.45f) * L + V(grow, grow, grow), m);

        public void Forearm(int side, int m, float grow = 0)
        {
            int part = F.Part();
            F.Cone(Skeleton.Arm(side, 1), V(0, 0, 0), 0.38f * L + grow, V(0, -2.35f, 0), 0.26f * L + grow, m, part);
            F.Ellipsoid(Skeleton.Arm(side, 1), V(0.04f, -0.75f, 0), V(0.4f, 0.8f, 0.4f) * L + V(grow, grow, grow), m, part);
        }

        /// <summary>La mano (el puño cierra alrededor de <see cref="WeaponModels.Fist"/>): la palma, los dedos y el pulgar.</summary>
        public void Hand(int side, int m, int palm = -1, int fingers = -1)
        {
            int part = F.Part();
            var h = Skeleton.Arm(side, 2);
            F.Ellipsoid(h, V(0.02f, -0.42f, 0), V(0.34f, 0.46f, 0.2f) * Thick, palm < 0 ? m : palm, part);
            F.Ellipsoid(h, V(0.1f, -0.85f, 0), V(0.3f, 0.3f, 0.2f) * Thick, fingers < 0 ? m : fingers, part);
            F.Cone(h, V(0.28f, -0.25f, 0), 0.12f, V(0.4f, -0.7f, 0), 0.1f, fingers < 0 ? m : fingers, part);
        }

        public void Thigh(int side, int m, float grow = 0)
        {
            int part = F.Part();
            F.Cone(Skeleton.Leg(side, 0), V(0, 0.15f, 0), 0.7f * L + grow, V(0, -3.55f, 0), 0.48f * L + grow, m, part);
            F.Ellipsoid(Skeleton.Leg(side, 0), V(0.12f, -1.35f, 0), V(0.66f, 1.55f, 0.64f) * L + V(grow, grow, grow), m, part);
        }

        /// <summary>Rodilla y canilla; <paramref name="paint"/> cambia el material por altura (vendas, caña de bota).</summary>
        public void Shin(int side, int m, float grow = 0, Func<Vector3, int> paint = null)
        {
            int part = F.Part();
            var k = F.Ellipsoid(Skeleton.Leg(side, 1), V(0.1f, 0.02f, 0), V(0.47f, 0.44f, 0.45f) * L + V(grow, grow, grow), m, part);
            var calf = F.Cone(Skeleton.Leg(side, 1), V(0, -0.1f, 0), 0.45f * L + grow, V(0, -3.55f, 0), 0.29f * L + grow, m, part);
            var mus = F.Ellipsoid(Skeleton.Leg(side, 1), V(-0.1f, -1.1f, 0), V(0.47f, 1.05f, 0.43f) * L + V(grow, grow, grow), m, part);
            if (paint != null)
            {
                calf.Paint = (p, _) => paint(p);
                mus.Paint = (p, _) => paint(p);
            }
        }

        /// <summary>El pie: zapato, bota o descalzo (el pie de piel, más fino); con <paramref name="ankle"/>, la caña sube un poco.</summary>
        public void Foot(int side, int m, bool ankle = false, bool bare = false)
        {
            int part = F.Part();
            var f = Skeleton.Leg(side, 2);
            float w = bare ? 0.85f : 1;
            F.Ellipsoid(f, V(0.62f, -0.42f, 0), V(1.1f, 0.3f, 0.4f) * Thick * w, m, part);
            F.Ellipsoid(f, V(-0.05f, -0.35f, 0), V(0.38f, 0.38f, 0.36f) * Thick * w, m, part);
            F.Cone(f, V(0, ankle ? 0.35f : 0.1f, 0), 0.33f * L, V(0.05f, -0.3f, 0), 0.36f * Thick * w, m, part);
        }

        /// <summary>
        /// Capucha puesta: una concha sobre la cabeza abierta en la cara, con la punta colgando
        /// atrás (usa el resorte de la melena, que con capucha no se dibuja) y, si
        /// <paramref name="cowl"/>, la esclavina sobre los hombros con el borde deshilachado.
        /// Honda (<paramref name="deep"/>), la abertura es más chica y sale más adelante.
        /// </summary>
        public void HoodUp(int m, bool cowl, float ragged, bool deep = false)
        {
            int part = F.Part();
            float grow = deep ? 0.34f : 0.24f;
            var shell = F.Ellipsoid(Bone.Head, V(-0.06f + (deep ? 0.1f : 0), 2.0f, 0), Body.HeadR + V(grow + (deep ? 0.12f : 0), grow, grow), m, part);
            float fy = deep ? 1.62f : 1.58f, ry = deep ? 0.78f : 0.92f, rz = deep ? 0.56f : 0.66f;
            shell.Mask = p =>
            {
                float dy = (p.Y - fy) / ry, dz = p.Z / rz;
                return !(p.X > 0.25f && dy * dy + dz * dz < 1);
            };
            // La punta, que cuelga atrás con inercia.
            F.Cone(Bone.Head, V(-0.55f, 2.6f, 0), 0.55f, D.HairBackRoot + V(-0.15f, 0, 0), 0.32f, m, part);
            F.Cone(Bone.HairBack1, V(0, 0, 0), 0.32f, V(-0.05f, -D.HairBackSeg, 0), 0.12f, m, part);
            // El cuello de la capucha tapa el cuello.
            F.Cone(Bone.Head, V(-0.05f, -0.4f, 0), 0.64f * Thick, V(-0.05f, 1.0f, 0), 0.7f * Thick, m, part);
            if (!cowl) return;
            var c = F.Ellipsoid(Bone.Spine, V(-0.05f, 3.15f, 0), V(D.ChestF + 0.5f, 0.95f, D.ShoulderW + 0.62f * L), m, part);
            c.Mask = p => p.Y > 2.45f + 0.2f * Smooth(0.9f, -0.6f, p.X) + ragged * Tatter(p.X * 2.3f + p.Z * 4.1f);
        }

        /// <summary>Pañuelo atado en la cabeza: cubre el cráneo hasta la frente y cae en punta sobre la nuca.</summary>
        public void Kerchief(int m)
        {
            int part = F.Part();
            var cap = F.Ellipsoid(Bone.Head, Body.HeadC + V(-0.06f, 0.08f, 0), Body.HeadR + V(0.14f, 0.12f, 0.14f), m, part);
            cap.Mask = p => p.Y > 2.05f + 0.35f * Smooth(0.2f, 0.9f, p.X) - 0.5f * Smooth(0, -0.9f, p.X);
            F.Cone(Bone.Head, V(-0.85f, 1.9f, 0), 0.45f, V(-1.05f, 0.95f, 0), 0.12f, m, part);
            F.Ellipsoid(Bone.Head, V(-1.0f, 2.1f, 0.25f), V(0.22f, 0.2f, 0.2f), m, part);
        }
    }

    // ------------------------------------------------------------------ texturas

    private static float Smooth(float e0, float e1, float x)
    {
        float t = Math.Clamp((x - e0) / (e1 - e0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static float Tatter(float x)
    {
        float s = MathF.Sin(x) * 0.6f + MathF.Sin(x * 2.7f + 1.3f) * 0.4f;
        return s > 0.35f ? 1 : s;
    }

    /// <summary>Acolchado en rombos (el gambesón).</summary>
    private static int Quilt(Vector3 p, int t)
    {
        float a = MathF.Sin((p.Y + p.Z) * 5.5f), b = MathF.Sin((p.Y - p.Z) * 5.5f);
        return (MathF.Abs(a) < 0.18f || MathF.Abs(b) < 0.18f) && t > 0 ? t - 1 : t;
    }

    /// <summary>Costuras verticales del cuero.</summary>
    private static int Seams(Vector3 p, int t) => MathF.Abs(MathF.Sin(MathF.Atan2(p.Z, p.X) * 5)) < 0.12f && t > 0 ? t - 1 : t;

    /// <summary>Pliegues largos de una túnica o una falda.</summary>
    private static int Folds(Vector3 p, int t) => MathF.Sin(MathF.Atan2(p.Z, p.X) * 9 + p.Y * 0.4f) > 0.55f && t > 0 ? t - 1 : t;

    /// <summary>Trama gruesa de un paño barato (el sambenito).</summary>
    private static int Weave(Vector3 p, int t) => MathF.Sin(p.Y * 14) * MathF.Sin(p.Z * 14) > 0.6f && t > 1 ? t - 1 : t;

    /// <summary>Remiendos: parches cuadrados un tono más oscuros.</summary>
    private static int Patches(Vector3 p, int t)
    {
        float n = MathF.Sin(MathF.Floor(p.Y * 1.3f) * 3.7f + MathF.Floor(p.Z * 2.1f) * 5.3f);
        return n > 0.75f && t > 0 ? t - 1 : t;
    }

    /// <summary>Cuerda retorcida.</summary>
    private static int Twist(Vector3 p, int t) => MathF.Sin(p.X * 9 + p.Z * 9 + p.Y * 14) > 0.3f && t > 0 ? t - 1 : t;

    private static int LinenFolds(Vector3 p, int t)
    {
        float s = MathF.Sin(p.Z * 9 + p.X * 4);
        return s > 0.8f && t > 1 && t < 4 ? t - 1 : t;
    }

    private static int CapeFolds(Vector3 p, int t) => MathF.Sin(MathF.Atan2(p.Z, p.X) * 7) > 0.75f && t > 1 ? t - 1 : t;

    private static int SashFolds(Vector3 p, int t) => MathF.Sin(p.Y * 22 + p.Z * 3) > 0.6f && t > 1 ? t - 1 : t;

    private static int WrapBands(Vector3 p, int t)
    {
        float a = MathF.Atan2(p.Z, p.X);
        return MathF.Sin(p.Y * 9 + a * 1.2f) > 0.55f && t > 1 ? t - 1 : t;
    }
}
