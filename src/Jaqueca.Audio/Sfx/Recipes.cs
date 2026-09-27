using static Jaqueca.Audio.Synth;

namespace Jaqueca.Audio;

/// <summary>
/// Las recetas de los sonidos: cada una arma una variante (la semilla cambia los detalles: el
/// tono, los tiempos, cuántos granos). Pensadas desde lo que pasa físicamente, como las de
/// Inquisition: un tiro es el martillo que pega, el estallido de la pólvora (un chasquido de
/// ruido brevísimo) y el golpe de aire que sale del caño (un grave que cae); una patada es la
/// pierna que corta el aire y la suela contra la masa del cuerpo.
/// </summary>
public static partial class Recipes
{
    /// <summary>La variante <paramref name="v"/> de <paramref name="id"/>.</summary>
    public static float[] Make(Sound id, int v)
    {
        int seed = (int)id * 1009 + v * 7919 + 13;
        return id switch
        {
            Sound.StepWood => StepWood(new Synth(seed, 0.3f)),
            Sound.Land => Land(new Synth(seed, 0.45f)),
            Sound.Jump => Jump(new Synth(seed, 0.35f)),
            Sound.Dash => Dash(new Synth(seed, 0.45f)),
            Sound.Slide => Slide(new Synth(seed, 1.6f)),
            Sound.Slam => Slam(new Synth(seed, 1.1f)),
            Sound.WallJump => WallJump(new Synth(seed, 0.3f)),
            Sound.Hurt => Grunt(new Synth(seed, 0.5f), v % 2 == 0 ? 112 : 124, 1),
            Sound.Heal => Heal(new Synth(seed, 0.6f)),
            Sound.Revolver => Revolver(new Synth(seed, 0.9f)),
            Sound.RevolverCharge => RevolverCharge(new Synth(seed, 0.8f)),
            Sound.RevolverPierce => RevolverPierce(new Synth(seed, 1.3f)),
            Sound.Shotgun => Shotgun(new Synth(seed, 1.3f)),
            Sound.ShotgunPump => ShotgunPump(new Synth(seed, 0.5f)),
            Sound.Kick => Swing(new Synth(seed, 0.35f), 0.22f, 180, 1300, 0.5f, 0, 0.55f),
            Sound.KickHit => KickHit(new Synth(seed, 0.6f)),
            Sound.Parry => Parry(new Synth(seed, 1.4f)),
            Sound.Switch => Switch(new Synth(seed, 0.35f)),
            Sound.Ricochet => Ricochet(new Synth(seed, 0.6f)),
            Sound.HitFlesh => HitFlesh(new Synth(seed, 0.4f)),
            Sound.HitBlunt => HitBlunt(new Synth(seed, 0.35f)),
            Sound.HitHeavy => HitHeavy(new Synth(seed, 0.7f)),
            Sound.Dismember => Dismember(new Synth(seed, 0.7f)),
            Sound.Headshot => Headshot(new Synth(seed, 0.9f)),
            Sound.BloodSpray => BloodSpray(new Synth(seed, 0.7f)),
            Sound.BloodDrip => BloodDrip(new Synth(seed, 0.15f)),
            Sound.BodyFall => BodyFall(new Synth(seed, 0.8f)),
            Sound.GibLand => GibLand(new Synth(seed, 0.3f)),
            Sound.Spawn => Spawn(new Synth(seed, 0.9f)),
            Sound.NeighborGrunt => Grunt(new Synth(seed, 0.5f), v % 2 == 0 ? 88 : 96, 0.9f),
            Sound.Drill => Drill(new Synth(seed, 2.0f)),
            Sound.DrillHit => DrillHit(new Synth(seed, 0.8f)),
            Sound.Shush => Shush(new Synth(seed, 0.9f)),
            Sound.ChalkThrow => Swing(new Synth(seed, 0.3f), 0.18f, 800, 3800, 0.45f, 0.18f, 0),
            Sound.ChalkBreak => ChalkBreak(new Synth(seed, 0.4f)),
            Sound.Groan => Groan(new Synth(seed, 1.3f), v % 2 == 0 ? 96 : 190, v % 2 == 0 ? 0.95f : 1.15f),
            Sound.Clock => Clock(new Synth(seed, 0.3f), v),
            Sound.RoomTone => RoomTone(new Synth(seed, 6f)),
            Sound.StyleUp => StyleUp(new Synth(seed, 0.8f), v),
            Sound.WaveClear => WaveClear(new Synth(seed, 3f)),
            _ => new Synth(seed, 0.05f).Done(),
        };
    }

    // ------------------------------------------------------------------ piezas propias

    /// <summary>
    /// Un tono con armónicos (un motor, una sirena): la fundamental sigue a <paramref name="f"/>(t) y los
    /// armónicos caen como en una diente de sierra suave. <paramref name="env"/>(t), el volumen.
    /// </summary>
    private static void Tone(Synth s, float t0, float dur, Func<float, float> f, Func<float, float> env, float gain, int harmonics = 8, float tilt = 1)
    {
        int a = s.At(t0), b = s.At(t0 + dur);
        double ph = 0;
        for (int i = a; i < b; i++)
        {
            float t = (i - a) / (float)R;
            ph += f(t) / R;
            float x = 0;
            for (int k = 1; k <= harmonics; k++) x += MathF.Sin((float)(ph * k * MathF.Tau)) / MathF.Pow(k, tilt);
            s.B[i] += x * env(t) * gain;
        }
    }

    // ------------------------------------------------------------------ Ernesto

    private static float[] StepWood(Synth s)
    {
        // El taco contra el parquet: el golpe, y la tabla que resuena hueca debajo (modos graves de madera).
        float c = s.U(1400, 2200);
        s.Thump(0, s.U(150, 190), s.U(70, 90), 0.002f, 0.02f, 0.55f);
        s.Noise(0, 0.02f, t => AD(t, 0.0005f, 0.0035f), _ => c, 1.0f, 0.8f);
        s.Modes(0.001f, 0.35f, (s.U(210, 260), 1, 0.035f), (s.U(480, 560), 0.5f, 0.02f), (s.U(900, 1100), 0.25f, 0.012f));
        float toe = s.U(0.05f, 0.09f);
        s.Noise(toe, 0.02f, t => AD(t, 0.0005f, 0.003f), _ => c * 1.2f, 1.0f, 0.35f);
        s.Modes(toe, 0.12f, (s.U(230, 280), 1, 0.025f));
        return s.Done(0.6f);
    }

    private static float[] Jump(Synth s)
    {
        // El envión: el aire que se suelta y la ropa del pijama que se estira.
        float d = s.U(0.18f, 0.24f);
        s.Noise(0, d, t => Hump(t, d, 0.3f), t => 700 + 900 * t / d, 0.9f, 0.5f, color: 1);
        s.Noise(0, 0.06f, t => AD(t, 0.002f, 0.015f), _ => 300, 0.7f, 0.5f, lowpass: true);
        s.Grains(0, d, s.Rng.Next(6, 12), 1800, 5000, 0.12f, 0.0015f);
        return s.Done(0.5f);
    }

    private static float[] Dash(Synth s)
    {
        // Una ráfaga: el aire que pasa al lado de la cabeza, de grave a agudo y de vuelta, con un silbido.
        float d = s.U(0.3f, 0.38f);
        s.Noise(0, d, t => MathF.Pow(Hump(t, d, 0.25f), 1.3f), t => 400 + 2600 * Hump(t, d, 0.3f), 1.1f, 1, color: 1);
        float wf = s.U(2600, 3400);
        s.Noise(0, d, t => MathF.Pow(Hump(t, d, 0.3f), 3), t => wf * (0.7f + 0.3f * Hump(t, d, 0.3f)), 10, 0.25f);
        s.Noise(0, 0.1f, t => AD(t, 0.004f, 0.03f), _ => 160, 0.7f, 0.6f, lowpass: true);
        return s.Done(0.6f);
    }

    private static float[] Slide(Synth s)
    {
        // Deslizarse: la tela contra el parquet, un roce parejo con granitos (en bucle).
        float d = s.Seconds;
        s.Noise(0, d, t => 0.75f + 0.25f * MathF.Sin(t * 9.1f) * MathF.Sin(t * 3.3f), _ => 1600, 0.5f, 0.6f, color: 1);
        s.Noise(0, d, _ => 1, _ => 380, 0.7f, 0.35f, lowpass: true, color: 1);
        s.Grains(0, d, s.Rng.Next(80, 120), 1500, 6000, 0.14f, 0.0012f);
        return s.Looped(0.25f, 0.45f);
    }

    private static float[] Slam(Synth s)
    {
        // Todo el cuerpo contra el piso desde arriba: un grave enorme que cae, la madera que cruje y salta.
        s.Thump(0, s.U(80, 95), s.U(28, 34), 0.003f, 0.2f, 1);
        s.Noise(0, 0.25f, t => AD(t, 0.002f, 0.07f), _ => 700, 0.7f, 1, lowpass: true, color: 1);
        s.Noise(0, 0.03f, t => AD(t, 0.0005f, 0.006f), _ => 2400, 0.8f, 0.7f);
        s.Modes(0.002f, 0.6f, (s.U(140, 170), 1, 0.12f), (s.U(330, 380), 0.5f, 0.06f), (s.U(700, 820), 0.3f, 0.03f));
        s.Grains(0.01f, 0.5f, s.Rng.Next(40, 70), 900, 5000, 0.35f, 0.0015f, 2.5f);
        s.Drive(1.8f);
        return s.Done(0.95f);
    }

    private static float[] WallJump(Synth s)
    {
        // La suela contra la pared y el envión.
        s.Thump(0, 170, 85, 0.001f, 0.02f, 0.6f);
        s.Noise(0, 0.02f, t => AD(t, 0.0005f, 0.004f), _ => 1800, 0.8f, 0.6f);
        s.Noise(0.01f, 0.18f, t => Hump(t, 0.18f, 0.3f), t => 700 + 1400 * t / 0.18f, 0.9f, 0.4f, color: 1);
        return s.Done(0.55f);
    }

    private static float[] Heal(Synth s)
    {
        // Un sorbo tibio: lo mojado que entra y un brillo que sube (dos senos que se abren).
        s.Noise(0, 0.2f, t => Hump(t, 0.2f, 0.3f), t => 500 + 700 * t / 0.2f, 2, 0.4f);
        Tone(s, 0.05f, 0.45f, t => 520 + 380 * t, t => Hump(t, 0.45f, 0.2f), 0.18f, 3, 1.6f);
        Tone(s, 0.08f, 0.42f, t => 780 + 520 * t, t => Hump(t, 0.42f, 0.2f), 0.1f, 2, 1.6f);
        return s.Done(0.45f);
    }

    // ------------------------------------------------------------------ armas

    private static float[] Revolver(Synth s)
    {
        // El martillo, un instante antes; el estallido (un chasquido de ruido casi sin cola) y el golpe de aire.
        s.Noise(0, 0.006f, t => AD(t, 0.0002f, 0.0012f), _ => s.U(3500, 4500), 1.2f, 0.35f);
        float t0 = 0.004f;
        s.Noise(t0, 0.02f, t => AD(t, 0.0001f, 0.0035f), _ => 5200, 0.35f, 1.4f);
        s.Noise(t0, 0.12f, t => AD(t, 0.0005f, 0.025f), _ => s.U(1100, 1500), 0.6f, 1, color: 1);
        s.Thump(t0, s.U(160, 190), s.U(55, 65), 0.0008f, 0.06f, 1);
        // El eco del cuarto lo pone la mezcla; acá, la cola corta del caño.
        s.Noise(t0 + 0.02f, 0.5f, t => AD(t, 0.01f, 0.12f), _ => 900, 0.5f, 0.18f, lowpass: true, color: 2);
        s.Drive(2.2f);
        return s.Done(0.95f);
    }

    private static float[] RevolverCharge(Synth s)
    {
        // El cilindro que gira cada vez más rápido y un zumbido eléctrico que sube.
        float d = s.Seconds;
        s.Creak(0, d, t => 8 + 70 * t / d, t => 0.4f + 0.6f * t / d, 0.4f, 1800, 3200);
        Tone(s, 0, d, t => 90 + 700 * (t / d) * (t / d), t => Smooth(0, 0.1f, t) * (0.3f + 0.7f * t / d), 0.12f, 6, 1.2f);
        return s.Done(0.6f);
    }

    private static float[] RevolverPierce(Synth s)
    {
        // Como el tiro de siempre pero más grande, con el chispazo que atraviesa (un chirrido que cae).
        s.Noise(0, 0.03f, t => AD(t, 0.0001f, 0.005f), _ => 5600, 0.35f, 1.4f);
        s.Noise(0, 0.25f, t => AD(t, 0.0005f, 0.05f), _ => 1200, 0.6f, 1, color: 1);
        s.Thump(0, 150, 42, 0.001f, 0.14f, 1);
        Tone(s, 0.005f, 0.4f, t => 2600 * MathF.Exp(-t * 7) + 300, t => AD(t, 0.002f, 0.12f), 0.35f, 5, 0.9f);
        s.Grains(0.01f, 0.3f, s.Rng.Next(25, 40), 3000, 9000, 0.25f, 0.0005f, 2);
        s.Noise(0.03f, 0.9f, t => AD(t, 0.02f, 0.25f), _ => 700, 0.5f, 0.2f, lowpass: true, color: 2);
        s.Drive(2.4f);
        return s.Done(0.95f);
    }

    private static float[] Shotgun(Synth s)
    {
        // El estampido: más grave y más largo que el revólver, con el pecho del caño que retumba.
        s.Noise(0, 0.03f, t => AD(t, 0.0001f, 0.006f), _ => 4200, 0.35f, 1.3f);
        s.Noise(0, 0.3f, t => AD(t, 0.001f, 0.07f), _ => s.U(700, 950), 0.5f, 1.2f, color: 1);
        s.Thump(0, s.U(110, 130), s.U(34, 40), 0.001f, 0.16f, 1.2f);
        s.Noise(0.02f, 0.9f, t => AD(t, 0.02f, 0.28f), _ => 500, 0.5f, 0.3f, lowpass: true, color: 2);
        s.Grains(0.005f, 0.15f, s.Rng.Next(12, 20), 2500, 8000, 0.2f, 0.0005f, 3);
        s.Drive(2.6f);
        return s.Done(0.98f);
    }

    private static float[] ShotgunPump(Synth s)
    {
        // La corredera: atrás (un clac con el roce) y adelante (un clac más seco).
        s.Noise(0, 0.07f, t => Hump(t, 0.07f, 0.4f), _ => 2600, 1, 0.25f);
        s.Metal(0.06f, s.U(1300, 1500), 0.5f, 0.03f, 1.2f);
        s.Noise(0.06f, 0.01f, t => AD(t, 0.0002f, 0.002f), _ => 3500, 1, 0.8f);
        float b = s.U(0.19f, 0.23f);
        s.Noise(b - 0.05f, 0.05f, t => Hump(t, 0.05f, 0.5f), _ => 2800, 1, 0.2f);
        s.Metal(b, s.U(1600, 1800), 0.6f, 0.025f, 1.3f);
        s.Noise(b, 0.01f, t => AD(t, 0.0002f, 0.002f), _ => 4200, 1, 0.9f);
        return s.Done(0.7f);
    }

    private static float[] KickHit(Synth s)
    {
        // La suela contra el cuerpo: el golpe sordo, la ropa que chasquea y el aire que le sacan.
        s.Thump(0, s.U(120, 140), s.U(45, 52), 0.001f, 0.07f, 1);
        s.Noise(0, 0.03f, t => AD(t, 0.0005f, 0.008f), _ => 1900, 0.7f, 0.9f);
        s.Noise(0, 0.08f, t => AD(t, 0.001f, 0.02f), _ => 400, 0.7f, 0.7f, lowpass: true, color: 1);
        s.Noise(0.03f, 0.25f, t => Hump(t, 0.25f, 0.2f), _ => 1100, 0.7f, 0.15f);
        s.Drive(1.7f);
        return s.Done(0.9f);
    }

    private static float[] Parry(Synth s)
    {
        // Un tañido: lo devuelto suena como una campanita de metal, larga, y el golpe seco que lo dio vuelta.
        s.Thump(0, 180, 80, 0.001f, 0.03f, 0.7f);
        s.Noise(0, 0.01f, t => AD(t, 0.0002f, 0.002f), _ => 5000, 1, 0.8f);
        s.Metal(0.002f, s.U(1800, 2000), 0.9f, 0.5f, 1.4f);
        s.Metal(0.002f, s.U(2700, 2900), 0.4f, 0.35f, 1.6f);
        return s.Done(0.85f);
    }

    private static float[] Switch(Synth s)
    {
        // Guarda una y saca la otra: tela, y un clic de metal.
        s.Noise(0, 0.15f, t => Hump(t, 0.15f, 0.3f), _ => 1500, 0.7f, 0.3f, color: 1);
        s.Metal(0.13f, s.U(1100, 1300), 0.5f, 0.03f, 1.2f);
        s.Noise(0.13f, 0.008f, t => AD(t, 0.0002f, 0.0015f), _ => 3800, 1, 0.7f);
        return s.Done(0.6f);
    }

    private static float[] Ricochet(Synth s)
    {
        // La bala contra la madera o el yeso: un clac, astillas y a veces el zumbido del rebote.
        s.Noise(0, 0.01f, t => AD(t, 0.0002f, 0.0025f), _ => s.U(2500, 3500), 0.8f, 1);
        s.Modes(0, 0.4f, (s.U(600, 900), 1, 0.02f), (s.U(1500, 2000), 0.5f, 0.01f));
        s.Grains(0.002f, 0.1f, s.Rng.Next(8, 16), 1500, 6000, 0.35f, 0.0007f, 2);
        if (s.Chance(0.5f)) Tone(s, 0.01f, 0.35f, t => s.U(2800, 3400) * (1 - 0.35f * t / 0.35f), t => Hump(t, 0.35f, 0.1f), 0.08f, 2, 1);
        return s.Done(0.6f);
    }

    private static float[] Headshot(Synth s)
    {
        // El cráneo: un crac seco que se abre en astillas, y lo que sale (mucho, mojado).
        s.Thump(0, 170, 60, 0.0008f, 0.05f, 1);
        int cracks = s.Rng.Next(4, 7);
        for (int i = 0; i < cracks; i++)
        {
            float f = s.U(1800, 5000);
            s.Noise(s.U(0, 0.012f), 0.008f, t => AD(t, 0.0002f, 0.0012f), _ => f, 1.5f, s.U(0.7f, 1));
        }
        s.Grains(0.004f, 0.08f, s.Rng.Next(20, 35), 1500, 7000, 0.4f, 0.0006f, 2);
        float bub = s.U(18, 28);
        s.Noise(0.01f, 0.6f, t => AD(t, 0.005f, 0.12f) * (0.6f + 0.4f * MathF.Abs(MathF.Sin(MathF.PI * bub * t))), t => 700 + 2200 * MathF.Exp(-t * 8), 2.2f, 1);
        int drops = s.Rng.Next(5, 10);
        for (int i = 0; i < drops; i++)
        {
            float f0 = s.U(900, 1900);
            s.Plop(0.1f + 0.6f * s.U(0, 1), f0, f0 * 1.6f, 0.012f, s.U(0.05f, 0.12f));
        }
        s.Drive(1.5f);
        return s.Done(0.95f);
    }

    // ------------------------------------------------------------------ los pensamientos

    private static float[] Spawn(Synth s)
    {
        // Un pensamiento que aparece: el aire que se chupa hacia un punto (ruido que sube y se cierra) y revienta.
        float d = s.U(0.45f, 0.55f);
        s.Noise(0, d, t => MathF.Pow(t / d, 3), t => 300 + 3500 * (t / d) * (t / d), 1.4f, 0.8f, color: 1);
        Tone(s, 0, d, t => 60 + 240 * (t / d), t => MathF.Pow(t / d, 2) * 0.8f, 0.15f, 5, 1.1f);
        s.Thump(d, 130, 45, 0.001f, 0.08f, 0.9f);
        s.Noise(d, 0.2f, t => AD(t, 0.0005f, 0.05f), _ => 900, 0.6f, 0.6f, color: 1);
        return s.Done(0.7f);
    }

    private static float[] Drill(Synth s)
    {
        // El taladro: el motor (un tono con armónicos que tiembla), la mecha que chilla y el ruido del ventilador.
        float d = s.Seconds;
        float f0 = s.U(175, 190);
        Tone(s, 0, d, t => f0 * (1 + 0.012f * MathF.Sin(t * 31 * MathF.Tau / 31 * 17)), _ => 1, 0.22f, 10, 0.9f);
        Tone(s, 0, d, t => f0 * 7.03f, _ => 1, 0.05f, 2, 1);
        s.Noise(0, d, _ => 1, _ => 2400, 0.6f, 0.25f, color: 1);
        s.Noise(0, d, t => 0.7f + 0.3f * MathF.Sin(t * f0 * 0.5f), _ => 600, 0.8f, 0.2f, lowpass: true);
        s.Drive(1.4f);
        return s.Looped(0.3f, 0.55f);
    }

    private static float[] DrillHit(Synth s)
    {
        // La mecha que entra: el motor que se ahoga (baja de tono), lo que se desgarra y lo mojado.
        float d = s.U(0.55f, 0.7f);
        Tone(s, 0, d, t => 180 * (1 - 0.3f * Smooth(0, d, t)) * (1 + 0.04f * MathF.Sin(t * 70)), t => Hump(t, d, 0.1f), 0.3f, 10, 0.8f);
        s.Noise(0, d, t => Hump(t, d, 0.15f) * (0.6f + 0.4f * MathF.Abs(MathF.Sin(t * 120))), _ => 1300, 1.5f, 0.6f);
        s.Grains(0, d, s.Rng.Next(30, 50), 1500, 6000, 0.3f, 0.0008f);
        s.Drive(1.8f);
        return s.Done(0.9f);
    }

    private static float[] Shush(Synth s)
    {
        // "Shhh": el aire entre los dientes, largo, con la lengua que lo afila (sin voz).
        float d = s.U(0.6f, 0.75f);
        s.Noise(0.02f, d, t => Hump(t, d, 0.15f), _ => s.U(3000, 3600), 2.2f, 0.9f);
        s.Noise(0.02f, d, t => Hump(t, d, 0.15f), _ => 5800, 1.2f, 0.35f);
        s.Noise(0, 0.05f, t => AD(t, 0.005f, 0.02f), _ => 900, 0.7f, 0.2f, color: 1);
        return s.Done(0.6f);
    }

    private static float[] ChalkBreak(Synth s)
    {
        // La tiza: quebradiza, un chasquido agudo que se deshace en polvo.
        s.Noise(0, 0.006f, t => AD(t, 0.0002f, 0.0015f), _ => s.U(3500, 4800), 1.4f, 1);
        s.Modes(0, 0.3f, (s.U(2600, 3200), 1, 0.012f), (s.U(4800, 5600), 0.6f, 0.008f));
        s.Grains(0.002f, 0.12f, s.Rng.Next(15, 30), 3000, 9000, 0.3f, 0.0004f, 2.5f);
        s.Noise(0.005f, 0.2f, t => AD(t, 0.01f, 0.06f), _ => 6000, 0.8f, 0.12f);
        return s.Done(0.55f);
    }

    // ------------------------------------------------------------------ el lugar

    private static float[] Clock(Synth s, int v)
    {
        // El péndulo del reloj de pie: tic (agudo) y tac (más grave), madera y bronce.
        float f = v == 0 ? 2100 : 1600;
        s.Noise(0, 0.006f, t => AD(t, 0.0002f, 0.0012f), _ => f * 1.6f, 1.2f, 0.7f);
        s.Modes(0, 0.4f, (f, 1, 0.012f), (f * 2.7f, 0.4f, 0.006f), (f * 0.23f, 0.5f, 0.03f));
        return s.Done(0.35f);
    }

    private static float[] RoomTone(Synth s)
    {
        // La heladera (el zumbido de la red y sus armónicos) y el televisor prendido sin señal (una lluvia fina).
        float d = s.Seconds;
        Tone(s, 0, d, _ => 50, t => 0.8f + 0.2f * MathF.Sin(t * 0.7f), 0.12f, 6, 1.4f);
        s.Noise(0, d, t => 0.8f + 0.2f * MathF.Sin(t * 1.3f), _ => 4800, 0.6f, 0.05f);
        Tone(s, 0, d, _ => 15734f / 4, _ => 1, 0.004f, 1, 1);
        return s.Looped(1f, 0.3f);
    }

    private static float[] StyleUp(Synth s, int v)
    {
        // Dos notas que suben (una quinta), brillantes.
        float f = v == 0 ? 660 : 740;
        s.Modes(0, 0.35f, (f, 1, 0.12f), (f * 2, 0.3f, 0.06f));
        s.Modes(0.09f, 0.4f, (f * 1.5f, 1, 0.2f), (f * 3, 0.3f, 0.08f));
        return s.Done(0.5f);
    }

    private static float[] WaveClear(Synth s)
    {
        // Una campana grave y lejana: se terminó (por ahora).
        s.Metal(0, 196, 0.9f, 1.4f, 0.8f);
        s.Metal(0.01f, 293, 0.4f, 1.1f, 0.8f);
        s.Noise(0, 0.05f, t => AD(t, 0.001f, 0.015f), _ => 800, 0.7f, 0.4f, color: 1);
        return s.Done(0.7f);
    }
}
