using Jaqueca.Audio;
using SVec3 = System.Numerics.Vector3;
using XVec3 = Microsoft.Xna.Framework.Vector3;

namespace Jaqueca.Client.Audio;

/// <summary>
/// Lo que el juego usa para hacer sonar un efecto: elige una variante al azar (nunca la misma dos
/// veces seguidas), le cambia un poco el tono (así cien pasos no suenan a uno repetido) y no deja
/// que el mismo sonido se amontone (un tope de veces por segundo). Los que se repiten (el fuego,
/// el viento) empiezan en un punto al azar de su vuelta: dos antorchas nunca van juntas.
/// </summary>
public sealed class Sfx
{
    private readonly Mixer _mixer;
    private readonly Random _rng = new(3);
    private readonly Dictionary<Sound, (float time, int variant)> _last = new();
    private float _time;

    public Sfx(Mixer mixer) { _mixer = mixer; }

    public Mixer Mixer => _mixer;

    /// <summary>Avanza el reloj (para los topes).</summary>
    public void Update(float dt) => _time += dt;

    /// <summary>
    /// Hace sonar <paramref name="s"/> en <paramref name="at"/> (mundo). <paramref name="spread"/>:
    /// cuánto puede variar el tono (±); <paramref name="gap"/>: segundos mínimos entre dos del mismo.
    /// Devuelve la voz (null si no sonó).
    /// </summary>
    public Mixer.Voice Play(Sound s, SVec3 at, float gain = 1, float pitch = 1, float spread = 0.06f, float gap = 0.03f, Bus bus = Bus.Effects, bool flat = false)
    {
        var bank = Sounds.Bank;
        if (bank == null || gain <= 0.001f) return null;
        _last.TryGetValue(s, out var last);
        if (_time - last.time < gap && last.time > 0) return null;
        int n = bank.Count(s);
        int v = _rng.Next(n);
        if (n > 1 && v == last.variant) v = (v + 1 + _rng.Next(n - 1)) % n;
        _last[s] = (_time, v);
        float p = pitch * (1 + spread * (float)(_rng.NextDouble() * 2 - 1));
        var voice = _mixer.Play(bank.Get(s, v), SoundBank.Rate, at, false, gain, 0.002f, 0, p, bus, flat);
        return voice.Done ? null : voice;
    }

    public Mixer.Voice Play(Sound s, XVec3 at, float gain = 1, float pitch = 1, float spread = 0.06f, float gap = 0.03f, Bus bus = Bus.Effects, bool flat = false) =>
        Play(s, new SVec3(at.X, at.Y, at.Z), gain, pitch, spread, gap, bus, flat);

    /// <summary>Un sonido que no tiene lugar (la interfaz, una campana lejana): igual en los dos oídos.</summary>
    public Mixer.Voice Flat(Sound s, float gain = 1, float pitch = 1) => Play(s, SVec3.Zero, gain, pitch, 0.02f, 0.05f, Bus.Effects, flat: true);

    /// <summary>Un sonido que se repite (el fuego, el viento) desde <paramref name="at"/>, entrando despacio.</summary>
    public Mixer.Voice Loop(Sound s, SVec3 at, float gain, Bus bus = Bus.Ambience, bool flat = false, float fadeIn = 1)
    {
        var bank = Sounds.Bank;
        if (bank == null) return null;
        var data = bank.Get(s, _rng.Next(bank.Count(s)));
        float start = (float)_rng.NextDouble() * data.Length / SoundBank.Rate;
        var voice = _mixer.Play(data, SoundBank.Rate, at, true, gain, fadeIn, start, 1, bus, flat);
        return voice.Done ? null : voice;
    }

    public void Stop(Mixer.Voice v, float fade = 0.5f)
    {
        if (v != null) _mixer.Stop(v, fade);
    }
}
