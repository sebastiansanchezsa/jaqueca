using System.Numerics;
using Jaqueca.Figures.Content;
using Jaqueca.Figures.Model;
using Jaqueca.Figures.Render;
using Jaqueca.Figures.Rig;
using Jaqueca.Look;
using Jaqueca.Sprites;

namespace Jaqueca.Figures.Live;

/// <summary>
/// La figura completa de un personaje para dibujarlo en vivo: cuerpo, pelo, ojos (y equipo)
/// unidos en una sola figura. Cada capa conserva sus propias partes, así los contornos y
/// pliegues entre prendas salen igual que al componer las capas horneadas.
/// </summary>
public sealed class LiveFigure
{
    public Figure Fig;
    public Skeleton Skel;
    public Appearance Look;
    /// <summary>A qué escala se dibuja la figura (se arma a la medida de referencia; el esqueleto ya viene a esta escala).</summary>
    public float Scale = 1;
    /// <summary>Rampa de cada material de la figura, ya con los colores del jugador.</summary>
    public Ramp[] Ramps;

    public int Materials => Fig.Mats.Count;

    /// <summary>
    /// Fila de paleta de un material (8 colores): los 5 tonos, el contorno y los parámetros
    /// del material (R = lift + 0,5; G = flat; B = brillo), en 0xAARRGGBB.
    /// </summary>
    public void PaletteRow(int mat, Span<uint> row)
    {
        var r = Ramps[mat];
        var d = Fig.Mats[mat].Def;
        for (int t = 0; t < 5; t++) row[t] = r[t];
        row[5] = Col.Mix(Composer.Ink, r[0], 0.3f);
        row[6] = Col.Make((int)Math.Clamp((d.Lift + 0.5f) * 255, 0, 255), (int)Math.Clamp(d.Flat * 255, 0, 255), d.Shiny ? 255 : 0);
        row[7] = 0xFF000000;
    }
}

public static class FigureSet
{
    /// <summary>Arma la figura de una apariencia (se rehace sólo cuando cambia el equipo o el creador).</summary>
    public static LiveFigure Build(Appearance look, Palette palette)
    {
        var fig = new Figure();
        var body = Body.Build(look.Build, look.Outfit);
        var bodyParts = Merge(fig, body, out int bodyOff);
        // La cara: las partes que marca la ropa (si la cabeza lleva capucha, sólo la piel) o, si no
        // marca ninguna, todas las de la cabeza del cuerpo (donde caen los ojos).
        if (body.StampParts.Count > 0)
            foreach (int p in body.StampParts) fig.StampParts.Add(bodyParts[p]);
        else
            foreach (var s in body.Shapes)
                if (s.Bone == Bone.Head) fig.StampParts.Add(bodyParts[s.Part]);
        int skin = bodyOff; // el primer material del cuerpo es la piel

        // El pelo, salvo lo que tapa la ropa (la capucha, todo; el pañuelo, lo de arriba).
        var hide = (HairHide)Math.Max((int)Outfits.Hides(look.Outfit), (int)GearModels.Hides(look.Helm));
        if (hide != HairHide.All) Merge(fig, Hair.Build(look.HairStyle), out _);
        if (hide == HairHide.Top) fig.HideHairTop = true;
        if (Hair.Beard(look.Beard) is { } beard) Merge(fig, beard, out _);

        var eyes = Face.Eyes(look.Eyes);
        Merge(fig, eyes, out int eyeOff);
        foreach (var st in eyes.Stamps)
        {
            var px = st.Pixels.Select(p => (p.dx, p.dy, p.mat + eyeOff, p.tone)).ToArray();
            // Parpadeo: el párpado es una línea de piel oscura donde estaba el ojo.
            var closed = new[] { (0, 0, skin, 1) };
            fig.Stamps.Add(new Stamp { Bone = st.Bone, At = st.At, Normal = st.Normal, MinFacing = st.MinFacing, Outward = st.Outward, Pixels = px, Closed = closed });
        }

        // El equipo puesto (armadura, reliquias, mochila), encima de la ropa.
        foreach (var id in look.Worn)
        {
            if (GearModels.Build(id, look.Build, look.Coins) is not { } g) continue;
            int from = fig.Shapes.Count;
            Merge(fig, g, out _);
            for (int i = from; i < fig.Shapes.Count; i++) fig.Shapes[i].Gear = id;
        }

        // Lo que lleva en la mano derecha (y lo que tiene un momento en la izquierda).
        AddWeapon(fig, look.MainHand);
        if (Props.Build(look.Prop) is { } prop) Merge(fig, prop, out _);

        // Los muñones se ubican con el esqueleto de referencia (como el resto de la figura); la gente
        // se anima y se dibuja a su escala (ver Dims.People).
        var lf = new LiveFigure { Fig = fig, Skel = new Skeleton(look.Build), Look = look.Clone() };
        Gore.AddStumps(fig, lf.Skel);
        lf.Skel = new Skeleton(look.Build, Dims.Of(look.Build).Scaled(Dims.People));
        lf.Scale = Dims.People;
        lf.Ramps = fig.Mats.Select(m => palette.Get(m.Def, look)).ToArray();
        return lf;
    }

    /// <summary>
    /// Una cosa para su retrato (el ícono del inventario, lo que queda tirado en el piso). Un arma
    /// va sola, acostada a lo largo como en la mano con los huesos quietos; una pieza del equipo (y
    /// los puños de pelea), puesta en un condenado sin otra cosa encima, que el retrato dibuja como
    /// una sombra (ver Render.ItemArt). Con los colores de <paramref name="look"/> (lo que lleva el
    /// color del jugador). null si no hay modelo.
    /// </summary>
    public static LiveFigure BuildPiece(string id, Appearance look, Palette palette)
    {
        var fig = new Figure();
        AddWeapon(fig, id);
        if (fig.Shapes.Count == 0) return null;
        var own = look.Clone();
        own.MainHand = id;
        return new LiveFigure
        {
            Fig = fig, Skel = new Skeleton(look.Build, Dims.Of(look.Build).Scaled(Dims.People)), Scale = Dims.People, Look = own,
            Ramps = fig.Mats.Select(m => palette.Get(m.Def, look)).ToArray(),
        };
    }

    /// <summary>
    /// La figura de pie y quieta (un animador que respira un segundo sin moverse), en el espacio del
    /// personaje (mirando a +X): para lo que se dibuja sin nadie que lo anime (el retrato de una
    /// pieza, lo que cuelga del maniquí). <paramref name="hand"/>: lo que empuña (con los puños, en
    /// guardia).
    /// </summary>
    public static Matrix4x4[] Standing(LiveFigure lf, WeaponFamily hand = WeaponFamily.None)
    {
        var a = new Anim.Animator(lf.Skel, Vector2.Zero, 0, 1) { MainHand = hand };
        for (int i = 0; i < 60; i++) a.Update(Vector2.Zero, Vector2.Zero, 0, 1 / 60f);
        return (Matrix4x4[])a.Bones.Clone();
    }

    /// <summary>
    /// Arma la figura de una criatura (goblins...): su cuerpo propio, con los ojos estampados, y
    /// el arma de la mano derecha. No tiene creador: sus materiales son de color fijo.
    /// </summary>
    public static LiveFigure BuildCreature(Figure body, Dims dims, string weapon, Palette palette, float scale = 1)
    {
        var fig = new Figure();
        var parts = Merge(fig, body, out int off);
        foreach (int p in body.StampParts) fig.StampParts.Add(parts[p]);
        foreach (var st in body.Stamps)
        {
            fig.Stamps.Add(new Stamp
            {
                Bone = st.Bone, At = st.At, Normal = st.Normal, MinFacing = st.MinFacing, Outward = st.Outward,
                Pixels = st.Pixels.Select(p => (p.dx, p.dy, p.mat + off, p.tone)).ToArray(),
                Closed = st.Closed?.Select(p => (p.dx, p.dy, p.mat + off, p.tone)).ToArray(),
            });
        }
        AddWeapon(fig, weapon);
        var look = new Appearance { MainHand = weapon };
        var lf = new LiveFigure { Fig = fig, Skel = new Skeleton(Look.Build.Slim, dims), Look = look };
        Gore.AddStumps(fig, lf.Skel);
        // Lo que se arma a una medida y se dibuja a otra (el alcaide): el esqueleto va a la escala del dibujo.
        if (scale != 1)
        {
            lf.Skel = new Skeleton(Look.Build.Slim, dims.Scaled(scale));
            lf.Scale = scale;
        }
        lf.Ramps = fig.Mats.Select(m => palette.Get(m.Def, look)).ToArray();
        return lf;
    }

    /// <summary>
    /// Suma el arma de la mano derecha (y, si es un par, la otra en la izquierda), marcada como arma
    /// (si le cortan la mano, se cae aparte).
    /// </summary>
    private static void AddWeapon(Figure fig, string weapon)
    {
        foreach (int side in new[] { 1 })
        {
            if (WeaponModels.Build(weapon, side) is not { } w) return;
            int from = fig.Shapes.Count;
            Merge(fig, w, out _);
            for (int i = from; i < fig.Shapes.Count; i++) fig.Shapes[i].Weapon = true;
        }
    }

    /// <summary>Suma las formas y materiales de una capa con índices corridos; devuelve el mapa de partes.</summary>
    private static Dictionary<int, int> Merge(Figure dst, Figure src, out int matOffset)
    {
        int off = dst.Mats.Count;
        matOffset = off;
        dst.Mats.AddRange(src.Mats);
        var parts = new Dictionary<int, int>();
        foreach (var s in src.Shapes)
        {
            if (!parts.TryGetValue(s.Part, out int part)) parts[s.Part] = part = dst.Part();
            var c = s.Copy();
            c.Mat = s.Mat + off;
            c.Part = part;
            if (s.Paint != null)
            {
                var paint = s.Paint;
                c.Paint = (p, m) => paint(p, m - off) + off;
            }
            dst.Shapes.Add(c);
        }
        // Las telas son las mismas (sus triángulos ya vinieron con las formas).
        foreach (var cloth in src.Cloths) if (!dst.Cloths.Contains(cloth)) dst.Cloths.Add(cloth);
        return parts;
    }
}

/// <summary>
/// Pasa un rasterizado en vivo al formato que reilumina el shader (el mismo de las hojas
/// compuestas): Index RGBA = (fila de paleta, bits bajos; corrimiento de tono + 8 o 255 si es
/// contorno; profundidad; 128 + bits altos de la fila) y Normal RGBA = normal en el espacio
/// de la cámara del horneado. Alfa 0 = vacío.
/// </summary>
public static class LiveEncoder
{
    public static void Encode(Raster r, LiveFigure lf, int rowBase, byte[] index, byte[] normal)
    {
        int w = r.W, h = r.H;
        Array.Clear(index);
        Array.Clear(normal);
        if (r.X1 < r.X0) return;
        int x0 = r.X0, y0 = r.Y0, x1 = r.X1, y1 = r.Y1;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int i = y * w + x;
            int m = r.Mat[i];
            if (m < 0) continue;
            var n = r.Normal[i];
            byte nr = (byte)Math.Clamp((int)MathF.Round((n.X * 0.5f + 0.5f) * 255), 0, 255);
            byte ng = (byte)Math.Clamp((int)MathF.Round((n.Y * 0.5f + 0.5f) * 255), 0, 255);
            byte nb = (byte)Math.Clamp((int)MathF.Round((n.Z * 0.5f + 0.5f) * 255), 0, 255);
            var nq = Vector3.Normalize(new Vector3(nr / 255f * 2 - 1, ng / 255f * 2 - 1, nb / 255f * 2 - 1));
            int bias = r.Tone[i] - Shading.BakeTone(nq, lf.Fig.Mats[m].Def);
            int o = i * 4;
            int row = rowBase + m;
            index[o] = (byte)(row & 255);
            index[o + 1] = (byte)Math.Clamp(bias + 8, 0, 15);
            index[o + 2] = LayerLibrary.QuantDepth(r.Depth[i]);
            index[o + 3] = (byte)(128 + (row >> 8));
            normal[o] = nr; normal[o + 1] = ng; normal[o + 2] = nb; normal[o + 3] = 255;
        }
        // Contorno exterior de 1 px (4 vecinos): toma rampa, profundidad y normal del vecino.
        for (int y = Math.Max(0, y0 - 1); y <= Math.Min(h - 1, y1 + 1); y++)
        for (int x = Math.Max(0, x0 - 1); x <= Math.Min(w - 1, x1 + 1); x++)
        {
            int i = y * w + x;
            if (r.Mat[i] >= 0) continue;
            int from = x > 0 && r.Mat[i - 1] >= 0 ? i - 1 : x < w - 1 && r.Mat[i + 1] >= 0 ? i + 1 : y < h - 1 && r.Mat[i + w] >= 0 ? i + w : y > 0 && r.Mat[i - w] >= 0 ? i - w : -1;
            if (from < 0) continue;
            int o = i * 4, f = from * 4;
            index[o] = index[f];
            index[o + 1] = 255;
            index[o + 2] = index[f + 2];
            index[o + 3] = index[f + 3];
            normal[o] = normal[f]; normal[o + 1] = normal[f + 1]; normal[o + 2] = normal[f + 2]; normal[o + 3] = 255;
        }
    }
}
