namespace Jaqueca.Audio;

/// <summary>Guarda sonido en WAV (PCM de 16 bits), para escucharlo fuera del juego.</summary>
public static class Wav
{
    /// <summary>Escribe <paramref name="samples"/> (-1..1, intercaladas si hay más de un canal).</summary>
    public static void Write(string path, float[] samples, int rate, int channels = 1)
    {
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        int bytes = samples.Length * 2;
        w.Write("RIFF"u8.ToArray());
        w.Write(36 + bytes);
        w.Write("WAVEfmt "u8.ToArray());
        w.Write(16);
        w.Write((short)1);
        w.Write((short)channels);
        w.Write(rate);
        w.Write(rate * channels * 2);
        w.Write((short)(channels * 2));
        w.Write((short)16);
        w.Write("data"u8.ToArray());
        w.Write(bytes);
        foreach (var s in samples) w.Write((short)Math.Clamp(MathF.Round(s * 32767), -32768, 32767));
    }

    /// <summary>Pasa un sonido mono por la reverberación de piedra (lo que se oiría en una cripta), en estéreo.</summary>
    public static float[] InCrypt(float[] mono, int rate, float wet = 0.35f)
    {
        var rev = new Reverb(rate);
        int tail = rate * 2;
        var st = new float[(mono.Length + tail) * 2];
        for (int i = 0; i < mono.Length + tail; i++)
        {
            float x = i < mono.Length ? mono[i] : 0;
            var (l, r) = rev.Run(x);
            st[2 * i] = x * (1 - wet) + l * wet;
            st[2 * i + 1] = x * (1 - wet) + r * wet;
        }
        float peak = st.Max(MathF.Abs);
        if (peak > 0.95f) for (int i = 0; i < st.Length; i++) st[i] *= 0.95f / peak;
        return st;
    }
}
