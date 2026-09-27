using Jaqueca.Audio;
using Microsoft.Xna.Framework.Audio;
using SoundBank = Jaqueca.Audio.SoundBank;

namespace Jaqueca.Client.Audio;

/// <summary>
/// La salida de sonido del juego: el <see cref="Mixer"/> (3D, por software) mezcla y esto le
/// pasa los bloques a la placa (un solo sonido estéreo de MonoGame que se alimenta a pedido).
/// Mantiene unos pocos bloques en cola (~85 ms): poca demora y sin cortes. Antes de algo que
/// frena el juego (armar un lugar nuevo) se adelanta más (<see cref="Prefill"/>), así la música no
/// se corta. Si la máquina no tiene sonido, el juego sigue igual, callado.
/// </summary>
public sealed class SoundOut : IDisposable
{
    public const int Block = 1024, Queue = 4;
    public readonly Mixer Mixer = new();
    private readonly DynamicSoundEffectInstance _out;
    private readonly float[] _mix = new float[Block * 2];
    private readonly byte[] _pcm = new byte[Block * 4];

    public bool Enabled => _out != null;
    /// <summary>Grabando (--grabar): la mezcla va al paso del juego, sin placa en el medio.</summary>
    public bool Recording => _record != null;

    /// <summary>
    /// Cuánto va adelantada la mezcla de lo que se oye (s): los bloques en cola y lo que la placa
    /// guarda antes de sonar (se estima). Para que lo que se ve caiga con lo que suena.
    /// </summary>
    public float Latency => _out == null ? 0 : _out.PendingBufferCount * Block / (float)Mixer.Rate + DeviceLatency;
    public const float DeviceLatency = 0.03f;
    /// <summary>Cuántas veces la placa se quedó sin nada que tocar (un corte que se oye): para --debug.</summary>
    public int Underruns { get; private set; }
    private bool _started;
    // Grabando (--grabar): no va a la placa; se mezcla al paso del juego y se guarda en un WAV.
    private readonly string _record;
    private readonly List<float> _tape = new();
    private double _owed;

    public SoundOut(string record = null)
    {
        _record = record;
        if (record != null) return;
        try
        {
            _out = new DynamicSoundEffectInstance(Mixer.Rate, AudioChannels.Stereo);
            _out.Play();
        }
        catch (Exception e)
        {
            Console.WriteLine($"[sonido] no hay salida de audio ({e.GetType().Name}): el juego sigue sin sonido");
            _out = null;
        }
    }

    /// <summary>Mezcla y encola lo que falte (una vez por cuadro de <paramref name="dt"/> segundos).</summary>
    public void Update(float dt)
    {
        if (_record != null) { Tape(dt); return; }
        Fill(Queue);
    }

    /// <summary>Grabando: mezcla exactamente lo que duró el cuadro (el juego puede ir más lento que el reloj).</summary>
    private void Tape(float dt)
    {
        _owed += dt * Mixer.Rate;
        while (_owed >= Block)
        {
            Mixer.Mix(_mix, Block);
            _tape.AddRange(_mix);
            _owed -= Block;
        }
    }

    /// <summary>Deja <paramref name="seconds"/> segundos ya mezclados en la cola (antes de algo que va a frenar el juego).</summary>
    public void Prefill(float seconds) => Fill(Math.Max(Queue, (int)MathF.Ceiling(seconds * Mixer.Rate / Block)));

    private void Fill(int blocks)
    {
        if (_out == null) return;
        try
        {
            if (_started && _out.PendingBufferCount == 0) Underruns++;
            _started = true;
            while (_out.PendingBufferCount < blocks)
            {
                Mixer.Mix(_mix, Block);
                for (int i = 0; i < Block * 2; i++)
                {
                    short s = (short)Math.Clamp(MathF.Round(_mix[i] * 32767), -32768, 32767);
                    _pcm[2 * i] = (byte)s;
                    _pcm[2 * i + 1] = (byte)(s >> 8);
                }
                _out.SubmitBuffer(_pcm);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[sonido] se cortó la salida ({e.GetType().Name})");
        }
    }

    public void Dispose()
    {
        _out?.Dispose();
        if (_record == null || _tape.Count == 0) return;
        Wav.Write(_record, _tape.ToArray(), Mixer.Rate, 2);
        Console.WriteLine($"[sonido] grabado {_tape.Count / 2f / Mixer.Rate:0.0} s en {_record}");
    }
}

/// <summary>
/// Los sonidos del juego, hechos por código: el banco de efectos. Se sintetiza una vez, en segundo
/// plano al empezar (mientras tanto, lo que los pide no suena).
/// </summary>
public static class Sounds
{
    private static Task<SoundBank> _bank;

    /// <summary>Arranca la síntesis de todo (al abrir el juego).</summary>
    public static void Warm() => _bank ??= Task.Run(SoundBank.Render);

    /// <summary>Todos los efectos (null mientras se están haciendo).</summary>
    public static SoundBank Bank => _bank is { IsCompletedSuccessfully: true } t ? t.Result : null;

    /// <summary>Espera a que estén (las capturas, que duran pocos segundos, los quieren desde el principio).</summary>
    public static void Wait() => _bank?.Wait();
}
