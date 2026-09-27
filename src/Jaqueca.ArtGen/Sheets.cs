using System.Numerics;
using Jaqueca.Anim;
using Jaqueca.Figures.Anim;
using Jaqueca.Figures.Content;
using Jaqueca.Figures.Model;
using Jaqueca.Figures.Render;
using Jaqueca.Figures.Rig;
using Jaqueca.Look;
using Jaqueca.Sprites;

namespace Jaqueca.ArtGen;

/// <summary>
/// Las hojas de revisión de los modelos de Jaqueca, rasterizados en pixel art con el mismo motor que
/// dibuja los personajes de Inquisition (rayos por subpíxel contra las primitivas, luz en cinco tonos,
/// contorno): cada pensamiento parado en las 8 direcciones, caminando y atacando, y lo que Ernesto
/// tiene en las manos, de costado. Sirven para mirar un modelo sin abrir el juego.
/// </summary>
public static class Sheets
{
    private const uint Ink = 0xFF1A1014, Paper = 0xFF2A2026;

    /// <summary>Un pensamiento: su figura, sus medidas, su escala y cómo ataca.</summary>
    private sealed record Thought(string Name, Func<Figure> Build, Dims Dims, float Scale, WeaponFamily Hand, Action<Animator, float> Attack);

    public static void Write(string dir)
    {
        var thoughts = new[]
        {
            new Thought("vecino", Thoughts.Neighbor, Thoughts.NeighborDims, 1.1f, WeaponFamily.Dagger,
                (a, t) => a.SetAction(ClipDefs.Get(ClipId.Stab1), t * ClipDefs.Get(ClipId.Stab1).Duration)),
            new Thought("maestra", Thoughts.Teacher, Thoughts.TeacherDims, 1f, WeaponFamily.None,
                (a, t) => { if (t == 0) a.Gesture(GestureKind.Throw, 0.8f); }),
        };
        foreach (var th in thoughts)
        {
            var path = Path.Combine(dir, th.Name + ".png");
            Png.Write(path, Sheet(th));
            Console.WriteLine("Hoja: " + path);
        }
        var weapons = Path.Combine(dir, "armas.png");
        Png.Write(weapons, WeaponSheet());
        Console.WriteLine("Hoja: " + weapons);
    }

    // ------------------------------------------------------------------ los pensamientos

    private const int Cell = 160, CellH = 190;

    private static Canvas Sheet(Thought th)
    {
        var fig = th.Build();
        var skel = new Skeleton(Build.Robust, th.Dims.Scaled(th.Scale));
        var cam = new Camera { K = 5.5f, W = Cell, H = CellH, PivotX = Cell / 2f, PivotY = CellH - 18 };
        cam.Update();
        var ctx = new RasterContext(cam);
        var sheet = new Canvas(Cell * 8, CellH * 3);
        sheet.Rect(0, 0, sheet.W, sheet.H, Paper);

        // Fila 1: parado, en las 8 direcciones (la primera mira al este; va girando hacia la cámara).
        for (int d = 0; d < 8; d++)
        {
            var a = Fresh(skel, th.Hand, 0);
            for (int i = 0; i < 90; i++) a.Update(Vector2.Zero, Vector2.Zero, 0, 1 / 60f);
            Paste(sheet, Paint(Rasterizer.RenderLive(ctx, fig, a.Bones, d * MathF.PI / 4, scale: th.Scale), fig), d * Cell, 0);
        }
        // Fila 2: caminando hacia el costado, mirando un poco a la cámara (ocho momentos del paso).
        {
            float yaw = MathF.PI / 6;
            var a = Fresh(skel, th.Hand, yaw);
            var dir = new Vector2(MathF.Cos(yaw), MathF.Sin(yaw));
            var pos = Vector2.Zero;
            float speed = 60;
            for (int i = 0; i < 120; i++) { pos += dir * speed / 60f; a.Update(pos, dir * speed, yaw, 1 / 60f); }
            for (int f = 0; f < 8; f++)
            {
                for (int i = 0; i < 4; i++) { pos += dir * speed / 60f; a.Update(pos, dir * speed, yaw, 1 / 60f); }
                var local = ToLocal(a);
                Paste(sheet, Paint(Rasterizer.RenderLive(ctx, fig, local, yaw, scale: th.Scale), fig), f * Cell, CellH);
            }
        }
        // Fila 3: el ataque, de perfil y un poco de frente.
        {
            float yaw = MathF.PI / 5;
            var a = Fresh(skel, th.Hand, yaw);
            for (int i = 0; i < 60; i++) a.Update(Vector2.Zero, Vector2.Zero, yaw, 1 / 60f);
            for (int f = 0; f < 8; f++)
            {
                float t = f / 7f;
                th.Attack(a, t);
                for (int i = 0; i < 6; i++) a.Update(Vector2.Zero, Vector2.Zero, yaw, 1 / 60f);
                Paste(sheet, Paint(Rasterizer.RenderLive(ctx, fig, a.Bones, yaw, scale: th.Scale), fig), f * Cell, CellH * 2);
            }
        }
        return sheet;
    }

    private static Animator Fresh(Skeleton skel, WeaponFamily hand, float yaw) => new(skel, Vector2.Zero, yaw, 3) { MainHand = hand, Hunch = hand == WeaponFamily.Dagger ? 0.25f : 0 };

    /// <summary>Los huesos de alguien que caminó: centrado en su origen (el sprite lo pone en el medio de la casilla).</summary>
    private static Matrix4x4[] ToLocal(Animator a) => (Matrix4x4[])a.Bones.Clone();

    // ------------------------------------------------------------------ las armas

    private static Canvas WeaponSheet()
    {
        // Las piezas de Ernesto cuelgan de un hueso que es un lugar: con todos los huesos en el origen se
        // ven como en la mano, de costado (+X hacia la derecha de la hoja).
        var pieces = new (Func<Figure> build, float k)[] { (Ernesto.Revolver, 34), (Ernesto.Shotgun, 22), (Ernesto.Leg, 18) };
        const int W = 420, H = 300;
        var sheet = new Canvas(W * pieces.Length, H);
        sheet.Rect(0, 0, sheet.W, sheet.H, Paper);
        for (int i = 0; i < pieces.Length; i++)
        {
            var fig = pieces[i].build();
            var cam = new Camera { K = pieces[i].k, W = W, H = H, PivotX = W * 0.55f, PivotY = H * 0.5f, Elevation = 10 };
            cam.Update();
            var ctx = new RasterContext(cam);
            var bones = new Matrix4x4[(int)Bone.Count];
            Array.Fill(bones, Matrix4x4.Identity);
            // La corredera, en su lugar de la escopeta.
            bones[(int)Bone.HandL] = Matrix4x4.CreateTranslation(Ernesto.ShotgunPump);
            Paste(sheet, Paint(Rasterizer.RenderLive(ctx, fig, bones, -MathF.PI / 2 + 0.35f), fig), i * W, 0);
        }
        return sheet;
    }

    // ------------------------------------------------------------------ pintar

    /// <summary>
    /// Los colores de un raster: cada píxel es su material en el tono que le dio la luz (la rampa de
    /// cinco tonos del motor), con el contorno oscuro alrededor de la silueta.
    /// </summary>
    private static Canvas Paint(Raster r, Figure fig)
    {
        var c = new Canvas(r.W, r.H);
        var ramps = new Ramp[fig.Mats.Count];
        for (int i = 0; i < ramps.Length; i++) ramps[i] = Ramp.From(fig.Mats[i].Def.Rgb, fig.Mats[i].Def.Shiny);
        for (int y = 0; y < r.H; y++)
        for (int x = 0; x < r.W; x++)
        {
            int m = r.Mat[y * r.W + x];
            if (m < 0 || m >= ramps.Length) continue;
            c.Set(x, y, Col.Rgb(ramps[m][r.Tone[y * r.W + x]]));
        }
        c.Outline(Ink);
        return c;
    }

    private static void Paste(Canvas dst, Canvas src, int ox, int oy)
    {
        for (int y = 0; y < src.H; y++)
        for (int x = 0; x < src.W; x++)
        {
            uint p = src.Get(x, y);
            if (Col.A(p) > 0) dst.Set(ox + x, oy + y, p);
        }
    }
}
