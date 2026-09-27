using Jaqueca.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Jaqueca.Client.Render;

/// <summary>
/// Las rampas de las figuras que se dibujan como mallas (los pensamientos, las manos y las armas de
/// Ernesto): cada material es una fila de cinco tonos armada con la misma regla que las de los
/// personajes de Inquisition (<see cref="Ramp.From"/>: las sombras hacia el violeta, los brillos hacia
/// el amarillo), así la luz elige un tono dentro de la rampa en vez de un gris. La columna 6 lleva cómo
/// se trabaja el material (lo que corre la luz, lo plano y si brilla). Las filas se reparten al armar
/// cada malla (los materiales iguales comparten fila) y la textura se rehace si aparecen nuevas.
/// </summary>
public static class FigurePalette
{
    private static readonly Dictionary<(uint rgb, bool shiny, float lift, float flat), int> Rows = new();
    private static readonly List<(uint rgb, bool shiny, float lift, float flat)> List = new();
    private static Texture2D _tex;
    private static bool _dirty = true;

    public static int Count => List.Count;

    /// <summary>La fila de un color con su manera de brillar (la crea si no estaba).</summary>
    public static int Row(uint rgb, bool shiny, float lift = 0, float flat = 0)
    {
        var key = (rgb & 0xFFFFFF, shiny, lift, flat);
        if (Rows.TryGetValue(key, out int r)) return r;
        r = List.Count;
        List.Add(key);
        Rows[key] = r;
        _dirty = true;
        return r;
    }

    /// <summary>La textura de las rampas (8 columnas: los cinco tonos, el contorno y los parámetros).</summary>
    public static Texture2D Texture(GraphicsDevice gd)
    {
        if (!_dirty && _tex != null && !_tex.IsDisposed) return _tex;
        int n = Math.Max(1, List.Count);
        var px = new Color[8 * n];
        for (int r = 0; r < List.Count; r++)
        {
            var (rgb, shiny, lift, flat) = List[r];
            var ramp = Ramp.From(rgb, shiny);
            for (int t = 0; t < 5; t++)
            {
                uint c = ramp[t];
                px[r * 8 + t] = new Color((int)Col.R(c), Col.G(c), Col.B(c));
            }
            uint ink = ramp[0];
            px[r * 8 + 5] = new Color((int)Col.R(ink), Col.G(ink), Col.B(ink));
            px[r * 8 + 6] = new Color(Math.Clamp(lift + 0.5f, 0, 1), Math.Clamp(flat, 0, 1), shiny ? 1f : 0f);
        }
        _tex?.Dispose();
        _tex = new Texture2D(gd, 8, n);
        _tex.SetData(px);
        _dirty = false;
        return _tex;
    }
}
