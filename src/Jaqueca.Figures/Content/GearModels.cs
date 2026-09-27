using System.Numerics;
using Jaqueca.Figures.Model;
using Jaqueca.Figures.Rig;
using Jaqueca.Look;
using Jaqueca.Sprites;
using static Jaqueca.Figures.Model.Figure;

namespace Jaqueca.Figures.Content;

/// <summary>
/// Los modelos del equipo que no es arma (ver Items.Gear): cada pieza, armada con las mismas
/// primitivas que el cuerpo sobre los huesos que tapa, un poco más grande que el cuerpo y que la
/// ropa de las clases (los cascos, más grandes que las capuchas más hondas), así se ve encima.
/// Se suma a la figura en FigureSet.Build, después de la ropa.
/// </summary>
public static partial class GearModels
{
    private const uint Steel = 0x6E767E, SteelDark = 0x444A52, Brass = 0xB0904A, Gold = 0xC8A048, Leather = 0x5A3A26,
        LeatherDark = 0x2E2220, Silver = 0xD0D4DC, Ink = 0x0E0C10;

    /// <summary>Cuánto pelo tapa lo que lleva en la cabeza.</summary>
    public static HairHide Hides(string head) => head switch
    {
        "capucha_penitente" or "capirote" or "yelmo_yacente" => HairHide.All,
        "morrion" or "sombrero_ala" or "celada" or "capacete_santiago" or "yelmo_campeador" => HairHide.Top,
        _ => HairHide.None,
    };

    /// <summary>
    /// El modelo de una pieza para una complexión (null si no tiene). <paramref name="coins"/>: cuánta
    /// plata asoma de la mochila (0 nada, 3 repleta; ver Items.Denarios.Fill).
    /// </summary>
    public static Figure Build(string id, Build build, int coins = 0)
    {
        var d = Dims.Of(build);
        var g = new G(new Figure(), d);
        switch (id)
        {
            case "capucha_penitente": g.Hood(); break;
            case "morrion": g.Morion(); break;
            case "capirote": g.Capirote(); break;
            case "yelmo_yacente": g.GreatHelm(); break;
            case "aureola_hierro": g.Halo(); break;
            case "escapulario": g.Scapular(); break;
            case "relicario_plata": g.Reliquary(); break;
            case "jubon_acolchado": g.Doublet(0x8A7458, Quilt, false); break;
            case "cota_malla": g.Doublet(0x6E747C, Mail, true); break;
            case "peto_hierro": g.Cuirass(Steel, 0, false); break;
            case "coraza_tercio": g.Cuirass(0x34363C, Gold, true); break;
            case "guantes_cuero": g.Gloves(Leather, false); break;
            case "manoplas": g.Gloves(Steel, true); break;
            case "guanteletes_verdugo": g.Gauntlets(); break;
            case "calzas_reforzadas": g.Patches(); break;
            case "quijotes": g.Cuisses(); break;
            case "grebas": g.Greaves(); break;
            case "alpargatas": g.Espadrilles(); break;
            case "botas_marcha": g.Boots(); break;
            case "escarpes": g.Sabatons(); break;
            case "mochila_peregrino": g.Pack(false, coins); break;
            case "morral_buhonero": g.Pack(true, coins); break;
            default: return null;
        }
        return g.F;
    }

    /// <summary>El armado de una pieza, con las medidas del cuerpo que la lleva.</summary>
    private sealed partial class G
    {
        public readonly Figure F;
        private readonly Dims D;
        private readonly float L, Th;
        private static readonly Vector3 HC = Body.HeadC, HR = Body.HeadR;

        public G(Figure f, Dims d) { F = f; D = d; L = d.Limb; Th = 0.9f + 0.1f * L; }

        private int Mat(string name, uint rgb, bool shiny = false, float lift = 0, Func<Vector3, int, int> tex = null) =>
            F.AddMat(name, MatChannel.Fixed, rgb, shiny, tex, lift > 0 ? 0.4f : 0, lift);

        /// <summary>Una forma girada sobre su propio centro (ángulos en grados: alrededor de X, Y y Z).</summary>
        private static Matrix4x4 Around(Vector3 c, float x, float y, float z) =>
            Matrix4x4.CreateTranslation(-c) * Matrix4x4.CreateFromYawPitchRoll(y * MathF.PI / 180, x * MathF.PI / 180, z * MathF.PI / 180) * Matrix4x4.CreateTranslation(c);

        // -------------------------------------------------------------- cabeza

        /// <summary>Capucha de penitente: tela parda sobre la cabeza, abierta en la cara, y el cuello de la capucha sobre los hombros.</summary>
        public void Hood()
        {
            int cloth = Mat("capucha parda", 0x5A4A38, tex: Folds);
            int part = F.Part();
            var shell = F.Ellipsoid(Bone.Head, HC + V(-0.08f, 0.06f, 0), HR + V(0.42f, 0.4f, 0.42f), cloth, part);
            shell.Mask = p => !(p.X > 0.3f && MathF.Abs(p.Z) < 0.62f && p.Y > 0.8f && p.Y < 2.35f);
            F.Cone(Bone.Head, V(-0.05f, -0.45f, 0), 0.8f * Th, V(-0.05f, 1.0f, 0), 0.9f * Th, cloth, part);
            var cowl = F.Ellipsoid(Bone.Spine, V(-0.05f, 3.2f, 0), V(D.ChestF + 0.55f, 0.85f, D.ShoulderW + 0.4f * L), cloth, part);
            cowl.Mask = p => p.Y > 2.6f + 0.12f * MathF.Sin(p.Z * 6 + p.X * 3);
        }

        /// <summary>Morrión: la cúpula de acero, la cresta alta y el ala que sube adelante y atrás, con remaches de latón.</summary>
        public void Morion()
        {
            int steel = Mat("acero", Steel, shiny: true);
            int brass = Mat("latón", Brass, shiny: true);
            int part = F.Part();
            var c = HC + V(-0.05f, 0.1f, 0);
            var dome = F.Ellipsoid(Bone.Head, c, V(1.4f, 1.3f, 1.25f), steel, part);
            dome.Mask = p => p.Y > c.Y - 0.1f;
            // El ala: dos mitades inclinadas hacia arriba (adelante y atrás), como un barco.
            foreach (int s in new[] { 1, -1 })
            {
                var bc = c + V(0, -0.05f, 0);
                var brim = F.Ellipsoid(Bone.Head, bc, V(2.0f, 0.1f, 1.65f), steel, part);
                brim.Local = Around(bc, 0, 0, s * 18);
                brim.Mask = p => s * (p.X - bc.X) > 0 && ((p.X - c.X) * (p.X - c.X) / (1.4f * 1.4f) + p.Z * p.Z / (1.25f * 1.25f)) > 0.8f;
            }
            // La cresta, un arco de lámina sobre la cúpula.
            var crest = F.Box(Bone.Head, c + V(-0.05f, 1.35f, 0), V(1.05f, 0.5f, 0.06f), steel, part);
            crest.Thin = true;
            crest.Mask = p =>
            {
                float u = (p.X - c.X + 0.05f) / 1.05f;
                return MathF.Abs(u) < 1 && p.Y < c.Y + 1.15f + 0.6f * MathF.Sqrt(MathF.Max(0, 1 - u * u));
            };
            for (int k = 0; k < 8; k++)
            {
                float a = k * MathF.Tau / 8;
                F.Ellipsoid(Bone.Head, c + V(MathF.Cos(a) * 1.36f, 0.05f, MathF.Sin(a) * 1.2f), V(0.07f, 0.07f, 0.07f), brass, part);
            }
        }

        /// <summary>Capirote: el cucurucho alto (echado un poco atrás), la tela que tapa la cara con los agujeros de los ojos, el antifaz sobre el pecho y el escudo de la cofradía.</summary>
        public void Capirote()
        {
            int cloth = Mat("paño de nazareno", 0x2E2438, shiny: false, tex: Folds);
            int hole = Mat("ojos", Ink);
            int badge = Mat("escudo", Gold, shiny: true, lift: 0.15f);
            int part = F.Part();
            F.Ellipsoid(Bone.Head, HC + V(0.02f, -0.02f, 0), HR + V(0.48f, 0.44f, 0.48f), cloth, part);
            F.Cone(Bone.Head, HC + V(-0.1f, 0.7f, 0), 1.3f, HC + V(-0.75f, 4.6f, 0), 0.07f, cloth, part);
            // El antifaz: cae de la cara al pecho y a la espalda.
            F.Cone(Bone.Head, HC + V(0.2f, -0.4f, 0), 1.35f, HC + V(0.25f, -2.4f, 0), 1.75f, cloth, part);
            var drape = F.Ellipsoid(Bone.Spine, V(0.05f, 3.0f, 0), V(D.ChestF + 0.45f, 1.1f, D.ShoulderW + 0.35f * L), cloth, part);
            drape.Mask = p => p.Y > 2.0f + 0.1f * MathF.Sin(p.Z * 5);
            foreach (int s in new[] { 1, -1 })
                F.Ellipsoid(Bone.Head, HC + V(1.47f, 0.02f, s * 0.36f), V(0.08f, 0.1f, 0.14f), hole, F.Part());
            F.Ellipsoid(Bone.Spine, V(D.ChestF + 0.5f, 2.35f, 0), V(0.06f, 0.26f, 0.2f), badge, F.Part());
        }

        /// <summary>Yelmo del yacente: cerrado, de fondo plano, con la ranura de los ojos, los respiraderos y una cruz de oro en la visera.</summary>
        public void GreatHelm()
        {
            int steel = Mat("acero frío", 0x8A96A0, shiny: true);
            int dark = Mat("ranura", 0x121216);
            int gold = Mat("cruz de oro", Gold, shiny: true, lift: 0.2f);
            int part = F.Part();
            var bottom = HC + V(0.05f, -1.35f, 0);
            var top = HC + V(0.05f, 1.05f, 0);
            var shell = F.Cone(Bone.Head, bottom, 1.42f, top, 1.36f, steel, part);
            shell.Local = Matrix4x4.CreateTranslation(-HC) * Matrix4x4.CreateScale(1, 1, 0.9f) * Matrix4x4.CreateTranslation(HC);
            F.Ellipsoid(Bone.Head, top, V(1.36f, 0.22f, 1.24f), steel, part);
            F.Box(Bone.Head, HC + V(1.42f, 0.18f, 0), V(0.05f, 0.07f, 0.95f), dark, F.Part());
            for (int k = 0; k < 4; k++) F.Box(Bone.Head, HC + V(1.38f, -0.45f - 0.18f * (k % 2), 0.5f + 0.15f * k), V(0.04f, 0.04f, 0.04f), dark, F.Part());
            int cross = F.Part();
            F.Box(Bone.Head, HC + V(1.45f, -0.55f, 0), V(0.03f, 0.6f, 0.08f), gold, cross);
            F.Box(Bone.Head, HC + V(1.45f, -0.4f, 0), V(0.03f, 0.08f, 0.4f), gold, cross);
        }

        /// <summary>Aureola de hierro: un aro detrás de la cabeza con rayos hacia arriba, y en las puntas, luz.</summary>
        public void Halo()
        {
            int iron = Mat("hierro", 0x4A4A50, shiny: true);
            int glow = Mat("luz de las puntas", 0xFFF0C0, lift: 0.8f);
            int part = F.Part();
            var c = HC + V(-1.0f, 0.3f, 0);
            var ring = F.Ellipsoid(Bone.Head, c, V(0.08f, 1.55f, 1.55f), iron, part);
            ring.Mask = p => { float r = MathF.Sqrt((p.Y - c.Y) * (p.Y - c.Y) + p.Z * p.Z); return r > 1.3f; };
            for (int k = 0; k < 11; k++)
            {
                float a = (-10 + k * 20) * MathF.PI / 180;
                var from = c + V(0, MathF.Sin(a) * 1.5f, MathF.Cos(a) * 1.5f);
                var to = c + V(0, MathF.Sin(a) * 2.3f, MathF.Cos(a) * 2.3f);
                F.Cone(Bone.Head, from, 0.06f, to, 0.02f, iron, part);
                F.Ellipsoid(Bone.Head, to, V(0.09f, 0.09f, 0.09f), glow, part);
            }
        }

        // -------------------------------------------------------------- cuello

        /// <summary>Escapulario: dos paños bordados (pecho y espalda) colgados de cordones.</summary>
        public void Scapular()
        {
            int cloth = Mat("paño", 0x6A3E2A);
            int red = Mat("bordado", 0xA02828);
            int cord = Mat("cordón", 0xC8B488);
            int part = F.Part();
            float f = D.ChestF + 0.14f;
            F.Box(Bone.Spine, V(f, 2.15f, 0), V(0.04f, 0.42f, 0.36f), cloth, part);
            F.Box(Bone.Spine, V(f + 0.05f, 2.2f, 0), V(0.02f, 0.18f, 0.06f), red, part);
            F.Box(Bone.Spine, V(f + 0.05f, 2.25f, 0), V(0.02f, 0.06f, 0.15f), red, part);
            F.Box(Bone.Spine, V(-f, 2.2f, 0), V(0.04f, 0.42f, 0.36f), cloth, part);
            foreach (int s in new[] { 1, -1 })
            {
                F.Cone(Bone.Spine, V(0.15f, 3.55f, s * 0.42f), 0.04f, V(f, 2.55f, s * 0.3f), 0.04f, cord, part).Thin = true;
                F.Cone(Bone.Spine, V(-0.15f, 3.55f, s * 0.42f), 0.04f, V(-f, 2.6f, s * 0.3f), 0.04f, cord, part).Thin = true;
            }
        }

        /// <summary>Relicario de plata: la cadena y el óvalo sobre el pecho, con el vidrio que brilla.</summary>
        public void Reliquary()
        {
            int silver = Mat("plata", Silver, shiny: true);
            int glass = Mat("vidrio", 0xE8E0C8, lift: 0.35f);
            int part = F.Part();
            float f = D.ChestF + 0.16f;
            foreach (int s in new[] { 1, -1 })
                F.Cone(Bone.Spine, V(0.2f, 3.55f, s * 0.42f), 0.035f, V(f, 2.75f, s * 0.05f), 0.035f, silver, part).Thin = true;
            F.Ellipsoid(Bone.Spine, V(f, 2.45f, 0), V(0.09f, 0.34f, 0.26f), silver, part);
            F.Ellipsoid(Bone.Spine, V(f + 0.07f, 2.45f, 0), V(0.04f, 0.2f, 0.15f), glass, F.Part());
        }

        // -------------------------------------------------------------- pecho

        /// <summary>Jubón o cota: el torso cubierto (cintura, pecho, hombros) con faldón hasta el muslo; la cota, con mangas al codo.</summary>
        public void Doublet(uint rgb, Func<Vector3, int, int> tex, bool sleeves)
        {
            int m = Mat("jubón", rgb, shiny: sleeves, tex: tex);
            int part = F.Part();
            F.Ellipsoid(Bone.Spine, V(0.02f, 0.25f, 0), V(D.WaistF + 0.18f, 1.15f, D.WaistS + 0.18f), m, part);
            F.Ellipsoid(Bone.Spine, V(0.02f, 1.95f, 0), V(D.ChestF + 0.2f, 1.5f, D.ChestS + 0.2f), m, part);
            F.Ellipsoid(Bone.Spine, V(0.37f, 2.35f, 0), V(0.78f * Th, 0.74f, D.ChestS * 0.85f), m, part);
            foreach (int s in new[] { 1, -1 })
                F.Ellipsoid(Bone.Spine, V(-0.02f, 3.0f, s * (D.ShoulderW - 0.12f)), V(0.62f, 0.56f, 0.62f) * L, m, part);
            var skirt = F.Cone(Bone.Pelvis, V(0.02f, 0.6f, 0), D.WaistS + 0.15f, V(0.05f, -1.25f, 0), D.HipW + 0.95f * L, m, part);
            skirt.Local = Matrix4x4.CreateScale((D.WaistF + 0.2f) / (D.WaistS + 0.15f), 1, 1);
            skirt.Mask = p => p.Y < 0.65f && p.Y > -1.2f;
            if (!sleeves) return;
            foreach (int s in new[] { 1, -1 })
                F.Cone(Skeleton.Arm(s, 0), V(0, -0.1f, 0), 0.58f * L, V(0, -2.1f, 0), 0.52f * L, m, part);
        }

        /// <summary>Peto y espaldar con la arista al medio y correas a los costados; la coraza del tercio, pavonada con ribetes de oro, hombreras y una banda carmesí.</summary>
        public void Cuirass(uint rgb, uint trim, bool captain)
        {
            int steel = Mat("peto", rgb, shiny: true);
            int ridge = Mat("arista", captain ? 0x1E2024 : SteelDark, shiny: true);
            int strap = Mat("correas", Leather);
            int gold = trim != 0 ? Mat("ribete", trim, shiny: true, lift: 0.12f) : -1;
            int part = F.Part();
            var shell = F.Ellipsoid(Bone.Spine, V(0.06f, 1.85f, 0), V(D.ChestF + 0.3f, 1.6f, D.ChestS + 0.26f), steel, part);
            float top = 3.15f, bottom = 0.55f;
            shell.Mask = p => p.Y > bottom && p.Y < top;
            shell.Paint = (p, m) =>
            {
                if (gold >= 0 && (p.Y > top - 0.14f || p.Y < bottom + 0.14f)) return gold;
                if (MathF.Abs(p.Z) < 0.07f && p.X > 0.2f) return ridge;
                if (MathF.Abs(p.X) < 0.14f) return strap;
                return m;
            };
            // La falda de lamas sobre la cadera.
            var fauld = F.Cone(Bone.Pelvis, V(0.02f, 0.75f, 0), D.WaistS + 0.3f, V(0.04f, -0.2f, 0), D.HipW + 0.7f * L, steel, part);
            fauld.Local = Matrix4x4.CreateScale((D.WaistF + 0.32f) / (D.WaistS + 0.3f), 1, 1);
            fauld.Paint = (p, m) => MathF.Sin(p.Y * 11) > 0.7f ? ridge : m;
            if (!captain) return;
            foreach (int s in new[] { 1, -1 })
            {
                var pad = F.Ellipsoid(Skeleton.Arm(s, 0), V(0.02f, -0.35f, 0), V(0.76f, 0.78f, 0.76f) * L, steel, part);
                pad.Mask = p => p.Y > -1.05f;
                pad.Paint = (p, m) => p.Y < -0.92f ? gold : MathF.Sin(p.Y * 9) > 0.75f ? ridge : m;
            }
            int sash = Mat("banda carmesí", 0x8A2020);
            var n = Vector3.Normalize(V(0, 0.6f, 0.8f));
            var band = F.Ellipsoid(Bone.Spine, V(0.06f, 1.8f, 0), V(D.ChestF + 0.38f, 1.75f, D.ChestS + 0.34f), sash, F.Part());
            band.Mask = p => MathF.Abs(Vector3.Dot(p - V(0, 1.7f, 0), n)) < 0.16f && p.Y > 0.7f && p.Y < 3.0f;
        }

        // -------------------------------------------------------------- manos

        /// <summary>Guantes (o manoplas de acero): la mano cubierta y el puño del guante sobre la muñeca.</summary>
        public void Gloves(uint rgb, bool plate)
        {
            int m = Mat(plate ? "manopla" : "guante", rgb, shiny: plate);
            foreach (int s in new[] { 1, -1 })
            {
                int part = F.Part();
                var hand = Skeleton.Arm(s, 2);
                F.Ellipsoid(hand, V(0.02f, -0.42f, 0), V(0.42f, 0.54f, 0.28f) * Th, m, part);
                F.Ellipsoid(hand, V(0.1f, -0.86f, 0), V(0.38f, 0.38f, 0.28f) * Th, m, part);
                if (plate) F.Ellipsoid(hand, V(0.12f, -0.95f, 0), V(0.42f, 0.16f, 0.3f) * Th, m, part);
                F.Cone(Skeleton.Arm(s, 1), V(0, plate ? -1.5f : -1.85f, 0), (plate ? 0.36f : 0.32f) * L, V(0, -2.4f, 0), (plate ? 0.52f : 0.42f) * L, m, part);
            }
        }

        /// <summary>Guanteletes de verdugo: cuero negro hasta medio brazo con placas de hierro en el dorso.</summary>
        public void Gauntlets()
        {
            int leather = Mat("cuero negro", LeatherDark);
            int iron = Mat("placas", SteelDark, shiny: true);
            foreach (int s in new[] { 1, -1 })
            {
                int part = F.Part();
                var hand = Skeleton.Arm(s, 2);
                F.Cone(Skeleton.Arm(s, 1), V(0, -0.5f, 0), 0.46f * L, V(0, -2.4f, 0), 0.38f * L, leather, part);
                F.Ellipsoid(hand, V(0.02f, -0.42f, 0), V(0.42f, 0.54f, 0.28f) * Th, leather, part);
                F.Ellipsoid(hand, V(0.1f, -0.86f, 0), V(0.38f, 0.38f, 0.28f) * Th, leather, part);
                for (int k = 0; k < 3; k++) F.Box(Skeleton.Arm(s, 1), V(0, -0.9f - 0.45f * k, 0), V(0.5f, 0.16f, 0.3f) * L, iron, part);
                F.Box(hand, V(0.02f, -0.5f, 0), V(0.3f, 0.26f, 0.32f) * Th, iron, part);
            }
        }

        // -------------------------------------------------------------- piernas

        /// <summary>Calzas reforzadas: paño oscuro en los muslos y rodilleras de cuero cosidas.</summary>
        public void Patches()
        {
            int wool = Mat("calzas", 0x3E3834);
            int leather = Mat("rodilleras", Leather);
            foreach (int s in new[] { 1, -1 })
            {
                int part = F.Part();
                F.Cone(Skeleton.Leg(s, 0), V(0, 0.1f, 0), 0.76f * L, V(0, -3.5f, 0), 0.54f * L, wool, part);
                var knee = F.Ellipsoid(Skeleton.Leg(s, 1), V(0.22f, 0.02f, 0), V(0.5f, 0.54f, 0.52f) * L, leather, F.Part());
                knee.Mask = p => p.X > 0.05f;
            }
        }

        /// <summary>Quijotes: la placa del muslo (por delante, con sus lamas) y la rodillera de acero.</summary>
        public void Cuisses()
        {
            int steel = Mat("acero", Steel, shiny: true);
            int dark = Mat("lamas", SteelDark, shiny: true);
            foreach (int s in new[] { 1, -1 })
            {
                int part = F.Part();
                var plate = F.Ellipsoid(Skeleton.Leg(s, 0), V(0.18f, -1.4f, 0), V(0.8f, 1.7f, 0.78f) * L, steel, part);
                plate.Mask = p => p.X > -0.05f;
                plate.Paint = (p, m) => MathF.Sin(p.Y * 4.5f) > 0.85f ? dark : m;
                var knee = F.Ellipsoid(Skeleton.Leg(s, 1), V(0.2f, 0.02f, 0), V(0.58f, 0.58f, 0.58f) * L, steel, part);
                knee.Mask = p => p.X > -0.1f;
            }
        }

        /// <summary>Grebas: canilleras de acero por delante, de la rodilla al tobillo, y rodilleras con aleta.</summary>
        public void Greaves()
        {
            int steel = Mat("acero", Steel, shiny: true);
            int dark = Mat("filo", SteelDark, shiny: true);
            foreach (int s in new[] { 1, -1 })
            {
                int part = F.Part();
                var shin = F.Cone(Skeleton.Leg(s, 1), V(0.05f, -0.2f, 0), 0.58f * L, V(0.02f, -3.3f, 0), 0.42f * L, steel, part);
                shin.Mask = p => p.X > -0.15f;
                shin.Paint = (p, m) => MathF.Abs(p.Z) < 0.06f ? dark : m;
                var knee = F.Ellipsoid(Skeleton.Leg(s, 1), V(0.2f, 0.02f, 0), V(0.6f, 0.6f, 0.6f) * L, steel, part);
                knee.Mask = p => p.X > -0.1f;
                F.Ellipsoid(Skeleton.Leg(s, 1), V(0.1f, 0.02f, s * 0.55f * L), V(0.3f, 0.36f, 0.08f), steel, part);
            }
        }

        // -------------------------------------------------------------- pies

        /// <summary>Alpargatas: lona clara, suela de esparto y cintas negras cruzadas al tobillo.</summary>
        public void Espadrilles()
        {
            int canvas = Mat("lona", 0xC8B48A);
            int jute = Mat("esparto", 0xA89060, tex: (p, t) => MathF.Sin(p.X * 14) > 0.5f && t > 0 ? t - 1 : t);
            int ribbon = Mat("cintas", 0x1E1A1C);
            foreach (int s in new[] { 1, -1 })
            {
                int part = F.Part();
                var foot = Skeleton.Leg(s, 2);
                F.Ellipsoid(foot, V(0.62f, -0.36f, 0), V(1.16f, 0.36f, 0.45f) * Th, canvas, part);
                F.Ellipsoid(foot, V(0.55f, -0.62f, 0), V(1.22f, 0.1f, 0.48f) * Th, jute, part);
                F.Cone(Skeleton.Leg(s, 1), V(0, -3.15f, 0), 0.34f * L, V(0, -3.3f, 0), 0.34f * L, ribbon, part);
            }
        }

        /// <summary>Botas de marcha: la bota hasta debajo de la rodilla, con la caña doblada.</summary>
        public void Boots()
        {
            int leather = Mat("bota", 0x3E2A1E);
            int fold = Mat("caña doblada", 0x5A3E2A);
            foreach (int s in new[] { 1, -1 })
            {
                int part = F.Part();
                var foot = Skeleton.Leg(s, 2);
                F.Ellipsoid(foot, V(0.62f, -0.38f, 0), V(1.18f, 0.38f, 0.46f) * Th, leather, part);
                F.Ellipsoid(foot, V(-0.05f, -0.33f, 0), V(0.44f, 0.44f, 0.42f) * Th, leather, part);
                F.Cone(Skeleton.Leg(s, 1), V(0, -1.35f, 0), 0.55f * L, V(0, -3.5f, 0), 0.44f * L, leather, part);
                F.Ellipsoid(Skeleton.Leg(s, 1), V(0, -1.3f, 0), V(0.62f, 0.24f, 0.62f) * L, fold, part);
            }
        }

        /// <summary>Escarpes: el pie de láminas de acero articuladas y el tobillo cubierto.</summary>
        public void Sabatons()
        {
            int steel = Mat("escarpe", Steel, shiny: true, tex: (p, t) => MathF.Sin(p.X * 7) > 0.6f && t > 0 ? t - 1 : t);
            foreach (int s in new[] { 1, -1 })
            {
                int part = F.Part();
                var foot = Skeleton.Leg(s, 2);
                F.Ellipsoid(foot, V(0.66f, -0.36f, 0), V(1.2f, 0.38f, 0.46f) * Th, steel, part);
                F.Ellipsoid(foot, V(-0.05f, -0.33f, 0), V(0.44f, 0.44f, 0.42f) * Th, steel, part);
                F.Cone(Skeleton.Leg(s, 1), V(0, -2.6f, 0), 0.44f * L, V(0, -3.5f, 0), 0.42f * L, steel, part);
            }
        }

        // -------------------------------------------------------------- espalda

        /// <summary>
        /// La mochila del peregrino (lona, la manta enrollada arriba y la concha de Santiago en la
        /// tapa) o el morral del buhonero (más grande, con una olla, un farol y un cucharón colgando);
        /// las correas pasan por los hombros.
        /// </summary>
        public void Pack(bool peddler, int coins = 0)
        {
            int canvas = Mat("lona", peddler ? 0x6E5A3Eu : 0x8A7A5Au, tex: Folds);
            int wool = Mat("manta", 0x7A3A2A, tex: (p, t) => MathF.Sin(p.Z * 9) > 0.6f && t > 0 ? t - 1 : t);
            int strap = Mat("correas", Leather);
            int part = F.Part();
            float back = -(D.ChestF + (peddler ? 0.7f : 0.55f));
            var half = peddler ? V(0.6f, 1.35f, 1.05f) : V(0.45f, 1.05f, 0.85f);
            F.Box(Bone.Spine, V(back, 1.9f, 0), half, canvas, part);
            F.Box(Bone.Spine, V(back - half.X * 0.2f, 1.9f + half.Y - 0.2f, 0), V(half.X * 0.9f, 0.25f, half.Z * 1.02f), canvas, part);
            F.Cone(Bone.Spine, V(back, 1.9f + half.Y + 0.3f, -half.Z - 0.2f), 0.38f, V(back, 1.9f + half.Y + 0.3f, half.Z + 0.2f), 0.38f, wool, part);
            foreach (int s in new[] { 1, -1 })
            {
                F.Cone(Bone.Spine, V(back + 0.3f, 2.9f, s * 0.55f), 0.1f, V(0.1f, 3.35f, s * 0.62f), 0.1f, strap, part);
                F.Cone(Bone.Spine, V(0.1f, 3.35f, s * 0.62f), 0.1f, V(D.ChestF + 0.1f, 2.0f, s * 0.6f), 0.1f, strap, part);
            }
            if (coins > 0) Coins(back, half, coins);
            if (!peddler)
            {
                // La concha de Santiago cosida en la tapa.
                int shell = Mat("concha", 0xF0E6D0, tex: (p, t) => MathF.Sin(MathF.Atan2(p.Z, p.Y - 1.6f) * 11) > 0.4f && t > 0 ? t - 1 : t);
                F.Ellipsoid(Bone.Spine, V(back - half.X - 0.05f, 1.95f, 0), V(0.07f, 0.36f, 0.38f), shell, F.Part());
                return;
            }
            int iron = Mat("olla", 0x2E2C2E, shiny: true);
            int copper = Mat("cobre", 0xB06A3A, shiny: true);
            int glow = Mat("luz del farol", 0xFFD890, lift: 0.8f);
            F.Ellipsoid(Bone.Spine, V(back, 1.3f, half.Z + 0.3f), V(0.42f, 0.34f, 0.3f), iron, F.Part());
            F.Cone(Bone.Spine, V(back - 0.2f, 1.8f, -half.Z - 0.1f), 0.06f, V(back - 0.1f, 0.6f, -half.Z - 0.25f), 0.06f, copper, part);
            F.Ellipsoid(Bone.Spine, V(back - 0.1f, 0.5f, -half.Z - 0.25f), V(0.2f, 0.12f, 0.2f), copper, part);
            int lamp = F.Part();
            F.Box(Bone.Spine, V(back - half.X - 0.2f, 1.0f, 0.4f), V(0.2f, 0.26f, 0.2f), iron, lamp);
            F.Ellipsoid(Bone.Spine, V(back - half.X - 0.3f, 1.0f, 0.4f), V(0.1f, 0.14f, 0.12f), glow, F.Part());
        }

        /// <summary>
        /// Los denarios del grupo en la mochila: con algunos asoman un par de monedas entre la tapa y
        /// la manta; con bastantes, un montón arriba; repleta, el montón se desborda por la tapa y
        /// quedan monedas trabadas en las correas y colgando del costado.
        /// </summary>
        private void Coins(float back, Vector3 half, int level)
        {
            // El oro brilla apenas (en lo oscuro de las cárceles se ve igual).
            int gold = Mat("denarios", 0xE0B848, shiny: true, lift: 0.3f);
            int deep = Mat("denarios de abajo", 0xA87C2C, lift: 0.15f);
            int part = F.Part();
            float top = 1.9f + half.Y;
            // Detrás de la manta enrollada (del lado de afuera), que no la tape.
            float x = back - half.X * 0.72f;
            void Edge(float dx, float y, float z) => F.Ellipsoid(Bone.Spine, V(x + dx, y, z), V(0.12f, 0.3f, 0.3f), gold, part);
            void Flat(float dx, float y, float z) => F.Ellipsoid(Bone.Spine, V(x + dx, y, z), V(0.3f, 0.08f, 0.3f), gold, part);
            // Algunas: asoman de canto entre la tapa y la manta.
            Edge(0, top + 0.3f, -half.Z * 0.45f);
            Edge(-0.05f, top + 0.28f, half.Z * 0.35f);
            Edge(0.05f, top + 0.34f, 0);
            if (level >= 2)
            {
                // Bastantes: un montón que se ve por encima.
                F.Ellipsoid(Bone.Spine, V(x, top + 0.3f, 0), V(half.X * 0.5f, 0.4f, half.Z * 0.78f), deep, part);
                Flat(-0.1f, top + 0.66f, -0.35f);
                Flat(0.08f, top + 0.7f, 0.3f);
                Edge(-0.1f, top + 0.72f, 0.02f);
            }
            if (level >= 3)
            {
                // Repleta: el montón sube por encima de la manta, se derrama por la tapa y quedan
                // monedas trabadas en las correas y colgando de los costados.
                F.Ellipsoid(Bone.Spine, V(x - 0.1f, top + 0.5f, 0), V(half.X * 0.62f, 0.58f, half.Z * 0.95f), deep, part);
                for (int i = 0; i < 6; i++) Flat(-0.25f + 0.12f * (i % 3), top + 1.0f + 0.06f * (i % 2), -0.62f + 0.25f * i);
                Edge(-0.2f, top + 1.12f, -0.15f);
                Edge(0.05f, top + 1.08f, 0.4f);
                // Por la tapa, de afuera: las que se escapan.
                for (int i = 0; i < 3; i++)
                    F.Ellipsoid(Bone.Spine, V(back - half.X - 0.08f, top - 0.25f - 0.35f * i, -0.3f + 0.3f * i), V(0.07f, 0.26f, 0.26f), gold, part);
                foreach (int s in new[] { 1, -1 })
                {
                    F.Ellipsoid(Bone.Spine, V(back + 0.35f, 2.6f, s * 0.62f), V(0.2f, 0.2f, 0.06f), gold, part);
                    F.Ellipsoid(Bone.Spine, V(back - half.X * 0.3f, top - 0.4f, s * (half.Z + 0.05f)), V(0.24f, 0.24f, 0.07f), gold, part);
                }
            }
        }

        // -------------------------------------------------------------- texturas

        private static int Folds(Vector3 p, int t) => MathF.Sin(p.Z * 8 + p.X * 3) > 0.8f && t > 1 ? t - 1 : t;
    }

    /// <summary>Pespunte en rombos (el jubón).</summary>
    private static int Quilt(Vector3 p, int t) => (MathF.Sin((p.Y + p.Z) * 6) > 0.88f || MathF.Sin((p.Y - p.Z) * 6) > 0.88f) && t > 1 ? t - 1 : t;

    /// <summary>Anillos de malla: un damero chico que brilla alternado.</summary>
    private static int Mail(Vector3 p, int t) => (((int)MathF.Floor(p.Y * 7) + (int)MathF.Floor((p.Z + p.X) * 7)) & 1) == 0 && t > 1 ? t - 1 : t;
}
