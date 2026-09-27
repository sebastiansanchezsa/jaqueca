namespace Jaqueca.Audio;

/// <summary>
/// Filtro bicuadrático (las recetas de Robert Bristow-Johnson): pasa-banda para los formantes de
/// la voz, pasa-bajos y pasa-altos para darle color al ruido y a la distancia. Se puede
/// reconfigurar en marcha (los formantes se deslizan de una vocal a otra) sin perder el estado.
/// </summary>
public sealed class Biquad
{
    private float _b0, _b1, _b2, _a1, _a2, _z1, _z2;

    /// <summary>Pasa-banda de ganancia 1 en <paramref name="freq"/> con ancho de banda <paramref name="bw"/> (Hz).</summary>
    public void Bandpass(float freq, float bw, float rate)
    {
        float w = 2 * MathF.PI * Math.Clamp(freq, 10, rate * 0.45f) / rate;
        float q = MathF.Max(0.3f, freq / MathF.Max(bw, 1));
        float alpha = MathF.Sin(w) / (2 * q), a0 = 1 + alpha;
        Set(alpha / a0, 0, -alpha / a0, -2 * MathF.Cos(w) / a0, (1 - alpha) / a0);
    }

    public void Lowpass(float freq, float q, float rate)
    {
        float w = 2 * MathF.PI * Math.Clamp(freq, 10, rate * 0.45f) / rate;
        float alpha = MathF.Sin(w) / (2 * q), c = MathF.Cos(w), a0 = 1 + alpha;
        Set((1 - c) / 2 / a0, (1 - c) / a0, (1 - c) / 2 / a0, -2 * c / a0, (1 - alpha) / a0);
    }

    public void Highpass(float freq, float q, float rate)
    {
        float w = 2 * MathF.PI * Math.Clamp(freq, 10, rate * 0.45f) / rate;
        float alpha = MathF.Sin(w) / (2 * q), c = MathF.Cos(w), a0 = 1 + alpha;
        Set((1 + c) / 2 / a0, -(1 + c) / a0, (1 + c) / 2 / a0, -2 * c / a0, (1 - alpha) / a0);
    }

    private void Set(float b0, float b1, float b2, float a1, float a2) { _b0 = b0; _b1 = b1; _b2 = b2; _a1 = a1; _a2 = a2; }

    /// <summary>Procesa una muestra (forma directa II transpuesta).</summary>
    public float Run(float x)
    {
        float y = _b0 * x + _z1;
        _z1 = _b1 * x - _a1 * y + _z2;
        _z2 = _b2 * x - _a2 * y;
        return y;
    }

    public void Reset() { _z1 = _z2 = 0; }
}

/// <summary>Pasa-bajos de un polo (barato: para suavizar y para el aire que se come los agudos con la distancia).</summary>
public struct OnePole
{
    private float _y;
    public float Run(float x, float k) => _y += (x - _y) * k;
    /// <summary>El coeficiente para una frecuencia de corte.</summary>
    public static float Coef(float freq, float rate) => 1 - MathF.Exp(-2 * MathF.PI * freq / rate);
}

/// <summary>Ruido blanco determinista (xorshift): la misma semilla da siempre el mismo sonido.</summary>
public sealed class Noise
{
    private uint _s;
    public Noise(int seed) { _s = (uint)seed * 2654435761u + 0x9E3779B9u; if (_s == 0) _s = 1; }
    /// <summary>Uniforme en -1..1.</summary>
    public float Next()
    {
        _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
        return (_s & 0xFFFFFF) / (float)0x800000 - 1;
    }
    /// <summary>Uniforme en 0..1.</summary>
    public float Unit() => (Next() + 1) * 0.5f;
}

/// <summary>
/// Reverberación de sala (el esquema de Freeverb: ocho peines con amortiguación en paralelo y
/// cuatro pasa-todos en serie por canal), afinada para piedra: cola larga y oscura. Estéreo: el
/// canal derecho usa retardos un poco más largos.
/// </summary>
public sealed class Reverb
{
    private static readonly int[] Combs = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
    private static readonly int[] Passes = { 556, 441, 341, 225 };
    private readonly float[][] _combL, _combR, _passL, _passR;
    private readonly int[] _ciL, _ciR, _piL, _piR;
    private readonly float[] _dampL, _dampR;
    /// <summary>Cuánto dura la cola (0..1) y cuánto se oscurece (0..1).</summary>
    public float Room = 0.86f, Damp = 0.45f;

    public Reverb(int rate)
    {
        float k = rate / 44100f;
        _combL = Combs.Select(n => new float[(int)(n * k)]).ToArray();
        _combR = Combs.Select(n => new float[(int)((n + 23) * k)]).ToArray();
        _passL = Passes.Select(n => new float[(int)(n * k)]).ToArray();
        _passR = Passes.Select(n => new float[(int)((n + 23) * k)]).ToArray();
        _ciL = new int[Combs.Length]; _ciR = new int[Combs.Length];
        _piL = new int[Passes.Length]; _piR = new int[Passes.Length];
        _dampL = new float[Combs.Length]; _dampR = new float[Combs.Length];
    }

    /// <summary>Una muestra de entrada (mono) → la cola en los dos canales.</summary>
    public (float l, float r) Run(float x)
    {
        x *= 0.015f;
        float l = 0, r = 0;
        for (int i = 0; i < Combs.Length; i++)
        {
            l += Comb(_combL[i], ref _ciL[i], ref _dampL[i], x);
            r += Comb(_combR[i], ref _ciR[i], ref _dampR[i], x);
        }
        for (int i = 0; i < Passes.Length; i++)
        {
            l = Pass(_passL[i], ref _piL[i], l);
            r = Pass(_passR[i], ref _piR[i], r);
        }
        return (l, r);
    }

    private float Comb(float[] buf, ref int i, ref float store, float x)
    {
        float y = buf[i];
        store = y * (1 - Damp) + store * Damp;
        buf[i] = x + store * Room;
        if (++i >= buf.Length) i = 0;
        return y;
    }

    private static float Pass(float[] buf, ref int i, float x)
    {
        float b = buf[i];
        float y = -x + b;
        buf[i] = x + b * 0.5f;
        if (++i >= buf.Length) i = 0;
        return y;
    }
}
