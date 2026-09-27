namespace Jaqueca.Audio;

/// <summary>
/// El taller donde se arma un sonido: un buffer mono a <see cref="Mixer.Rate"/> y las piezas con
/// que se hacen los sonidos del mundo, todas físicas o casi:
/// <list type="bullet">
/// <item>ruido (blanco, rosa, marrón) filtrado y con envolvente: el aire, el roce, lo mojado;</item>
/// <item>golpes graves que caen de tono (la masa de un cuerpo contra el piso);</item>
/// <item>modos: senos amortiguados en frecuencias no armónicas (el hierro, las llaves, una campana);</item>
/// <item>granos: chasquidos cortísimos al azar (arenilla, crujidos, chisporroteo, gotas);</item>
/// <item>cuerda pulsada (Karplus-Strong) y roce que traba y suelta (el crujido de la madera y las bisagras);</item>
/// <item>la voz (<see cref="Voice"/>): pulsos glóticos con temblor y aspereza por formantes.</item>
/// </list>
/// Todo con una semilla: la misma variante suena siempre igual.
/// </summary>
public sealed class Synth
{
    public const int R = Mixer.Rate;
    public float[] B;
    public readonly Random Rng;
    private readonly Noise _noise;

    public Synth(int seed, float seconds)
    {
        Rng = new Random(seed);
        _noise = new Noise(seed * 7919 + 17);
        B = new float[Math.Max(1, (int)(seconds * R))];
    }

    public float Seconds => B.Length / (float)R;
    public int At(float t) => Math.Clamp((int)(t * R), 0, B.Length);
    /// <summary>Al azar entre a y b.</summary>
    public float U(float a, float b) => a + (b - a) * (float)Rng.NextDouble();
    public bool Chance(float p) => Rng.NextDouble() < p;
    /// <summary>Ruido blanco -1..1.</summary>
    public float W() => _noise.Next();

    // ------------------------------------------------------------------ envolventes

    /// <summary>Sube lineal en <paramref name="a"/> y cae exponencial con constante <paramref name="d"/>.</summary>
    public static float AD(float t, float a, float d) => t < 0 ? 0 : t < a ? t / MathF.Max(a, 1e-5f) : MathF.Exp(-(t - a) / MathF.Max(d, 1e-5f));
    /// <summary>Una campana (seno²) de largo <paramref name="len"/> con el pico en <paramref name="peak"/> (0..1).</summary>
    public static float Hump(float t, float len, float peak = 0.5f)
    {
        if (t < 0 || t > len) return 0;
        float u = t / len;
        float v = u < peak ? u / peak : 1 - (u - peak) / (1 - peak);
        return MathF.Sin(v * MathF.PI / 2) * MathF.Sin(v * MathF.PI / 2);
    }
    public static float Smooth(float e0, float e1, float x) { float t = Math.Clamp((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t); }

    // ------------------------------------------------------------------ piezas

    /// <summary>
    /// Ruido filtrado desde <paramref name="t0"/> durante <paramref name="dur"/>, con envolvente
    /// <paramref name="env"/>(t) y un filtro que puede moverse: <paramref name="band"/>(t) da el
    /// centro (Hz) y <paramref name="q"/> su agudeza (pasa-banda); si <paramref name="lowpass"/>,
    /// es pasa-bajos en esa frecuencia. <paramref name="color"/>: 0 blanco, 1 rosa, 2 marrón.
    /// </summary>
    public void Noise(float t0, float dur, Func<float, float> env, Func<float, float> band, float q = 1, float gain = 1, bool lowpass = false, int color = 0)
    {
        var f = new Biquad();
        var f2 = new Biquad();
        int i0 = At(t0), n = Math.Min(B.Length - i0, (int)(dur * R));
        var pink = new Pink();
        float brown = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)R;
            if ((i & 15) == 0)
            {
                float c = band(t);
                if (lowpass) { f.Lowpass(c, q, R); f2.Lowpass(c, 0.7f, R); }
                else { f.Bandpass(c, c / MathF.Max(q, 0.05f), R); f2.Bandpass(c, c / MathF.Max(q, 0.05f), R); }
            }
            float x = W();
            if (color == 1) x = pink.Run(x);
            else if (color == 2) { brown = (brown + 0.02f * x) * 0.995f; x = brown * 6; }
            float y = f.Run(x);
            if (q > 2 && !lowpass) y = f2.Run(y) * 2;   // más angosto: dos en serie
            else if (lowpass) y = f2.Run(y);
            B[i0 + i] += y * env(t) * gain;
        }
    }

    /// <summary>Un golpe grave: un seno que cae de <paramref name="f0"/> a <paramref name="f1"/> Hz, que sube en <paramref name="a"/> y se apaga en <paramref name="d"/>.</summary>
    public void Thump(float t0, float f0, float f1, float a, float d, float gain)
    {
        int i0 = At(t0), n = Math.Min(B.Length - i0, (int)((a + d * 6) * R));
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)R;
            float f = f1 + (f0 - f1) * MathF.Exp(-t / MathF.Max(d * 0.6f, 1e-4f));
            ph += f / R;
            B[i0 + i] += MathF.Sin((float)(ph * 2 * Math.PI)) * AD(t, a, d) * gain;
        }
    }

    /// <summary>Modos: senos amortiguados (frecuencia, amplitud, cuánto duran) que arrancan en <paramref name="t0"/>.</summary>
    public void Modes(float t0, float gain, params (float f, float a, float d)[] modes)
    {
        int i0 = At(t0);
        foreach (var (f, a, d) in modes)
        {
            if (f >= R * 0.45f) continue;
            int n = Math.Min(B.Length - i0, (int)(d * 7 * R) + 1);
            float w = 2 * MathF.PI * f / R, ph = U(0, 6.28f);
            float k = MathF.Exp(-1f / (d * R)), env = a * gain;
            // Un seno que se apaga: recurrencia (barata y exacta).
            float s1 = MathF.Sin(ph), s0 = MathF.Sin(ph - w), c = 2 * MathF.Cos(w);
            for (int i = 0; i < n; i++)
            {
                float s = c * s1 - s0;
                s0 = s1; s1 = s;
                // Arranca en 1 ms (sin clic).
                float att = i < R / 1000 ? i / (R / 1000f) : 1;
                B[i0 + i] += s0 * env * att;
                env *= k;
            }
        }
    }

    /// <summary>
    /// Un objeto de metal golpeado: modos inarmónicos a partir de <paramref name="f"/> (proporciones
    /// de una barra o un aro), más agudos más cortos, y el chasquido del golpe.
    /// </summary>
    public void Metal(float t0, float f, float gain, float decay, float bright = 1, float[] ratios = null)
    {
        ratios ??= new[] { 1f, 2.76f, 5.40f, 8.93f, 13.3f };
        var modes = new (float, float, float)[ratios.Length];
        for (int i = 0; i < ratios.Length; i++)
            modes[i] = (f * ratios[i] * U(0.985f, 1.015f), MathF.Pow(0.62f, i) * (i == 0 ? 1 : bright), decay / (1 + 0.7f * i));
        Modes(t0, gain, modes);
        Noise(t0, 0.004f, t => 1, _ => MathF.Min(f * 3, 9000), 0.7f, gain * 0.5f * bright);
    }

    /// <summary>
    /// Granos: <paramref name="count"/> chasquidos al azar entre <paramref name="t0"/> y
    /// <paramref name="t0"/>+<paramref name="dur"/> (más al principio si <paramref name="front"/> &gt; 1),
    /// cada uno de <paramref name="len"/> s, filtrados en una banda al azar entre <paramref name="fLo"/> y <paramref name="fHi"/>.
    /// </summary>
    public void Grains(float t0, float dur, int count, float fLo, float fHi, float gain, float len = 0.0015f, float front = 1, float q = 1.5f)
    {
        var f = new Biquad();
        for (int g = 0; g < count; g++)
        {
            float t = t0 + dur * MathF.Pow(U(0, 1), front);
            float c = U(fLo, fHi);
            f.Reset();
            f.Bandpass(c, c / q, R);
            float a = gain * U(0.3f, 1);
            int i0 = At(t), n = Math.Min(B.Length - i0, (int)(len * 6 * R) + 1);
            for (int i = 0; i < n; i++)
            {
                float tt = i / (float)R;
                float e = tt < len ? 1 : MathF.Exp(-(tt - len) / (len * 0.8f));
                B[i0 + i] += f.Run(W()) * e * a;
            }
        }
    }

    /// <summary>Una gota que cae en agua: un seno cuyo tono sube de golpe (la burbuja que resuena) y se apaga.</summary>
    public void Plop(float t0, float f0, float f1, float dur, float gain)
    {
        int i0 = At(t0), n = Math.Min(B.Length - i0, (int)(dur * R));
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)R, u = t / dur;
            float f = f0 * MathF.Pow(f1 / f0, MathF.Min(1, u * 1.3f));
            ph += f / R;
            float e = MathF.Min(1, t / 0.0008f) * MathF.Exp(-t / (dur * 0.35f));
            B[i0 + i] += MathF.Sin((float)(ph * 2 * Math.PI)) * e * gain;
        }
    }

    /// <summary>Una cuerda pulsada (Karplus-Strong) de <paramref name="f"/> Hz que dura <paramref name="d"/> s.</summary>
    public void Pluck(float t0, float f, float d, float gain, float bright = 0.5f)
    {
        int period = Math.Max(2, (int)(R / f));
        var line = new float[period];
        for (int i = 0; i < period; i++) line[i] = W();
        int i0 = At(t0), n = Math.Min(B.Length - i0, (int)(d * 4 * R));
        float loss = MathF.Pow(0.001f, 1f / (d * f));
        float prev = 0;
        for (int i = 0; i < n; i++)
        {
            int k = i % period;
            float y = line[k];
            float next = (y * bright + prev * (1 - bright));
            prev = y;
            line[k] = next * loss;
            B[i0 + i] += y * gain;
        }
    }

    /// <summary>
    /// Roce que traba y suelta (una bisagra, la madera del arco, el hierro tirante): pulsos cuya
    /// frecuencia sigue <paramref name="rate"/>(t) (Hz), cada uno haciendo sonar un par de
    /// resonancias (<paramref name="res"/>).
    /// </summary>
    public void Creak(float t0, float dur, Func<float, float> rate, Func<float, float> env, float gain, params float[] res)
    {
        var fs = res.Select(r => { var b = new Biquad(); b.Bandpass(r, r / 6, R); return b; }).ToArray();
        int i0 = At(t0), n = Math.Min(B.Length - i0, (int)(dur * R));
        float ph = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)R;
            ph += rate(t) * U(0.85f, 1.15f) / R;
            float x = 0;
            if (ph >= 1) { ph -= 1; x = U(0.6f, 1); }
            x += W() * 0.03f;
            float y = 0;
            foreach (var f in fs) y += f.Run(x);
            B[i0 + i] += y * env(t) * gain;
        }
    }

    // ------------------------------------------------------------------ al final

    public void Lowpass(float f, float q = 0.7f) { var b = new Biquad(); b.Lowpass(f, q, R); for (int i = 0; i < B.Length; i++) B[i] = b.Run(B[i]); }
    public void Highpass(float f, float q = 0.7f) { var b = new Biquad(); b.Highpass(f, q, R); for (int i = 0; i < B.Length; i++) B[i] = b.Run(B[i]); }

    /// <summary>Satura suave (tanh): más cuerpo y aspereza.</summary>
    public void Drive(float k) { float n = MathF.Tanh(k); for (int i = 0; i < B.Length; i++) B[i] = MathF.Tanh(B[i] * k) / n; }

    /// <summary>Deja el pico en <paramref name="peak"/>, con fundidos cortos en los bordes (sin clic) y sin continua.</summary>
    public float[] Done(float peak = 0.8f)
    {
        // Sin componente continua (un pasa-altos muy bajo).
        var hp = new Biquad();
        hp.Highpass(25, 0.7f, R);
        for (int i = 0; i < B.Length; i++) B[i] = hp.Run(B[i]);
        float m = 1e-6f;
        foreach (var v in B) m = MathF.Max(m, MathF.Abs(v));
        float g = peak / m;
        int fade = Math.Min(B.Length / 4, R / 500);
        for (int i = 0; i < B.Length; i++)
        {
            float e = 1;
            if (i < 16) e = i / 16f;
            if (B.Length - 1 - i < fade) e = (B.Length - 1 - i) / (float)fade;
            B[i] *= g * e;
        }
        // Recorta el silencio del final.
        int last = B.Length - 1;
        while (last > R / 50 && MathF.Abs(B[last]) < 1e-4f) last--;
        if (last < B.Length - 1) B = B[..(last + 1)];
        return B;
    }

    /// <summary>
    /// Para un sonido que se repite (el fuego, el viento): hace que el final empalme con el
    /// principio, fundiendo los últimos <paramref name="xfade"/> segundos sobre los primeros.
    /// </summary>
    public float[] Looped(float xfade, float peak = 0.8f)
    {
        int x = (int)(xfade * R);
        int n = B.Length - x;
        var o = new float[n];
        Array.Copy(B, o, n);
        // Los primeros x cuadros: lo del final se va, lo del principio viene (igual potencia).
        for (int i = 0; i < x; i++)
        {
            float u = (i + 0.5f) / x;
            o[i] = B[n + i] * MathF.Cos(u * MathF.PI / 2) + B[i] * MathF.Sin(u * MathF.PI / 2);
        }
        float m = 1e-6f;
        foreach (var v in o) m = MathF.Max(m, MathF.Abs(v));
        for (int i = 0; i < n; i++) o[i] *= peak / m;
        return o;
    }
}

/// <summary>Ruido rosa (el filtro de Paul Kellet): cae 3 dB por octava, como el viento y el mar.</summary>
public sealed class Pink
{
    private float _b0, _b1, _b2, _b3, _b4, _b5, _b6;
    public float Run(float w)
    {
        _b0 = 0.99886f * _b0 + w * 0.0555179f;
        _b1 = 0.99332f * _b1 + w * 0.0750759f;
        _b2 = 0.96900f * _b2 + w * 0.1538520f;
        _b3 = 0.86650f * _b3 + w * 0.3104856f;
        _b4 = 0.55000f * _b4 + w * 0.5329522f;
        _b5 = -0.7616f * _b5 - w * 0.0168980f;
        float y = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + w * 0.5362f;
        _b6 = w * 0.115926f;
        return y * 0.11f;
    }
}
