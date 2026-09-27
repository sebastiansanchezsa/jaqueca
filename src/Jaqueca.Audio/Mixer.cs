using System.Numerics;

namespace Jaqueca.Audio;

/// <summary>Por dónde pasa un sonido (cada uno con su volumen).</summary>
public enum Bus : byte
{
    /// <summary>Lo que pasa: pasos, golpes, voces.</summary>
    Effects,
    /// <summary>El lugar: el fuego de las antorchas, el viento, las gotas.</summary>
    Ambience,
}

/// <summary>
/// El mezclador del juego, por software y en 3D (los oídos son los de la cámara: <see cref="ListenerRight"/>). Cada voz suena desde un punto del mundo y se oye:
/// <list type="bullet">
/// <item>más bajo cuanto más lejos (atenuación inversa desde <see cref="Near"/> y corte en <see cref="Far"/>);</item>
/// <item>de su lado (paneo de igual potencia) y un poco antes en el oído más cercano (retardo interaural);</item>
/// <item>más apagada con la distancia (el aire se come los agudos) y detrás de un muro (<see cref="Occlusion"/>);</item>
/// <item>con más eco cuanto más lejos (un envío a la reverberación de la sala, <see cref="Room"/>).</item>
/// </list>
/// Encima, la música (<see cref="PlayMusic"/>): sin lugar ni eco, de fondo, cruzando un tema con
/// otro. Hay un tope de voces: si se pasa, se calla la que menos se oye. Mezcla estéreo
/// intercalado a <see cref="Rate"/>. Todo se suaviza por muestra: moverse, cambiar de volumen o
/// de tema nunca hace clic.
/// </summary>
public sealed class Mixer
{
    public const int Rate = 48000;
    public const int MaxVoices = 64;
    /// <summary>Hasta dónde se oye igual de fuerte y desde dónde ya no se oye (unidades del mundo; un hombre mide 16).</summary>
    public float Near = 18, Far = 280;
    public float Master = 0.9f;
    /// <summary>Volumen de la música y de cada bus.</summary>
    public float MusicVolume = 0.35f;
    public readonly float[] BusVolume = { 0.6f, 0.8f };
    /// <summary>Cuánta reverberación tiene el lugar (0 al aire libre, ~0.5 en una cripta).</summary>
    public float Room = 0.45f;
    /// <summary>Dónde están los oídos (el mundo: x este, y arriba, z sur).</summary>
    public Vector3 Listener;
    /// <summary>Hacia dónde queda la oreja derecha y hacia dónde mira (en primera persona, los de la cámara).</summary>
    public Vector3 ListenerRight = Vector3.UnitX, ListenerForward = -Vector3.UnitZ;
    /// <summary>Cuánto tapa lo que hay entre el oído y la voz (0 nada, 1 un muro): la apaga y la baja. null = nada.</summary>
    public Func<Vector3, Vector3, float> Occlusion;

    private readonly List<Voice> _voices = new();
    private readonly Reverb _reverb = new(Rate);
    private readonly Biquad _revLow = new();
    private float[] _send = Array.Empty<float>();
    private float[] _music = Array.Empty<float>();
    private float _room = -1;

    public Mixer()
    {
        // Lo que va a la reverberación, sin graves que embarren.
        _revLow.Highpass(180, 0.7f, Rate);
    }

    /// <summary>Un sonido que se está oyendo.</summary>
    public sealed class Voice
    {
        internal float[] Data;
        internal int SourceRate;
        internal double Pos;
        public bool Loop;
        /// <summary>Dónde suena (mundo).</summary>
        public Vector3 At;
        /// <summary>Volumen propio (0..1) y velocidad (1 = normal; más rápido, más agudo).</summary>
        public float Gain = 1, Pitch = 1;
        /// <summary>Suena "sin lugar" (una interfaz): sin distancia ni paneo.</summary>
        public bool Flat;
        public Bus Bus;
        public bool Done { get; internal set; }
        /// <summary>Segundos que lleva sonando (vuelve a 0 al repetirse).</summary>
        public float Time => (float)(Pos / SourceRate);
        internal float Fade = 1, FadeTo = 1, FadeRate = 1 / (0.02f * Rate);
        internal float GL, GR, Cut = 1, Send;
        internal OnePole LpL, LpR;
        internal readonly float[] Delay = new float[64];
        internal int DelayAt;
        internal float DelayL, DelayR;
        /// <summary>Cuánto la tapa algo ahora (sigue despacio a lo que dice <see cref="Occlusion"/>: una reja no la corta de golpe).</summary>
        internal float Occ = -1;
        /// <summary>Cuánto se oye (para decidir a quién callar si hay demasiadas).</summary>
        internal float Heard => (GL + GR) * Gain * Fade;
    }

    public IReadOnlyList<Voice> Voices => _voices;

    /// <summary>
    /// Hace sonar <paramref name="data"/> (mono, a <paramref name="rate"/>) desde <paramref name="at"/>,
    /// empezando a los <paramref name="start"/> segundos (para seguir donde había quedado); entra con
    /// un fundido de <paramref name="fadeIn"/> segundos. Si ya hay demasiadas voces, se calla la que
    /// menos se oye (o no suena ésta, si es la que menos se oiría).
    /// </summary>
    public Voice Play(float[] data, int rate, Vector3 at, bool loop = false, float gain = 1, float fadeIn = 0.02f, float start = 0, float pitch = 1, Bus bus = Bus.Effects, bool flat = false)
    {
        double pos = Math.Max(0, start) * rate;
        if (pos >= data.Length - 1) pos = loop ? pos % Math.Max(1, data.Length - 1) : data.Length - 1;
        var v = new Voice
        {
            Data = data, SourceRate = rate, Pos = pos, At = at, Loop = loop, Gain = gain, Pitch = pitch, Bus = bus, Flat = flat,
            Fade = 0, FadeTo = 1, FadeRate = 1 / (MathF.Max(fadeIn, 0.002f) * Rate),
        };
        Aim(v, init: true);
        int live = 0;
        foreach (var x in _voices) if (x.FadeTo > 0) live++;
        if (live >= MaxVoices)
        {
            // Demasiadas: la que menos se oye se va (rápido, sin clic).
            Voice least = null;
            foreach (var x in _voices)
                if (x.FadeTo > 0 && !x.Loop && (least == null || x.Heard < least.Heard)) least = x;
            float mine = (v.GL + v.GR) * v.Gain;
            if (least == null || least.Heard > mine) { v.Done = true; return v; }
            Stop(least, 0.03f);
        }
        _voices.Add(v);
        return v;
    }

    /// <summary>La calla con un fundido de <paramref name="fade"/> segundos (y la saca cuando termina).</summary>
    public void Stop(Voice v, float fade = 0.05f)
    {
        if (v == null) return;
        v.FadeTo = 0;
        v.FadeRate = 1 / (MathF.Max(fade, 0.002f) * Rate);
    }

    /// <summary>Calla todo lo que suena (no la música).</summary>
    public void StopAll(float fade = 0.1f)
    {
        foreach (var v in _voices) Stop(v, fade);
    }

    /// <summary>
    /// Hacia dónde tiene que ir cada voz según dónde está (volumen de cada oído, cuánto se apaga,
    /// cuánto eco). Se llama una vez por bloque; <see cref="Mix"/> va de lo de antes a lo de ahora
    /// muestra a muestra (el volumen, el filtro y el retardo entre oídos): moverse no hace clic.
    /// </summary>
    private void Aim(Voice v, bool init = false)
    {
        float gl, gr, cut, send, itd;
        float bus = BusVolume[(int)v.Bus];
        if (v.Flat) { gl = gr = 0.7071f * bus; cut = 1; send = 0.1f * bus; itd = 0; }
        else
        {
            var d = v.At - Listener;
            float dist = d.Length();
            float att = dist <= Near ? 1 : Near / (Near + (dist - Near));
            att *= 1 - Smooth(Far * 0.75f, Far, dist);
            float want = att > 0.001f ? Occlusion?.Invoke(Listener, v.At) ?? 0 : 0;
            // Lo que tapa cambia en ~0.2 s (un paso detrás de una columna no corta la voz).
            v.Occ = v.Occ < 0 || init ? want : v.Occ + (want - v.Occ) * 0.1f;
            float occ = v.Occ;
            att *= 1 - 0.55f * occ;
            att *= bus;
            // Paneo: según de qué lado de la cabeza queda; cerca, suave.
            float side = Vector3.Dot(d, ListenerRight), front = Vector3.Dot(d, ListenerForward);
            float pan = Math.Clamp(side / MathF.Sqrt(side * side + front * front + 36), -1, 1) * 0.9f;
            float a = (pan + 1) * MathF.PI / 4;
            gl = MathF.Cos(a) * att;
            gr = MathF.Sin(a) * att;
            // El aire y los muros se comen los agudos.
            float freq = MathF.Max(900, 16000 - 13000 * MathF.Min(1, dist / Far)) * (1 - 0.7f * occ);
            cut = OnePole.Coef(freq, Rate);
            send = (0.25f + 0.55f * MathF.Min(1, dist / 150)) * att + 0.2f * occ * bus;
            itd = pan * 0.00055f * Rate;
        }
        v.GL = gl; v.GR = gr; v.Cut = cut; v.Send = send;
        v.DelayL = MathF.Max(0, itd);
        v.DelayR = MathF.Max(0, -itd);
        if (init) { v.LpL = default; v.LpR = default; }
    }

    private static float Smooth(float e0, float e1, float x) { float t = Math.Clamp((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t); }

    // ------------------------------------------------------------------ música

    /// <summary>Un tema sonando: cuánto se oye y hacia dónde va (sube, o se apaga para irse).</summary>
    private sealed class Song
    {
        public Track Track;
        public float Gain, Target, Step;
    }

    private readonly List<Song> _songs = new();

    /// <summary>El tema que se está oyendo (o subiendo), si hay.</summary>
    public Track Music => _songs.LastOrDefault(s => s.Target > 0)?.Track;

    /// <summary>
    /// Pone <paramref name="track"/> (null = silencio): lo que sonaba se apaga en <paramref name="fade"/>
    /// segundos mientras sube éste. Si ya estaba sonando, sigue donde iba.
    /// </summary>
    public void PlayMusic(Track track, float fade = 3)
    {
        float step = 1 / (MathF.Max(fade, 0.01f) * Rate);
        foreach (var s in _songs)
        {
            s.Target = s.Track == track ? 1 : 0;
            s.Step = step;
        }
        if (track != null && !_songs.Any(s => s.Track == track)) _songs.Add(new Song { Track = track, Gain = 0, Target = 1, Step = step });
    }

    /// <summary>Mezcla <paramref name="frames"/> cuadros estéreo en <paramref name="out"/> (intercalado L, R).</summary>
    public void Mix(float[] @out, int frames)
    {
        Array.Clear(@out, 0, frames * 2);
        if (_send.Length < frames) _send = new float[frames];
        Array.Clear(_send, 0, frames);
        for (int vi = _voices.Count - 1; vi >= 0; vi--)
        {
            var v = _voices[vi];
            // Lo de antes de este bloque → lo de ahora, repartido en el bloque (sin saltos).
            float gl0 = v.GL, gr0 = v.GR, send0 = v.Send, cut0 = v.Cut, dl0 = v.DelayL, dr0 = v.DelayR;
            Aim(v);
            float step = v.SourceRate / (float)Rate * v.Pitch;
            var data = v.Data;
            for (int i = 0; i < frames; i++)
            {
                float u = (i + 1) / (float)frames;
                int p0 = (int)v.Pos;
                float x;
                if (v.Loop)
                {
                    // En bucle, la última muestra empalma con la primera (sin perder ninguna).
                    if (p0 >= data.Length) { v.Pos -= data.Length; p0 = (int)v.Pos; }
                    float fr = (float)(v.Pos - p0);
                    x = data[p0] + (data[p0 + 1 < data.Length ? p0 + 1 : 0] - data[p0]) * fr;
                }
                else
                {
                    if (p0 >= data.Length - 1) { v.Done = true; break; }
                    float fr = (float)(v.Pos - p0);
                    x = data[p0] + (data[p0 + 1] - data[p0]) * fr;
                }
                v.Pos += step;
                // Fundido de entrada o de salida.
                if (v.Fade != v.FadeTo) v.Fade = v.FadeTo > v.Fade ? MathF.Min(v.FadeTo, v.Fade + v.FadeRate) : MathF.Max(v.FadeTo, v.Fade - v.FadeRate);
                // Callada (también si se la calló antes de que llegara a sonar): se va.
                if (v.Fade <= 0 && v.FadeTo <= 0) { v.Done = true; break; }
                x *= v.Gain * v.Fade;
                // Retardo interaural: el oído del otro lado lo escucha un poquito después.
                v.Delay[v.DelayAt] = x;
                float xl = Tap(v, dl0 + (v.DelayL - dl0) * u), xr = Tap(v, dr0 + (v.DelayR - dr0) * u);
                v.DelayAt = (v.DelayAt + 1) & 63;
                float cut = cut0 + (v.Cut - cut0) * u;
                xl = v.LpL.Run(xl, cut);
                xr = v.LpR.Run(xr, cut);
                @out[2 * i] += xl * (gl0 + (v.GL - gl0) * u);
                @out[2 * i + 1] += xr * (gr0 + (v.GR - gr0) * u);
                _send[i] += x * (send0 + (v.Send - send0) * u);
            }
            if (v.Done) _voices.RemoveAt(vi);
        }
        // La sala: la reverberación (al cambiar de lugar, cambia en un par de segundos).
        if (_room < 0) _room = Room;
        for (int i = 0; i < frames; i++)
        {
            _room += (Room - _room) * 0.00005f;
            var (l, r) = _reverb.Run(_revLow.Run(_send[i]));
            @out[2 * i] += l * _room * 2.2f;
            @out[2 * i + 1] += r * _room * 2.2f;
        }
        // La música, de fondo.
        if (_songs.Count > 0)
        {
            if (_music.Length < frames * 2) _music = new float[frames * 2];
            for (int si = _songs.Count - 1; si >= 0; si--)
            {
                var s = _songs[si];
                s.Track.Read(_music, 0, frames);
                for (int i = 0; i < frames; i++)
                {
                    if (s.Gain != s.Target) s.Gain = s.Target > s.Gain ? MathF.Min(s.Target, s.Gain + s.Step) : MathF.Max(s.Target, s.Gain - s.Step);
                    // Curva de igual potencia: el cruce entre dos temas no se hunde en el medio.
                    float g = MathF.Sin(s.Gain * MathF.PI / 2) * MusicVolume;
                    @out[2 * i] += _music[2 * i] * g;
                    @out[2 * i + 1] += _music[2 * i + 1] * g;
                }
                if (s.Gain <= 0 && s.Target <= 0) { s.Track.Dispose(); _songs.RemoveAt(si); }
            }
        }
        // Un limitador suave al final.
        for (int i = 0; i < frames * 2; i++) @out[i] = SoftClip(@out[i] * Master);
    }

    private static float Tap(Voice v, float delay)
    {
        float at = v.DelayAt - delay;
        int i0 = (int)MathF.Floor(at);
        float f = at - i0;
        float a = v.Delay[i0 & 63], b = v.Delay[(i0 + 1) & 63];
        return a + (b - a) * f;
    }

    /// <summary>Recorta suave arriba de ~0.8 (nunca satura de golpe).</summary>
    private static float SoftClip(float x) => MathF.Abs(x) < 0.8f ? x : MathF.Sign(x) * (0.8f + 0.2f * MathF.Tanh((MathF.Abs(x) - 0.8f) / 0.2f));
}
