using System.Numerics;
using Jaqueca.Figures.Rig;
using Jaqueca.Look;

namespace Jaqueca.Figures.Anim;

/// <summary>
/// Cómo remata cada arma a uno que quedó en el piso (arrastrándose sin piernas, tumbado, de rodillas): ver
/// <see cref="GestureKind.Execute"/> y la escena (GameScene.Executions).
/// </summary>
public enum ExecStyle : byte
{
    /// <summary>La espada, a uno tumbado: la alza con las dos manos, de punta, y la clava en el pecho; la arranca y sacude la sangre.</summary>
    Plunge,
    /// <summary>La espada, a uno boca abajo: la alza por encima de la cabeza y baja de un tajo sobre la nuca.</summary>
    Behead,
    /// <summary>Las dagas: de rodillas, una puñalada con cada mano y el degüello en cruz.</summary>
    Throat,
    /// <summary>El arco: parado sobre la cabeza, tensa apuntando abajo y suelta a quemarropa.</summary>
    Point,
    /// <summary>Los puños: de rodillas, dos golpes y el martillazo con las dos manos juntas.</summary>
    Hammer,
    /// <summary>El cayado: la señal de la cruz y el regatón sobre el cráneo.</summary>
    Rites,
    /// <summary>El cetro: lo alza, apunta al pecho y le arranca el alma.</summary>
    Soul,
}

public sealed partial class Animator
{
    /// <summary>La ejecución en curso (ver <see cref="GestureKind.Execute"/>): cuál y dónde pega (en el mundo: el pecho o la cabeza del que está en el piso).</summary>
    public ExecStyle Exec;
    public Vector3 ExecTarget;

    /// <summary>A qué distancia del punto donde pega se para el que remata (medidas humanas: la escena la escala con el cuerpo).</summary>
    public static float ExecReach(ExecStyle s) => s switch
    {
        ExecStyle.Plunge => 3.3f,
        ExecStyle.Behead => 6.4f,
        ExecStyle.Throat => 3.0f,
        ExecStyle.Point => 5.2f,
        ExecStyle.Hammer => 2.9f,
        ExecStyle.Rites => 3.4f,
        _ => 5.0f,
    };

    /// <summary>Cuánto dura cada ejecución (segundos).</summary>
    public static float ExecDuration(ExecStyle s) => s switch
    {
        ExecStyle.Plunge => 2.1f,
        ExecStyle.Behead => 1.8f,
        ExecStyle.Throat => 2.0f,
        ExecStyle.Point => 2.1f,
        ExecStyle.Hammer => 2.0f,
        ExecStyle.Rites => 2.5f,
        _ => 2.4f,
    };

    /// <summary>Cuándo pega cada golpe (fracciones de la duración); el último mata.</summary>
    public static float[] ExecHits(ExecStyle s) => s switch
    {
        ExecStyle.Plunge => new[] { 0.44f },
        ExecStyle.Behead => new[] { 0.5f },
        ExecStyle.Throat => new[] { 0.3f, 0.44f, 0.64f },
        ExecStyle.Point => new[] { 0.62f },
        ExecStyle.Hammer => new[] { 0.3f, 0.44f, 0.72f },
        ExecStyle.Rites => new[] { 0.7f },
        _ => new[] { 0.42f, 0.56f, 0.78f },
    };

    /// <summary>La espada clavada sale del pecho (la sangre salta ahí), en fracción de la duración.</summary>
    public const float ExecPull = 0.75f;
    /// <summary>La cruz del cayado: cuándo toca la frente, el pecho y los hombros.</summary>
    public const float RitesCross = 0.1f;

    private float ExecU => Math.Clamp(_gestureT / _gestureDur, 0, 1);

    /// <summary>¿Tensa el arco apuntando abajo (la flecha va en la cuerda)? Y cuánto.</summary>
    private bool ExecNocked(out float draw)
    {
        draw = 0;
        if (CurrentGesture != GestureKind.Execute || Exec != ExecStyle.Point || MainHand != WeaponFamily.Bow || _mask.NoWeapon) return false;
        float u = ExecU;
        if (u < 0.22f || u >= 0.62f) return false;
        draw = Smooth(0.28f, 0.56f, u);
        return true;
    }

    /// <summary>
    /// Los brazos de la ejecución, encima de la postura del arma: cada estilo lleva las manos (y el arma) a donde
    /// tiene que pegar (<see cref="ExecTarget"/>, pasado al espacio del personaje). El cuerpo (agacharse,
    /// arrodillarse, inclinarse, mirar) va en <see cref="ExecuteBody"/>.
    /// </summary>
    private void ExecuteArms(Pose p, Vector3 pelvis)
    {
        Matrix4x4.Invert(World, out var inv);
        var t = Vector3.Transform(ExecTarget, inv);
        float u = ExecU;
        var shR = pelvis + new Vector3(0, _shoulderY, _s.D.ShoulderW);
        var shL = pelvis + new Vector3(0, _shoulderY, -_s.D.ShoulderW);
        var restR = p.Arms[0].On ? p.Arms[0].Target : shR + new Vector3(0.3f, -3.1f, 0.25f) * _armK;
        var restRotR = p.Arms[0].On && p.Arms[0].Rot is { } rr ? rr : Grip(Aim(0, -70), Vector3.UnitX);
        var restL = p.Arms[1].On ? p.Arms[1].Target : shL + new Vector3(0.3f, -3.1f, -0.25f) * _armK;
        var restRotL = p.Arms[1].On && p.Arms[1].Rot is { } rl ? rl : Grip(Aim(0, -70), -Vector3.UnitZ);
        switch (Exec)
        {
            case ExecStyle.Plunge: PlungeArms(p, t, u, shR, restR, restRotR); break;
            case ExecStyle.Behead: BeheadArms(p, t, u, shR, restR, restRotR); break;
            case ExecStyle.Throat: ThroatArms(p, t, u, shR, shL, restR, restRotR, restL, restRotL); break;
            case ExecStyle.Point: PointArms(p, t, u, shR, shL, restR, restRotR, restL); break;
            case ExecStyle.Hammer: HammerArms(p, t, u, shR, shL, restR, restL); break;
            case ExecStyle.Rites: RitesArms(p, t, u, shR, shL, restR, restRotR, restL, restRotL); break;
            default: SoulArms(p, t, u, shR, shL, restR, restRotR, restL); break;
        }
    }

    /// <summary>La otra mano sobre el mismo mango, <paramref name="along"/> más atrás (hacia el pomo) que la del arma.</summary>
    private void SecondHand(Pose p, Vector3 at, Quaternion rot, float along, float w)
    {
        if (w < 0.25f) return;
        var grip = at + Vector3.Transform(new Vector3(-along * _k, 0, 0), rot);
        p.Arms[1] = new Ik { On = true, Target = grip, Hint = new Vector3(-0.3f, -0.4f, -1), Rot = rot };
        p[Skeleton.Arm(-1, 2)] = Vector3.Zero;
    }

    private void Right(Pose p, Vector3 at, Quaternion rot, Vector3 hint)
    {
        p.Arms[0] = new Ik { On = true, Target = at, Hint = hint, Rot = rot };
        p[Skeleton.Arm(1, 2)] = Vector3.Zero;
    }

    private void Left(Pose p, Vector3 at, Quaternion rot, Vector3 hint)
    {
        p.Arms[1] = new Ik { On = true, Target = at, Hint = hint, Rot = rot };
        p[Skeleton.Arm(-1, 2)] = Vector3.Zero;
    }

    /// <summary>
    /// La estocada (espada, a uno tumbado): la alza con las dos manos por encima de la cabeza, de punta hacia
    /// abajo; la clava de una vez en el pecho y se apoya encima (la hoja tiembla); la arranca, y con un golpe de
    /// muñeca sacude la sangre hacia el costado.
    /// </summary>
    private void PlungeArms(Pose p, Vector3 t, float u, Vector3 shR, Vector3 restR, Quaternion restRot)
    {
        float blade = MathF.Max(3, BladeTip.Length());
        float lift = Smooth(0, 0.3f, u), plunge = Smooth(0.38f, 0.45f, u), pull = Smooth(0.72f, 0.8f, u);
        float flick = Smooth(0.82f, 0.88f, u), back = Smooth(0.9f, 1, u);
        var down = Vector3.Normalize(new Vector3(0.1f, -1, 0));
        var high = new Vector3(1.3f * _k, shR.Y + 2.6f * _armK, 0.2f * _k);
        var into = t + new Vector3(0, blade - 1.7f * _k, 0);
        float hold = plunge * (1 - pull);
        into += new Vector3(MathF.Sin(_gestureT * 47) * 0.07f * _k * hold, 0, 0);
        var outp = t + new Vector3(-0.3f * _k, blade + 2.4f * _k, 0);
        var flickAt = shR + new Vector3(1.9f, -2.4f, 1.1f) * _armK;
        var at = Vector3.Lerp(restR, high, lift);
        at = Vector3.Lerp(at, into, plunge);
        at = Vector3.Lerp(at, outp, pull);
        at = Vector3.Lerp(at, flickAt, flick);
        at = Vector3.Lerp(at, restR, back);
        var point = Grip(down, Vector3.UnitZ);
        var rot = Quaternion.Slerp(restRot, point, Smooth(0, 0.22f, u));
        rot = Quaternion.Slerp(rot, Grip(Aim(55, -35), Vector3.UnitY), flick);
        rot = Quaternion.Slerp(rot, restRot, back);
        Right(p, at, rot, new Vector3(-0.2f, -0.3f, 1));
        SecondHand(p, at, rot, 1.05f, lift * (1 - flick));
        _smearOn = (u > 0.38f && u < 0.46f) || (u > 0.82f && u < 0.88f);
    }

    /// <summary>
    /// El tajo a la nuca (espada, a uno boca abajo): la alza con las dos manos por encima y atrás de la cabeza,
    /// espera un instante y baja de un tajo en arco hasta el piso; queda abajo un momento y vuelve.
    /// </summary>
    private void BeheadArms(Pose p, Vector3 t, float u, Vector3 shR, Vector3 restR, Quaternion restRot)
    {
        float blade = MathF.Max(3, BladeTip.Length());
        float raise = Smooth(0, 0.34f, u), chop = Smooth(0.43f, 0.51f, u), back = Smooth(0.78f, 1, u);
        var high = new Vector3(-0.4f * _k, shR.Y + 2.9f * _armK, 0.3f * _k);
        var dir = Vector3.Normalize(new Vector3(0.78f, -0.62f, 0));
        var end = t - dir * (blade * 0.72f) + new Vector3(0, 0.3f * _k, 0);
        var mid = new Vector3((high.X + end.X) / 2 + 2.2f * _k, (high.Y + end.Y) / 2 + 1.6f * _k, 0.2f * _k);
        // El arco del tajo: de arriba, pasando adelante, hasta abajo.
        var arc = (1 - chop) * (1 - chop) * high + 2 * (1 - chop) * chop * mid + chop * chop * end;
        var at = chop > 0 ? arc : Vector3.Lerp(restR, high, raise);
        at = Vector3.Lerp(at, restR, back);
        var up = Grip(Aim(0, 118), Vector3.UnitZ);
        var cut = Grip(dir, Vector3.UnitZ);
        var rot = Quaternion.Slerp(restRot, up, raise);
        rot = Quaternion.Slerp(rot, cut, chop);
        rot = Quaternion.Slerp(rot, restRot, back);
        Right(p, at, rot, new Vector3(-0.3f, 0.2f, 1));
        SecondHand(p, at, rot, 1.05f, raise * (1 - back));
        _smearOn = u > 0.42f && u < 0.53f;
    }

    /// <summary>
    /// El degüello (dagas): de rodillas junto al caído, una puñalada de arriba con la derecha, otra con la
    /// izquierda, y las dos hojas en cruz por el cuello.
    /// </summary>
    private void ThroatArms(Pose p, Vector3 t, float u, Vector3 shR, Vector3 shL, Vector3 restR, Quaternion restRotR, Vector3 restL, Quaternion restRotL)
    {
        float blade = MathF.Max(2, BladeTip.Length());
        var stab = Grip(Aim(0, -82), Vector3.UnitX);
        (Vector3 at, float w) Stab(Vector3 rest, Vector3 sh, int side, float hit)
        {
            float load = Smooth(hit - 0.12f, hit - 0.03f, u) * (1 - Smooth(hit - 0.02f, hit, u));
            float strike = Smooth(hit - 0.02f, hit, u) * (1 - Smooth(hit + 0.03f, hit + 0.1f, u));
            var up = sh + new Vector3(1.4f, 0.3f, side * 0.3f) * _armK;
            var into = t + new Vector3(0, blade - 1.0f * _k, side * 0.45f * _k);
            var at = Vector3.Lerp(rest, up, load);
            at = Vector3.Lerp(at, into, strike);
            return (at, MathF.Max(load, strike));
        }
        var (r1, wR) = Stab(restR, shR, 1, 0.3f);
        var (l1, wL) = Stab(restL, shL, -1, 0.44f);
        // La cruz: las dos arriba y afuera, y bajan cruzándose por el cuello (la izquierda apenas después).
        float open = Smooth(0.5f, 0.6f, u) * (1 - Smooth(0.72f, 0.86f, u));
        float cross = Smooth(0.61f, 0.65f, u);
        float crossL = Smooth(0.625f, 0.665f, u);
        var hiR = shR + new Vector3(1.0f, 0.5f, 1.1f) * _armK;
        var hiL = shL + new Vector3(1.0f, 0.5f, -1.1f) * _armK;
        var loR = t + new Vector3(0.4f * _k, blade * 0.6f, -1.6f * _k);
        var loL = t + new Vector3(0.4f * _k, blade * 0.6f, 1.6f * _k);
        var atR = Vector3.Lerp(r1, Vector3.Lerp(hiR, loR, cross), open);
        var atL = Vector3.Lerp(l1, Vector3.Lerp(hiL, loL, crossL), open);
        var rotR = Quaternion.Slerp(restRotR, stab, wR);
        var rotL = Quaternion.Slerp(restRotL, stab, wL);
        rotR = Quaternion.Slerp(rotR, Grip(Aim(-35, -40), Vector3.UnitY), open);
        rotL = Quaternion.Slerp(rotL, Grip(Aim(35, -40), Vector3.UnitY), open);
        if (MainHand == WeaponFamily.Dagger && !_mask.NoWeapon)
        {
            Right(p, atR, rotR, new Vector3(-1, -0.2f, 0.7f));
            Left(p, atL, rotL, new Vector3(-1, -0.2f, -0.7f));
        }
        else
        {
            // Con una sola hoja: la izquierda agarra al caído del hombro mientras la derecha pega.
            Right(p, atR, rotR, new Vector3(-1, -0.2f, 0.7f));
            Left(p, t + new Vector3(0.2f * _k, 1.0f * _k, -0.8f * _k), restRotL, new Vector3(-0.5f, -0.2f, -1));
        }
        _smearOn = (u > 0.27f && u < 0.31f) || (u > 0.6f && u < 0.67f);
    }

    /// <summary>
    /// A quemarropa (arco): parado junto a la cabeza del caído, levanta el arco apuntando abajo, pone la flecha,
    /// tensa despacio hasta la mejilla y suelta; la mano de la cuerda sigue de largo hacia atrás.
    /// </summary>
    private void PointArms(Pose p, Vector3 t, float u, Vector3 shR, Vector3 shL, Vector3 restR, Quaternion restRot, Vector3 restL)
    {
        float up = Smooth(0, 0.22f, u) * (1 - Smooth(0.82f, 1, u));
        float draw = Smooth(0.28f, 0.56f, u);
        float after = Smooth(0.62f, 0.7f, u);
        var anchor0 = shL + new Vector3(0.6f, 0.85f, 0) * _armK;
        var aim = Vector3.Normalize(t - anchor0);
        var anchor = anchor0 + aim * (0.45f * _armK);
        var stave = Vector3.Normalize(Vector3.UnitY - aim * Vector3.Dot(Vector3.UnitY, aim));
        var face = -Vector3.Normalize(Vector3.Cross(stave, aim));
        var bowRot = Grip(stave, face);
        var grip = anchor + aim * (7.4f * _armK);
        var bowPos = grip - Vector3.Transform(Fist, bowRot);
        Right(p, Vector3.Lerp(restR, bowPos, up), Quaternion.Slerp(restRot, bowRot, up), Vector3.Lerp(new Vector3(-0.4f, -1, 0.6f), new Vector3(0.2f, -1, 0) - aim * 0.3f, up));
        if (up < 0.01f) return;
        var nock = bowPos + Vector3.Transform(StringRest, bowRot);
        bool loosed = u >= 0.62f;
        var fist = Vector3.Lerp(nock, anchor, loosed ? 1 : 0.06f + 0.94f * draw);
        if (loosed) fist -= aim * (1.4f * _armK * after);
        var drawRot = Grip(stave, face);
        var hand = fist - Vector3.Transform(Fist, drawRot);
        var elbow = shL - aim * (2.6f * _armK) + new Vector3(0.1f, 0.9f, 0) * _armK;
        Left(p, Vector3.Lerp(restL, hand, up), drawRot, Vector3.Lerp(new Vector3(-1, -0.3f, -0.7f), elbow - (shL + hand) * 0.5f, up));
    }

    /// <summary>
    /// El martillazo (puños): de rodillas junto a la cabeza, un golpe de derecha, uno de izquierda, y las dos
    /// manos juntas bien arriba que bajan de una vez sobre el cráneo.
    /// </summary>
    private void HammerArms(Pose p, Vector3 t, float u, Vector3 shR, Vector3 shL, Vector3 restR, Vector3 restL)
    {
        var fistDown = Grip(Aim(0, -75), Vector3.UnitZ);
        (Vector3 at, float w) Punch(Vector3 rest, Vector3 sh, int side, float hit)
        {
            float load = Smooth(hit - 0.12f, hit - 0.03f, u) * (1 - Smooth(hit - 0.02f, hit, u));
            float strike = Smooth(hit - 0.02f, hit, u) * (1 - Smooth(hit + 0.03f, hit + 0.09f, u));
            var up = sh + new Vector3(0.9f, 0.6f, side * 0.5f) * _armK;
            var into = t + new Vector3(0, 0.9f * _k, side * 0.35f * _k);
            return (Vector3.Lerp(Vector3.Lerp(rest, up, load), into, strike), MathF.Max(load, strike));
        }
        var (r1, wR) = Punch(restR, shR, 1, 0.3f);
        var (l1, wL) = Punch(restL, shL, -1, 0.44f);
        float raise = Smooth(0.5f, 0.64f, u), slam = Smooth(0.685f, 0.72f, u), back = Smooth(0.84f, 1, u);
        var high = new Vector3(1.3f * _k, shR.Y + 2.7f * _armK, 0);
        var low = t + new Vector3(0, 1.1f * _k, 0);
        var both = Vector3.Lerp(high, low, slam);
        float w = raise * (1 - back);
        var atR = Vector3.Lerp(r1, both + new Vector3(0, 0, 0.45f * _k), w);
        var atL = Vector3.Lerp(l1, both + new Vector3(0, 0, -0.45f * _k), w);
        var rotR = Quaternion.Slerp(Grip(Aim(0, -30), Vector3.UnitZ), fistDown, MathF.Max(wR, w));
        var rotL = Quaternion.Slerp(Grip(Aim(0, -30), -Vector3.UnitZ), fistDown, MathF.Max(wL, w));
        Right(p, atR, rotR, new Vector3(-0.5f, 0, 1));
        Left(p, atL, rotL, new Vector3(-0.5f, 0, -1));
    }

    /// <summary>
    /// La extremaunción (cayado): con la izquierda hace la señal de la cruz sobre el caído (la frente, el
    /// pecho, un hombro y el otro); después alza el cayado con las dos manos, derecho, y lo baja de una vez con
    /// el regatón sobre el cráneo.
    /// </summary>
    private void RitesArms(Pose p, Vector3 t, float u, Vector3 shR, Vector3 shL, Vector3 restR, Quaternion restRotR, Vector3 restL, Quaternion restRotL)
    {
        // La cruz, con dos dedos, en el aire sobre el caído.
        float sign = Smooth(0.02f, 0.1f, u) * (1 - Smooth(0.38f, 0.44f, u));
        var over = new Vector3(MathF.Min(t.X, 3.2f * _k), shL.Y - 1.2f * _k, -0.4f * _k);
        Vector3[] marks = { over + new Vector3(0, 1.2f, 0) * _k, over + new Vector3(0, -1.2f, 0) * _k, over + new Vector3(0, 0, -0.9f) * _k, over + new Vector3(0, 0, 0.9f) * _k };
        float k = Math.Clamp((u - RitesCross) / 0.07f, 0, 3.999f);
        int i = (int)k;
        var mark = i < 3 ? Vector3.Lerp(marks[i], marks[i + 1], Smooth(0.3f, 1, k - i)) : marks[3];
        if (u < RitesCross) mark = marks[0];
        var blessRot = Grip(Aim(0, 20), -Vector3.UnitY);
        // El cayado: arriba, derecho (el regatón para abajo), y abajo contra el cráneo.
        float raise = Smooth(0.44f, 0.6f, u), drive = Smooth(0.665f, 0.7f, u), back = Smooth(0.84f, 1, u);
        var upright = Grip(Aim(0, 90), -Vector3.UnitX);
        float below = WeaponModelsStaffBelow * _k;
        var top = new Vector3(t.X, t.Y + below + 3.8f * _k, t.Z);
        var hit = new Vector3(t.X, t.Y + below - 0.6f * _k, t.Z);
        var at = Vector3.Lerp(Vector3.Lerp(restR, top, raise), hit, drive);
        at = Vector3.Lerp(at, restR, back);
        var rot = Quaternion.Slerp(Quaternion.Slerp(restRotR, upright, raise), restRotR, back);
        Right(p, at, rot, new Vector3(-0.2f, -0.3f, 1));
        if (raise > 0.3f && back < 0.7f)
        {
            // La izquierda, más abajo en la vara.
            var grip = at + Vector3.Transform(new Vector3((-2.4f - Fist.X) * _k, 0, 0), rot);
            Left(p, grip, rot, new Vector3(-0.3f, -0.4f, -1));
        }
        else Left(p, Vector3.Lerp(restL, mark, sign), Quaternion.Slerp(restRotL, blessRot, sign), new Vector3(-0.6f, -0.5f, -1));
    }

    /// <summary>El largo de la vara del cayado por debajo del puño (el de los modelos, ver Content.WeaponModels).</summary>
    private const float WeaponModelsStaffBelow = Content.WeaponModels.StaffBelow;

    /// <summary>
    /// Arrancar el alma (cetro): lo alza bien alto, apunta el cráneo al pecho del caído y lo sostiene; la
    /// izquierda, abierta, se estira hacia él y tira (algo sale del cuerpo y sube al cetro), y al final se cierra
    /// de golpe contra el pecho propio.
    /// </summary>
    private void SoulArms(Pose p, Vector3 t, float u, Vector3 shR, Vector3 shL, Vector3 restR, Quaternion restRot, Vector3 restL)
    {
        float raise = Smooth(0, 0.22f, u), point = Smooth(0.28f, 0.36f, u), back = Smooth(0.84f, 1, u);
        var high = shR + new Vector3(0.9f, 1.4f, 0.2f) * _armK;
        var low = shR + new Vector3(2.4f, -1.2f, 0.1f) * _armK;
        float pull = Smooth(0.36f, 0.78f, u);
        var tremble = new Vector3(0, MathF.Sin(_gestureT * 53) * 0.08f, MathF.Cos(_gestureT * 41) * 0.08f) * (_k * point * (1 - back));
        var at = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(restR, high, raise), low, point), restR, back) + tremble;
        var toward = Vector3.Normalize(t - low);
        var rot = Quaternion.Slerp(restRot, Grip(Aim(0, 88), Vector3.UnitX), raise);
        rot = Quaternion.Slerp(rot, Grip(toward, Vector3.UnitY), point);
        rot = Quaternion.Slerp(rot, restRot, back);
        Right(p, at, rot, new Vector3(-0.8f, -0.3f, 0.6f));
        // La izquierda: se estira abierta hacia el caído y, al arrancarlo, vuelve cerrada al pecho.
        float reach = Smooth(0.3f, 0.42f, u) * (1 - Smooth(0.76f, 0.8f, u));
        float clutch = Smooth(0.76f, 0.8f, u) * (1 - back);
        var toT = Vector3.Normalize(t - shL);
        var open = shL + toT * (4.6f * _armK) - toT * (0.9f * _armK * pull);
        var chest = shL + new Vector3(1.2f, -1.6f, 0.9f) * _armK;
        var atL = Vector3.Lerp(Vector3.Lerp(restL, open, reach), chest, clutch);
        Left(p, atL, Grip(toT, Vector3.UnitY), new Vector3(-0.4f, -0.6f, -1));
    }

    /// <summary>
    /// El cuerpo en la ejecución: la estocada se arquea al alzar y se echa encima al clavar; el tajo, igual pero
    /// más abajo; el degüello y el martillazo, de rodillas y encima del caído; a quemarropa, inclinado y mirando
    /// abajo; la extremaunción, la cabeza gacha al bendecir y el peso sobre el cayado; el alma, arqueado atrás
    /// al arrancarla.
    /// </summary>
    private void ExecuteBody(Pose p, float u)
    {
        var mv = p.Move;
        switch (Exec)
        {
            case ExecStyle.Plunge:
            {
                float lift = Smooth(0, 0.3f, u) * (1 - Smooth(0.38f, 0.45f, u));
                float over = Smooth(0.38f, 0.46f, u) * (1 - Smooth(0.72f, 0.84f, u));
                float end = 1 - Smooth(0.85f, 1, u);
                p[Bone.Spine] += new Vector3(0, 0, (9 * lift - 26 * over) * end);
                p[Bone.Head] += new Vector3(0, 0, (8 * lift - 24 * over) * end);
                mv[(int)Bone.Pelvis] += new Vector3(-0.3f * over, -1.9f * over + 0.2f * lift, 0) * _k;
                break;
            }
            case ExecStyle.Behead:
            {
                float raise = Smooth(0, 0.34f, u) * (1 - Smooth(0.43f, 0.5f, u));
                float low = Smooth(0.44f, 0.52f, u) * (1 - Smooth(0.76f, 1, u));
                p[Bone.Spine] += new Vector3(0, -8 * low, 12 * raise - 34 * low);
                p[Bone.Head] += new Vector3(0, 0, 8 * raise - 20 * low);
                mv[(int)Bone.Pelvis] += new Vector3(0.6f * low - 0.2f * raise, -2.4f * low, 0) * _k;
                break;
            }
            case ExecStyle.Throat:
            case ExecStyle.Hammer:
            {
                float kneel = Smooth(0, 0.18f, u) * (1 - Smooth(0.84f, 1, u));
                float hits = 0;
                foreach (float hit in ExecHits(Exec)) hits = MathF.Max(hits, Smooth(hit - 0.03f, hit, u) * (1 - Smooth(hit + 0.02f, hit + 0.1f, u)));
                float rise = Exec == ExecStyle.Hammer ? Smooth(0.5f, 0.64f, u) * (1 - Smooth(0.685f, 0.72f, u)) : 0;
                p[Bone.Pelvis] += new Vector3(0, 0, -16 * kneel);
                p[Bone.Spine] += new Vector3(0, 0, (-24 - 10 * hits + 30 * rise) * kneel);
                p[Bone.Head] += new Vector3(0, 0, (-20 + 12 * rise) * kneel);
                mv[(int)Bone.Pelvis] += new Vector3(0.5f * kneel, -3.9f * kneel + 0.6f * rise, 0) * _k;
                break;
            }
            case ExecStyle.Point:
            {
                float aim = Smooth(0, 0.22f, u) * (1 - Smooth(0.82f, 1, u));
                p[Bone.Spine] += new Vector3(0, 0, -10 * aim);
                p[Bone.Head] += new Vector3(0, 0, -30 * aim);
                mv[(int)Bone.Pelvis] += new Vector3(-0.3f, -0.8f, 0) * (_k * aim);
                break;
            }
            case ExecStyle.Rites:
            {
                float bow = Smooth(0.02f, 0.1f, u) * (1 - Smooth(0.4f, 0.46f, u));
                float lift = Smooth(0.44f, 0.6f, u) * (1 - Smooth(0.665f, 0.7f, u));
                float drive = Smooth(0.665f, 0.7f, u) * (1 - Smooth(0.84f, 1, u));
                p[Bone.Spine] += new Vector3(0, 0, -6 * bow + 8 * lift - 22 * drive);
                p[Bone.Head] += new Vector3(0, 0, -18 * bow + 14 * lift - 26 * drive);
                mv[(int)Bone.Pelvis] += new Vector3(0.3f * drive, -1.4f * drive + 0.3f * lift, 0) * _k;
                break;
            }
            default:
            {
                float raise = Smooth(0, 0.22f, u) * (1 - Smooth(0.28f, 0.36f, u));
                float pull = Smooth(0.36f, 0.78f, u) * (1 - Smooth(0.84f, 1, u));
                float tear = Smooth(0.76f, 0.8f, u) * (1 - Smooth(0.86f, 1, u));
                float shake = MathF.Sin(_gestureT * 37) * pull * (1 - tear);
                p[Bone.Spine] += new Vector3(1.5f * shake, 0, 10 * raise - 8 * pull + 16 * tear);
                p[Bone.Head] += new Vector3(0, 0, 12 * raise - 12 * pull + 20 * tear);
                mv[(int)Bone.Pelvis] += new Vector3(-0.5f * tear, -0.6f * pull, 0) * _k;
                break;
            }
        }
    }
}
