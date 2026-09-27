using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Jaqueca.Client.Ui;

/// <summary>
/// Fuente pixel armada por código al arrancar (la de Kill Kill Again más minúsculas): letras de
/// 7 px de alto con 2 filas arriba para los acentos de las mayúsculas y 2 abajo para las colas
/// (g, j, p, q, y). Cada glifo tiene su ancho (espaciado proporcional). Se dibuja en píxeles
/// del mundo (640×360) y se escala con la imagen: queda nítida a cualquier tamaño de ventana.
/// </summary>
public sealed class PixelFont : IDisposable
{
    public const int GlyphH = 11, Spacing = 1, LineH = 12;

    private static readonly Dictionary<char, string> G = new()
    {
        ['A'] = ".###.|#...#|#...#|#####|#...#|#...#|#...#",
        ['B'] = "####.|#...#|#...#|####.|#...#|#...#|####.",
        ['C'] = ".###.|#...#|#....|#....|#....|#...#|.###.",
        ['D'] = "####.|#...#|#...#|#...#|#...#|#...#|####.",
        ['E'] = "#####|#....|#....|####.|#....|#....|#####",
        ['F'] = "#####|#....|#....|####.|#....|#....|#....",
        ['G'] = ".###.|#...#|#....|#.###|#...#|#...#|.####",
        ['H'] = "#...#|#...#|#...#|#####|#...#|#...#|#...#",
        ['I'] = "###|.#.|.#.|.#.|.#.|.#.|###",
        ['J'] = "..###|...#.|...#.|...#.|#..#.|#..#.|.##..",
        ['K'] = "#...#|#..#.|#.#..|##...|#.#..|#..#.|#...#",
        ['L'] = "#....|#....|#....|#....|#....|#....|#####",
        ['M'] = "#...#|##.##|#.#.#|#.#.#|#...#|#...#|#...#",
        ['N'] = "#...#|#...#|##..#|#.#.#|#..##|#...#|#...#",
        ['O'] = ".###.|#...#|#...#|#...#|#...#|#...#|.###.",
        ['P'] = "####.|#...#|#...#|####.|#....|#....|#....",
        ['Q'] = ".###.|#...#|#...#|#...#|#.#.#|#..#.|.##.#",
        ['R'] = "####.|#...#|#...#|####.|#.#..|#..#.|#...#",
        ['S'] = ".####|#....|#....|.###.|....#|....#|####.",
        ['T'] = "#####|..#..|..#..|..#..|..#..|..#..|..#..",
        ['U'] = "#...#|#...#|#...#|#...#|#...#|#...#|.###.",
        ['V'] = "#...#|#...#|#...#|#...#|#...#|.#.#.|..#..",
        ['W'] = "#...#|#...#|#...#|#.#.#|#.#.#|#.#.#|.#.#.",
        ['X'] = "#...#|#...#|.#.#.|..#..|.#.#.|#...#|#...#",
        ['Y'] = "#...#|#...#|.#.#.|..#..|..#..|..#..|..#..",
        ['Z'] = "#####|....#|...#.|..#..|.#...|#....|#####",
        ['a'] = ".....|.....|.###.|....#|.####|#...#|.####",
        ['b'] = "#....|#....|####.|#...#|#...#|#...#|####.",
        ['c'] = ".....|.....|.###.|#....|#....|#....|.###.",
        ['d'] = "....#|....#|.####|#...#|#...#|#...#|.####",
        ['e'] = ".....|.....|.###.|#...#|#####|#....|.###.",
        ['f'] = "..##|.#..|####|.#..|.#..|.#..|.#..",
        ['g'] = ".....|.....|.####|#...#|#...#|#...#|.####|....#|.###.",
        ['h'] = "#....|#....|####.|#...#|#...#|#...#|#...#",
        ['i'] = ".#.|...|##.|.#.|.#.|.#.|###",
        ['j'] = "..#|...|.##|..#|..#|..#|..#|#.#|.#.",
        ['k'] = "#...|#...|#..#|#.#.|##..|#.#.|#..#",
        ['l'] = "##.|.#.|.#.|.#.|.#.|.#.|###",
        ['m'] = ".....|.....|##.#.|#.#.#|#.#.#|#.#.#|#.#.#",
        ['n'] = ".....|.....|####.|#...#|#...#|#...#|#...#",
        ['o'] = ".....|.....|.###.|#...#|#...#|#...#|.###.",
        ['p'] = ".....|.....|####.|#...#|#...#|#...#|####.|#....|#....",
        ['q'] = ".....|.....|.####|#...#|#...#|#...#|.####|....#|....#",
        ['r'] = "....|....|#.##|##..|#...|#...|#...",
        ['s'] = ".....|.....|.####|#....|.###.|....#|####.",
        ['t'] = ".#..|.#..|####|.#..|.#..|.#..|..##",
        ['u'] = ".....|.....|#...#|#...#|#...#|#...#|.####",
        ['v'] = ".....|.....|#...#|#...#|#...#|.#.#.|..#..",
        ['w'] = ".....|.....|#...#|#...#|#.#.#|#.#.#|.#.#.",
        ['x'] = ".....|.....|#...#|.#.#.|..#..|.#.#.|#...#",
        ['y'] = ".....|.....|#...#|#...#|#...#|#...#|.####|....#|.###.",
        ['z'] = ".....|.....|#####|...#.|..#..|.#...|#####",
        ['á'] = "...#.|..#..|.###.|....#|.####|#...#|.####",
        ['é'] = "...#.|..#..|.###.|#...#|#####|#....|.###.",
        ['í'] = "..#|.#.|##.|.#.|.#.|.#.|###",
        ['ó'] = "...#.|..#..|.###.|#...#|#...#|#...#|.###.",
        ['ú'] = "...#.|..#..|#...#|#...#|#...#|#...#|.####",
        ['ñ'] = ".##.#|#.##.|####.|#...#|#...#|#...#|#...#",
        ['ü'] = ".#.#.|.....|#...#|#...#|#...#|#...#|.####",
        ['0'] = ".###.|#...#|#..##|#.#.#|##..#|#...#|.###.",
        ['1'] = ".#.|##.|.#.|.#.|.#.|.#.|###",
        ['2'] = ".###.|#...#|....#|...#.|..#..|.#...|#####",
        ['3'] = "####.|....#|....#|.###.|....#|....#|####.",
        ['4'] = "...#.|..##.|.#.#.|#..#.|#####|...#.|...#.",
        ['5'] = "#####|#....|####.|....#|....#|#...#|.###.",
        ['6'] = ".###.|#....|#....|####.|#...#|#...#|.###.",
        ['7'] = "#####|....#|...#.|..#..|.#...|.#...|.#...",
        ['8'] = ".###.|#...#|#...#|.###.|#...#|#...#|.###.",
        ['9'] = ".###.|#...#|#...#|.####|....#|....#|.###.",
        ['.'] = ".|.|.|.|.|.|#",
        [','] = "..|..|..|..|..|..|.#|#.",
        ['!'] = "#|#|#|#|#|.|#",
        ['¡'] = "#|.|#|#|#|#|#",
        ['?'] = ".###.|#...#|....#|...#.|..#..|.....|..#..",
        ['¿'] = "..#..|.....|..#..|.#...|#....|#...#|.###.",
        [':'] = ".|.|#|.|.|#|.",
        [';'] = "..|..|.#|..|..|..|.#|#.",
        ['-'] = "...|...|...|###|...|...|...",
        ['+'] = "...|...|.#.|###|.#.|...|...",
        ['−'] = "...|...|...|###|...|...|...",
        ['—'] = ".....|.....|.....|#####|.....|.....|.....",
        ['…'] = ".....|.....|.....|.....|.....|.....|#.#.#",
        ['('] = "..#|.#.|#..|#..|#..|.#.|..#",
        [')'] = "#..|.#.|..#|..#|..#|.#.|#..",
        ['\''] = "#|#|.|.|.|.|.",
        ['"'] = "#.#|#.#|...|...|...|...|...",
        ['«'] = ".....|..#.#|.#.#.|#.#..|.#.#.|..#.#|.....",
        ['»'] = ".....|#.#..|.#.#.|..#.#|.#.#.|#.#..|.....",
        ['/'] = "....#|...#.|...#.|..#..|.#...|.#...|#....",
        ['%'] = "##..#|##..#|...#.|..#..|.#...|#..##|#..##",
        ['·'] = ".|.|.|#|.|.|.",
        ['▼'] = ".....|.....|#####|.###.|..#..|.....|.....",
        ['▶'] = "#...|##..|###.|####|###.|##..|#...",
        ['◀'] = "...#|..##|.###|####|.###|..##|...#",
        ['[' ] = "##|#.|#.|#.|#.|#.|##",
        [']'] = "##|.#|.#|.#|.#|.#|##",
    };

    /// <summary>Mayúsculas acentuadas: la letra y el acento en las 2 filas de arriba.</summary>
    private static readonly Dictionary<char, (char letter, string accent)> Accented = new()
    {
        ['Á'] = ('A', "...#.|..#.."), ['É'] = ('E', "...#.|..#.."), ['Í'] = ('I', "..#|.#."),
        ['Ó'] = ('O', "...#.|..#.."), ['Ú'] = ('U', "...#.|..#.."), ['Ñ'] = ('N', ".##.#|#.##."),
    };

    private readonly Texture2D _tex;
    private readonly Dictionary<char, Rectangle> _src = new();
    private const int SpaceW = 3;

    public PixelFont(GraphicsDevice gd)
    {
        var chars = G.Keys.Concat(Accented.Keys).ToList();
        var shapes = chars.Select(c => (c, rows: Shape(c))).ToList();
        int w = shapes.Sum(s => s.rows.Max(r => r.Length) + 1);
        var px = new Color[w * GlyphH];
        int x = 0;
        foreach (var (c, rows) in shapes)
        {
            int gw = rows.Max(r => r.Length);
            for (int y = 0; y < rows.Length; y++)
            for (int i = 0; i < rows[y].Length; i++)
                if (rows[y][i] == '#') px[y * w + x + i] = Color.White;
            _src[c] = new Rectangle(x, 0, gw, GlyphH);
            x += gw + 1;
        }
        _tex = new Texture2D(gd, w, GlyphH);
        _tex.SetData(px);
    }

    /// <summary>Las 11 filas de un glifo (las 2 de arriba sólo para acentos de mayúscula).</summary>
    private static string[] Shape(char c)
    {
        var rows = new string[GlyphH];
        string body, accent = null;
        if (Accented.TryGetValue(c, out var a)) { body = G[a.letter]; accent = a.accent; }
        else body = G[c];
        var b = body.Split('|');
        int w = b.Max(r => r.Length);
        for (int i = 0; i < GlyphH; i++) rows[i] = new string('.', w);
        for (int i = 0; i < b.Length; i++) rows[2 + i] = b[i];
        if (accent != null)
        {
            var ac = accent.Split('|');
            for (int i = 0; i < ac.Length; i++) rows[i] = ac[i].PadRight(w, '.');
        }
        return rows;
    }

    private Rectangle? Glyph(char c) => _src.TryGetValue(c, out var r) ? r : _src.TryGetValue(char.ToUpperInvariant(c), out r) ? r : null;

    /// <summary>Ancho del texto en píxeles (a escala <paramref name="scale"/>).</summary>
    public int Measure(string text, int scale = 1)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int w = 0;
        foreach (char c in text) w += (c == ' ' ? SpaceW : Glyph(c)?.Width ?? SpaceW) + Spacing;
        return (w - Spacing) * scale;
    }

    /// <summary>Corta el texto en renglones de hasta <paramref name="width"/> píxeles (por palabras).</summary>
    public List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        foreach (var para in text.Split('\n'))
        {
            string line = "";
            foreach (var word in para.Split(' '))
            {
                string tryLine = line.Length == 0 ? word : line + " " + word;
                if (Measure(tryLine) > width && line.Length > 0) { lines.Add(line); line = word; }
                else line = tryLine;
            }
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>
    /// Dibuja el texto con la esquina de arriba a la izquierda en <paramref name="pos"/> (píxeles
    /// del mundo). <paramref name="count"/> limita cuántos caracteres se ven (máquina de escribir);
    /// con <paramref name="shadow"/> lleva un contorno oscuro de un píxel.
    /// </summary>
    public void Draw(SpriteBatch sb, string text, Vector2 pos, Color color, Color? shadow = null, int count = int.MaxValue, int scale = 1)
    {
        if (string.IsNullOrEmpty(text)) return;
        pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));
        if (shadow is { } sc)
        {
            foreach (var o in new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1), new Vector2(1, 1) })
                Run(sb, text, pos + o * scale, sc, count, scale);
        }
        Run(sb, text, pos, color, count, scale);
    }

    public void DrawCentered(SpriteBatch sb, string text, Vector2 center, Color color, Color? shadow = null, int count = int.MaxValue, int scale = 1)
        => Draw(sb, text, new Vector2(center.X - Measure(text, scale) / 2f, center.Y - GlyphH * scale / 2f), color, shadow, count, scale);

    private void Run(SpriteBatch sb, string text, Vector2 pos, Color color, int count, int scale)
    {
        float x = pos.X;
        int n = Math.Min(count, text.Length);
        for (int i = 0; i < n; i++)
        {
            char c = text[i];
            if (c == ' ') { x += (SpaceW + Spacing) * scale; continue; }
            if (Glyph(c) is not { } src) { x += (SpaceW + Spacing) * scale; continue; }
            sb.Draw(_tex, new Vector2(x, pos.Y), src, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            x += (src.Width + Spacing) * scale;
        }
    }

    /// <summary>
    /// Los píxeles encendidos de un texto (a escala 1, desde la esquina de arriba a la izquierda,
    /// igual que <see cref="Draw"/>), con el índice de la letra a la que pertenece cada uno: para
    /// efectos que tratan cada píxel por separado (letras que se queman en brasas).
    /// </summary>
    public List<(Point p, int letter)> Pixels(string text)
    {
        var list = new List<(Point, int)>();
        int x = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == ' ' || (!G.ContainsKey(c) && !Accented.ContainsKey(c) && !G.ContainsKey(char.ToUpperInvariant(c)))) { x += SpaceW + Spacing; continue; }
            var rows = Shape(G.ContainsKey(c) || Accented.ContainsKey(c) ? c : char.ToUpperInvariant(c));
            int gw = rows.Max(r => r.Length);
            for (int y = 0; y < rows.Length; y++)
            for (int k = 0; k < rows[y].Length; k++)
                if (rows[y][k] == '#') list.Add((new Point(x + k, y), i));
            x += gw + Spacing;
        }
        return list;
    }

    public void Dispose() => _tex.Dispose();
}
