using static Jaqueca.Audio.Synth;

namespace Jaqueca.Audio;

/// <summary>
/// Las recetas de los sonidos: cada una arma una variante (la semilla cambia los detalles: el
/// tono, los tiempos, cuántos granos). Pensadas desde lo que pasa físicamente: un paso es el taco
/// que pega, la punta que apoya y la suela que roza; un tajo es el aire que corta la hoja, más
/// fuerte y más agudo cuanto más rápido va; un golpe en la carne es la masa que recibe (un grave
/// que cae), el chasquido del filo y lo mojado que se abre.
/// </summary>
public static partial class Recipes
{
    // ------------------------------------------------------------------ pasos

    internal static float[] StepStone(Synth s)
    {
        // El taco: la masa (un grave corto) y el clic duro de la suela contra la piedra, con arenilla.
        float c1 = s.U(1300, 2300), c2 = s.U(1800, 2800), c3 = s.U(3200, 5200);
        s.Thump(0, s.U(140, 180), s.U(65, 85), 0.002f, 0.018f, 0.55f);
        s.Noise(0, 0.03f, t => AD(t, 0.0006f, 0.004f), _ => c1, 0.9f, 0.9f);
        s.Grains(0, 0.02f, s.Rng.Next(3, 7), 2500, 7500, 0.25f, 0.0005f, 2);
        // La punta, un instante después, más suave; y la suela que roza al despegar.
        float toe = s.U(0.06f, 0.1f);
        s.Thump(toe, 170, 90, 0.002f, 0.012f, 0.22f);
        s.Noise(toe, 0.03f, t => AD(t, 0.0006f, 0.003f), _ => c2, 1.2f, 0.45f);
        s.Noise(toe - 0.01f, 0.08f, t => Hump(t, 0.08f, 0.3f), _ => c3, 1.2f, 0.1f);
        return s.Done(0.7f);
    }

    internal static float[] StepHeavy(Synth s)
    {
        // Una bota grande con un hombre pesado arriba: el grave largo, el cuero que cruje.
        float c1 = s.U(800, 1400);
        s.Thump(0, s.U(105, 125), s.U(48, 58), 0.003f, 0.035f, 1);
        s.Noise(0, 0.05f, t => AD(t, 0.001f, 0.008f), _ => c1, 0.8f, 0.8f);
        s.Noise(0, 0.06f, t => AD(t, 0.002f, 0.015f), _ => 420, 0.7f, 0.5f, lowpass: true, color: 1);
        s.Grains(0, 0.03f, s.Rng.Next(4, 9), 2000, 6000, 0.25f, 0.0006f, 2);
        float toe = s.U(0.09f, 0.13f);
        s.Thump(toe, 140, 70, 0.002f, 0.02f, 0.4f);
        s.Noise(toe, 0.1f, t => Hump(t, 0.1f, 0.3f), _ => 2800, 1, 0.1f);
        s.Creak(0.02f, 0.2f, t => 90 + 60 * t, t => Hump(t, 0.2f, 0.3f), 0.15f, 520, 1300);
        return s.Done(0.75f);
    }

    internal static float[] StepDirt(Synth s)
    {
        // Tierra con piedritas: el crujido (muchos granos juntos) sobre el golpe.
        s.Thump(0, 130, 65, 0.002f, 0.022f, 0.45f);
        s.Noise(0, 0.04f, t => AD(t, 0.001f, 0.01f), _ => 900, 0.7f, 0.4f, lowpass: true);
        s.Grains(0, s.U(0.07f, 0.11f), s.Rng.Next(30, 55), 1200, 5500, 0.4f, 0.0009f, 1.7f);
        return s.Done(0.55f);
    }

    internal static float[] Land(Synth s)
    {
        // Cae de un salto: los dos pies casi juntos, la rodilla que amortigua y la ropa.
        s.Thump(0, 100, 48, 0.003f, 0.05f, 1);
        s.Noise(0, 0.07f, t => AD(t, 0.001f, 0.015f), _ => 600, 0.7f, 0.7f, lowpass: true, color: 1);
        float c = s.U(1100, 1700);
        s.Noise(0, 0.02f, t => AD(t, 0.0005f, 0.004f), _ => c, 0.8f, 0.6f);
        s.Noise(s.U(0.01f, 0.025f), 0.02f, t => AD(t, 0.0005f, 0.004f), _ => c * 1.15f, 0.8f, 0.45f);
        s.Grains(0, 0.06f, s.Rng.Next(6, 12), 2000, 6000, 0.2f, 0.0006f, 2);
        s.Noise(0, 0.18f, t => Hump(t, 0.18f, 0.15f), _ => 1900, 0.5f, 0.1f);
        return s.Done(0.75f);
    }

    // ------------------------------------------------------------------ golpes

    /// <summary>
    /// El aire que corta algo que pasa rápido: ruido rosa por un filtro que sigue a la velocidad
    /// (campana): más fuerte y más agudo en el medio del golpe. El filo, además, silba (una banda
    /// angosta); lo que es grande (un mazo, un cuerpo) mueve aire grave.
    /// </summary>
    internal static float[] Swing(Synth s, float dur, float fLo, float fHi, float peak, float whistle, float body)
    {
        dur *= s.U(0.9f, 1.12f);
        peak *= s.U(0.9f, 1.1f);
        float k = s.U(0.9f, 1.1f);
        s.Noise(0, dur, t => MathF.Pow(Hump(t, dur, peak), 1.5f), t => fLo + (fHi - fLo) * k * Hump(t, dur, peak), 1.3f, 1, color: 1);
        if (whistle > 0)
        {
            float wf = fHi * s.U(1.1f, 1.4f);
            s.Noise(0, dur, t => MathF.Pow(Hump(t, dur, peak), 3), t => wf * (0.65f + 0.35f * Hump(t, dur, peak)), 12, whistle);
        }
        if (body > 0) s.Noise(0, dur, t => Hump(t, dur, peak * 0.8f), _ => 260, 0.7f, body, lowpass: true, color: 1);
        return s.Done(0.7f);
    }

    internal static float[] HitFlesh(Synth s)
    {
        // El impacto: la masa (grave que cae) y el chasquido del filo.
        s.Thump(0, s.U(135, 165), s.U(55, 65), 0.001f, 0.035f, 0.9f);
        float c = s.U(1800, 3000);
        s.Noise(0, 0.01f, t => AD(t, 0.0003f, 0.0025f), _ => c, 0.8f, 0.9f);
        // Lo mojado: la carne que se abre (ruido que baja de agudo a grave, con resonancia).
        float f0 = s.U(2200, 3200);
        s.Noise(0.003f, 0.16f, t => AD(t, 0.004f, 0.035f), t => f0 * MathF.Exp(-t * 14) + 480, 3.5f, 0.8f);
        s.Grains(0.01f, 0.1f, s.Rng.Next(10, 20), 1500, 6000, 0.25f, 0.0012f, 2);
        s.Drive(1.2f);
        return s.Done(0.85f);
    }

    internal static float[] HitBlunt(Synth s)
    {
        // Sin filo: el golpe sordo en el cuerpo y la palmada de la piel o la ropa.
        s.Thump(0, s.U(110, 135), s.U(50, 60), 0.001f, 0.05f, 1);
        s.Noise(0, 0.02f, t => AD(t, 0.0005f, 0.006f), _ => 2500, 0.7f, 0.8f, lowpass: true);
        s.Noise(0, 0.05f, t => AD(t, 0.001f, 0.012f), _ => 420, 0.7f, 0.6f, lowpass: true, color: 1);
        s.Noise(0.005f, 0.12f, t => Hump(t, 0.12f, 0.2f), _ => 2000, 0.6f, 0.08f);
        s.Drive(1.4f);
        return s.Done(0.85f);
    }

    internal static float[] HitHeavy(Synth s)
    {
        // Algo pesado que aplasta: grave largo, crujido de huesos y el aire que sale del cuerpo.
        s.Thump(0, s.U(90, 105), s.U(38, 45), 0.002f, 0.09f, 1);
        s.Noise(0, 0.08f, t => AD(t, 0.001f, 0.03f), _ => 900, 0.7f, 0.8f, lowpass: true, color: 1);
        s.Grains(0.002f, 0.05f, s.Rng.Next(15, 25), 1200, 4500, 0.35f, 0.0008f, 1.5f);
        float f0 = s.U(1600, 2200);
        s.Noise(0.004f, 0.2f, t => AD(t, 0.006f, 0.05f), t => f0 * MathF.Exp(-t * 9) + 350, 2.5f, 0.5f);
        s.Drive(1.6f);
        return s.Done(0.9f);
    }

    internal static float[] Dismember(Synth s)
    {
        // El filo que entra.
        s.Thump(0, 150, 60, 0.001f, 0.04f, 0.9f);
        s.Noise(0, 0.01f, t => AD(t, 0.0003f, 0.0025f), _ => 2400, 0.8f, 0.8f);
        // El hueso: un crac seco (varios chasquidos juntos) y astillas.
        float c0 = s.U(0.004f, 0.012f);
        int cracks = s.Rng.Next(3, 6);
        for (int i = 0; i < cracks; i++)
        {
            float f = s.U(1500, 4500);
            s.Noise(c0 + s.U(0, 0.014f), 0.008f, t => AD(t, 0.0002f, 0.0012f), _ => f, 1.5f, s.U(0.6f, 1));
        }
        s.Thump(c0, 320, 160, 0.0005f, 0.008f, 0.5f);
        s.Grains(c0, 0.05f, s.Rng.Next(15, 30), 1500, 6000, 0.35f, 0.0006f, 2);
        // Lo mojado, largo: la carne y el chorro que burbujea.
        float bub = s.U(22, 38), f1 = s.U(800, 1100);
        s.Noise(0.01f, 0.45f, t => AD(t, 0.01f, 0.1f) * (0.55f + 0.45f * MathF.Abs(MathF.Sin(MathF.PI * bub * t))), t => f1 + 1500 * MathF.Exp(-t * 6), 2.5f, 0.8f);
        s.Drive(1.3f);
        return s.Done(0.9f);
    }

    internal static float[] BloodSpray(Synth s)
    {
        // El chorro corto y las gotitas que golpean el piso, cada vez menos.
        s.Noise(0, 0.14f, t => AD(t, 0.003f, 0.04f), _ => 1300, 0.8f, 0.45f, color: 1);
        s.Grains(0.02f, 0.5f, s.Rng.Next(45, 75), 1500, 7000, 0.35f, 0.0008f, 2.2f);
        int drops = s.Rng.Next(4, 9);
        for (int i = 0; i < drops; i++)
        {
            float f0 = s.U(1100, 2200);
            s.Plop(0.03f + 0.5f * MathF.Pow(s.U(0, 1), 1.8f), f0, f0 * 1.5f, 0.01f, s.U(0.04f, 0.1f));
        }
        return s.Done(0.5f);
    }

    internal static float[] BloodDrip(Synth s)
    {
        float f0 = s.U(850, 1400);
        s.Plop(0, f0, f0 * s.U(1.8f, 2.4f), 0.022f, 0.6f);
        s.Grains(0, 0.01f, 3, 3000, 7000, 0.1f, 0.0005f);
        return s.Done(0.35f);
    }

    internal static float[] BodyFall(Synth s)
    {
        // Todo el peso contra el piso: grave largo, el golpe blando del cuerpo, la ropa y un rebote chico.
        s.Thump(0, s.U(85, 100), s.U(36, 42), 0.003f, 0.08f, 1);
        s.Noise(0, 0.12f, t => AD(t, 0.002f, 0.03f), _ => 520, 0.7f, 0.9f, lowpass: true, color: 1);
        s.Noise(0, 0.28f, t => Hump(t, 0.28f, 0.1f), _ => 2100, 0.6f, 0.12f);
        float b = s.U(0.1f, 0.19f);
        s.Thump(b, 115, 55, 0.002f, 0.04f, 0.4f);
        s.Noise(b, 0.05f, t => AD(t, 0.001f, 0.012f), _ => 700, 0.7f, 0.35f, lowpass: true);
        if (s.Chance(0.6f))
        {
            float b2 = b + s.U(0.08f, 0.15f);
            s.Thump(b2, 140, 70, 0.002f, 0.025f, 0.2f);
        }
        s.Grains(0, 0.2f, s.Rng.Next(6, 12), 1500, 5000, 0.1f, 0.001f, 2);
        s.Drive(1.2f);
        return s.Done(0.85f);
    }

    internal static float[] GibLand(Synth s)
    {
        // Un pedazo mojado contra la piedra.
        s.Thump(0, s.U(150, 190), 80, 0.001f, 0.02f, 0.6f);
        float c = s.U(700, 1100);
        s.Noise(0, 0.08f, t => AD(t, 0.001f, 0.02f), _ => c, 1.5f, 0.8f);
        s.Grains(0.003f, 0.08f, s.Rng.Next(6, 14), 1200, 5000, 0.2f, 0.0012f, 2);
        return s.Done(0.6f);
    }

    // ------------------------------------------------------------------ el grupo

    internal static float[] Grunt(Synth s, float f0, float scale)
    {
        // "¡Uh!": el golpe de la glotis, el tono que cae, mucho aire; y el aire que sale después.
        var vowels = new[] { Vocal.Uh, Vocal.A, Vocal.E, Vocal.O };
        var v = vowels[s.Rng.Next(vowels.Length)];
        float d = s.U(0.16f, 0.26f), p = f0 * s.U(0.92f, 1.08f);
        s.Voice(0.01f, new VoiceSpec
        {
            Dur = d, F0 = u => p * (1.15f - 0.3f * u), Amp = u => AD(u * d, 0.012f, d * 0.35f),
            Formants = u => Vocal.Scale(Vocal.Mix(v, Vocal.Uh, u), scale), Breath = 0.45f, Jitter = 0.02f, Rough = 0.3f, Drive = 0.3f,
        }, 1);
        s.Noise(0.01f + d * 0.6f, 0.16f, t => AD(t, 0.01f, 0.05f), _ => 1400 * scale, 0.8f, 0.1f);
        return s.Done(0.7f);
    }

    internal static float[] Groan(Synth s, float f0, float scale)
    {
        // Cae sin fuerzas: un quejido largo que se apaga y el último aire.
        float d = s.U(0.7f, 0.95f), p = f0 * s.U(0.92f, 1.06f);
        s.Voice(0.02f, new VoiceSpec
        {
            Dur = d, F0 = u => p * (1.1f - 0.35f * u), Amp = u => Hump(u, 1, 0.2f),
            Formants = u => Vocal.Scale(Vocal.Mix(Vocal.O, Vocal.Uu, u), scale), Breath = 0.55f, Jitter = 0.03f, Shimmer = 0.1f, Rough = 0.35f, Drive = 0.2f,
        }, 1);
        s.Noise(d * 0.8f, 0.4f, t => Hump(t, 0.4f, 0.2f), _ => 1100 * scale, 0.8f, 0.18f);
        return s.Done(0.65f);
    }

    internal static float[] Gasp(Synth s, float f0, float scale)
    {
        // Vuelve en sí: una bocanada de aire (ruido que sube) y un "ah" corto.
        float d = s.U(0.3f, 0.42f);
        s.Noise(0, d, t => Hump(t, d, 0.75f), t => 800 + 1600 * t / d, 1.2f, 0.7f);
        s.Voice(d - 0.02f, new VoiceSpec
        {
            Dur = 0.14f, F0 = u => f0 * (1.25f - 0.2f * u), Amp = u => AD(u * 0.14f, 0.01f, 0.05f),
            Formants = _ => Vocal.Scale(Vocal.A, scale), Breath = 0.7f,
        }, 0.6f);
        return s.Done(0.55f);
    }
}
