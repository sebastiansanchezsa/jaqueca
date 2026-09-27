namespace Jaqueca.Audio;

/// <summary>
/// Todos los sonidos del juego ya hechos: cada uno en varias variantes (ver <see cref="Variants"/>),
/// sintetizados una vez al abrir el juego (en paralelo; tarda un par de segundos, en segundo
/// plano). Mono, a <see cref="Mixer.Rate"/>.
/// </summary>
public sealed class SoundBank
{
    /// <summary>Los que se repiten sin corte (el taladro, el deslizarse, la heladera): se tocan en bucle.</summary>
    public static readonly HashSet<Sound> Loops = new() { Sound.Slide, Sound.Drill, Sound.RoomTone };

    /// <summary>Cuántas variantes tiene cada uno (los pasos y los tiros, muchas: se oyen todo el tiempo).</summary>
    public static int Variants(Sound s) => s switch
    {
        Sound.StepWood => 8,
        Sound.Revolver or Sound.Shotgun or Sound.HitFlesh or Sound.HitBlunt or Sound.Ricochet => 6,
        Sound.NeighborGrunt or Sound.Hurt or Sound.ChalkBreak or Sound.BloodDrip => 6,
        Sound.Slide or Sound.Drill or Sound.RoomTone or Sound.RevolverCharge or Sound.WaveClear => 1,
        Sound.Clock or Sound.StyleUp or Sound.Switch or Sound.ShotgunPump => 2,
        _ => 4,
    };

    private readonly float[][][] _data;

    private SoundBank(float[][][] data) { _data = data; }

    public static int Rate => Mixer.Rate;

    /// <summary>La variante <paramref name="v"/> (se da la vuelta si pide de más).</summary>
    public float[] Get(Sound s, int v)
    {
        var all = _data[(int)s];
        return all[(v % all.Length + all.Length) % all.Length];
    }

    public int Count(Sound s) => _data[(int)s].Length;

    /// <summary>Hace todos (en paralelo).</summary>
    public static SoundBank Render()
    {
        var ids = Enum.GetValues<Sound>();
        var data = new float[ids.Max(i => (int)i) + 1][][];
        var jobs = ids.SelectMany(id => Enumerable.Range(0, Variants(id)).Select(v => (id, v))).ToList();
        foreach (var id in ids) data[(int)id] = new float[Variants(id)][];
        Parallel.ForEach(jobs, j => data[(int)j.id][j.v] = Recipes.Make(j.id, j.v));
        return new SoundBank(data);
    }
}
