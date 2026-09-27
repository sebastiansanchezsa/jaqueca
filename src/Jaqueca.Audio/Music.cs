using NVorbis;

namespace Jaqueca.Audio;

/// <summary>Dónde se repite un tema: de <see cref="End"/> vuelve a <see cref="Start"/> (cuadros), cruzando los dos durante <see cref="Fade"/> cuadros.</summary>
public readonly record struct LoopPoints(long Start, long End, int Fade);

/// <summary>
/// Un tema del OST, leído del .ogg a medida que suena (nunca entero en memoria). Se repite sin
/// corte: al llegar a <see cref="LoopPoints.End"/> sigue desde <see cref="LoopPoints.Start"/>, y
/// los últimos cuadros antes del final se funden con los cuadros de antes del comienzo (dos
/// lectores del mismo archivo: uno termina, el otro ya viene sonando). Los puntos los busca
/// <c>ArtGen --musica</c> donde la música de los dos lados es la misma (ver <see cref="LoopFinder"/>).
/// Siempre da estéreo intercalado a la frecuencia del archivo.
/// </summary>
public sealed class Track : IDisposable
{
    public readonly string Name;
    public readonly int Rate, Channels;
    /// <summary>Cuadros del archivo.</summary>
    public readonly long Length;
    public readonly LoopPoints Loop;
    private readonly VorbisReader _a, _b;
    private VorbisReader _main, _alt;
    private long _pos;
    private bool _altReady;
    private float[] _ta = Array.Empty<float>(), _tb = Array.Empty<float>(), _mono = Array.Empty<float>();

    /// <summary>En qué cuadro va y cuántas veces volvió al principio del bucle.</summary>
    public long Position => _pos;
    public int Loops { get; private set; }
    /// <summary>Suena una sola vez (una intro): al terminar, silencio (el cuadro sigue contando).</summary>
    public readonly bool Once;
    /// <summary>Si ya terminó (sólo los de una vez).</summary>
    public bool Finished => Once && _pos >= Length;

    public Track(string path, LoopPoints? loop = null, bool once = false)
    {
        Once = once;
        Name = Path.GetFileNameWithoutExtension(path);
        _a = new VorbisReader(path);
        _b = new VorbisReader(path);
        Rate = _a.SampleRate;
        Channels = _a.Channels;
        Length = _a.TotalSamples;
        // Sin puntos: todo el tema, cruzando el último segundo y medio con el principio (una sola vez: todo, sin cruce).
        var l = once ? new LoopPoints(0, Length, 0) : loop ?? new LoopPoints(0, Length, (int)(1.5f * Rate));
        long end = Math.Clamp(l.End, 1, Length);
        int fade = (int)Math.Clamp(l.Fade, 0, end / 2);
        long start = Math.Clamp(l.Start, fade, end - 1);
        Loop = new LoopPoints(start, end, fade);
        _main = _a;
        _alt = _b;
    }

    /// <summary>Salta al cuadro <paramref name="frame"/> (dentro del bucle).</summary>
    public void Seek(long frame)
    {
        _pos = Math.Clamp(frame, 0, Loop.End - 1);
        _main.SamplePosition = _pos;
        _altReady = false;
    }

    /// <summary>Llena <paramref name="frames"/> cuadros estéreo de <paramref name="buf"/> (desde <paramref name="offset"/>, en muestras) y avanza, repitiendo.</summary>
    public void Read(float[] buf, int offset, int frames)
    {
        if (Once)
        {
            int n = (int)Math.Clamp(Length - _pos, 0, frames);
            if (n > 0) Pull(_main, buf, offset, n);
            Array.Clear(buf, offset + n * 2, (frames - n) * 2);
            _pos += frames;
            return;
        }
        int done = 0;
        while (done < frames)
        {
            long xStart = Loop.End - Loop.Fade;
            if (_pos < xStart)
            {
                int n = (int)Math.Min(frames - done, xStart - _pos);
                Pull(_main, buf, offset + done * 2, n);
                _pos += n;
                done += n;
                continue;
            }
            // Cruce: el final del bucle se apaga mientras sube lo de antes del comienzo.
            if (!_altReady)
            {
                _alt.SamplePosition = Loop.Start - Loop.Fade + (_pos - xStart);
                _altReady = true;
            }
            int m = (int)Math.Min(frames - done, Loop.End - _pos);
            if (_ta.Length < m * 2) { _ta = new float[m * 2]; _tb = new float[m * 2]; }
            Pull(_main, _ta, 0, m);
            Pull(_alt, _tb, 0, m);
            for (int i = 0; i < m; i++)
            {
                float u = Loop.Fade > 0 ? (_pos + i - xStart + 0.5f) / Loop.Fade : 1;
                float ga = MathF.Cos(u * MathF.PI / 2), gb = MathF.Sin(u * MathF.PI / 2);
                buf[offset + (done + i) * 2] = _ta[2 * i] * ga + _tb[2 * i] * gb;
                buf[offset + (done + i) * 2 + 1] = _ta[2 * i + 1] * ga + _tb[2 * i + 1] * gb;
            }
            _pos += m;
            done += m;
            if (_pos >= Loop.End)
            {
                (_main, _alt) = (_alt, _main);
                _pos = Loop.Start;
                _altReady = false;
                Loops++;
            }
        }
    }

    /// <summary>Lee <paramref name="frames"/> cuadros de un lector, en estéreo (lo que falte, silencio).</summary>
    private void Pull(VorbisReader r, float[] buf, int offset, int frames)
    {
        if (Channels == 2)
        {
            int got = 0;
            while (got < frames * 2)
            {
                int n = r.ReadSamples(buf, offset + got, frames * 2 - got);
                if (n <= 0) break;
                got += n;
            }
            Array.Clear(buf, offset + got, frames * 2 - got);
            return;
        }
        int ch = Channels;
        if (_mono.Length < frames * ch) _mono = new float[frames * ch];
        int have = 0;
        while (have < frames * ch)
        {
            int n = r.ReadSamples(_mono, have, frames * ch - have);
            if (n <= 0) break;
            have += n;
        }
        for (int i = 0; i < frames; i++)
        {
            float l = i * ch < have ? _mono[i * ch] : 0;
            float rr = ch > 1 && i * ch + 1 < have ? _mono[i * ch + 1] : l;
            buf[offset + 2 * i] = l;
            buf[offset + 2 * i + 1] = rr;
        }
    }

    public void Dispose()
    {
        _a.Dispose();
        _b.Dispose();
    }

    /// <summary>Cuándo empieza a sonar un tema (s): el primer cuadro que pasa de <paramref name="threshold"/> (en los primeros <paramref name="within"/> s).</summary>
    public static double FirstSound(string path, float threshold = 0.02f, double within = 5)
    {
        using var r = new VorbisReader(path);
        int ch = Math.Max(1, r.Channels);
        var buf = new float[4096 * ch];
        long frame = 0, max = (long)(within * r.SampleRate);
        while (frame < max)
        {
            int n = r.ReadSamples(buf, 0, buf.Length);
            if (n <= 0) break;
            for (int i = 0; i < n; i++)
                if (MathF.Abs(buf[i]) > threshold) return (frame + i / ch) / (double)r.SampleRate;
            frame += n / ch;
        }
        return 0;
    }

    /// <summary>
    /// Lee la tabla de bucles (una línea por tema: <c>nombre;inicio;fin;fundido</c>, en cuadros; lo
    /// que empieza con # es comentario).
    /// </summary>
    public static Dictionary<string, LoopPoints> ReadLoops(string path)
    {
        var d = new Dictionary<string, LoopPoints>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return d;
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var p = line.Split(';');
            if (p.Length < 4) continue;
            if (long.TryParse(p[1], out long s) && long.TryParse(p[2], out long e) && int.TryParse(p[3], out int f)) d[p[0].Trim()] = new LoopPoints(s, e, f);
        }
        return d;
    }

    /// <summary>Todo el tema en mono (para analizarlo), con su frecuencia.</summary>
    public static (float[] mono, int rate) Decode(string path)
    {
        using var r = new VorbisReader(path);
        int ch = r.Channels;
        var mono = new float[r.TotalSamples];
        var buf = new float[4096 * ch];
        long at = 0;
        int n;
        while ((n = r.ReadSamples(buf, 0, buf.Length)) > 0)
        {
            for (int i = 0; i + ch <= n && at < mono.Length; i += ch)
            {
                float s = 0;
                for (int c = 0; c < ch; c++) s += buf[i + c];
                mono[at++] = s / ch;
            }
        }
        return (mono, r.SampleRate);
    }
}
