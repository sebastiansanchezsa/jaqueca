using System.Numerics;
using Jaqueca.Figures.Model;
using Jaqueca.Figures.Rig;
using Jaqueca.Sprites;
using static Jaqueca.Figures.Model.Figure;

namespace Jaqueca.Figures.Content;

/// <summary>
/// Medidas y materiales de un arma de hoja: cada espada es un juego de estos números sobre
/// el mismo armado (hoja con punta, guarda, mango y pomo).
/// </summary>
public sealed class BladeSpec
{
    /// <summary>Largo de la hoja desde la guarda hasta la punta, y largo de la punta (donde se afina).</summary>
    public float Length = 6.2f, Point = 1.0f;
    /// <summary>Ancho de la hoja junto a la guarda, y cuánto queda (fracción) donde empieza la punta.</summary>
    public float Width = 0.62f, Taper = 0.8f;
    /// <summary>Ancho total de la guarda (de punta a punta), largo del mango y radio del pomo.</summary>
    public float Guard = 1.9f, Grip = 1.15f, Pommel = 0.3f;
    public uint Steel = 0x8C969E, Hilt = 0x6E6860, Wrap = 0x4E3223;
    /// <summary>Color de la estela que deja la hoja al golpear.</summary>
    public uint Trail = 0xA9C8EA;
    /// <summary>
    /// Hoja de un solo filo (un cuchillo de cocina): el lomo es recto y el filo baja en panza y
    /// sube curvo hasta la punta. Si no, la hoja es de doble filo y se angosta pareja.
    /// </summary>
    public bool Single;
    /// <summary>Cuánto está oxidada (0 nada, 1 casi toda) y el color del óxido.</summary>
    public float Rust;
    public uint RustRgb = 0x6E3E26;
    /// <summary>Muescas en el filo (cuántas; 0 = ninguna).</summary>
    public int Notches;
    /// <summary>Hoja ondulada como una llama (la flamígera): cuánto ondulan los filos.</summary>
    public float Wave;
    /// <summary>Sin punta: la hoja termina redondeada (la espada de verdugo).</summary>
    public bool Blunt;
    /// <summary>Una canaleta o un grabado a lo largo del medio de la hoja (0 = no), de este color.</summary>
    public uint Fuller;
    /// <summary>Luz propia de la hoja (0 nada; las de luz, ~0.4).</summary>
    public float SteelLift;
    /// <summary>Luz propia de la canaleta (menos de 0: la de la hoja). La Hoguera: hierro negro con el corazón al rojo.</summary>
    public float FullerLift = -1;
    /// <summary>Los filos de otro color (0 = el de la hoja) y su luz: al rojo, escarchados.</summary>
    public uint Edge;
    public float EdgeLift;
    /// <summary>Grietas que serpentean por la hoja, de este color (0 = ninguna): el hierro que arde por dentro.</summary>
    public uint Veins;
}

/// <summary>
/// Medidas y materiales de un arco: media vara (del puño a cada punta), cuánto se curvan las
/// puntas hacia el lado de la cuerda, grosor y colores.
/// </summary>
public sealed class BowSpec
{
    public float Half = 5.2f, Depth = 1.5f, Thick = 0.21f;
    public uint Wood = 0x8A5A32, Wrap = 0x4E3322, String = 0xE2DCCB;
    /// <summary>Las puntas (culatines de cuerno o metal; 0 = sin) y cuánto se vuelven hacia afuera (arco recurvo).</summary>
    public uint Tips;
    public float Recurve;
    /// <summary>Cuánta luz propia tienen la cuerda y la vara (una cuerda de luz, ~0.6), y si la vara brilla (cuerno, bronce).</summary>
    public float StringLift = 0.2f, WoodLift;
    public bool WoodShiny;
    /// <summary>Una cruz de espada en el puño (el arco que fue espada).</summary>
    public bool Cross;
}

/// <summary>
/// Modelos de las armas, colgados de la mano derecha. Se agarran con el puño: el mango cruza
/// la mano de atrás hacia adelante (eje X del hueso), así con el brazo colgando la hoja apunta
/// adelante; los filos quedan arriba y abajo (eje Y) y las caras a los costados (eje Z). Las del
/// botín están en WeaponModels.Catalog.cs.
/// </summary>
public static partial class WeaponModels
{
    /// <summary>Centro del puño en el hueso de la mano (por donde pasa el mango).</summary>
    public static readonly Vector3 Fist = V(0.05f, -0.64f, 0);

    private static readonly Dictionary<string, BladeSpec> BaseBlades = new()
    {
        ["espada_comun"] = new(),
        // Cuchillo de goblin: corto, ancho y romo, de hierro oxidado con el mango atado con trapo.
        ["cuchillo_goblin"] = new() { Length = 3.0f, Point = 0.7f, Width = 0.72f, Taper = 1.0f, Guard = 0.75f, Grip = 0.9f, Pommel = 0.22f, Steel = 0x8E8479, Hilt = 0x5B4633, Wrap = 0x3C2E22, Trail = 0xC9B79A },
        // Cuchillo de familiar: uno de cocina, común y corriente, de un solo filo y con panza; oxidado,
        // con el mango de madera negra y sin guarda (apenas el recalce). Para su tamaño es enorme.
        ["cuchillo_familiar"] = new()
        {
            Length = 3.5f, Point = 1.4f, Width = 0.72f, Guard = 0.3f, Grip = 1.15f, Pommel = 0.14f, Single = true, Rust = 0.4f,
            Steel = 0x77736B, Hilt = 0x4F4A44, Wrap = 0x3A2A1E, RustRgb = 0x6A3A22, Trail = 0xBDB29A,
        },
        // Misericordias del pícaro: dagas finas y largas de punta (para meterla entre las placas), guarda
        // chica de hierro negro y mango de cuero oscuro. Van de a dos, una en cada mano.
        ["misericordias"] = new()
        {
            Length = 2.8f, Point = 1.0f, Width = 0.34f, Taper = 0.72f, Guard = 0.85f, Grip = 1.0f, Pommel = 0.2f,
            Steel = 0x9AA3AA, Hilt = 0x4A4038, Wrap = 0x2E2420, Trail = 0xC8D2DE,
        },
    };

    private static readonly Dictionary<string, BowSpec> BaseBows = new()
    {
        ["arco_corto"] = new(),
    };

    private static Dictionary<string, BladeSpec> _blades;
    private static Dictionary<string, BowSpec> _bows;

    /// <summary>Las armas de hoja (espadas y dagas): las de siempre y las del botín.</summary>
    public static IReadOnlyDictionary<string, BladeSpec> Blades => _blades ??= BaseBlades.ToDictionary(k => k.Key, k => k.Value);
    public static IReadOnlyDictionary<string, BowSpec> Bows => _bows ??= BaseBows.ToDictionary(k => k.Key, k => k.Value);

    /// <summary>El modelo de un ID visual en la mano de ese lado (1 derecha, -1 izquierda; null si no hay modelo).</summary>
    public static Figure Build(string id, int side = 1) =>
        id == null ? null
        : Blades.TryGetValue(id, out var b) ? Blade(b, side)
        : Bows.TryGetValue(id, out var bow) ? Bow(bow)
        : id switch
        {
            "nudilleras" => Knuckles(side),
            "cayado" => Staff(),
            "cetro_hueso" => Scepter(),
            "lanza_tarkariana" => TarkarianSpear(),
            "baston_tarkariano" => TarkarianStaff(),
            _ => null,
        };

    /// <summary>La punta de arriba del arco y la cuerda en reposo (donde va la flecha), en el hueso de la mano.</summary>
    public static Vector3 BowTip(BowSpec b) => V(b.Half, Fist.Y + b.Depth, 0);
    public static Vector3 StringRest(BowSpec b) => V(0, Fist.Y + b.Depth, 0);

    /// <summary>
    /// Arco: la vara pasa por el puño (eje X de la mano) y se curva hacia la cuerda (+Y), que va
    /// de punta a punta pasando por la muesca (el hueso <see cref="Bone.Nock"/>): en reposo la
    /// muesca está sobre la cuerda recta; tensando, en la mano que tira, y la cuerda hace la V.
    /// </summary>
    public static Figure Bow(BowSpec b)
    {
        var f = new Figure();
        int wood = f.AddMat("madera del arco", MatChannel.Fixed, b.Wood, shiny: b.WoodShiny, flat: b.WoodLift > 0 ? 0.3f : 0, lift: b.WoodLift);
        int wrap = f.AddMat("empuñadura", MatChannel.Fixed, b.Wrap);
        int cord = f.AddMat("cuerda", MatChannel.Fixed, b.String, flat: 0.5f, lift: b.StringLift);
        int tips = b.Tips != 0 ? f.AddMat("puntas", MatChannel.Fixed, b.Tips, shiny: true) : -1;
        int part = f.Part();
        var hand = Skeleton.Arm(1, 2);
        float y = Fist.Y;
        foreach (int s in new[] { 1, -1 })
        {
            var mid = V(s * b.Half * 0.55f, y + b.Depth * 0.3f, 0);
            var tip = V(s * b.Half, y + b.Depth, 0);
            f.Cone(hand, V(0, y, 0), b.Thick, mid, b.Thick * 0.85f, wood, part);
            f.Cone(hand, mid, b.Thick * 0.85f, tip, b.Thick * 0.55f, wood, part);
            f.Cord(hand, tip, Bone.Nock, Vector3.Zero, 0.07f, cord, part);
            // Recurvo: pasada la cuerda, la punta se vuelve hacia afuera.
            var end = tip;
            if (b.Recurve > 0)
            {
                end = tip + V(s * b.Recurve * 0.8f, -b.Recurve, 0);
                f.Cone(hand, tip, b.Thick * 0.55f, end, b.Thick * 0.45f, tips >= 0 ? tips : wood, part);
            }
            if (tips >= 0) f.Ellipsoid(hand, end, V(0.17f, 0.17f, 0.15f), tips, part);
        }
        f.Cone(hand, V(-0.45f, y, 0), b.Thick * 1.35f, V(0.45f, y, 0), b.Thick * 1.35f, wrap, part);
        // El arco que fue espada: la cruz de la guarda atraviesa el puño.
        if (b.Cross) f.Box(hand, V(0, y - 0.1f, 0), V(0.12f, 0.2f, 0.75f), tips >= 0 ? tips : wood, part);
        return f;
    }

    /// <summary>Dónde está la guarda, adelante del puño.</summary>
    private const float GuardX = 0.6f;

    /// <summary>La punta de la hoja en el hueso de la mano (para golpes, estelas y pruebas).</summary>
    public static Vector3 Tip(BladeSpec b) => V(GuardX + 0.1f + b.Length, Fist.Y, 0);

    public static Figure Blade(BladeSpec b, int side = 1)
    {
        var f = new Figure();
        int steel = f.AddMat("acero", MatChannel.Fixed, b.Steel, shiny: true, flat: b.SteelLift > 0 ? 0.35f : 0, lift: b.SteelLift);
        float fullerLift = b.FullerLift >= 0 ? b.FullerLift : b.SteelLift;
        int fuller = b.Fuller != 0 ? f.AddMat("grabado", MatChannel.Fixed, b.Fuller, shiny: true, flat: fullerLift > 0 ? 0.35f : 0, lift: fullerLift) : -1;
        int edgeMat = b.Edge != 0 ? f.AddMat("filo", MatChannel.Fixed, b.Edge, shiny: true, flat: b.EdgeLift > 0 ? 0.4f : 0, lift: b.EdgeLift) : -1;
        int veinMat = b.Veins != 0 ? f.AddMat("grietas", MatChannel.Fixed, b.Veins, flat: 0.5f, lift: 0.75f) : -1;
        int hilt = f.AddMat("guarda", MatChannel.Fixed, b.Hilt, shiny: true);
        int wrap = f.AddMat("mango", MatChannel.Fixed, b.Wrap);
        // La estela es plana y clara: brilla igual desde cualquier lado.
        int trail = f.AddMat("estela", MatChannel.Fixed, b.Trail, flat: 1, lift: 0.4f);
        int rust = b.Rust > 0 ? f.AddMat("óxido", MatChannel.Fixed, b.RustRgb) : -1;
        int part = f.Part();
        var hand = Skeleton.Arm(side, 2);
        float y = Fist.Y;

        // Hoja: una lámina fina que se angosta hacia la punta (la recorta la máscara). De un solo
        // filo, el lomo queda recto arriba y el filo baja en panza y sube en curva hasta la punta.
        float start = GuardX + 0.1f, end = start + b.Length, pointAt = end - b.Point;
        float half = b.Width / 2;
        float spine = b.Single ? 0.55f * half : half, belly = b.Single ? 1.45f * half : half;
        Func<Vector3, bool> edge = b.Single
            ? p =>
            {
                float dy = p.Y - y;
                if (p.X >= pointAt)
                {
                    float t = Math.Clamp((p.X - pointAt) / b.Point, 0, 1);
                    float top = spine * (1 - t * t * 0.35f);
                    return dy <= top && dy >= top - (top + belly) * MathF.Sqrt(1 - t * t);
                }
                return dy <= spine && dy >= -belly;
            }
            : p => MathF.Abs(p.Y - Mid(p)) <= Half(p);
        // El medio ancho de la hoja de doble filo en cada punto (y dónde queda su medio, que en la flamígera ondula).
        float Half(Vector3 p)
        {
            float w;
            if (p.X < pointAt) w = half * (1 - (1 - b.Taper) * (p.X - start) / (pointAt - start));
            else if (b.Blunt)
            {
                // Sin punta: termina en un arco.
                float t = (p.X - pointAt) / b.Point;
                w = half * b.Taper * MathF.Sqrt(MathF.Max(0, 1 - t * t));
            }
            else w = half * b.Taper * (end - p.X) / b.Point;
            // La flamígera: los dos filos ondulan juntos (la hoja se tuerce como una llama).
            if (b.Wave > 0) w *= 1 + 0.18f * MathF.Sin((p.X - start) * 6.4f + 1.2f) * (p.X < pointAt ? 1 : 0);
            return w;
        }
        float Mid(Vector3 p) => b.Wave > 0 && p.X < pointAt ? y + b.Wave * MathF.Sin((p.X - start) * 3.2f) : y;
        if (b.Notches > 0)
        {
            // Las muescas: mordiscos en el filo de abajo, parejos pero no iguales.
            var whole = edge;
            edge = p =>
            {
                if (!whole(p)) return false;
                if (p.Y - y > -half * 0.45f) return true;
                for (int i = 0; i < b.Notches; i++)
                {
                    float nx = start + 0.5f + (pointAt - start - 0.8f) * (i + 0.3f * MathF.Sin(i * 2.3f)) / b.Notches;
                    float depth = half * (0.35f + 0.25f * MathF.Abs(MathF.Sin(i * 1.7f)));
                    if (MathF.Abs(p.X - nx) < 0.12f && p.Y - y < -half + depth) return false;
                }
                return true;
            };
        }
        var bladeC = V((start + end) / 2, y + (spine - belly) / 2, 0);
        var bladeHalf = V(b.Length / 2, (spine + belly) / 2, 0.1f);
        var blade = f.Box(hand, bladeC, bladeHalf, steel, part);
        blade.Thin = true;
        blade.Mask = edge;
        // La canaleta (o el grabado) por el medio de la hoja; los filos, de su color (de doble filo).
        if (fuller >= 0 || edgeMat >= 0 || veinMat >= 0)
            blade.Paint = (p, m) =>
            {
                if (edgeMat >= 0 && !b.Single && MathF.Abs(p.Y - Mid(p)) > Half(p) - 0.075f) return edgeMat;
                if (veinMat >= 0 && p.X > start + 0.2f && p.X < end - 0.3f)
                {
                    // Dos grietas que serpentean de la guarda a la punta, cada una por su lado de la canaleta.
                    float u = p.X - start;
                    for (int k = 0; k < 2; k++)
                    {
                        float c = y + (k == 0 ? 1 : -1) * Half(p) * (0.5f + 0.25f * MathF.Sin(u * (2.1f + k) + k * 2.3f) + 0.1f * MathF.Sin(u * 7.3f + k));
                        if (MathF.Abs(p.Y - c) < 0.045f) return veinMat;
                    }
                }
                if (fuller >= 0 && MathF.Abs(p.Y - y) < half * 0.22f && p.X > start + 0.3f && p.X < pointAt - 0.2f) return fuller;
                return m;
            };
        // Óxido: manchas que comen el lomo y el talón; el filo, que se afila, queda limpio.
        if (rust >= 0)
            blade.Paint = (p, m) =>
            {
                float n = MathF.Sin(p.X * 5.3f + 1.7f) * MathF.Sin(p.Y * 9.1f - p.X * 2.3f) + 0.55f * MathF.Sin(p.X * 13.7f + p.Y * 6.1f);
                float fromEdge = (p.Y - y + belly) / (spine + belly);
                float heel = 1 - Math.Clamp((p.X - start) / b.Length, 0, 1);
                return n + 0.9f * fromEdge + 0.7f * heel > 2.3f - 1.2f * b.Rust ? rust : m;
            };
        // Estela: la misma hoja, dibujada en las posiciones por donde pasó (misma parte: sin contorno entre las dos).
        var smear = f.Box(hand, bladeC, bladeHalf, trail, part);
        smear.Thin = true;
        smear.Mask = edge;
        smear.Smear = true;

        // Guarda en cruz (en el plano de los filos), mango envuelto en cuero y pomo.
        f.Box(hand, V(GuardX, y, 0), V(0.13f, b.Guard / 2, 0.18f), hilt, part).Thin = true;
        f.Cone(hand, V(-b.Grip / 2, y, 0), 0.16f, V(GuardX - 0.1f, y, 0), 0.16f, wrap, part);
        f.Ellipsoid(hand, V(-b.Grip / 2 - 0.2f, y, 0), V(b.Pommel, b.Pommel, b.Pommel), hilt, part);
        return f;
    }

    /// <summary>
    /// Nudilleras de hierro (del blasfemo): una barra sobre los cuatro nudillos con un tope por
    /// dedo, la empuñadura escondida en la palma y una placa al costado del meñique. El puño pega
    /// con el extremo de la mano (-Y del hueso), así la barra va ahí, a lo ancho (eje X).
    /// </summary>
    public static Figure Knuckles(int side)
    {
        var f = new Figure();
        int iron = f.AddMat("hierro", MatChannel.Fixed, 0x6A6E72, shiny: true);
        int dark = f.AddMat("hierro negro", MatChannel.Fixed, 0x3E4044, shiny: true);
        int part = f.Part();
        var hand = Skeleton.Arm(side, 2);
        f.Box(hand, V(0.08f, -1.1f, 0), V(0.42f, 0.09f, 0.26f), iron, part);
        for (int i = 0; i < 4; i++)
            f.Ellipsoid(hand, V(-0.22f + 0.2f * i, -1.2f, 0), V(0.09f, 0.08f, 0.22f), dark, part);
        f.Box(hand, V(-0.36f, -0.8f, 0), V(0.06f, 0.36f, 0.22f), iron, part);
        return f;
    }

    /// <summary>
    /// La lanza de los tarkarianos: un asta larga de caña dura, con la punta de piedra verde tallada
    /// atada con tiento, dos plumas colgando debajo de la punta y el regatón forrado en cuero. Se
    /// lleva como el cayado (de pie, la punta arriba), así que la punta queda por encima de la cresta.
    /// </summary>
    public static Figure TarkarianSpear()
    {
        var f = new Figure();
        int wood = f.AddMat("asta de caña", MatChannel.Fixed, 0x8A6A40, tex: Bark);
        int wrap = f.AddMat("tiento", MatChannel.Fixed, 0x5A3A24);
        int stone = f.AddMat("piedra verde tallada", MatChannel.Fixed, 0x3E7A62, shiny: true, lift: 0.08f);
        int feather = f.AddMat("pluma roja", MatChannel.Fixed, 0xB0402A);
        int feather2 = f.AddMat("pluma clara", MatChannel.Fixed, 0xD8CCA8);
        int part = f.Part();
        var hand = Skeleton.Arm(1, 2);
        float y = Fist.Y, below = StaffBelow - 0.4f, above = 5.2f;
        f.Cone(hand, V(-below, y, 0), 0.15f, V(above, y, 0), 0.13f, wood, part);
        // El regatón y las ataduras.
        f.Cone(hand, V(-below, y, 0), 0.19f, V(-below + 0.9f, y, 0), 0.18f, wrap, part);
        f.Cone(hand, V(above - 0.35f, y, 0), 0.18f, V(above + 0.1f, y, 0), 0.17f, wrap, part);
        f.Cone(hand, V(0.6f, y, 0), 0.18f, V(1.3f, y, 0), 0.18f, wrap, part);
        // La punta: una hoja de piedra en rombo, chata en Z.
        var tip = f.Ellipsoid(hand, V(above + 0.95f, y, 0), V(1.05f, 0.34f, 0.08f), stone, part);
        tip.Mask = p => MathF.Abs(p.Y - y) < 0.34f * (1 - MathF.Abs(p.X - (above + 0.95f)) / 1.05f) + 0.04f;
        // Dos plumas colgando de la atadura.
        foreach (int s in new[] { 1, -1 })
        {
            var root = V(above - 0.2f, y, s * 0.16f);
            f.Cone(hand, root, 0.05f, root + V(-1.2f, 0.12f, s * 0.2f), 0.12f, s > 0 ? feather : feather2, part).Thin = true;
        }
        return f;
    }

    /// <summary>El bastón de los ancianos tarkarianos: una rama retorcida con una piedra verde atada arriba entre raíces.</summary>
    public static Figure TarkarianStaff()
    {
        var f = new Figure();
        int wood = f.AddMat("rama retorcida", MatChannel.Fixed, 0x6A4A30, tex: Bark);
        int stone = f.AddMat("piedra verde", MatChannel.Fixed, 0x3E8A6A, shiny: true, lift: 0.25f);
        int wrap = f.AddMat("tiento", MatChannel.Fixed, 0x5A3A24);
        int shell = f.AddMat("caracolitos", MatChannel.Fixed, 0xE8E0CC);
        int part = f.Part();
        var hand = Skeleton.Arm(1, 2);
        float y = Fist.Y;
        const int n = 8;
        Vector3 At(int i) => V(-StaffBelow + (StaffBelow + 3.4f) * i / n, y + 0.1f * MathF.Sin(i * 1.7f), 0.12f * MathF.Sin(i * 2.3f + 0.5f));
        for (int i = 0; i < n; i++) f.Cone(hand, At(i), 0.19f, At(i + 1), 0.2f, wood, part);
        var top = At(n);
        // Tres raíces que abrazan la piedra.
        for (int k = 0; k < 3; k++)
        {
            float a = k * MathF.Tau / 3;
            var side = V(0, MathF.Cos(a), MathF.Sin(a)) * 0.34f;
            f.Cone(hand, top, 0.12f, top + V(0.55f, 0, 0) + side, 0.09f, wood, part);
            f.Cone(hand, top + V(0.55f, 0, 0) + side, 0.09f, top + V(1.15f, 0, 0) + side * 0.4f, 0.05f, wood, part);
        }
        f.Ellipsoid(hand, top + V(0.7f, 0, 0), V(0.36f, 0.3f, 0.3f), stone, part);
        f.Cone(hand, top - V(0.5f, 0, 0), 0.24f, top - V(0.1f, 0, 0), 0.24f, wrap, part);
        for (int k = 0; k < 3; k++)
            f.Ellipsoid(hand, top - V(0.9f + 0.25f * k, 0, 0) + V(0, -0.1f, -0.25f), V(0.1f, 0.12f, 0.1f), shell, part);
        return f;
    }

    /// <summary>Del puño hacia abajo y hacia arriba en el cayado (a lo largo del eje X de la mano).</summary>
    public const float StaffBelow = 10.0f, StaffAbove = 3.0f;

    /// <summary>
    /// El cayado de la curandera: avellano nudoso (la vara no es recta: se tuerce apenas entre
    /// nudo y nudo), la punta curvada hacia adelante, un manojo de hierbas secas atado con hilo
    /// debajo de la curva y un frasquito de barro colgando. Se lleva de pie: el puño en el eje de
    /// la vara, la curva hacia adelante (eje Z de la mano).
    /// </summary>
    public static Figure Staff()
    {
        var f = new Figure();
        int wood = f.AddMat("avellano", MatChannel.Fixed, 0x6E5034, tex: Bark);
        int knot = f.AddMat("nudos", MatChannel.Fixed, 0x5A3E28);
        int twine = f.AddMat("hilo", MatChannel.Fixed, 0xB09A6A);
        int herb = f.AddMat("hierbas secas", MatChannel.Fixed, 0x6E7A3A);
        int flower = f.AddMat("lavanda", MatChannel.Fixed, 0x7A6A9A);
        int clay = f.AddMat("barro", MatChannel.Fixed, 0x9A5A3A);
        int part = f.Part();
        var hand = Skeleton.Arm(1, 2);
        float y = Fist.Y;
        // La vara, en tramos que se tuercen un poco.
        const int N = 9;
        Vector3 At(int i)
        {
            float x = -StaffBelow + (StaffBelow + StaffAbove) * i / N;
            return V(x, y + 0.07f * MathF.Sin(i * 1.9f), 0.08f * MathF.Sin(i * 2.7f + 1));
        }
        for (int i = 0; i < N; i++)
        {
            float r0 = 0.2f + 0.03f * i / N, r1 = 0.2f + 0.03f * (i + 1) / N;
            f.Cone(hand, At(i), r0, At(i + 1), r1, wood, part);
            if (i % 3 == 1) f.Ellipsoid(hand, At(i + 1) + V(0, 0.05f, 0.06f), V(0.22f, 0.3f, 0.3f), knot, part);
        }
        // La curva de arriba, hacia adelante.
        var top = At(N);
        var c1 = top + V(0.55f, 0, 0.25f);
        var c2 = top + V(0.8f, 0, 0.75f);
        var c3 = top + V(0.6f, 0, 1.15f);
        f.Cone(hand, top, 0.23f, c1, 0.22f, wood, part);
        f.Cone(hand, c1, 0.22f, c2, 0.2f, wood, part);
        f.Cone(hand, c2, 0.2f, c3, 0.17f, wood, part);
        // El manojo de hierbas, atado con hilo, colgando hacia abajo alrededor de la vara.
        var tie = V(StaffAbove - 0.9f, y, 0);
        f.Cone(hand, tie - V(0.12f, 0, 0), 0.3f, tie + V(0.12f, 0, 0), 0.3f, twine, part);
        for (int i = 0; i < 7; i++)
        {
            float a = i * 0.9f;
            var root = tie + V(0.05f, 0.22f * MathF.Cos(a), 0.22f * MathF.Sin(a));
            var tip = root + V(-1.1f - 0.25f * (i % 3), 0.35f * MathF.Cos(a), 0.35f * MathF.Sin(a));
            f.Cone(hand, root, 0.08f, tip, 0.12f, i % 3 == 0 ? flower : herb, part).Thin = true;
        }
        // El frasquito, de un hilo.
        var hang = tie + V(-0.1f, 0, -0.3f);
        f.Cone(hand, hang, 0.04f, hang + V(-0.9f, 0, -0.1f), 0.04f, twine, part).Thin = true;
        f.Ellipsoid(hand, hang + V(-1.2f, 0, -0.12f), V(0.3f, 0.24f, 0.24f), clay, part);
        f.Cone(hand, hang + V(-0.95f, 0, -0.1f), 0.1f, hang + V(-0.85f, 0, -0.1f), 0.12f, knot, part);
        return f;
    }

    /// <summary>
    /// El cetro de la ocultista: un hueso largo (con las cabezas nudosas de un fémur), vendado de
    /// negro donde se agarra, un cráneo de cuervo en la punta con el pico hacia adelante (eje Z de
    /// la mano) y una piedra violeta que brilla en cada ojo; de abajo del cráneo cuelga una sarta
    /// de cuentas de hueso.
    /// </summary>
    public static Figure Scepter()
    {
        var f = new Figure();
        int bone = f.AddMat("hueso", MatChannel.Fixed, 0xC8BFAE);
        int old = f.AddMat("hueso viejo", MatChannel.Fixed, 0x9A907C);
        int cloth = f.AddMat("vendas negras", MatChannel.Fixed, 0x201C22, tex: Bands);
        int gem = f.AddMat("piedra del ojo", MatChannel.Fixed, 0xA46AE8, flat: 0.5f, lift: 0.45f);
        int part = f.Part();
        var hand = Skeleton.Arm(1, 2);
        float y = Fist.Y;
        f.Cone(hand, V(-1.3f, y, 0), 0.19f, V(3.0f, y, 0), 0.16f, bone, part);
        f.Ellipsoid(hand, V(-1.45f, y + 0.08f, 0), V(0.28f, 0.22f, 0.24f), old, part);
        f.Ellipsoid(hand, V(-1.4f, y - 0.14f, 0), V(0.22f, 0.18f, 0.2f), old, part);
        f.Cone(hand, V(-0.65f, y, 0), 0.23f, V(0.65f, y, 0), 0.23f, cloth, part);
        // El cráneo de cuervo: la bóveda, las cuencas y el pico largo.
        var skull = V(3.45f, y, 0);
        f.Ellipsoid(hand, skull, V(0.42f, 0.36f, 0.38f), bone, part);
        f.Ellipsoid(hand, skull + V(-0.1f, 0, 0.3f), V(0.3f, 0.26f, 0.26f), bone, part);
        f.Cone(hand, skull + V(-0.05f, 0, 0.45f), 0.2f, skull + V(-0.25f, 0, 1.6f), 0.04f, old, part);
        foreach (int s in new[] { 1, -1 })
            f.Ellipsoid(hand, skull + V(0.02f, s * 0.3f, 0.2f), V(0.1f, 0.06f, 0.1f), gem, part);
        // La sarta de cuentas, colgando por el costado.
        for (int i = 0; i < 5; i++)
            f.Ellipsoid(hand, V(2.9f - 0.32f * i, y + 0.28f, -0.1f + 0.05f * (i % 2)), V(0.09f, 0.08f, 0.08f), bone, part);
        return f;
    }

    /// <summary>Corteza: vetas a lo largo de la vara.</summary>
    private static int Bark(Vector3 p, int t) => MathF.Sin(MathF.Atan2(p.Z, p.Y + 0.64f) * 5 + p.X * 0.8f) > 0.6f && t > 0 ? t - 1 : t;

    /// <summary>Vueltas de venda.</summary>
    private static int Bands(Vector3 p, int t) => MathF.Sin(p.X * 12) > 0.4f && t > 0 ? t - 1 : t;
}
