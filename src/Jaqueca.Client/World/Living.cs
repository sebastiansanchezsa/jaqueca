using Jaqueca.Client.Render;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Jaqueca.Client.World;

/// <summary>
/// El primer recuerdo: el living de la abuela, un domingo a la tarde de 1989. Todo es enorme (en los
/// recuerdos las cosas quedan del tamaño que tenían cuando uno era chico), así los muebles son las
/// plataformas de la pelea: las sillas, la mesa con el hule, el sillón bordó con capitoné, la mesita
/// ratona, el modular con el televisor prendido sin señal, el aparador, el reloj de pie. Las paredes no
/// llegan a ningún techo: arriba se terminan en carne, y encima está el adentro de la cabeza.
/// Espacio: x este, y arriba, z sur; el piso en y = 0; un hombre mide 16.
/// </summary>
public sealed class Living
{
    public const float W = 150, D = 120, WallH = 100;

    public readonly Solids Solids = new();
    public readonly List<Light> Lights = new();
    /// <summary>Dónde aparecen los pensamientos (en el piso).</summary>
    public readonly List<Vector3> Spawns = new();
    public Vector3 Start = new(0, 0, 60);
    public float StartYaw = -MathHelper.PiOver2;
    /// <summary>Dónde suena el reloj de pie.</summary>
    public Vector3 ClockAt;
    /// <summary>La pantalla del televisor (para la luz que titila y el ruido).</summary>
    public Vector3 TvAt;

    // Colores de la casa.
    private static readonly Color Parquet = new(126, 84, 58), Wallpaper = new(196, 176, 132), Baseboard = new(92, 58, 36),
        Oak = new(118, 74, 42), DarkWood = new(74, 44, 28), Velvet = new(118, 38, 44), Oilcloth = new(170, 36, 38),
        Carpet = new(120, 48, 40), Brass = new(186, 150, 80), Cream = new(222, 210, 180), FleshPink = new(196, 110, 118),
        Glass = new(56, 74, 82), Black = new(26, 22, 24), Plaster = new(206, 196, 170);

    /// <summary>Arma el lugar y lo sube a la GPU (la malla estática).</summary>
    public Mesh Build(GraphicsDevice gd) => Assemble().B.Build(gd);

    /// <summary>Arma el lugar: lo que se ve (en el kit) y lo que frena (en <see cref="Solids"/>). Sin GPU: lo usan las pruebas.</summary>
    public Kit Assemble()
    {
        Solids.Bounds = new Rectangle((int)-W, (int)-D, (int)(2 * W), (int)(2 * D));
        var k = new Kit(Solids);
        Floor(k);
        Walls(k);
        Cabinet(k);
        Clock(k);
        Table(k);
        Sofa(k);
        CoffeeTable(k);
        Sideboard(k);
        Armchair(k);
        Lamp(k);
        Chandelier(k);
        Frames(k);
        Solids.Build();

        Spawns.AddRange(new[]
        {
            new Vector3(-110, 0, -80), new Vector3(0, 0, -80), new Vector3(110, 0, -70), new Vector3(125, 0, 20),
            new Vector3(-10, 0, 20), new Vector3(-120, 0, 30), new Vector3(10, 0, 95), new Vector3(-60, 0, 90),
        });
        return k;
    }

    // ------------------------------------------------------------------ piso y paredes

    private static void Floor(Kit k)
    {
        // El parquet (un poco más allá de las paredes, para que no se vea el borde) y el zócalo.
        k.Block(new Vector3(-W - 10, -4, -D - 10), new Vector3(W + 10, 0, D + 10), Parquet, Materials.Parquet, 0, floor: -4);
    }

    private void Walls(Kit k)
    {
        // Cuatro paredes empapeladas con zócalo; en la del este, la ventana (afuera, lo de adentro de la cabeza).
        float t = 8;
        void Wall(Vector3 min, Vector3 max)
        {
            k.Block(min, max, Wallpaper, Materials.Wallpaper, 1, top: FleshPink, topMat: Materials.Flesh);
        }
        Wall(new Vector3(-W - t, 0, -D - t), new Vector3(W + t, WallH, -D));      // norte
        Wall(new Vector3(-W - t, 0, D), new Vector3(W + t, WallH, D + t));         // sur
        Wall(new Vector3(-W - t, 0, -D), new Vector3(-W, WallH, D));               // oeste
        // Este, con el hueco de la ventana (z −34..34, y 30..74).
        Wall(new Vector3(W, 0, -D), new Vector3(W + t, WallH, -34));
        Wall(new Vector3(W, 0, 34), new Vector3(W + t, WallH, D));
        Wall(new Vector3(W, 0, -34), new Vector3(W + t, 30, 34));
        Wall(new Vector3(W, 74, -34), new Vector3(W + t, WallH, 34));
        // El marco y el alféizar de la ventana.
        k.Block(new Vector3(W - 3, 27, -37), new Vector3(W + 1, 30, 37), Cream, Materials.Wood, 0);
        k.Block(new Vector3(W - 2, 30, -37), new Vector3(W, 74, -34), Cream, Materials.Wood, 0, solid: false);
        k.Block(new Vector3(W - 2, 30, 34), new Vector3(W, 74, 37), Cream, Materials.Wood, 0, solid: false);
        k.Block(new Vector3(W - 2, 74, -37), new Vector3(W, 77, 37), Cream, Materials.Wood, 0, solid: false);
        k.Block(new Vector3(W - 2, 30, -1), new Vector3(W, 74, 1), Cream, Materials.Wood, 0, solid: false);

        // Zócalos (no frenan: son parte de la pared).
        k.Block(new Vector3(-W, 0, -D), new Vector3(W, 6, -D + 1.2f), Baseboard, Materials.Wood, 0, solid: false);
        k.Block(new Vector3(-W, 0, D - 1.2f), new Vector3(W, 6, D), Baseboard, Materials.Wood, 0, solid: false);
        k.Block(new Vector3(-W, 0, -D), new Vector3(-W + 1.2f, 6, D), Baseboard, Materials.Wood, 0, solid: false);
        k.Block(new Vector3(W - 1.2f, 0, -D), new Vector3(W, 6, -34), Baseboard, Materials.Wood, 0, solid: false);
        k.Block(new Vector3(W - 1.2f, 0, 34), new Vector3(W, 6, D), Baseboard, Materials.Wood, 0, solid: false);

        // Arriba, las paredes se terminan en carne: bultos irregulares que laten.
        var rng = new Random(11);
        void Rim(Vector3 a, Vector3 b)
        {
            float len = Vector3.Distance(a, b);
            int n = (int)(len / 14);
            for (int i = 0; i < n; i++)
            {
                var p = Vector3.Lerp(a, b, (i + 0.5f) / n);
                float h = 5 + (float)rng.NextDouble() * 12, s = 7 + (float)rng.NextDouble() * 6;
                k.Blob(p + new Vector3(0, h * 0.35f, 0), new Vector3(s, h, s * (0.8f + (float)rng.NextDouble() * 0.5f)), FleshPink, Materials.Flesh);
            }
        }
        Rim(new Vector3(-W - 4, WallH, -D - 4), new Vector3(W + 4, WallH, -D - 4));
        Rim(new Vector3(-W - 4, WallH, D + 4), new Vector3(W + 4, WallH, D + 4));
        Rim(new Vector3(-W - 4, WallH, -D - 4), new Vector3(-W - 4, WallH, D + 4));
        Rim(new Vector3(W + 4, WallH, -D - 4), new Vector3(W + 4, WallH, D + 4));
    }

    // ------------------------------------------------------------------ muebles

    /// <summary>El modular contra la pared norte: la base cerrada, los estantes, el televisor prendido sin señal.</summary>
    private void Cabinet(Kit k)
    {
        float x0 = -50, x1 = 50, z0 = -D, z1 = -D + 22;
        k.Block(new Vector3(x0, 0, z0), new Vector3(x1, 18, z1), Oak, Materials.Wood);                    // base con puertas
        k.Block(new Vector3(x0, 18, z0), new Vector3(x0 + 4, 64, z1), Oak, Materials.Wood);               // costados
        k.Block(new Vector3(x1 - 4, 18, z0), new Vector3(x1, 64, z1), Oak, Materials.Wood);
        k.Block(new Vector3(-2, 18, z0), new Vector3(2, 61, z1), Oak, Materials.Wood);                    // divisor
        k.Block(new Vector3(x0, 38, z0), new Vector3(x1, 41, z1), Oak, Materials.Wood);                   // estante
        k.Block(new Vector3(x0 - 2, 61, z0), new Vector3(x1 + 2, 64, z1 + 2), DarkWood, Materials.Wood);  // tapa
        k.Block(new Vector3(x0, 18, z0), new Vector3(x1, 64, z0 + 2), DarkWood, Materials.Wood, solid: false); // fondo
        // Las manijas de las puertas de abajo.
        for (int i = -1; i <= 1; i += 2) k.Block(new Vector3(i * 6 - 1, 8, z1), new Vector3(i * 6 + 1, 12, z1 + 1), Brass, Materials.Plain, solid: false);
        // El televisor en el hueco de la izquierda: el mueble de madera, la pantalla gris que brilla sola.
        var tv0 = new Vector3(-44, 18, z0 + 3);
        k.Block(tv0, tv0 + new Vector3(38, 19, 17), new Color(58, 44, 36), Materials.Wood);
        TvAt = new Vector3(-28, 28, z1 + 0.6f);
        k.Panel(TvAt, Vector3.UnitX, Vector3.UnitY, 24, 14, new Color(150, 170, 176), Materials.Static, 0.6f);
        k.Block(new Vector3(-10, 22, z1 - 0.5f), new Vector3(-7, 32, z1 + 0.5f), Black, Materials.Plain, solid: false);
        Lights.Add(new Light { Pos = TvAt + new Vector3(0, 0, 14), Radius = 90, Color = new Vector3(0.35f, 0.45f, 0.55f), Flicker = 0.6f, Seed = 3 });
        // La antena de conejo.
        k.Block(new Vector3(-30, 37, z0 + 9), new Vector3(-26, 38.5f, z0 + 12), Black, Materials.Plain, solid: false);
        // En el estante de la derecha, un florero y unos libros.
        k.Lathe(26, z0 + 11, 41, new[] { (0f, 3f), (3f, 5f), (9f, 4f), (13f, 2f), (15f, 2.6f) }, new Color(60, 90, 120), Materials.Plain, 8);
        for (int i = 0; i < 6; i++)
        {
            float bx = 6 + i * 3.1f;
            var col = i % 3 == 0 ? new Color(110, 40, 36) : i % 3 == 1 ? new Color(40, 60, 90) : new Color(160, 140, 90);
            k.Block(new Vector3(bx, 18, z0 + 4), new Vector3(bx + 2.8f, 18 + 12 + (i % 2) * 3, z0 + 18), col, Materials.Plain, solid: false);
        }
    }

    /// <summary>El reloj de pie en el rincón: el que no para de sonar.</summary>
    private void Clock(Kit k)
    {
        float x0 = 116, x1 = 136, z0 = -D, z1 = -D + 14;
        k.Block(new Vector3(x0 - 2, 0, z0), new Vector3(x1 + 2, 10, z1 + 2), DarkWood, Materials.Wood);
        k.Block(new Vector3(x0, 10, z0), new Vector3(x1, 60, z1), DarkWood, Materials.Wood);
        k.Block(new Vector3(x0 - 2, 60, z0), new Vector3(x1 + 2, 80, z1 + 2), DarkWood, Materials.Wood);
        k.Block(new Vector3(x0 - 3, 80, z0), new Vector3(x1 + 3, 83, z1 + 3), DarkWood, Materials.Wood);
        // La ventanita del péndulo y la esfera.
        k.Panel(new Vector3(126, 34, z1 + 0.3f), Vector3.UnitX, Vector3.UnitY, 12, 34, Glass, 0);
        k.Lathe(126, z1 - 1, 12, new[] { (0f, 0.6f), (26f, 0.6f), (26.1f, 3.5f), (27.5f, 3.5f) }, Brass, Materials.Plain, 6);
        ClockAt = new Vector3(126, 70, z1 + 2.3f);
        k.Panel(ClockAt, Vector3.UnitX, Vector3.UnitY, 14, 14, Cream, 0, 0.15f);
        k.Block(ClockAt + new Vector3(-0.4f, 0, 0.1f), ClockAt + new Vector3(0.4f, 5, 0.5f), Black, Materials.Plain, solid: false);
        k.Block(ClockAt + new Vector3(0, -0.4f, 0.1f), ClockAt + new Vector3(4, 0.4f, 0.5f), Black, Materials.Plain, solid: false);
    }

    /// <summary>La mesa del comedor con el hule a cuadros y sus cuatro sillas.</summary>
    private static void Table(Kit k)
    {
        float x0 = -100, x1 = -30, z0 = -48, z1 = -8, top = 30;
        k.Block(new Vector3(x0, top - 3, z0), new Vector3(x1, top, z1), Oilcloth, Materials.Oilcloth, 0, top: Oilcloth, topMat: Materials.Oilcloth, bottom: true, floor: -100);
        // El hule cuelga un poco por los bordes.
        k.Block(new Vector3(x0 - 1.5f, top - 6, z0 - 1.5f), new Vector3(x1 + 1.5f, top - 0.2f, z0), Oilcloth, Materials.Oilcloth, solid: false, floor: -100);
        k.Block(new Vector3(x0 - 1.5f, top - 6, z1), new Vector3(x1 + 1.5f, top - 0.2f, z1 + 1.5f), Oilcloth, Materials.Oilcloth, solid: false, floor: -100);
        var leg = new[] { (0f, 2.2f), (4f, 2.8f), (8f, 1.9f), (16f, 2.4f), (20f, 1.8f), (27f, 2.2f) };
        foreach (var (lx, lz) in new[] { (x0 + 4, z0 + 4), (x1 - 4, z0 + 4), (x0 + 4, z1 - 4), (x1 - 4, z1 - 4) })
            k.Lathe(lx, lz, 0, leg, DarkWood, Materials.Wood, 8, solid: true);
        // Encima: la panera, la botella de soda y el sifón (cosas para romper... más adelante).
        k.Lathe(-60, -30, top, new[] { (0f, 2.5f), (1f, 2.5f), (10f, 2.2f), (13f, 1.2f), (16f, 1f) }, new Color(60, 110, 90), Materials.Plain, 8, solid: true);
        k.Lathe(-80, -24, top, new[] { (0f, 5f), (1.2f, 7f), (4f, 7.5f) }, new Color(180, 150, 100), Materials.Plain, 10, solid: true);
        // Las sillas: asiento, patas y el respaldo alto con travesaños.
        void Chair(float cx, float cz, int face)
        {
            float s = 8, seat = 16;
            k.Block(new Vector3(cx - s, seat - 2, cz - s), new Vector3(cx + s, seat, cz + s), Oak, Materials.Wood, top: Velvet, topMat: Materials.Upholstery, bottom: true, floor: -100);
            foreach (var (lx, lz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                k.Block(new Vector3(cx + lx * (s - 1.5f) - 1.2f, 0, cz + lz * (s - 1.5f) - 1.2f), new Vector3(cx + lx * (s - 1.5f) + 1.2f, seat - 2, cz + lz * (s - 1.5f) + 1.2f), DarkWood, Materials.Wood, solid: false);
            // El respaldo del lado de afuera (face: 1 al sur, -1 al norte).
            float bz = cz + face * (s - 1.2f);
            k.Block(new Vector3(cx - s, seat, bz - 1.2f), new Vector3(cx - s + 2.2f, 38, bz + 1.2f), DarkWood, Materials.Wood, floor: -100);
            k.Block(new Vector3(cx + s - 2.2f, seat, bz - 1.2f), new Vector3(cx + s, 38, bz + 1.2f), DarkWood, Materials.Wood, floor: -100);
            k.Block(new Vector3(cx - s, 32, bz - 1.2f), new Vector3(cx + s, 38, bz + 1.2f), DarkWood, Materials.Wood, floor: -100);
            k.Block(new Vector3(cx - s, 23, bz - 0.8f), new Vector3(cx + s, 25, bz + 0.8f), DarkWood, Materials.Wood, solid: false);
            k.S.Box(new Vector3(cx - s, 0, cz - s), new Vector3(cx + s, seat, cz + s));
        }
        Chair(-84, -60, -1);
        Chair(-46, -60, -1);
        Chair(-84, 4, 1);
        Chair(-46, 4, 1);
    }

    /// <summary>El sillón bordó con capitoné contra la pared sur (el de los domingos).</summary>
    private static void Sofa(Kit k)
    {
        float x0 = 20, x1 = 104, z0 = D - 34, z1 = D;
        k.Block(new Vector3(x0 + 7, 0, z0), new Vector3(x1 - 7, 16, z1 - 8), Velvet, Materials.Upholstery, 2);      // asiento
        k.Block(new Vector3(x0 + 7, 0, z1 - 10), new Vector3(x1 - 7, 36, z1), Velvet, Materials.Upholstery, 2);     // respaldo
        k.Block(new Vector3(x0, 0, z0 - 2), new Vector3(x0 + 8, 25, z1), Velvet, Materials.Upholstery, 2);          // brazos
        k.Block(new Vector3(x1 - 8, 0, z0 - 2), new Vector3(x1, 25, z1), Velvet, Materials.Upholstery, 2);
        // Los almohadones del asiento (tres, con las costuras que los separan).
        for (int i = 0; i < 3; i++)
        {
            float a = x0 + 8 + i * 22.6f;
            k.Block(new Vector3(a + 0.4f, 16, z0 + 0.5f), new Vector3(a + 22.2f, 19, z1 - 10), new Color(128, 44, 50), Materials.Upholstery, 2, solid: false);
        }
        k.S.Box(new Vector3(x0 + 8, 16, z0), new Vector3(x1 - 8, 19, z1 - 10), 2);
        // El tapete de crochet sobre el respaldo.
        k.Block(new Vector3(56, 36, z1 - 9), new Vector3(70, 36.4f, z1 - 1), Cream, Materials.Carpet, 2, solid: false);
        k.Block(new Vector3(56, 30, z1 - 10.4f), new Vector3(70, 36.2f, z1 - 10), Cream, Materials.Carpet, 2, solid: false);
        // Las patitas de madera.
        foreach (var lx in new[] { x0 + 2, x1 - 4 })
            foreach (var lz in new[] { z0, z1 - 3 })
                k.Block(new Vector3(lx, -0.01f, lz), new Vector3(lx + 2, 1, lz + 2), DarkWood, Materials.Wood, solid: false);
    }

    /// <summary>La mesita ratona sobre la alfombra, con el cenicero de vidrio y las revistas.</summary>
    private static void CoffeeTable(Kit k)
    {
        k.Block(new Vector3(10, 0, 30), new Vector3(110, 0.5f, 80), Carpet, Materials.Carpet, 2, solid: false);
        float x0 = 40, x1 = 84, z0 = 42, z1 = 64;
        k.Block(new Vector3(x0, 13, z0), new Vector3(x1, 16, z1), DarkWood, Materials.Wood, top: Glass, topMat: Materials.Plain, bottom: true, floor: -100);
        foreach (var (lx, lz) in new[] { (x0 + 2, z0 + 2), (x1 - 4, z0 + 2), (x0 + 2, z1 - 4), (x1 - 4, z1 - 4) })
            k.Block(new Vector3(lx, 0.5f, lz), new Vector3(lx + 2, 13, lz + 2), DarkWood, Materials.Wood, solid: false);
        k.S.Box(new Vector3(x0, 0, z0), new Vector3(x1, 16, z1));
        k.Lathe(70, 52, 16, new[] { (0f, 3.5f), (1.4f, 4f), (1.5f, 2.6f), (0.6f, 2.4f) }, Glass, Materials.Plain, 8);
        k.Block(new Vector3(46, 16, 46), new Vector3(58, 16.8f, 56), new Color(190, 180, 160), Materials.Plain, solid: false);
        k.Block(new Vector3(48, 16.8f, 47), new Vector3(60, 17.4f, 57), new Color(160, 60, 60), Materials.Plain, solid: false);
    }

    /// <summary>El aparador contra la pared oeste, con la vajilla "para las visitas".</summary>
    private static void Sideboard(Kit k)
    {
        float x0 = -W, x1 = -W + 20, z0 = -70, z1 = 10;
        k.Block(new Vector3(x0, 0, z0), new Vector3(x1, 34, z1), Oak, Materials.Wood, top: DarkWood);
        for (int i = 0; i < 4; i++)
        {
            float zc = z0 + 10 + i * 20;
            k.Block(new Vector3(x1, 6, zc - 8), new Vector3(x1 + 0.6f, 28, zc + 8), DarkWood, Materials.Wood, solid: false);
            k.Block(new Vector3(x1 + 0.6f, 16, zc - 1), new Vector3(x1 + 1.4f, 18, zc + 1), Brass, Materials.Plain, solid: false);
        }
        // Los platos parados y la sopera.
        for (int i = 0; i < 5; i++)
            k.Lathe(x0 + 6, z0 + 12 + i * 8, 34, new[] { (0f, 0.4f), (0.1f, 3.4f), (0.8f, 3.4f) }, Cream, Materials.Plain, 10);
        k.Lathe(x0 + 10, -8, 34, new[] { (0f, 3f), (2f, 5f), (6f, 5.4f), (8f, 4f), (9f, 1.2f), (10f, 1.6f) }, Cream, Materials.Plain, 10, solid: true);
    }

    /// <summary>El sillón individual del abuelo, con el apoyabrazos gastado.</summary>
    private static void Armchair(Kit k)
    {
        float x0 = -140, x1 = -104, z0 = 62, z1 = 100;
        var c = new Color(92, 84, 60);
        k.Block(new Vector3(x0 + 5, 0, z0), new Vector3(x1 - 5, 16, z1 - 8), c, Materials.Upholstery, 2);
        k.Block(new Vector3(x0 + 5, 0, z1 - 10), new Vector3(x1 - 5, 38, z1), c, Materials.Upholstery, 2);
        k.Block(new Vector3(x0, 0, z0 - 2), new Vector3(x0 + 6, 26, z1), c, Materials.Upholstery, 2);
        k.Block(new Vector3(x1 - 6, 0, z0 - 2), new Vector3(x1, 26, z1), c, Materials.Upholstery, 2);
    }

    /// <summary>La lámpara de pie al lado del sillón: la luz cálida de la tarde.</summary>
    private void Lamp(Kit k)
    {
        float x = 124, z = 96;
        k.Lathe(x, z, 0, new[] { (0f, 6f), (1f, 6f), (1.5f, 1f), (54f, 0.8f), (56f, 1.2f) }, Brass, Materials.Plain, 8, solid: true);
        k.Lathe(x, z, 50, new[] { (0f, 11f), (12f, 6f), (12.1f, 0.1f) }, new Color(222, 190, 140, 180), Materials.Plain, 10);
        Lights.Add(new Light { Pos = new Vector3(x, 54, z), Radius = 130, Color = new Vector3(0.95f, 0.62f, 0.32f), Seed = 1 });
    }

    /// <summary>La araña del techo que ya no tiene techo: flota sola, prendida.</summary>
    private void Chandelier(Kit k)
    {
        var c = new Vector3(0, 86, 0);
        k.Lathe(c.X, c.Z, c.Y - 6, new[] { (0f, 0.1f), (2f, 3f), (5f, 7f), (6f, 7.2f), (7f, 2f), (14f, 0.6f) }, Brass, Materials.Plain, 10);
        for (int i = 0; i < 6; i++)
        {
            float a = i * MathF.Tau / 6;
            var p = c + new Vector3(MathF.Cos(a) * 12, -2, MathF.Sin(a) * 12);
            k.Block(p - new Vector3(0.6f, 0, 0.6f), p + new Vector3(0.6f, 4, 0.6f), Cream, Materials.Plain, solid: false, floor: -100);
            k.Block(p + new Vector3(-0.8f, 4, -0.8f), p + new Vector3(0.8f, 6, 0.8f), new Color(255, 220, 150, 40), Materials.Plain, solid: false, floor: -100);
            k.Block(new Vector3(c.X, p.Y - 1, c.Z) + (p - c) * 0.1f, p + new Vector3(0.4f, 0, 0.4f), Brass, Materials.Plain, solid: false, floor: -100);
        }
        Lights.Add(new Light { Pos = c, Radius = 160, Color = new Vector3(0.7f, 0.55f, 0.35f), Flicker = 0.08f, Seed = 7 });
    }

    /// <summary>Los cuadros: la foto del casamiento, la Virgen, el paisaje del calendario.</summary>
    private static void Frames(Kit k)
    {
        void Frame(Vector3 c, Vector3 right, float w, float h, Color inner)
        {
            var n = Vector3.Normalize(Vector3.Cross(right, Vector3.UnitY));
            k.Panel(c, right, Vector3.UnitY, w + 3, h + 3, new Color(160, 120, 60));
            k.Panel(c + n * 0.3f, right, Vector3.UnitY, w, h, inner);
        }
        // Pared sur (mirando al norte).
        Frame(new Vector3(62, 60, D - 0.5f), -Vector3.UnitX, 22, 16, new Color(170, 160, 140));
        Frame(new Vector3(-40, 62, D - 0.5f), -Vector3.UnitX, 14, 20, new Color(60, 80, 130));
        // Pared oeste (mirando al este).
        Frame(new Vector3(-W + 0.5f, 58, -30), -Vector3.UnitZ, 30, 20, new Color(90, 120, 80));
        // Pared norte (mirando al sur), al lado del reloj.
        Frame(new Vector3(84, 58, -D + 0.5f), Vector3.UnitX, 16, 16, new Color(140, 100, 90));
        Frame(new Vector3(-100, 56, -D + 0.5f), Vector3.UnitX, 20, 26, new Color(100, 70, 60));
    }
}
