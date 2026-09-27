using System.Numerics;

namespace Jaqueca.Sprites;

/// <summary>
/// La regla de luz → tono de los personajes, en un solo lugar: la usa el generador al hornear,
/// el compositor para calcular cuánto se corrió cada píxel de esa regla (pliegues, mechones,
/// líneas entre prendas) y el shader del juego la repite para reiluminar en tiempo real.
/// </summary>
public static class Shading
{
    /// <summary>Luz del horneado en el espacio de la cámara del horneado (x derecha, y arriba, z hacia la cámara).</summary>
    public static readonly Vector3 BakeLight = Vector3.Normalize(new Vector3(-0.5f, 0.62f, 0.6f));

    /// <summary>Umbrales de los tonos 4, 3 y 2 (debajo queda 1; el 0 es para pliegues y contornos).</summary>
    public const float T4 = 0.93f, T4Shiny = 0.8f, T3 = 0.56f, T2 = 0.1f;

    public static float Light(Vector3 n, Vector3 light, float flat, float lift)
    {
        float l = Vector3.Dot(n, light);
        return l * (1 - flat) + 0.55f * flat + lift;
    }

    public static int Tone(float l, bool shiny) => l > (shiny ? T4Shiny : T4) ? 4 : l > T3 ? 3 : l > T2 ? 2 : 1;

    /// <summary>Tono que da la luz del horneado a una normal con ese material.</summary>
    public static int BakeTone(Vector3 n, MatDef m) => Tone(Light(n, BakeLight, m.Flat, m.Lift), m.Shiny);
}
