using System.Numerics;

namespace Jaqueca.Figures.Physics;

/// <summary>
/// Las colas de la disciplina del flagelante: seis cuerdas cortas que salen de la punta del
/// mango, cada una con un pedacito de metal en la punta (pesan más). Siguen al mango con su
/// inercia: cuelgan quieto, se quedan atrás y latiguean cuando lo descarga.
/// </summary>
public sealed class Flail
{
    public const int Tails = 6;
    public const float Length = 2.6f;
    public readonly Chain[] Cords = new Chain[Tails];

    public Flail(Vector3 tip, Func<float, float, float> ground)
    {
        for (int i = 0; i < Tails; i++)
            Cords[i] = new Chain(5, tip, Length * (0.85f + 0.06f * (i % 3)), 1.8f) { Ground = ground, Radius = 0.1f, Damping = 0.985f, Iterations = 8, Hook = false };
    }

    /// <summary>
    /// Avanza <paramref name="dt"/>: cada cola sale de la punta del mango (<paramref name="tip"/>,
    /// null = soltó la disciplina: caen), un poco corridas entre sí sobre el plano del mango.
    /// </summary>
    public void Step(Vector3? tip, Vector3 side, float dt)
    {
        int n = Math.Max(1, (int)MathF.Ceiling(dt / (1 / 120f) - 1e-3f));
        float h = dt / n;
        for (int k = 0; k < n; k++)
            for (int i = 0; i < Tails; i++)
            {
                Cords[i].Start = tip is { } t ? t + side * ((i - (Tails - 1) * 0.5f) * 0.07f) : null;
                Cords[i].Step(h);
            }
    }
}
