namespace Jaqueca.Audio;

/// <summary>
/// Cómo es una voz que dura <see cref="Dur"/> segundos (todo en función de u, de 0 a 1 a lo
/// largo del sonido): su tono, su volumen y sus formantes (la vocal); cuánto aire lleva, cuánto
/// tiembla (<see cref="Jitter"/> en el tono, <see cref="Shimmer"/> en el volumen), cuánto se
/// quiebra (<see cref="Rough"/>: la glotis que golpea un ciclo sí y otro no, como en un grito o un
/// gruñido), cuánto sale por la nariz (<see cref="Nasal"/>: con la boca cerrada, o cosida) y
/// cuánto se satura (<see cref="Drive"/>).
/// </summary>
public sealed class VoiceSpec
{
    public float Dur = 0.3f;
    public Func<float, float> F0 = _ => 120;
    public Func<float, float> Amp = u => MathF.Sin(MathF.PI * u);
    public Func<float, (float f1, float f2, float f3)> Formants = _ => (650, 1200, 2500);
    public float Breath = 0.3f, Jitter = 0.01f, Shimmer = 0.05f, Rough, Nasal, Drive, Width = 1;
}

/// <summary>
/// La voz: pulsos glóticos (el modelo de Rosenberg, con el cierre brusco que da los armónicos),
/// aire (ruido que sale con cada apertura), temblor y quiebre, por tres formantes en paralelo que
/// se mueven con la vocal; con la boca cerrada, sólo el murmullo grave de la nariz.
/// </summary>
public static class Vocal
{
    public static void Voice(this Synth s, float t0, VoiceSpec v, float gain)
    {
        int R = Synth.R;
        int i0 = s.At(t0), n = Math.Min(s.B.Length - i0, (int)(v.Dur * R));
        var f1 = new Biquad(); var f2 = new Biquad(); var f3 = new Biquad();
        var nose = new Biquad();
        nose.Lowpass(380, 1.2f, R);
        var nose2 = new Biquad();
        nose2.Bandpass(2300, 500, R);
        var air = new Biquad();
        air.Highpass(700, 0.7f, R);
        var roll = new Biquad();
        roll.Lowpass(4500, 0.7f, R);
        float ph = 0, jit = 0, shim = 0, prevGlot = 0;
        int cycle = 0;
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            if ((i & 15) == 0)
            {
                var (a, b, c) = v.Formants(u);
                f1.Bandpass(a, 90 * v.Width, R);
                f2.Bandpass(b, 120 * v.Width, R);
                f3.Bandpass(c, 180 * v.Width, R);
            }
            // Temblor: ruido muy lento en el tono y el volumen.
            jit += (s.W() - jit) * 0.002f;
            shim += (s.W() - shim) * 0.004f;
            float f0 = v.F0(u) * (1 + v.Jitter * jit * 8);
            ph += f0 / R;
            if (ph >= 1) { ph -= 1; cycle++; }
            float g = Glottal(ph);
            // Quiebre: un ciclo sí y otro no, más débil (medio tono más grave, áspero).
            if (v.Rough > 0 && (cycle & 1) == 1) g *= 1 - v.Rough * 0.7f;
            float open = ph < 0.6f ? 1 : 0.2f;
            float aspir = air.Run(s.W()) * open;
            float exc = g * (1 - v.Breath) + aspir * v.Breath * 1.6f;
            float mouth = f1.Run(exc) + f2.Run(exc) * 0.55f + f3.Run(exc) * 0.28f;
            float nasal = nose.Run(g) * 1.8f + nose2.Run(exc) * 0.15f;
            float y = roll.Run(mouth * (1 - v.Nasal) + nasal * v.Nasal);
            y *= v.Amp(u) * (1 + v.Shimmer * shim * 6);
            if (v.Drive > 0) y = MathF.Tanh(y * (1 + v.Drive * 6)) / (1 + v.Drive);
            s.B[i0 + i] += y * gain;
            prevGlot = g;
        }
    }

    private static float Glottal(float ph)
    {
        const float open = 0.45f, close = 0.16f;
        if (ph < open) return MathF.Sin(MathF.PI * ph / open) * 0.5f;
        if (ph < open + close) return -MathF.Sin(MathF.PI * 0.5f * (ph - open) / close) * 1.2f;
        return 0;
    }

    /// <summary>Las vocales (formantes de una voz de hombre).</summary>
    public static (float, float, float) A => (730, 1240, 2500);
    public static (float, float, float) E => (500, 1750, 2450);
    public static (float, float, float) I => (310, 2200, 2950);
    public static (float, float, float) O => (520, 900, 2400);
    public static (float, float, float) Uu => (350, 750, 2350);
    public static (float, float, float) Uh => (620, 1200, 2450);
    /// <summary>De una vocal a otra.</summary>
    public static (float, float, float) Mix((float a, float b, float c) x, (float a, float b, float c) y, float t) => (x.a + (y.a - x.a) * t, x.b + (y.b - x.b) * t, x.c + (y.c - x.c) * t);
    /// <summary>Una voz más chica (mujer, criatura): los formantes suben.</summary>
    public static (float, float, float) Scale((float a, float b, float c) x, float k) => (x.a * k, x.b * k, x.c * k);
}
