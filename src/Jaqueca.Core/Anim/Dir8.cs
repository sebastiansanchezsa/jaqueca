using System.Numerics;

namespace Jaqueca.Anim;

/// <summary>
/// Las 8 direcciones horneadas de los sprites. El índice por 45° da el ángulo en el plano del
/// piso: 0 = este (+x), y crece hacia el sur (+y, hacia la cámara), igual que la simulación.
/// </summary>
public enum Dir8 : byte { E, SE, S, SW, W, NW, N, NE }

public static class Dirs
{
    public const int Count = 8;

    /// <summary>Ángulo en radianes (0 = este, π/2 = sur).</summary>
    public static float Angle(this Dir8 d) => (int)d * MathF.PI / 4;

    public static Vector2 Vector(this Dir8 d) => new(MathF.Cos(d.Angle()), MathF.Sin(d.Angle()));

    /// <summary>La dirección más cercana a un ángulo cualquiera.</summary>
    public static Dir8 FromAngle(float a)
    {
        int i = (int)MathF.Round(a / (MathF.PI / 4));
        return (Dir8)(((i % Count) + Count) % Count);
    }

    public static Dir8 FromVector(Vector2 v) => FromAngle(MathF.Atan2(v.Y, v.X));

    public static string Name(this Dir8 d) => d.ToString().ToLowerInvariant();
}
