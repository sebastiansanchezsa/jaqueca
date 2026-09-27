using System.Numerics;
using Jaqueca.Anim;
using Jaqueca.Figures.Physics;
using Jaqueca.Figures.Render;
using Jaqueca.Figures.Rig;
using Jaqueca.Look;

namespace Jaqueca.Figures.Anim;

/// <summary>
/// Animación procedural continua de un personaje (se evalúa a 120 Hz, se dibuja a 60 fps):
/// orientación con giro suave, marcha de caminar a correr con los pies clavados al piso por
/// IK (la fase avanza por distancia recorrida: no patinan), pasos para acomodarse al frenar o
/// girar en el lugar, pies que se apoyan en el terreno, reposo vivo (respiración, cambio de
/// peso, mirada, parpadeo) y pelo y pañuelo con inercia.
/// Espacio del mundo: x este, y arriba, z sur. El personaje mira a +X en su espacio.
/// </summary>
/// <summary>Los gestos de las habilidades (ver <see cref="Animator.Gesture"/>).</summary>
/// <summary>
/// Los gestos que se hacen encima de lo que esté haciendo: los de las habilidades (embestida, grito,
/// tiro, maldición), los de mostrar un arma recién tomada: mirarla (<see cref="Inspect"/>) o
/// alzarla (<see cref="Raise"/>), abrir un cofre (<see cref="Heave"/>) y tomar el brebaje de Ossa
/// (<see cref="Drink"/>).
/// </summary>
/// <summary>
/// Los gestos que se hacen encima de lo de siempre. <see cref="Execute"/>: rematar a uno que quedó en el piso (cuál, en
/// <see cref="Animator.Exec"/>; ver Animator.Execute.cs).
/// </summary>
public enum GestureKind : byte { None, Charge, Roar, Throw, Curse, Inspect, Raise, Heave, Drink, Execute }

public sealed partial class Animator
{
    public const float Step = 1f / 120;
    public const float RunSpeed = 58;

    private readonly Skeleton _s;
    private readonly float _legLen;
    /// <summary>
    /// Escalas del cuerpo respecto de la pierna y el brazo con que se afinó la marcha (la
    /// humana): los largos de paso, la altura de los pies, las agachadas y dónde van las manos
    /// se escalan con esto, así un goblin anda y pega igual con su tamaño.
    /// </summary>
    private readonly float _k, _armK;
    /// <summary>Altura de los hombros sobre la pelvis.</summary>
    private readonly float _shoulderY;
    /// <summary>El puño en el hueso de la mano (por donde pasa el mango).</summary>
    private static readonly Vector3 Fist = Content.WeaponModels.Fist;

    /// <summary>Altura del piso en (x, z) del mundo.</summary>
    public Func<float, float, float> Ground = (_, _) => 0;
    /// <summary>Donde se apoya el muñeco de trapo y los pedazos (null = el piso): sobre el agua, flotan.</summary>
    public Func<float, float, float> FloatGround;
    /// <summary>
    /// Nadando: la altura de la superficie del agua (null = hace pie). El cuerpo flota con los hombros
    /// en la superficie, inclinado hacia adelante, y nada a pecho (ver <see cref="SwimPose"/>).
    /// </summary>
    public float? SwimLevel;
    /// <summary>Hundimiento y pataleo al forcejear con unas manos bajo el agua (0..1).</summary>
    public float SwimDrag, SwimStruggle;
    /// <summary>Los muros para el muñeco de trapo y los pedazos (ver <see cref="VerletBody.Walls"/>; null = ninguno).</summary>
    public Func<Vector3, Vector3, float, Vector3> Walls;
    /// <summary>Familia del arma de la mano derecha: decide cómo se lleva y cómo golpea.</summary>
    public WeaponFamily MainHand;
    /// <summary>Cuán encorvado anda (0 derecho, 1 goblin): espalda adelante, cabeza levantada, rodillas dobladas.</summary>
    public float Hunch;
    /// <summary>
    /// Cuánto quiere agacharse para pasar bajo algo (0 derecho, 1 del todo): baja la cadera
    /// doblando las rodillas y se inclina adelante con la cabeza gacha. Lo sigue suave.
    /// </summary>
    public float Duck;
    /// <summary>Cuánto se agacha por su cuenta (los gestos de una criatura: enroscarse antes de saltar, galopar bajo); se suma a <see cref="Duck"/>.</summary>
    public float Crouch;
    private float _duck;
    /// <summary>La altura de la coronilla parado derecho (para saber si pasa bajo un dintel).</summary>
    public float Top => _s.D.PelvisY + _s.D.ShoulderY + 3.1f * _k;
    /// <summary>Murió: es un muñeco de trapo (ver <see cref="Die"/>).</summary>
    public bool Dead => _dead;
    /// <summary>Qué se dibuja: lo cortado no, los muñones sí, y el arma si todavía la tiene.</summary>
    public RenderMask Mask => _mask;
    /// <summary>Soltó el arma (le cortaron la mano o el brazo que la tenía).</summary>
    public bool Disarmed => _mask.NoWeapon;
    /// <summary>Sin piernas: se arrastra.</summary>
    public bool Crawling => _mode == Mode.Crawl;
    /// <summary>Le falta una pierna de la rodilla para abajo: renguea sobre el muñón.</summary>
    public bool Limping => _mode == Mode.Normal && (_legCut[0] > 0 || _legCut[1] > 0);
    /// <summary>Decapitado, de rodillas (se desploma solo al rato).</summary>
    public bool Kneeling => _mode == Mode.Kneel && !_dead;
    /// <summary>Muerto y quieto: el muñeco se durmió (no hace falta volver a dibujarlo).</summary>
    public bool Still => _rag?.Body.Asleep == true;
    /// <summary>Tirado en el piso o levantándose (ver <see cref="LieDown"/>): no camina ni pega.</summary>
    public bool Down => _down;
    /// <summary>Ya empezó a levantarse (y todavía no terminó).</summary>
    public bool Rising => _down && _riseT >= 0 && _riseRate > 0;
    /// <summary>Segundos que tarda en levantarse del piso.</summary>
    public const float RiseTime = 2.95f;
    /// <summary>Punta de la hoja en el hueso de la mano (la pone quien arma la figura; mide cuánto barre la estela).</summary>
    public Vector3 BladeTip = Content.WeaponModels.Tip(new Content.BladeSpec());
    /// <summary>Arco: dónde está la cuerda en reposo (la muesca), en el hueso de la mano.</summary>
    public Vector3 StringRest = new(0, 1.0f, 0);
    /// <summary>Gestos propios de una criatura, encima de la animación de siempre (null = ninguno).</summary>
    public Mannerism Mannerism;
    /// <summary>Segundos que el cuerpo sigue convulsionando al morir (0 = cae quieto).</summary>
    public float Convulse;
    /// <summary>Resucitando: el cadáver todavía se está acomodando (ver <see cref="Reanimate"/>).</summary>
    public bool Reanimating => _reanimT >= 0;
    /// <summary>Cuánto tarda el cadáver en acomodarse boca arriba antes de levantarse.</summary>
    public const float ReanimBlend = 0.75f;
    private Matrix4x4[] _reanimFrom;
    private float _reanimT = -1, _twitch;
    /// <summary>
    /// Lo arrastra una cadena enganchada en el pecho: hacia dónde tira (mundo; el largo, 0..1, es
    /// cuánta fuerza) y el tirón del momento (0..1). Se resiste echándose atrás, los tirones lo
    /// doblan hacia la cadena y las manos la agarran.
    /// </summary>
    public Vector2 Tug;
    public float TugYank;
    /// <summary>Los huesos y medidas del cuerpo.</summary>
    public Skeleton Skel => _s;
    /// <summary>En el aire, saltando (ver <see cref="Leap"/>).</summary>
    public bool Airborne => _leapT < _leapDur;
    /// <summary>Rodando (ver <see cref="Roll"/>).</summary>
    public bool Rolling => _rollT < _rollDur;
    /// <summary>Del espacio del personaje al mundo.</summary>
    public Matrix4x4 WorldMatrix => World;
    /// <summary>El arma deja estela en este paso (los gestos de una criatura la prenden en sus golpes propios).</summary>
    internal bool SmearOn { get => _smearOn; set => _smearOn = value; }

    /// <summary>
    /// Flechas adjuntas, para dibujar: las que quedaron clavadas (en su hueso) y la que está en
    /// la cuerda mientras tensa (en la muesca).
    /// </summary>
    public IReadOnlyList<(Bone bone, Matrix4x4 local)> Attached => _attached;
    /// <summary>
    /// Estela del golpe: posiciones recientes de la mano del arma (espacio del personaje, ya
    /// afinadas hacia la cola) donde el rasterizado dibuja las formas de estela del arma.
    /// </summary>
    public IReadOnlyList<Matrix4x4> Smear => _smear;

    // ---- salida
    /// <summary>Origen del personaje en el mundo (entre los pies, a la altura del piso suavizada).</summary>
    public Vector3 Root;
    /// <summary>Hacia dónde mira el cuerpo (radianes; 0 = este, π/2 = sur).</summary>
    public float Yaw;
    public bool EyesClosed;
    /// <summary>Registro de pasos para diagnóstico (null = apagado): tiempo, pie, motivo.</summary>
    public List<(float t, int foot, string why)> Log;
    public Pose Pose { get; private set; } = new();
    public Matrix4x4[] Bones { get; private set; } = new Matrix4x4[(int)Bone.Count];
    private readonly Vector3[] _local = new Vector3[2];

    // ---- entrada del frame
    private Vector2 _pos0, _pos1, _vel;
    private float _wantYaw;
    /// <summary>Golpe en curso (lo decide la simulación) y cuánto lleva al final del frame y en este paso.</summary>
    private ClipDef _act;
    private float _actTime, _actT, _frameLeft;

    // ---- estado
    private float _time, _yawVel, _phase, _speed, _run, _exert;
    private float _stillTime, _sinceLift = 10;
    private int _lastLift = -1;
    private string _why;
    private float _leanFwd, _leanSide, _prevSpeed;
    private bool _moving;
    /// <summary>Cuánto está en marcha (0 parado, 1 andando), sin saltos al arrancar o frenar.</summary>
    private float _go;
    /// <summary>Hacia adelante, suavizado (ver <see cref="Tick"/>).</summary>
    private Vector2 _fwd;
    private readonly Foot[] _feet = { new(), new() };
    private float _settleCooldown;
    // Mano de la espada: lo último que se dibujó y desde dónde se mezcla cuando cambia el golpe.
    private ClipDef _armState;
    private float _armStateT, _armSince = 10, _armBlend = 0.1f;
    private Vector3 _armPos, _armFromPos;
    private Quaternion _armRot = Quaternion.Identity, _armFromRot = Quaternion.Identity;
    private bool _armInit;
    // Estela: la mano en el mundo en cada paso del golpe (la más nueva al final).
    private const float SmearTime = 0.075f;
    private readonly (Matrix4x4 hand, float t)[] _trail = new (Matrix4x4, float)[16];
    private int _trailN;
    private bool _smearOn;
    private readonly List<Matrix4x4> _smear = new();
    // Recorrido de la hoja en el golpe activo (la mano en el mundo, paso a paso), para saber qué atravesó.
    private List<Matrix4x4> _sweep = new(), _sweepBefore = new();
    private ClipDef _sweepAct;
    private float _sweepT;
    // Golpes recibidos: hace cuánto (segundos) y hacia dónde empujó (mundo); muerte.
    private float _hurtT = 10;
    private Vector2 _hurtPush;
    private bool _dead;
    // Cortes: lo que falta, qué le quedó de cada pierna (0 entera, 1 sin canilla, 2 nada), cómo
    // se mueve y el muñeco de trapo al morir. Los huesos en el mundo, ahora y un paso antes, arman
    // los muñecos con el impulso que traían.
    private readonly RenderMask _mask = new();
    private readonly int[] _legCut = new int[2];
    private enum Mode { Normal, Crawl, Kneel }
    private Mode _mode;
    private float _modeT, _crawlPh;
    private Ragdoll _rag;
    private readonly Matrix4x4[] _world = new Matrix4x4[(int)Bone.Count], _worldBefore = new Matrix4x4[(int)Bone.Count];
    private bool _haveWorld;
    // Arco: si tensa, cuánto, hacia dónde apunta, hace cuánto soltó, cuánto está en posición de tiro y la mano en la cuerda.
    private bool _bowDrawing;
    private float _bowDraw, _bowAim, _bowSince = 10, _bowUp, _bowHeld;
    private readonly List<(Bone bone, Matrix4x4 local)> _stuck = new(), _attached = new();
    // Puños: cuánto está en guardia y desde cuándo no pega; por brazo (0 derecho, 1 izquierdo), qué golpe
    // tiró y hace cuánto (cada brazo termina el suyo aunque ya haya salido el del otro).
    private float _guard, _sinceFist = 10;
    private readonly ClipDef[] _fistClip = new ClipDef[2];
    private readonly float[] _fistT = { 10, 10 };
    private ClipDef _fistAct;
    private float _fistActT;
    // Tirado en el piso: segundos desde que empezó a levantarse (negativo: sigue tirado).
    private bool _down;
    private float _riseT = -1, _riseRate = 1;
    // Salto: cuánto lleva, cuánto dura, qué tan alto y hace cuánto aterrizó; lo que sabe una criatura del paso.
    private float _leapT = 10, _leapDur, _leapH, _sinceLand = 10;
    // Rueda: cuánto lleva y cuánto dura (los pies los lleva el salto: van recogidos y apoyan al final).
    private float _rollT = 10, _rollDur;
    private bool _rollAir, _handsDown;
    /// <summary>Dónde apoyó las manos al zambullirse (mundo): quedan ahí mientras el cuerpo pasa por encima.</summary>
    private readonly Vector3[] _hands = new Vector3[2];
    private readonly Motion _motion = new();
    // Convulsiones del muñeco: segundos desde que murió y hasta la próxima sacudida.
    private float _deadT, _joltT;
    // Reposo.
    private float _shiftT, _shift, _shiftGoal, _lookT, _look, _lookGoal, _lookPitch, _lookPitchGoal, _blinkT, _blinkLeft;
    private float _pelvisY;
    // Nado: cuánto está nadando (0..1, suave al entrar y salir), la fase de la brazada, cuánto se
    // inclina el cuerpo (radianes) y la pose de nado que se mezcla con la de siempre.
    private float _swimW, _swimPh, _swimTilt = 0.7f, _swimStrike;
    private bool _wasSwim;
    private readonly Pose _swimPose = new();
    private readonly SpringChain _hair, _tail, _scarf;
    private uint _rng;

    private sealed class Foot
    {
        public Vector3 Plant;       // dónde está apoyado (mundo, altura del tobillo)
        public float PlantYaw;
        public bool Swing;
        public Vector3 From, Pos;
        public float FromYaw, Yaw, T, Height, Pitch;
        /// <summary>Hasta cuándo puede seguir en el aire (lo acorta el otro pie al despegar).</summary>
        public float Deadline = float.PositiveInfinity;
        /// <summary>Cuándo apoyó por última vez.</summary>
        public float PlantedAt;
    }

    public Animator(Skeleton s, Vector2 pos, float yaw, int seed = 1)
    {
        _s = s;
        _legLen = s.LegLen;
        _k = _legLen / Dims.HumanLeg;
        _armK = (s.D.ArmLen + 0.7f) / (new Dims().ArmLen + 0.7f);
        _shoulderY = s.D.ShoulderY;
        var d = s.D;
        _hair = new(Bone.HairBack1, Bone.HairBack2, d.HairBackSeg, d.HairBackSeg) { MaxAngle = 0.75f, MaxX = 0.2f, Stiffness = d.HairStiff };
        _tail = new(Bone.Tail1, Bone.Tail2, d.TailSeg, d.TailSeg) { MaxAngle = 1.0f, MinX = d.TailMinX, MaxX = d.TailMaxX, Stiffness = d.TailStiff };
        _scarf = new(Bone.Scarf1, Bone.Scarf2, d.ScarfSeg, d.ScarfSeg) { MaxAngle = 0.7f, MinX = 0.05f, Stiffness = d.ScarfStiff };
        _rng = (uint)seed * 2654435761u + 12345;
        _pos0 = _pos1 = pos;
        Yaw = _wantYaw = yaw;
        _blinkT = 1.5f + Rand() * 3;
        _shiftT = 3 + Rand() * 3;
        _lookT = 2 + Rand() * 3;
        Root = new Vector3(pos.X, 0, pos.Y);
        _pelvisY = 0;
        for (int k = 0; k < 2; k++)
        {
            var f = _feet[k];
            f.Plant = f.Pos = RestFoot(k, pos, yaw, Ground(pos.X, pos.Y));
            f.PlantYaw = f.Yaw = f.FromYaw = yaw;
        }
        Pose.Solve(_s, Bones);
    }

    private float Rand()
    {
        _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
        return (_rng & 0xFFFFFF) / (float)0x1000000;
    }

    private static int Side(int k) => k == 0 ? 1 : -1;
    private static Vector2 Dir(float a) => new(MathF.Cos(a), MathF.Sin(a));
    /// <summary>Derecha del personaje en el mundo (su +Z).</summary>
    private static Vector2 RightOf(float yaw) => new(-MathF.Sin(yaw), MathF.Cos(yaw));
    public static float Wrap(float a) { a %= MathF.Tau; if (a > MathF.PI) a -= MathF.Tau; if (a <= -MathF.PI) a += MathF.Tau; return a; }
    private static float Smooth(float e0, float e1, float x) { float t = Math.Clamp((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t); }
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float Damp(float cur, float goal, float rate, float h) => cur + (goal - cur) * (1 - MathF.Exp(-rate * h));

    /// <summary>Dónde descansa cada pie parado (un poco abierto y escalonado).</summary>
    private Vector3 RestFoot(int k, Vector2 pos, float yaw, float groundY)
    {
        int side = Side(k);
        var p = pos + Dir(yaw) * ((k == 0 ? 0.45f : -0.3f) * _k) + RightOf(yaw) * side * _s.D.HipW * 1.2f;
        return new Vector3(p.X, Ground(p.X, p.Y) + _s.AnkleY, p.Y);
    }

    // ------------------------------------------------------------------ marcha

    /// <summary>
    /// Largo de un paso según la velocidad, para una pierna de largo <paramref name="leg"/>
    /// (muslo + canilla). Con la pierna humana da pasos de verdad: ~7 u caminando ligero y ~15
    /// en carrera. El trayecto de cada pie apoyado no pasa de una pierna, así que para correr
    /// rápido se acorta el apoyo y se alarga el vuelo.
    /// </summary>
    public static float StepLength(float speed, float leg = Dims.HumanLeg)
    {
        float k = leg / 5.15f;
        float s = speed / k;
        return k * (s < 13 ? 2.6f + s * (1.4f / 13) : 4.0f + (s - 13) * (6.5f / 45));
    }
    /// <summary>Fracción del ciclo con cada pie apoyado: caminando siempre hay uno, corriendo hay vuelo.</summary>
    private float Duty => Lerp(0.62f, 0.25f, _run);

    /// <summary>
    /// El arco según la simulación: si está tensando, cuánto (0..1), hacia dónde apunta (radianes,
    /// mundo) y hace cuánto soltó la última flecha. Se llama antes de <see cref="Update"/>.
    /// </summary>
    public void SetBow(bool drawing, float draw, float aim, float sinceShot)
    {
        _bowHeld = drawing ? (_bowDrawing ? _bowHeld : 0) : 0;
        _bowDrawing = drawing;
        _bowDraw = draw;
        _bowAim = aim;
        _bowSince = sinceShot;
    }

    /// <summary>La muesca en el mundo (de donde sale la flecha; su eje X apunta hacia donde vuela).</summary>
    public Matrix4x4 NockWorld
    {
        get { EnsureWorld(); return _world[(int)Bone.Nock]; }
    }

    /// <summary>Le queda clavada una flecha: <paramref name="arrow"/> es la flecha en el mundo (cola en el origen, punta hacia +X).</summary>
    public void Stick(Bone bone, Matrix4x4 arrow)
    {
        EnsureWorld();
        if (_mask.Hidden[(int)bone]) return;
        Matrix4x4.Invert(_world[(int)bone], out var inv);
        if (_stuck.Count >= 12) _stuck.RemoveAt(0);
        _stuck.Add((bone, arrow * inv));
    }

    /// <summary>
    /// Lo pone de golpe en otro lugar (al pasar de un nivel a otro): parado, quieto, con los pies
    /// en su lugar y el pelo colgando en reposo. Si estaba tirado, sigue tirado.
    /// </summary>
    public void Place(Vector2 pos, float yaw)
    {
        _pos0 = _pos1 = pos;
        _vel = _fwd = Vector2.Zero;
        _speed = _prevSpeed = _yawVel = _go = _leanFwd = _leanSide = 0;
        _moving = false;
        Yaw = _wantYaw = yaw;
        Root = new Vector3(pos.X, Ground(pos.X, pos.Y), pos.Y);
        for (int k = 0; k < 2; k++)
        {
            var f = _feet[k];
            f.Swing = false;
            f.Plant = f.Pos = f.From = RestFoot(k, pos, yaw, Root.Y);
            f.PlantYaw = f.Yaw = f.FromYaw = yaw;
            f.Deadline = float.PositiveInfinity;
        }
        _trailN = 0;
        _smearOn = false;
        _haveWorld = false;
        _hair.Reset();
        _tail.Reset();
        _scarf.Reset();
    }

    /// <summary>Queda tirado boca arriba, desmayado, con los ojos cerrados (hasta <see cref="GetUp"/>).</summary>
    public void LieDown()
    {
        _down = true;
        _riseT = -1;
        _riseRate = 1;
    }

    /// <summary>
    /// Se desploma (sin fuerzas, no muerto): el levantarse al revés, hasta quedar tirado. Con <paramref name="rate"/>
    /// más chico, más despacio (el que se desmaya con el brebaje: se le doblan las rodillas y se sienta antes de caer).
    /// </summary>
    public void Collapse(float rate = 3.5f)
    {
        if (_down) return;
        _down = true;
        _riseT = RiseTime;
        _riseRate = -rate;
    }

    /// <summary>
    /// Abre los ojos y se levanta: se sienta, apoya un pie y una rodilla, y se para (<see cref="RiseTime"/>;
    /// con <paramref name="rate"/> mayor que 1, más rápido: el que se levanta de un revolcón).
    /// </summary>
    public void GetUp(float rate = 1)
    {
        if (!_down || (_riseT >= 0 && _riseRate > 0)) return;
        if (_riseT < 0) _riseT = 0;
        _riseRate = rate;
    }

    /// <summary>Muerto (muñeco de trapo): el golpe más fuerte contra el piso desde la última vez (unidades por segundo; 0 si no).</summary>
    public float TakeImpact() => _rag?.Body.TakeImpact() ?? 0;

    /// <summary>El cadáver se sacude (sin levantarse): tirones sueltos durante <paramref name="seconds"/>.</summary>
    public void Twitch(float seconds) => _twitch = MathF.Max(_twitch, seconds);

    /// <summary>
    /// Vuelve de la muerte. El muñeco de trapo se acomoda boca arriba como tirado de hilos (cada
    /// hueso va de donde quedó a la pose de tirado, primero la cadera y el pecho, los brazos
    /// arrastrados después, la cabeza última y de golpe: <see cref="ReanimBlend"/>) y se levanta
    /// como después de un desmayo, pero más rápido (<paramref name="rate"/>). Lo cortado sigue
    /// cortado (sin cabeza también se levanta); sin piernas no puede. Devuelve si se levantó.
    /// </summary>
    public bool Reanimate(float rate = 1.6f)
    {
        if (_rag == null || _mode == Mode.Crawl) return false;
        _reanimFrom ??= new Matrix4x4[_world.Length];
        Array.Copy(_world, _reanimFrom, _world.Length);
        // Tirado, los pies quedan adelante y la cabeza atrás (ver RiseKeys): hacia dónde quedó la cabeza.
        var up = Vector3.TransformNormal(Vector3.UnitY, _world[(int)Bone.Spine]);
        var toFeet = new Vector2(-up.X, -up.Z);
        toFeet = toFeet.LengthSquared() > 1e-4f ? Vector2.Normalize(toFeet) : Dir(Yaw);
        var pelvis = _world[(int)Bone.Pelvis].Translation;
        var at = new Vector2(pelvis.X, pelvis.Z) + toFeet * (4.2f * _k);
        _rag = null;
        _dead = false;
        _mode = Mode.Normal;
        _deadT = 0;
        _twitch = 0;
        _hurtT = 10;
        Place(at, MathF.Atan2(toFeet.Y, toFeet.X));
        LieDown();
        GetUp();
        _riseRate = rate;
        _reanimT = 0;
        // Hasta el próximo paso, los huesos siguen donde los dejó el cadáver (ahora desde el origen nuevo).
        Matrix4x4.Invert(World, out var toChar);
        for (int i = 1; i < (int)Bone.Count; i++)
            if (!_mask.Hidden[i] && !Skeleton.Secondary((Bone)i) && (Bone)i != Bone.Nock) Bones[i] = _reanimFrom[i] * toChar;
        HangSecondary();
        return true;
    }

    /// <summary>
    /// Mientras resucita, lo que quedó del cadáver pesa sobre la pose: cada hueso va de donde lo
    /// dejó el muñeco de trapo a donde lo pone el levantarse, a su tiempo (a los tirones).
    /// </summary>
    private void FromCorpse(float h)
    {
        _reanimT += h;
        var world = World;
        Matrix4x4.Invert(world, out var toChar);
        for (int i = 1; i < (int)Bone.Count; i++)
        {
            var b = (Bone)i;
            if (_mask.Hidden[i] || Skeleton.Secondary(b) || b == Bone.Nock) continue;
            // La cadera y el pecho primero; brazos y piernas después; la cabeza, última y de golpe.
            float delay = b switch
            {
                Bone.Pelvis or Bone.Spine => 0,
                Bone.Head => 0.42f,
                _ => 0.12f + 0.05f * (i % 3),
            };
            float span = b == Bone.Head ? 0.12f : 0.3f;
            float w = Smooth(delay, delay + span, _reanimT);
            // Un tirón: pasa de largo un poco y vuelve.
            w += 0.12f * MathF.Sin(MathF.PI * w) * (1 - w);
            if (w >= 1) continue;
            Matrix4x4.Decompose(_reanimFrom[i], out var s0, out var r0, out var t0);
            Matrix4x4.Decompose(Bones[i] * world, out var s1, out var r1, out var t1);
            var m = Matrix4x4.CreateScale(Vector3.Lerp(s0, s1, w)) * Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(r0, r1, w)) * Matrix4x4.CreateTranslation(Vector3.Lerp(t0, t1, w));
            Bones[i] = m * toChar;
        }
        HangSecondary();
        if (_reanimT >= ReanimBlend) _reanimT = -1;
    }

    /// <summary>
    /// Salta: los dos pies dejan el piso durante <paramref name="duration"/> segundos (la cadera
    /// sube en arco hasta <paramref name="height"/> y las piernas se recogen) y al caer apoyan
    /// donde estén, con un amortiguado. Adónde va lo decide la simulación, que mueve el cuerpo.
    /// </summary>
    public void Leap(float duration, float height)
    {
        if (_rag != null || _mode != Mode.Normal || _down || duration <= 0) return;
        _leapT = 0;
        _leapDur = duration;
        _leapH = height;
    }

    /// <summary>
    /// Se tira al piso y rueda: una voltereta hacia adelante en <paramref name="duration"/> segundos.
    /// Se agacha y se zambulle con los brazos adelante, se hace una bola (las rodillas al pecho,
    /// la cabeza metida, los brazos abrazando las canillas), da la vuelta entera sobre el hombro y
    /// cae de pie, amortiguando. Adónde va lo decide la simulación (mueve el cuerpo); el giro ya
    /// mira hacia allá (ver <see cref="FaceNow"/>).
    /// </summary>
    public void Roll(float duration)
    {
        if (_rag != null || _mode != Mode.Normal || _down || duration <= 0) return;
        _rollT = 0;
        _rollDur = duration;
        _rollAir = false;
        _handsDown = false;
    }

    /// <summary>La rueda: cuándo despegan los pies (se impulsa) y cuándo vuelven a apoyar (fracción de la rueda).</summary>
    public const float RollLift = 0.17f, RollLand = 0.72f;

    /// <summary>Mira ya hacia <paramref name="yaw"/>, sin girar (al tirarse a rodar hacia otro lado).</summary>
    public void FaceNow(float yaw)
    {
        Yaw = _wantYaw = yaw;
        _yawVel = 0;
    }

    /// <summary>Recibió un golpe que lo empuja hacia <paramref name="push"/> (mundo): el cuerpo se sacude para ese lado y se recupera.</summary>
    public void Hurt(Vector2 push)
    {
        _hurtT = 0;
        _hurtPush = push.LengthSquared() > 1e-6f ? Vector2.Normalize(push) : -Dir(Yaw);
    }

    /// <summary>
    /// Muere empujado hacia <paramref name="push"/> (mundo): se vuelve un muñeco de trapo con
    /// todo lo que le queda, y sigue con el impulso que traía más el del golpe.
    /// </summary>
    public void Die(Vector2 push, float lift = 20)
    {
        Hurt(push);
        _dead = true;
        if (_rag == null) StartRagdoll(new Vector3(push.X, 0, push.Y) * 0.5f + new Vector3(0, lift, 0));
    }

    /// <summary>
    /// Corta el cuerpo en la articulación de <paramref name="at"/>: ese hueso y todo lo que cuelga
    /// de él se van y en el cuerpo queda el muñón. Devuelve los pedazos que salen volando, ya
    /// empujados hacia <paramref name="push"/> (mundo) y girando: el miembro y, si en esa mano iba
    /// el arma, el arma aparte. Sin la cabeza cae de rodillas y al rato se desploma; sin una
    /// canilla renguea sobre el muñón; sin las dos piernas (o sin una entera) se arrastra.
    /// </summary>
    public List<Gib> Sever(Bone at, Vector2 push, int seed = 1)
    {
        var gibs = new List<Gib>();
        if (_mask.Hidden[(int)at] || _rag != null) return gibs;
        EnsureWorld();
        var gone = new List<Bone>();
        for (int i = 0; i < (int)Bone.Count; i++)
            if (!_mask.Hidden[i] && Hangs((Bone)i, at)) gone.Add((Bone)i);
        var rng = new Random(seed);
        var dir = new Vector3(push.X, 0, push.Y);
        bool weapon = gone.Contains(Bone.HandR) && !_mask.NoWeapon && MainHand != WeaponFamily.None;
        var limb = new Gib(gone, at, weapon ? GibKind.NoWeapon : GibKind.Limb, _world, _worldBefore, Bones, Yaw, BladeTip, FloatGround ?? Ground, _s.D);
        limb.Body.Body.Walls = Walls;
        limb.Body.Push(dir * 1.1f + new Vector3(0, 50 + 25 * rng.NextSingle(), 0), Step, 0);
        limb.Body.Spin(RandomAxis(rng) * 14, Step);
        gibs.Add(limb);
        if (weapon)
        {
            var w = new Gib(new[] { Bone.HandR, Bone.Nock }, Bone.HandR, GibKind.Weapon, _world, _worldBefore, Bones, Yaw, BladeTip, Ground);
            w.Body.Body.Walls = Walls;
            w.Body.Push(dir * 0.8f + new Vector3(0, 40, 0), Step, 0);
            w.Body.Spin(RandomAxis(rng) * 18, Step);
            gibs.Add(w);
            _mask.NoWeapon = true;
        }
        foreach (var b in gone) _mask.Hidden[(int)b] = true;
        _mask.Cut[(int)at] = true;

        for (int k = 0; k < 2; k++)
        {
            if (at == Skeleton.Leg(Side(k), 0)) _legCut[k] = 2;
            else if (at == Skeleton.Leg(Side(k), 1)) _legCut[k] = Math.Max(_legCut[k], 1);
        }
        if (at == Bone.Head) { _mode = Mode.Kneel; _modeT = 0; }
        else if (_mode == Mode.Normal && (_legCut[0] == 2 || _legCut[1] == 2 || (_legCut[0] > 0 && _legCut[1] > 0))) { _mode = Mode.Crawl; _modeT = 0; }
        return gibs;
    }

    private static Vector3 RandomAxis(Random rng) => Vector3.Normalize(new Vector3(rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1) + new Vector3(0, 0.01f, 0));

    /// <summary>¿<paramref name="b"/> cuelga de <paramref name="at"/> (o es él)?</summary>
    private static bool Hangs(Bone b, Bone at)
    {
        for (var x = b; ; x = Skeleton.Parent[(int)x])
        {
            if (x == at) return true;
            if (x == Bone.Root) return false;
        }
    }

    /// <summary>¿Ya le cortaron este hueso?</summary>
    public bool IsCut(Bone b) => _mask.Hidden[(int)b];

    /// <summary>La luz de la Dolorosa cose un miembro que fue cortado en esta pelea.</summary>
    public bool Reattach(Bone at)
    {
        if (!_mask.Cut[(int)at]) return false;
        _mask.Cut[(int)at] = false;
        for (int i = 0; i < (int)Bone.Count; i++)
            if (Hangs((Bone)i, at)) _mask.Hidden[i] = false;
        if (!_mask.Hidden[(int)Bone.HandR]) _mask.NoWeapon = false;
        for (int k = 0; k < 2; k++)
        {
            _legCut[k] = _mask.Cut[(int)Skeleton.Leg(Side(k), 0)] ? 2
                : _mask.Cut[(int)Skeleton.Leg(Side(k), 1)] ? 1 : 0;
        }
        if (_mode == Mode.Crawl && _legCut[0] < 2 && _legCut[1] < 2 && (_legCut[0] == 0 || _legCut[1] == 0))
        {
            _mode = Mode.Normal;
            _modeT = 0;
        }
        return true;
    }

    /// <summary>Un punto de un hueso en el mundo.</summary>
    public Vector3 WorldPoint(Bone b, Vector3 local)
    {
        EnsureWorld();
        return Vector3.Transform(local, _world[(int)b]);
    }

    /// <summary>
    /// La hoja en el mundo (del puño a la punta) en cada paso del golpe activo del golpe en curso,
    /// o (<paramref name="previous"/>) del anterior, si ya empezó otro.
    /// </summary>
    public IEnumerable<(Vector3 a, Vector3 b)> BladeSweep(bool previous = false)
    {
        EnsureWorld();
        var list = previous ? _sweepBefore : _sweep;
        foreach (var hand in list) yield return (Vector3.Transform(Fist, hand), Vector3.Transform(BladeTip, hand));
        if (list.Count > 0) yield break;
        var now = _world[(int)Bone.HandR];
        yield return (Vector3.Transform(Fist, now), Vector3.Transform(BladeTip, now));
    }

    /// <summary>Guarda la hoja de este paso si el golpe está activo (un golpe nuevo empieza de cero).</summary>
    private void RecordSweep()
    {
        if (_act == null) return;
        if (_act != _sweepAct || _actT < _sweepT)
        {
            (_sweep, _sweepBefore) = (_sweepBefore, _sweep);
            _sweep.Clear();
            _sweepAct = _act;
        }
        _sweepT = _actT;
        if (_actT >= _act.HitStart - 0.03f && _actT <= _act.HitEnd + 0.02f && _sweep.Count < 64) _sweep.Add(_world[(int)Bone.HandR]);
    }

    /// <summary>El muñón de un corte en el mundo y hacia dónde sale la sangre (null si ya no está: se cortó más arriba).</summary>
    public (Vector3 at, Vector3 dir)? Wound(Bone cut)
    {
        foreach (var w in Wounds(cut)) return w;
        return null;
    }

    /// <summary>Muñones del cuerpo en el mundo: dónde están y hacia dónde sale la sangre.</summary>
    public IEnumerable<(Vector3 at, Vector3 dir)> Wounds(Bone? only = null)
    {
        if (!_haveWorld) yield break;
        for (int i = 0; i < (int)Bone.Count; i++)
        {
            if (!_mask.Cut[i] || (only != null && (int)only.Value != i)) continue;
            int parent = (int)Skeleton.Parent[i];
            if (_mask.Hidden[parent]) continue;
            var pm = _world[parent];
            var at = Vector3.Transform(_s.Offset[i], pm);
            var dir = at - pm.Translation;
            yield return (at, dir.LengthSquared() > 1e-6f ? Vector3.Normalize(dir) : Vector3.UnitY);
        }
    }

    private void EnsureWorld()
    {
        if (_haveWorld) return;
        var w = World;
        for (int i = 0; i < _world.Length; i++) _world[i] = Bones[i] * w;
        Array.Copy(_world, _worldBefore, _world.Length);
        _haveWorld = true;
    }

    /// <summary>Guarda los huesos en el mundo de este paso (y los del anterior).</summary>
    private void RecordWorld()
    {
        var w = World;
        if (_haveWorld) Array.Copy(_world, _worldBefore, _world.Length);
        for (int i = 0; i < _world.Length; i++) _world[i] = Bones[i] * w;
        if (!_haveWorld) { Array.Copy(_world, _worldBefore, _world.Length); _haveWorld = true; }
    }

    /// <summary>Pasa todo lo que le queda al muñeco de trapo, con el impulso que traía más <paramref name="push"/>.</summary>
    private void StartRagdoll(Vector3 push)
    {
        EnsureWorld();
        var bones = new List<Bone>();
        for (int i = 1; i < (int)Bone.Count; i++)
            if (!_mask.Hidden[i] && !Skeleton.Secondary((Bone)i)) bones.Add((Bone)i);
        _rag = new Ragdoll(bones, _world, _worldBefore, FloatGround ?? Ground, dims: _s.D);
        _rag.Body.Walls = Walls;
        _rag.Push(push, Step, 0.6f);
        _smearOn = false;
        _trailN = 0;
    }

    /// <summary>Muñeco de trapo: la física mueve los huesos y el personaje (su origen) sigue a la cadera.</summary>
    private void RagdollTick(float h)
    {
        // Convulsiones: sacudidas sueltas de manos, pies y cabeza, cada vez más débiles.
        _deadT += h;
        _twitch = MathF.Max(0, _twitch - h);
        if (_deadT < Convulse || _twitch > 0)
        {
            _joltT -= h;
            if (_joltT <= 0)
            {
                _joltT = 0.05f + Rand() * 0.2f;
                float left = _twitch > 0 ? 0.8f : 1 - _deadT / Convulse;
                _rag.Jolt(Rand(), Rand(), Rand(), (40 + 50 * Rand()) * left * _k, Step);
            }
        }
        _rag.Step(h);
        _rag.Write(_world);
        var c = _world[(int)Bone.Pelvis].Translation;
        Root = new Vector3(c.X, (FloatGround ?? Ground)(c.X, c.Z), c.Z);
        Matrix4x4.Invert(World, out var toChar);
        foreach (var b in _rag.Bones) Bones[(int)b] = _world[(int)b] * toChar;
        Bones[(int)Bone.Nock] = Matrix4x4.CreateTranslation(StringRest) * Bones[(int)Bone.HandR];
        _world[(int)Bone.Nock] = Bones[(int)Bone.Nock] * World;
        HangSecondary();
        UpdateSprings(h);
        if (Mannerism != null)
        {
            _motion.Time = _time;
            _motion.Step = h;
            Mannerism.Secondary(this, Pose, _motion, true);
        }
        HangSecondary();
        EyesClosed = true;
        StepCloth(h);
    }

    /// <summary>Pelo, coleta y pañuelo cuelgan de su hueso con la rotación de sus resortes.</summary>
    private void HangSecondary()
    {
        for (int i = (int)Bone.HairBack1; i < (int)Bone.Count; i++)
        {
            int parent = (int)Skeleton.Parent[i];
            var rot = Pose.Local[i] is { } q ? Matrix4x4.CreateFromQuaternion(q) : Matrix4x4.Identity;
            Bones[i] = rot * Matrix4x4.CreateTranslation(_s.Offset[i] + Pose.Move[i]) * Bones[parent];
        }
    }

    /// <summary>
    /// Golpe en curso según la simulación (null = ninguno) y cuántos segundos lleva al final
    /// de este frame. Se llama antes de <see cref="Update"/>.
    /// </summary>
    public void SetAction(ClipDef clip, float time)
    {
        _act = clip;
        _actTime = time;
    }

    /// <summary>
    /// Avanza la animación. <paramref name="pos"/> y <paramref name="vel"/> son los de la
    /// simulación (x, z del mundo); <paramref name="wantYaw"/> hacia dónde quiere mirar.
    /// </summary>
    public void Update(Vector2 pos, Vector2 vel, float wantYaw, float dt)
    {
        _pos0 = _pos1;
        _pos1 = pos;
        _vel = vel;
        _wantYaw = wantYaw;
        dt = Math.Min(dt, 0.1f);
        if (dt <= 0) return;
        // Se avanza exactamente el tiempo del frame (en pasos de a lo sumo 1/120 s): la pose
        // corresponde siempre al instante que se dibuja, a cualquier cantidad de fps.
        int n = Math.Max(1, (int)MathF.Ceiling(dt / Step - 1e-3f));
        float h = dt / n;
        for (int i = 1; i <= n; i++)
        {
            _frameLeft = dt * (n - i) / n;
            Tick(Vector2.Lerp(_pos0, _pos1, i / (float)n), h);
        }
        BuildSmear();
        _attached.Clear();
        foreach (var a in _stuck) if (!_mask.Hidden[(int)a.bone]) _attached.Add(a);
        if (((_bowDrawing && _bowDraw > 0.05f) || ExecNocked(out _)) && !_mask.NoWeapon && MainHand == WeaponFamily.Bow) _attached.Add((Bone.Nock, Matrix4x4.Identity));
    }

    private Matrix4x4 World => Matrix4x4.CreateRotationY(-Yaw) * Matrix4x4.CreateTranslation(Root);

    /// <summary>
    /// Guarda dónde estuvo la mano del arma en este paso (en el mundo: la estela no se mueve con
    /// el cuerpo). Cuando la hoja frena deja de crecer y lo que quedó se desvanece solo.
    /// </summary>
    private void RecordTrail()
    {
        if (!_smearOn) { _trailN = 0; return; }
        var hand = Bones[(int)Bone.HandR] * World;
        bool fast = _trailN == 0 || Vector3.Distance(Vector3.Transform(BladeTip, hand), Vector3.Transform(BladeTip, _trail[_trailN - 1].hand)) > 0.3f;
        if (fast)
        {
            if (_trailN == _trail.Length) { Array.Copy(_trail, 1, _trail, 0, _trail.Length - 1); _trailN--; }
            _trail[_trailN++] = (hand, _time);
        }
        int old = 0;
        while (old < _trailN - 1 && _time - _trail[old + 1].t >= SmearTime) old++;
        if (old > 0) { Array.Copy(_trail, old, _trail, 0, _trailN - old); _trailN -= old; }
    }

    /// <summary>
    /// Arma la estela: entre cada par de posiciones guardadas de la mano intercala las que hagan
    /// falta para que la punta no avance más de medio píxel entre copia y copia (sin huecos), y
    /// cada copia se afina y se acorta hacia la punta según su edad.
    /// </summary>
    private void BuildSmear()
    {
        _smear.Clear();
        if (_trailN < 2) return;
        Matrix4x4.Invert(World, out var toChar);
        for (int i = _trailN - 1; i > 0; i--)
        {
            var (a, ta) = _trail[i];
            var (b, tb) = _trail[i - 1];
            float dist = Vector3.Distance(Vector3.Transform(BladeTip, a), Vector3.Transform(BladeTip, b));
            int n = Math.Clamp((int)MathF.Ceiling(dist / 0.3f), 1, 20);
            Matrix4x4.Decompose(a, out _, out var ra, out var pa);
            Matrix4x4.Decompose(b, out _, out var rb, out var pb);
            for (int j = 0; j < n; j++)
            {
                float u = j / (float)n;
                float age = (_time - Lerp(ta, tb, u)) / SmearTime;
                if (age >= 1) return;
                var hand = Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(ra, rb, u)) * Matrix4x4.CreateTranslation(Vector3.Lerp(pa, pb, u));
                // Más fina y más corta hacia la cola: se achica hacia la punta y hacia el eje de la hoja.
                var thin = Matrix4x4.CreateTranslation(-BladeTip) * Matrix4x4.CreateScale(Lerp(0.8f, 0.35f, age), Lerp(0.8f, 0.2f, age), 1) * Matrix4x4.CreateTranslation(BladeTip);
                _smear.Add(thin * hand * toChar);
            }
        }
    }

    private void Tick(Vector2 pos, float h)
    {
        _time += h;
        _hurtT += h;
        if (_rag != null) { RagdollTick(h); return; }
        // Decapitado: de rodillas un momento y después se desploma hacia adelante.
        if (_mode == Mode.Kneel && _modeT > 0.95f)
        {
            _dead = true;
            var ahead = Dir(Yaw);
            StartRagdoll(new Vector3(ahead.X, 0, ahead.Y) * 16 + new Vector3(0, 4, 0));
            RagdollTick(h);
            return;
        }
        _actT = _act == null ? 0 : MathF.Max(0, _actTime - _frameLeft);
        if (_riseT >= 0)
        {
            _riseT += h * _riseRate;
            if (_riseRate > 0 && _riseT >= RiseTime) { _down = false; _riseT = -1; }
            else if (_riseRate < 0 && _riseT <= 0) { _riseT = -1; _riseRate = 1; }
        }
        // Salto: al despegar se recuerda dónde estaba cada pie; al caer apoyan donde quedaron.
        _rollT += h;
        // La rueda: al impulsarse, los pies dejan el piso (van recogidos) y al terminar la vuelta apoyan.
        if (Rolling && !_rollAir && _rollT >= RollLift * _rollDur)
        {
            _rollAir = true;
            Leap((RollLand - RollLift) * _rollDur, 0);
        }
        bool wasAir = Airborne;
        _leapT += h;
        _sinceLand += h;
        bool air = Airborne;
        if (air && _leapT <= h) foreach (var f in _feet) { f.From = f.Pos; f.FromYaw = f.Yaw; }
        if (wasAir && !air) Land();
        float rawSpeed = _vel.Length();
        _speed = Damp(_speed, rawSpeed, 18, h);
        float s = Math.Clamp(_speed / RunSpeed, 0, 1);
        // Caminar ↔ correr sigue a la velocidad real (al arrancar se pasa a correr enseguida).
        _run = Damp(_run, Smooth(0.34f, 0.72f, MathF.Max(s, rawSpeed / RunSpeed)), 22, h);
        _moving = rawSpeed > 2.5f || (_moving && rawSpeed > 1f);
        _go = Damp(_go, _moving ? 1 : 0, 20, h);
        var moveDir = rawSpeed > 0.01f ? _vel / rawSpeed : Dir(Yaw);
        _exert = Math.Clamp(_exert + (s > 0.6f ? h * 0.25f : -h * 0.08f), 0, 1);

        // Orientación: resorte crítico con tope de velocidad de giro. Al golpear gira mucho más
        // rápido: el golpe sale hacia donde se apunta, no media vuelta después.
        float err = Wrap(_wantYaw - Yaw);
        bool acting = _act != null;
        float accel = acting ? err * 1400 - _yawVel * 75 : err * 260 - _yawVel * 30;
        float maxTurn = acting ? 40 : 14;
        _yawVel = Math.Clamp(_yawVel + accel * h, -maxTurn, maxTurn);
        float prevYaw = Yaw;
        Yaw = Wrap(Yaw + _yawVel * h);
        float yawRate = Wrap(Yaw - prevYaw) / h;

        // Inclinación: adelante al acelerar y al correr, atrás al frenar, hacia adentro al girar.
        float dSpeed = (_speed - _prevSpeed) / h;
        _prevSpeed = _speed;
        _leanFwd = Damp(_leanFwd, Math.Clamp(dSpeed * 0.06f, -6, 8), 8, h);
        _leanSide = Damp(_leanSide, Math.Clamp(yawRate * s * 7, -14, 14), 8, h);

        // Piso bajo el cuerpo (suavizado: los escalones los resuelven las piernas). Nadando, el cuerpo
        // flota: los hombros en la superficie; tirado en el agua, flota boca arriba.
        float groundHere = Ground(pos.X, pos.Y);
        bool swim = SwimLevel != null && _mode == Mode.Normal && !_down && !_dead;
        _swimW = Damp(_swimW, swim ? 1 : 0, 7, h);
        // Cuánto se inclina: más cuanto más rápido nada; y en agua no tan honda, lo que haga falta para
        // que los hombros queden en la superficie sin que el cuerpo toque el fondo (flota más tendido).
        float tiltGoal = Lerp(SwimTiltIdle, SwimTiltMove, Smooth(0.04f, 0.3f, s));
        if (SwimLevel is { } sl)
        {
            float room = (sl - groundHere - 0.6f * _k) / (_s.PelvisY + _shoulderY);
            tiltGoal = MathF.Max(tiltGoal, MathF.Acos(Math.Clamp(room, 0.2f, 1)));
        }
        tiltGoal = Lerp(tiltGoal, MathF.Max(tiltGoal, 1.35f), Math.Clamp(SwimDrag * 3 + SwimStruggle, 0, 1));
        _swimTilt = Damp(_swimTilt, tiltGoal, 6, h);
        if (swim) _swimPh = (_swimPh + h * (Lerp(0.42f, 0.9f, Smooth(0.04f, 0.3f, s)) + SwimStruggle * 1.1f)) % 1f;
        float rootY = groundHere;
        if (SwimLevel is { } lvl && !_dead) rootY = _down ? lvl - 0.6f * _k : MathF.Max(groundHere - 0.4f * _k, SwimRootY(lvl) - SwimDrag * 2.8f * _k);
        Root = new Vector3(pos.X, Root.Y + (rootY - Root.Y) * (1 - MathF.Exp(-14 * h)), pos.Y);
        // Al salir del agua, los pies apoyan donde quedaron (como al caer de un salto).
        if (_wasSwim && !swim) Land();
        _wasSwim = swim;

        float stepLen = StepLength(_speed, _legLen);
        float duty = Duty;
        // Hasta dónde puede quedar atrás un pie apoyado antes de dar el paso, y dónde aterriza adelante.
        // Nunca más atrás de lo que la pierna alcanza cómoda (si no, se estira y el pie se despega).
        float lagMax = MathF.Min(duty * stepLen, 0.72f * _legLen);
        float land = lagMax * 0.95f;
        // Adelante, con peso: se achica al frenar y sigue suave a la velocidad, así el pie en el
        // aire no salta cuando el cuerpo para en seco o la velocidad se da vuelta.
        _fwd = Vector2.Lerp(_fwd, _vel / MathF.Max(rawSpeed, 12), 1 - MathF.Exp(-25 * h));
        var fwd = _fwd;
        // El vuelo se mide con la velocidad real (la suavizada tarda en subir al arrancar y el pie llegaría tarde).
        float swingDur = Math.Clamp((1 - duty) * 2 * stepLen / MathF.Max(MathF.Max(_speed, rawSpeed), 6), 0.12f, 0.42f);
        _stillTime = _moving ? 0 : _stillTime + h;
        _sinceLift += h;

        // ---- ¿quién da el próximo paso? (nadando, ninguno: los pies van con la brazada y quedan
        // listos para apoyar donde estén al salir)
        int lift = -1;
        bool anySwing = _feet[0].Swing || _feet[1].Swing;
        if (swim)
        {
            for (int k = 0; k < 2; k++)
            {
                var f = _feet[k];
                f.Swing = true;
                f.T = 0.5f;
                f.Pos = f.From = RestFoot(k, pos, Yaw, groundHere);
                f.Yaw = f.FromYaw = Yaw;
            }
        }
        else if (_moving && !air)
        {
            float best = float.NegativeInfinity;
            for (int k = 0; k < 2; k++)
            {
                var f = _feet[k];
                if (f.Swing) continue;
                var other = _feet[1 - k];
                var fp = new Vector2(f.Plant.X, f.Plant.Z);
                float lag = -Vector2.Dot(fp - pos, moveDir);
                // Si la pierna ya no llega (el cuerpo se alejó o dio media vuelta), el paso es urgente.
                var hip = pos + RightOf(Yaw) * Side(k) * _s.D.HipW;
                bool urgent = Vector2.Distance(fp, hip) > _legLen * 0.8f || lag > lagMax * 1.6f;
                // Si el cuerpo giró sobre él y quedó del otro lado, también le toca. En una media
                // vuelta se mira un poco hacia adelante con el giro que trae: con las piernas largas
                // (pasos largos y caderas angostas) el pie apoyado se cruza en un par de cuadros.
                bool reversing = MathF.Abs(Wrap(_wantYaw - Yaw)) > 1.9f;
                float ahead = Yaw + Math.Clamp(_yawVel, -14, 14) * 0.09f;
                bool crossed = Vector2.Dot(fp - pos, RightOf(Yaw) * Side(k)) < -_s.D.HipW * 0.5f
                               || (reversing && _time - f.PlantedAt > 0.06f && Vector2.Dot(fp - pos, RightOf(ahead) * Side(k)) < -_s.D.HipW * 0.2f);
                // Primer paso al arrancar: sale enseguida el pie de atrás.
                bool first = _sinceLift > 0.45f && !anySwing;
                bool due = lag >= lagMax || urgent || crossed || (first && lag > -0.5f);
                if (!due) continue;
                // Siempre alternando (salvo urgencia) y, caminando, nunca los dos pies en el aire.
                bool turn = _lastLift != k || urgent || _sinceLift > 0.6f;
                // Un vuelo breve está bien si el otro pie ya está por apoyar o si corre (y entonces
                // el otro apura el paso: ver más abajo).
                bool support = !other.Swing || other.T > 0.65f || _run > 0.4f || urgent || (crossed && other.T > 0.3f);
                if (!turn || !support) continue;
                float score = lag + (urgent ? 100 : 0);
                if (score > best) { best = score; lift = k; _why = urgent ? $"urgente d={Vector2.Distance(fp, hip):0.00} lag={lag:0.00}/{lagMax:0.00} T{(other.Swing ? other.T : -1):0.00}" : "marcha"; }
            }
            if (lift >= 0) Log?.Add((_time, lift, _why));
        }
        else if (!air && _stillTime > 0.08f && !anySwing && _settleCooldown <= 0)
        {
            // Parado: si un pie quedó lejos de su lugar o girado, da un paso para acomodarse (el que más lo necesita).
            float worst = 0;
            for (int k = 0; k < 2; k++)
            {
                var f = _feet[k];
                var rest = RestFoot(k, pos, Yaw, groundHere);
                float need = Vector2.Distance(new Vector2(f.Plant.X, f.Plant.Z), new Vector2(rest.X, rest.Z)) / _k + MathF.Abs(Wrap(f.PlantYaw - Yaw)) * 2;
                bool off = need > 1.1f || MathF.Abs(Wrap(f.PlantYaw - Yaw)) > 0.55f;
                if (off && need > worst) { worst = need; lift = k; }
            }
            if (lift >= 0)
            {
                _settleCooldown = 0.12f;
                Log?.Add((_time, lift, "acomodo"));
            }
        }
        _settleCooldown -= h;
        if (lift >= 0)
        {
            var f = _feet[lift];
            f.Swing = true;
            f.From = f.Plant;
            f.FromYaw = f.PlantYaw;
            f.T = 0;
            f.Height = (_moving ? Lerp(1.1f, 3.2f, _run) : 0.9f) * _k;
            f.Deadline = float.PositiveInfinity;
            _lastLift = lift;
            _sinceLift = 0;
            // Si el otro pie sigue en el aire, apoya enseguida: el vuelo con los dos pies arriba es
            // breve y los pasos vuelven solos a alternarse (si no, al arrancar o después de una
            // frenada los dos pies van casi juntos, como saltando, y el cuerpo parece flotar).
            var other = _feet[1 - lift];
            if (other.Swing) other.Deadline = MathF.Min(other.Deadline, _time + Lerp(0.06f, 0.1f, _run));
        }

        // ---- pies en el aire y apoyados
        for (int k = 0; k < 2 && !swim; k++)
        {
            var f = _feet[k];
            var other = _feet[1 - k];
            if (air) { AirFoot(f, k, pos); continue; }
            if (f.Swing)
            {
                // El vuelo dura lo que marca la velocidad actual (parado, un paso corto), o menos si
                // tiene que apoyar antes.
                float dur = Lerp(0.2f, swingDur, _go);
                float until = f.Deadline - _time;
                f.T = MathF.Min(1, f.T + h * MathF.Max(1 / dur, (1 - f.T) / MathF.Max(until, h)));
                // Andando aterriza donde va a estar el cuerpo cuando toque el piso, medio paso
                // adelante y a su lado de las caderas (corriendo, más abierto); parado, en su lugar.
                var rest = RestFoot(k, pos, Yaw, groundHere);
                var dest = new Vector2(rest.X, rest.Z);
                if (_go > 0.001f)
                {
                    var body = pos + _vel * (dur * (1 - f.T));
                    var p2 = body + fwd * land + RightOf(Yaw) * Side(k) * _s.D.HipW * Lerp(1.05f, 1.3f, _run);
                    dest = Vector2.Lerp(dest, p2, _go);
                }
                // Nunca cruzado sobre el otro pie.
                dest = Uncross(k, dest, other, pos, 1);
                var to = new Vector3(dest.X, Ground(dest.X, dest.Y) + _s.AnkleY, dest.Y);

                // Mitad lineal, mitad suave: el pie acompaña al cuerpo sin quedarse atrás al despegar.
                float e = Lerp(f.T, Smooth(0, 1, f.T), 0.5f);
                var flat = Vector3.Lerp(f.From, to, e);
                // En los giros la recta entre el despegue y el aterrizaje corta por delante del otro
                // pie: el pie en el aire lo rodea por su lado (entra de a poco, por si despegó cruzado).
                var around = Uncross(k, new Vector2(flat.X, flat.Z), other, pos, Smooth(0, 0.3f, f.T));
                flat = new Vector3(around.X, flat.Y, around.Y);
                // Corriendo, el talón sube hacia atrás al principio del vuelo.
                var kick = new Vector3(-fwd.X, 0, -fwd.Y) * (_run * 2.1f * _k * MathF.Sin(MathF.PI * MathF.Min(1, f.T * 1.7f)) * (1 - f.T));
                float arc = f.Height * MathF.Pow(MathF.Sin(MathF.PI * f.T), 0.8f);
                // Si el piso de llegada es más alto, el pie sube antes (no se engancha en el escalón).
                float climb = MathF.Max(0, to.Y - f.From.Y) * Smooth(0, 0.5f, f.T) * (1 - e);
                f.Pos = flat + kick + new Vector3(0, arc + climb, 0);
                f.Yaw = f.FromYaw + Wrap(Yaw - f.FromYaw) * e;
                f.Pitch = Lerp(-10 - 22 * _run, 10, Smooth(0.15f, 0.85f, f.T));
                if (f.T >= 1)
                {
                    Log?.Add((_time, k, "apoya"));
                    f.Swing = false;
                    f.Deadline = float.PositiveInfinity;
                    f.PlantedAt = _time;
                    f.Plant = to;
                    f.PlantYaw = Yaw;
                    f.Pos = to;
                    f.Yaw = Yaw;
                    f.Pitch = 0;
                }
            }
            else
            {
                f.Pos = f.Plant;
                f.Yaw = f.PlantYaw;
                // Final del apoyo: se despega el talón (el tobillo sube con la punta apoyada).
                float lag = -Vector2.Dot(new Vector2(f.Plant.X, f.Plant.Z) - pos, fwd);
                f.Pitch = -(30 * _run + 12 * (1 - _run)) * Smooth(0.55f, 1, (lag + land) / (lagMax + land)) * _go;
            }
        }

        // ---- fase de la marcha (para brazos, rebote y giro de caderas), sacada del pie derecho
        {
            var f = _feet[0];
            float pr;
            if (f.Swing) pr = duty + (1 - duty) * f.T;
            else
            {
                float lag = -Vector2.Dot(new Vector2(f.Plant.X, f.Plant.Z) - pos, moveDir);
                pr = duty * Math.Clamp((lag + land) / (lagMax + land), 0, 1);
            }
            // Se sigue suave y sin retroceder de golpe (salvo al pasar de 1 a 0).
            float d = pr - _phase;
            if (d < -0.5f) d += 1;
            if (d > 0.5f) d -= 1;
            _phase = ((_phase + d * (1 - MathF.Exp(-30 * h))) % 1f + 1f) % 1f;
        }

        UpdateIdle(h, s);
        var m = _motion;
        m.Time = _time; m.Step = h; m.Phase = _phase; m.Duty = duty; m.Run = _run; m.Move = Smooth(0.02f, 0.2f, s); m.Speed = _speed;
        m.Pos = pos; m.Vel = _vel; m.Yaw = Yaw; m.YawVel = _yawVel; m.WantYaw = _wantYaw; m.Root = Root;
        m.Act = _act; m.ActT = _actT; m.HurtT = _hurtT;
        m.HurtPush = Vector3.Transform(new Vector3(_hurtPush.X, 0, _hurtPush.Y), Matrix4x4.CreateRotationY(Yaw));
        m.Airborne = air; m.AirU = air ? _leapT / _leapDur : 0; m.SinceLand = _sinceLand;
        BuildPose(pos, h, s, stepLen, duty);
        Pose.Solve(_s, Bones);
        UpdateSprings(h);
        Mannerism?.Secondary(this, Pose, m, false);
        Pose.Solve(_s, Bones);
        if (_reanimT >= 0) FromCorpse(h);
        PlaceNock(h);
        RecordTrail();
        RecordWorld();
        RecordSweep();
        StepCloth(h);
    }

    // ------------------------------------------------------------------ la tela

    /// <summary>Las telas de la figura que se mueven solas (ver <see cref="ClothSim"/>); null si no tiene.</summary>
    public ClothSim[] Cloth { get; private set; }

    /// <summary>Las telas de <paramref name="fig"/>, dibujada a <paramref name="scale"/> (se simulan desde el próximo paso).</summary>
    public void UseCloth(Model.Figure fig, float scale)
    {
        Cloth = fig.Cloths.Count == 0 ? null : fig.Cloths.Select(c => new ClothSim(c, scale)).ToArray();
    }

    private readonly ClothSim.Capsule[] _clothCaps = new ClothSim.Capsule[7];

    /// <summary>Un paso de las telas con los huesos de este paso: las piernas (y, para una capa, el tronco) las empujan.</summary>
    private void StepCloth(float h)
    {
        if (Cloth == null) return;
        Matrix4x4.Invert(World, out var toChar);
        foreach (var sim in Cloth)
        {
            var d = sim.Def;
            if (_mask.Hidden[(int)d.Anchor]) continue;
            float scale = sim.Scale;
            int n = 0;
            float leg = d.LegR * scale + d.Pad;
            foreach (int side in new[] { 1, -1 })
            {
                for (int seg = 0; seg < 2; seg++)
                {
                    var a = Skeleton.Leg(side, seg);
                    var b = Skeleton.Leg(side, seg + 1);
                    if (_mask.Hidden[(int)a] || _mask.Hidden[(int)b]) continue;
                    _clothCaps[n++] = new ClothSim.Capsule(_world[(int)a].Translation, _world[(int)b].Translation, leg * (seg == 0 ? 1.1f : 0.95f));
                }
                // El pie (del tobillo a la punta): el ruedo que llega al piso lo acompaña cuando se levanta.
                var foot = Skeleton.Leg(side, 2);
                if (!_mask.Hidden[(int)foot])
                {
                    var ankle = _world[(int)foot].Translation;
                    float fs = _s.D.Shin / 3.8f;
                    var toe = Vector3.Transform(new Vector3(1.6f * fs, -0.4f * fs, 0), _world[(int)foot]);
                    _clothCaps[n++] = new ClothSim.Capsule(ankle, toe, leg * 0.8f);
                }
            }
            if (d.BodyR > 0)
            {
                var pel = _world[(int)Bone.Pelvis].Translation;
                var neck = Vector3.Transform(new Vector3(0, _s.D.ShoulderUp, 0), _world[(int)Bone.Spine]);
                _clothCaps[n++] = new ClothSim.Capsule(pel, neck, d.BodyR * scale + d.Pad);
            }
            sim.Step(_world[(int)d.Anchor], toChar, h, _clothCaps.AsSpan(0, n), Ground, SwimLevel);
        }
    }

    /// <summary>
    /// Un pie durante un salto: sale de donde estaba apoyado y se recoge bajo la cadera (el de
    /// adelante un poco adelante), subiendo con el cuerpo y un poco más (las rodillas se doblan).
    /// </summary>
    private void AirFoot(Foot f, int k, Vector2 pos)
    {
        float u = _leapT / _leapDur, s = MathF.Sin(MathF.PI * u);
        var p = pos + Dir(Yaw) * ((k == 0 ? 0.4f : -0.5f) * _k) + RightOf(Yaw) * Side(k) * _s.D.HipW * 1.15f;
        var tuck = new Vector3(p.X, Ground(p.X, p.Y) + _s.AnkleY + (_leapH + 1.4f * _k) * s, p.Y);
        f.Swing = true;
        f.T = 0.5f;
        f.Pos = Vector3.Lerp(f.From, tuck, Smooth(0, 0.3f, u));
        f.Yaw = f.FromYaw + Wrap(Yaw - f.FromYaw) * Smooth(0, 0.3f, u);
        f.Pitch = -28 * s;
    }

    /// <summary>Los dos pies vuelven a apoyar debajo del cuerpo, donde está ahora (el ahogado que termina de trepar un borde).</summary>
    public void Replant()
    {
        for (int k = 0; k < 2; k++)
        {
            var f = _feet[k];
            f.Swing = false;
            f.Plant = f.Pos = f.From = RestFoot(k, new Vector2(Root.X, Root.Z), Yaw, Root.Y);
            f.PlantYaw = f.Yaw = f.FromYaw = Yaw;
            f.PlantedAt = _time;
            f.Pitch = 0;
            f.Deadline = float.PositiveInfinity;
        }
    }

    /// <summary>Cae de un salto: los dos pies apoyan donde quedaron (el amortiguado lo hace la pose).</summary>
    private void Land()
    {
        foreach (var f in _feet)
        {
            f.Swing = false;
            f.Plant = f.Pos = new Vector3(f.Pos.X, Ground(f.Pos.X, f.Pos.Z) + _s.AnkleY, f.Pos.Z);
            f.PlantYaw = f.Yaw = f.FromYaw = Yaw;
            f.PlantedAt = _time;
            f.Pitch = 0;
            f.Deadline = float.PositiveInfinity;
        }
        _sinceLand = 0;
        _sinceLift = 0;
    }

    /// <summary>
    /// Corre de costado (visto desde las caderas: en los giros el cuerpo todavía no mira al
    /// camino) el pie <paramref name="k"/> en el aire para que quede de su lado, separado del otro
    /// pie y de la línea del cuerpo. Se mide contra dónde está el otro pie ahora, apoyado o en el
    /// aire, así no salta cuando el otro despega o apoya. Si el otro quedó cruzado (el cuerpo giró
    /// sobre él) cuenta como si estuviera en la línea del cuerpo: el que tiene que dar el paso es
    /// él, no éste irse lejos de la cadera. <paramref name="w"/> es cuánto se aplica (0..1).
    /// </summary>
    private Vector2 Uncross(int k, Vector2 p, Foot other, Vector2 pos, float w)
    {
        var right = RightOf(Yaw) * Side(k);
        float otherSide = MathF.Min(0, Vector2.Dot(new Vector2(other.Pos.X, other.Pos.Z) - pos, right));
        float need = MathF.Max(_s.D.HipW * 1.3f + otherSide, _s.D.HipW * 0.4f) - Vector2.Dot(p - pos, right);
        return need > 0 ? p + right * (need * w) : p;
    }

    // ------------------------------------------------------------------ reposo

    private void UpdateIdle(float h, float s)
    {
        float still = 1 - Smooth(0.02f, 0.18f, s);
        _shiftT -= h;
        if (_shiftT <= 0)
        {
            _shiftT = 3.5f + Rand() * 4;
            _shiftGoal = still > 0.5f ? (Rand() < 0.5f ? -1 : 1) * (0.5f + Rand() * 0.5f) : 0;
        }
        if (still < 0.5f) _shiftGoal = 0;
        _shift = Damp(_shift, _shiftGoal, 2.2f, h);

        _lookT -= h;
        if (_lookT <= 0)
        {
            _lookT = 2 + Rand() * 4;
            bool look = still > 0.5f && Rand() < 0.65f;
            _lookGoal = look ? (Rand() * 2 - 1) * 38 : 0;
            _lookPitchGoal = look ? (Rand() * 2 - 1) * 8 : 0;
        }
        if (still < 0.5f) { _lookGoal = 0; _lookPitchGoal = 0; }
        _look = Damp(_look, _lookGoal, 5, h);
        _lookPitch = Damp(_lookPitch, _lookPitchGoal, 5, h);

        _blinkT -= h;
        if (_blinkT <= 0)
        {
            _blinkLeft = 0.11f;
            _blinkT = 2.2f + Rand() * 3.5f;
            if (Rand() < 0.15f) _blinkT = 0.25f; // a veces parpadea dos veces
        }
        _blinkLeft -= h;
        EyesClosed = _blinkLeft > 0 || _dead || (_down && _riseT < 0.12f);
    }

    // ------------------------------------------------------------------ pose

    private void BuildPose(Vector2 pos, float h, float s, float stepLen, float duty)
    {
        var p = Pose;
        p.Reset();
        _smearOn = false;
        _modeT += h;
        if (_mode == Mode.Kneel) { KneelPose(p); return; }
        if (_mode == Mode.Crawl) { CrawlPose(p, h); return; }
        // El giro de todo el cuerpo sólo lo pide la rueda.
        p.Local[(int)Bone.Root] = null;
        Mannerism?.Prepare(this, _motion);
        float r = _run;
        float move = Smooth(0.02f, 0.2f, s);
        float still = 1 - move;
        float tau = MathF.Tau;
        float ph = _phase * tau;
        // Respiración: más rápida y marcada después de correr.
        float breathRate = Lerp(1.6f, 3.4f, _exert);
        float breath = 0.5f - 0.5f * MathF.Cos(_time * breathRate);

        // Cadera: agachada al correr, rebote de la marcha (camina: arriba en el apoyo; corre: abajo).
        float bob = Lerp(0.22f, -0.7f, r) * MathF.Cos(2 * tau * (_phase - duty * 0.5f)) * move * _k;
        float crouch = (-0.3f * still - Lerp(0.4f, 1.05f, r) * move) * _k;
        float idleBob = -0.3f * breath * still * _k;
        var shiftSide = _shift * 0.5f * still * _k;
        float dy = crouch + bob + idleBob - MathF.Abs(_shift) * 0.15f * still * _k;

        // Pies al espacio del personaje (sin giro ni traslación).
        var toChar = Matrix4x4.CreateRotationY(Yaw);
        var local = _local;
        for (int k = 0; k < 2; k++)
        {
            var f = _feet[k];
            var w = new Vector3(f.Pos.X - Root.X, f.Pos.Y - Root.Y, f.Pos.Z - Root.Z);
            // Talón despegado: el tobillo sube con la punta apoyada.
            if (!f.Swing && f.Pitch < 0) w.Y += 1.2f * _k * MathF.Sin(-f.Pitch * MathF.PI / 180);
            local[k] = Vector3.Transform(w, toChar);
        }

        // Que las dos piernas lleguen: la cadera baja lo necesario (escalones, zancadas largas).
        _duck = Damp(_duck, MathF.Max(Duck, Crouch), 9, h);
        var pelvis = new Vector3((0.2f * r * move - 0.55f * Hunch + 0.5f * _duck) * _k, _s.PelvisY + dy - 1.4f * Hunch * _k - 4.2f * _duck * _k, shiftSide);
        for (int k = 0; k < 2; k++)
        {
            // Sólo los pies apoyados: el que está en el aire, si no llega, estira la pierna.
            if (_feet[k].Swing) continue;
            var hip = pelvis + new Vector3(0, -_s.HipDown, Side(k) * _s.D.HipW);
            var d = local[k] - hip;
            float horiz2 = d.X * d.X + d.Z * d.Z;
            // Sin canilla apoya la rodilla: la cadera baja hasta que el muslo llegue al piso.
            float reach = (_legCut[k] == 1 ? _s.Thigh : _legLen) * 0.985f;
            float maxY = local[k].Y + MathF.Sqrt(MathF.Max(0.01f, reach * reach - horiz2)) + _s.HipDown;
            if (pelvis.Y > maxY) pelvis.Y = maxY;
        }
        // Suaviza sólo las subidas (las bajadas tienen que ser inmediatas para no estirar de más las piernas).
        if (pelvis.Y > _pelvisY) pelvis.Y = Damp(_pelvisY, pelvis.Y, 25, h);
        _pelvisY = pelvis.Y;
        // Salto: la cadera sube en arco; al caer amortigua (baja de golpe y vuelve).
        if (Airborne) pelvis.Y += _leapH * MathF.Sin(MathF.PI * _leapT / _leapDur);
        float landing = _sinceLand < 0.6f ? Smooth(0, 0.035f, _sinceLand) * MathF.Exp(-_sinceLand * 9) : 0;
        pelvis.Y -= 1.2f * _k * landing;
        p.Move[(int)Bone.Pelvis] = pelvis - new Vector3(0, _s.PelvisY, 0);

        // Giro de caderas con la marcha y contragiro del torso; la cabeza mira adelante (o alrededor).
        float twist = Lerp(5, 9, r) * MathF.Cos(ph) * move;
        float bodyTurn = Math.Clamp(_yawVel * 2.2f, -18, 18);
        p[Bone.Pelvis] = new Vector3(-_leanSide * 0.4f + _shift * 3 * still, twist + 4 * still, -(2 + 6 * r) * move);
        p[Bone.Spine] = new Vector3(-_leanSide * 0.6f, -twist * 1.8f - bodyTurn * 0.4f - 4 * still, -(3 + 11 * r) * move - _leanFwd - 3 * still + 2 * breath * still);
        p.Scale[(int)Bone.Spine] = new Vector3(1, 1 + 0.015f * (1 - breath) * still, 1 + 0.02f * (1 - breath) * still);
        p[Bone.Head] = new Vector3(_leanSide * 0.5f, twist * 0.8f + _look * still + bodyTurn * 0.9f, (5 + 9 * r) * move + _leanFwd * 0.6f + (2 - 3 * breath + _lookPitch) * still);
        // Encorvado: la espalda adelante y la cabeza levantada para mirar al frente.
        p[Bone.Pelvis] += new Vector3(0, 0, -8 * Hunch);
        p[Bone.Spine] += new Vector3(0, 0, -28 * Hunch);
        p[Bone.Head] += new Vector3(0, 0, 26 * Hunch);
        // Agachado: la espalda hacia adelante y la cabeza baja (sin dejar de mirar adelante del todo).
        p[Bone.Pelvis] += new Vector3(0, 0, -10 * _duck);
        p[Bone.Spine] += new Vector3(0, 0, -30 * _duck);
        p[Bone.Head] += new Vector3(0, 0, 14 * _duck);
        p[Bone.Spine] += new Vector3(0, 0, -12 * landing);
        Recoil(p, toChar);

        // Brazos: se balancean contra las piernas; el codo se dobla al correr.
        float swingAmp = Lerp(14, 40, r) * move;
        float elbow = Lerp(16, 78, r) * move + (12 + 5 * breath) * still;
        for (int k = 0; k < 2; k++)
        {
            int side = Side(k);
            float arm = -side * swingAmp * MathF.Cos(ph);
            float abd = Lerp(9 - breath, Lerp(9, 13, r), move) + MathF.Abs(_leanSide) * 0.3f;
            p[Skeleton.Arm(side, 0)] = new Vector3(-side * abd, 0, arm + (4 + 2 * breath) * still + 6 * move);
            p[Skeleton.Arm(side, 1)] = new Vector3(0, 0, elbow + 12 * MathF.Max(0, arm) / 40f);
            p[Skeleton.Arm(side, 2)] = new Vector3(-side * (4 + 2 * r), 0, 6 + 4 * r);
        }
        if (MainHand == WeaponFamily.Blade && !_mask.NoWeapon) BladeArm(p, pelvis, ph, r, move, still, breath, h);
        if (MainHand == WeaponFamily.Bow && !_mask.NoWeapon) BowArms(p, pelvis, h);
        if (MainHand is WeaponFamily.None or WeaponFamily.Unarmed) FistArms(p, h, move);
        if (!_mask.NoWeapon)
        {
            if (MainHand == WeaponFamily.Dagger) DaggerArms(p, pelvis, ph, r, move, still, breath);
            else if (MainHand == WeaponFamily.Staff) StaffArm(p, pelvis, ph, r, move, still, breath);
            else if (MainHand == WeaponFamily.Scepter) ScepterArm(p, pelvis, ph, r, move, still, breath);
        }
        if (CurrentGesture is GestureKind.Inspect or GestureKind.Raise) Present(p, pelvis);
        if (CurrentGesture == GestureKind.Heave) Heave(p, pelvis);
        if (CurrentGesture == GestureKind.Drink) DrinkArm(p, pelvis);
        if (CurrentGesture == GestureKind.Execute) ExecuteArms(p, pelvis);
        GesturePose(p, h);

        // Piernas por IK: rodilla hacia donde apunta el pie, pie con su giro y su inclinación.
        for (int k = 0; k < 2; k++)
        {
            int side = Side(k);
            var f = _feet[k];
            float rel = Wrap(f.Yaw - Yaw);
            var knee = new Vector3(MathF.Cos(rel), 0, MathF.Sin(rel)) + new Vector3(0, 0, side * 0.12f);
            var target = local[k];
            if (_legCut[k] == 1)
            {
                // Sin canilla: el muslo apunta derecho al punto de apoyo y ahí apoya el muñón de la rodilla.
                var hip = pelvis + new Vector3(0, -_s.HipDown, side * _s.D.HipW);
                var along = target - hip;
                along = along.LengthSquared() > 1e-4f ? Vector3.Normalize(along) : -Vector3.UnitY;
                target = hip + along * (_legLen - 0.02f);
            }
            p.Legs[Pose.SideIndex(side)] = new Ik
            {
                On = true,
                Target = target,
                Hint = knee,
                End = new Vector3(0, -rel * 180 / MathF.PI - side * 8, f.Pitch),
            };
        }
        if (_down) RisePose(p);
        else if (Tug.LengthSquared() > 1e-4f) TugPose(p, pelvis, toChar);
        if (Rolling) RollPose(p, pelvis);
        _motion.Pelvis = pelvis;
        _motion.Breath = breath;
        Mannerism?.Pose(this, p, _motion);
        if (_swimW > 0.002f && !_down) BlendSwim(p, move);

        // Las rotaciones de los resortes (Pose.Local) quedan las del paso anterior hasta que se actualicen.
    }

    // ------------------------------------------------------------------ nado

    /// <summary>Cuánto se inclina adelante nadando quieto (flota casi parado, pataleando) y avanzando (radianes).</summary>
    public const float SwimTiltIdle = 0.62f, SwimTiltMove = 1.08f;

    /// <summary>A qué altura va el origen del cuerpo para que los hombros queden en la superficie <paramref name="level"/>.</summary>
    private float SwimRootY(float level)
    {
        float bob = 0.28f * _k * MathF.Sin(_swimPh * MathF.Tau - 0.6f);
        return level - (_s.PelvisY + _shoulderY) * MathF.Cos(_swimTilt) - 0.25f * _k + bob;
    }

    /// <summary>Mezcla la pose de nado sobre la de siempre (entra y sale suave: ver <see cref="_swimW"/>).</summary>
    private void BlendSwim(Pose p, float move)
    {
        var sw = _swimPose;
        sw.Reset();
        SwimPose(sw, move);
        float w = Smooth(0, 1, _swimW);
        // Pegando en el agua: los brazos y el pecho hacen el golpe (el cuerpo se endereza un poco); las piernas siguen pataleando.
        _swimStrike = Damp(_swimStrike, _act != null ? 1 : 0, 12, _motion.Step);
        float upper = w * (1 - 0.9f * _swimStrike);
        for (int i = 0; i < (int)Bone.Count; i++)
        {
            float wi = IsUpper((Bone)i) ? upper : w;
            p.Rot[i] = Vector3.Lerp(p.Rot[i], sw.Rot[i], wi);
            p.Move[i] = Vector3.Lerp(p.Move[i], sw.Move[i], wi);
            p.Scale[i] = Vector3.Lerp(p.Scale[i], sw.Scale[i], wi);
        }
        for (int k = 0; k < 2; k++)
        {
            p.Legs[k] = BlendIk(p.Legs[k], sw.Legs[k], w);
            p.Arms[k] = BlendIk(p.Arms[k], sw.Arms[k], upper);
        }
        var q = sw.Local[(int)Bone.Root] ?? Quaternion.Identity;
        p.Local[(int)Bone.Root] = Quaternion.Slerp(p.Local[(int)Bone.Root] ?? Quaternion.Identity, q, w * (1 - 0.45f * _swimStrike));
        if (_swimStrike < 0.5f) _smearOn = false;

        static bool IsUpper(Bone b) => b is Bone.Spine or Bone.Head || Skeleton.IsArm(b);
    }

    /// <summary>Mezcla dos objetivos de IK aunque el primero esté apagado (entonces arranca del segundo: el brazo que colgaba entra a la brazada).</summary>
    private static Ik BlendIk(in Ik a, in Ik b, float w)
    {
        if (!b.On) return a;
        if (!a.On) return w > 0.35f ? b : a;
        return Ik.Lerp(a, b, w);
    }

    /// <summary>
    /// Nadando a pecho, en el espacio del personaje: el cuerpo inclinado adelante (más cuanto más
    /// rápido) con el pecho sobre el lugar y los hombros en la superficie, la espalda arqueada y la
    /// cabeza levantada mirando adelante.
    /// <list type="bullet">
    /// <item>Los brazos: estirados adelante, se abren y tiran hacia atrás, se juntan bajo el mentón y
    /// vuelven a estirarse. Quieto, las manos reman en círculos chicos adelante.</item>
    /// <item>Las piernas: estiradas atrás mientras los brazos tiran; se recogen (rodillas abajo y
    /// afuera) cuando los brazos vuelven, y patean abriéndose y juntándose. Quieto, pedalean abajo.
    /// Nunca más abajo que el fondo.</item>
    /// </list>
    /// </summary>
    private void SwimPose(Pose p, float move)
    {
        float th = _swimTilt, sn = MathF.Sin(th), cs = MathF.Cos(th);
        float py = _s.PelvisY, sy = _shoulderY, k = _k;
        float ph = _swimPh, tau = MathF.Tau;
        float arm = (_s.UpperArm + _s.Forearm) * 0.97f, leg = _legLen;
        float sw = _s.D.ShoulderW, hw = _s.D.HipW;
        p.Local[(int)Bone.Root] = Quaternion.CreateFromAxisAngle(-Vector3.UnitZ, th);
        p.Move[(int)Bone.Root] = new Vector3(-(py + 0.5f * sy) * sn, 0, 0);
        // La brazada tira de 0 a 0,45, se recoge hasta 0,6 y se estira hasta el final.
        float pull = Smooth(0.0f, 0.3f, ph) * (1 - Smooth(0.45f, 0.62f, ph));
        p[Bone.Pelvis] = new Vector3(0, 0, 3 + 3 * pull * move);
        p[Bone.Spine] = new Vector3(0, 4 * MathF.Sin(ph * tau) * (1 - move), 8 + 8 * pull * move);
        p[Bone.Head] = new Vector3(0, 0, th * 180 / MathF.PI * 0.62f - 6 * pull * move);

        // La superficie en el espacio del personaje, y los hombros y la cadera (aproximados: el cuerpo inclinado).
        float ys = (SwimLevel ?? Root.Y) - Root.Y - SwimDrag * 2.8f * k;
        p[Bone.Head] += new Vector3(SwimStruggle * 9 * MathF.Sin(ph * tau * 2), SwimStruggle * 12 * MathF.Sin(ph * tau), 0);
        float bed = Ground(Root.X, Root.Z) - Root.Y + _s.AnkleY + 0.35f * k;
        float shX = 0.5f * sy * sn;
        var pel = new Vector3(-0.5f * sy * sn, py * cs, 0);

        for (int side = 1; side >= -1; side -= 2)
        {
            int i = Pose.SideIndex(side);
            // ---- los brazos
            var ext = new Vector3(shX + 0.95f * arm, ys - 0.45f * k, side * 0.6f * k);
            var wide = new Vector3(shX + 0.55f * arm, ys - 0.5f * k, side * (sw + 0.72f * arm));
            var back = new Vector3(shX + 0.18f * arm, ys - 1.15f * k, side * (sw + 0.42f * arm));
            var tuck = new Vector3(shX + 0.32f * arm, ys - 0.95f * k, side * 0.55f * k);
            Vector3 stroke = ph < 0.28f ? Vector3.Lerp(ext, wide, Smooth(0, 0.28f, ph))
                : ph < 0.45f ? Vector3.Lerp(wide, back, Smooth(0.28f, 0.45f, ph))
                : ph < 0.6f ? Vector3.Lerp(back, tuck, Smooth(0.45f, 0.6f, ph))
                : Vector3.Lerp(tuck, ext, Smooth(0.6f, 0.95f, ph));
            float a = ph * tau;
            var scull = new Vector3(shX + 0.55f * arm + 0.45f * k * MathF.Cos(a), ys - 0.75f * k, side * (sw + 0.38f * arm + 0.55f * k * MathF.Sin(a)));
            var hand = Vector3.Lerp(scull, stroke, move);
            var fwd = Vector3.Normalize(new Vector3(1, -0.25f, side * Lerp(0.15f, 0.9f, pull * move)));
            p.Arms[i] = new Ik { On = true, Target = hand, Hint = new Vector3(-0.2f, -0.55f, side), Rot = Grip(fwd, new Vector3(0, -1, 0)) };
            p[Skeleton.Arm(side, 2)] = Vector3.Zero;

            // ---- las piernas
            var hip = pel + new Vector3(-_s.HipDown * sn, -_s.HipDown * cs, side * hw);
            float beta = (MathF.PI / 2 - th) * 0.75f;
            var along = new Vector3(-MathF.Cos(beta), -MathF.Sin(beta), 0);
            var straight = hip + along * (0.96f * leg) + new Vector3(0, 0, side * 0.3f * k);
            var drawn = hip + new Vector3(-0.42f * leg, -0.28f * leg, side * 0.42f * leg);
            var kick = hip + along * (0.72f * leg) + new Vector3(0, 0, side * 0.62f * leg);
            Vector3 frog = ph < 0.45f ? straight
                : ph < 0.7f ? Vector3.Lerp(straight, drawn, Smooth(0.45f, 0.7f, ph))
                : ph < 0.84f ? Vector3.Lerp(drawn, kick, Smooth(0.7f, 0.84f, ph))
                : Vector3.Lerp(kick, straight, Smooth(0.84f, 1, ph));
            float b = a + (side > 0 ? 0 : MathF.PI);
            var pedal = hip + new Vector3(-0.2f * leg + 0.22f * leg * MathF.Cos(b), -0.72f * leg + 0.12f * leg * MathF.Sin(b), side * 0.42f * leg);
            var foot = Vector3.Lerp(pedal, frog, move);
            foot += new Vector3(-0.6f * MathF.Sin(b * 2), 0.8f * MathF.Cos(b * 2), side * 0.5f) * (SwimStruggle * k);
            foot.Y = MathF.Max(foot.Y, bed);
            p.Legs[i] = new Ik { On = true, Target = foot, Hint = new Vector3(-0.1f, -1, side * 0.8f), End = new Vector3(0, -side * 8, Lerp(-40, -150, move)) };
        }
    }

    /// <summary>
    /// Lo arrastra una cadena enganchada en el pecho: se resiste echándose atrás (la cadera lejos
    /// de la cadena) y cada tirón lo dobla hacia ella; las dos manos agarran la cadena delante del
    /// pecho y la cabeza la mira.
    /// </summary>
    private void TugPose(Pose p, Vector3 pelvis, Matrix4x4 toChar)
    {
        float k = Math.Clamp(Tug.Length(), 0, 1);
        var d = Vector3.Transform(new Vector3(Tug.X, 0, Tug.Y), toChar);
        d = d.LengthSquared() > 1e-6f ? Vector3.Normalize(d) : Vector3.UnitX;
        float yank = Math.Clamp(TugYank, 0, 1);
        // Hacia dónde se vence: lejos de la cadena al resistir, hacia ella en el tirón.
        var v = d * (yank - 0.7f * (1 - yank)) * k;
        p[Bone.Pelvis] += new Vector3(8 * v.Z, 0, -12 * v.X);
        p[Bone.Spine] += new Vector3(18 * v.Z, 0, -32 * v.X);
        p[Bone.Head] += new Vector3(-6 * v.Z, 0, -14 * k + 10 * v.X);
        p.Move[(int)Bone.Pelvis] += (-d * (0.6f * (1 - yank)) + new Vector3(0, -0.5f, 0)) * (_k * k);
        var chest = pelvis + new Vector3(_s.D.ChestF * 0.9f, _shoulderY * 0.7f, 0) + d * (0.9f * _armK);
        for (int side = 1; side >= -1; side -= 2)
        {
            var grab = chest + d * ((side > 0 ? 0.3f : 1.3f) * _armK) + new Vector3(0, 0.25f * _armK, side * 0.35f * _armK);
            p.Arms[Pose.SideIndex(side)] = new Ik { On = true, Target = grab, Hint = new Vector3(-0.2f, -1, side * 0.9f), Rot = Grip(d, Vector3.UnitY) };
            p[Skeleton.Arm(side, 2)] = Vector3.Zero;
        }
    }

    /// <summary>
    /// La rueda, sobre la pose ya armada, en tres tiempos:
    /// <list type="bullet">
    /// <item>Se tira: baja la cadera y lleva los brazos atrás (la carga), se impulsa con las
    /// piernas y se zambulle adelante, estirado, con la cabeza arriba; las manos van al piso delante
    /// suyo y quedan apoyadas ahí.</item>
    /// <item>Rueda: mete la cabeza, se hace una bola (las rodillas al pecho, la espalda curvada,
    /// los brazos abrazando las canillas) y el cuerpo entero da la vuelta por encima del hombro
    /// alrededor del centro de la bola, que va a ras del piso, pasando por encima de las manos.</item>
    /// <item>Se para: cae agachado sobre los pies, los brazos adelante para no irse de boca, y se
    /// endereza (el amortiguado lo termina el aterrizaje).</item>
    /// </list>
    /// Las piernas y los brazos van por IK en el cuerpo sin girar y después se giran con él; las
    /// manos apoyadas, en el mundo.
    /// </summary>
    private void RollPose(Pose p, Vector3 pelvis)
    {
        float u = Math.Clamp(_rollT / _rollDur, 0, 1);
        float load = Smooth(0, 0.1f, u) * (1 - Smooth(0.1f, 0.2f, u));
        float dive = Smooth(0.08f, 0.2f, u) * (1 - Smooth(0.24f, 0.34f, u));
        float tuck = Smooth(0.2f, 0.32f, u) * (1 - Smooth(0.62f, 0.76f, u));
        float land = Smooth(0.64f, 0.76f, u) * (1 - Smooth(0.8f, 1, u));
        // La vuelta: lenta al entrar, rápida en el medio, y frena al pararse.
        float spin = Smooth(0.2f, 0.74f, u);
        var axis = Vector3.Normalize(new Vector3(0.22f, 0, 1));
        var c = new Vector3(0.6f * _k, 2.7f * _k, 0);
        var q = Quaternion.CreateFromAxisAngle(axis, -MathF.Tau * spin);
        var rot = Matrix4x4.CreateFromQuaternion(q);
        var move = c - Vector3.Transform(c, rot);
        p.Local[(int)Bone.Root] = q;
        p.Move[(int)Bone.Root] = move;
        var toRolled = rot * Matrix4x4.CreateTranslation(move);

        // ---- el cuerpo (en el marco sin girar): la cadera, la espalda, la cabeza.
        float stand = pelvis.Y;
        float py = stand - 1.1f * _k * load;
        py = Lerp(py, stand - 1.8f * _k, dive);
        py = Lerp(py, 3.1f * _k, tuck);
        py = Lerp(py, stand - 3.4f * _k, land);
        float px = pelvis.X + 1.4f * _k * dive + 0.5f * _k * tuck - 0.3f * _k * load;
        var pel = new Vector3(px, py, pelvis.Z);
        var mv = p.Move[(int)Bone.Pelvis];
        p.Move[(int)Bone.Pelvis] = new Vector3(pel.X, pel.Y - _s.PelvisY, mv.Z);
        p[Bone.Pelvis] += new Vector3(0, 0, -12 * load - 30 * dive - 22 * tuck - 18 * land);
        p[Bone.Spine] += new Vector3(4 * tuck, -8 * tuck, -18 * load - 40 * dive - 58 * tuck - 30 * land);
        p[Bone.Head] += new Vector3(-6 * tuck, 10 * tuck, 6 * load + 34 * dive - 46 * tuck + 16 * land);
        // Se estira al zambullirse y se encoge hecho bola.
        p.Scale[(int)Bone.Spine] *= new Vector3(1, 1 + 0.07f * dive - 0.07f * tuck, 1);

        // ---- las piernas: se impulsan (estiradas atrás en el vuelo), se recogen, y caen abiertas.
        for (int side = 1; side >= -1; side -= 2)
        {
            ref var leg = ref p.Legs[Pose.SideIndex(side)];
            var hip = pel + new Vector3(0, -_s.HipDown, side * _s.D.HipW);
            var push = hip + new Vector3(-2.8f * _k, -5.6f * _k, side * 0.4f * _k);
            var tucked = hip + new Vector3(0.9f * _k, -2.1f * _k, side * 0.3f * _k);
            float air = _rollAir ? dive : 0;
            var target = Vector3.Lerp(Vector3.Lerp(leg.Target, push, air), tucked, tuck);
            var hint = Vector3.Lerp(Vector3.Lerp(leg.Hint, new Vector3(1, -0.3f, side * 0.2f), air), new Vector3(1, 0.9f, side * 0.25f), tuck);
            var end = Pose.Euler(Vector3.Lerp(Vector3.Lerp(leg.End, new Vector3(0, -side * 8, 55), air), new Vector3(0, -side * 8, 50), tuck));
            leg.On = true;
            leg.Target = Vector3.Transform(target, toRolled);
            leg.Hint = Vector3.TransformNormal(hint, rot);
            leg.Rot = Quaternion.CreateFromRotationMatrix(end * rot);
        }

        // ---- las manos: atrás en la carga; al zambullirse van al piso delante suyo y quedan apoyadas
        // ahí mientras pasa por encima; después abrazan las canillas; al caer, adelante para el equilibrio.
        var world = World;
        Matrix4x4.Invert(world, out var toChar);
        if (!_handsDown && u >= 0.14f)
        {
            _handsDown = true;
            var fwd = Dir(Yaw);
            var right = RightOf(Yaw);
            for (int k = 0; k < 2; k++)
            {
                int side = Side(k);
                var at = new Vector2(Root.X, Root.Z) + fwd * (5.0f * _k) + right * (side * 1.2f * _k);
                _hands[k] = new Vector3(at.X, Ground(at.X, at.Y) + 0.45f * _k, at.Y);
            }
        }
        float planted = _handsDown ? Smooth(0.14f, 0.24f, u) * (1 - Smooth(0.36f, 0.46f, u)) : 0;
        for (int side = 1; side >= -1; side -= 2)
        {
            int k = Pose.SideIndex(side);
            ref var arm = ref p.Arms[k];
            float w = MathF.Max(MathF.Max(load, dive), MathF.Max(tuck, MathF.Max(land, planted)));
            if (w < 0.02f) continue;
            var sh = ShoulderAt(side, pel);
            var rest = arm.On ? arm.Target : sh + new Vector3(0.4f * _armK, -3.9f * _armK, side * 0.5f * _armK);
            var back = pel + new Vector3(-2.3f * _k, 1.6f * _k, side * 1.5f * _k);
            var hug = pel + new Vector3(1.9f * _k, 0.6f * _k, side * 1.05f * _k);
            var balance = pel + new Vector3(3.8f * _k, 2.7f * _k, side * 1.35f * _k);
            var goal = Vector3.Lerp(rest, back, load);
            goal = Vector3.Lerp(goal, hug, tuck);
            goal = Vector3.Lerp(goal, balance, land);
            var ahint = Vector3.Lerp(arm.On ? arm.Hint : new Vector3(-0.5f, -0.3f, side), new Vector3(-0.6f, 0.2f, side), w);
            var handRot = arm.Rot is { } hr ? Matrix4x4.CreateFromQuaternion(hr) : Pose.Euler(arm.End);
            var target = Vector3.Transform(Vector3.Lerp(rest, goal, w), toRolled);
            // Apoyadas en el piso: el punto del mundo, en el espacio del personaje ya girado.
            if (planted > 0) target = Vector3.Lerp(target, Vector3.Transform(_hands[k], toChar), planted);
            arm.On = true;
            arm.Target = target;
            arm.Hint = Vector3.TransformNormal(ahint, rot);
            arm.Rot = Quaternion.CreateFromRotationMatrix(handRot * rot);
            p[Skeleton.Arm(side, 2)] = Vector3.Zero;
        }
        _smearOn = false;
    }

    /// <summary>
    /// Brazo de la espada (el derecho, por IK). Sin golpe, el puño va a la altura de la cadera,
    /// un poco adelante y afuera, con la hoja hacia adelante y abajo, abierta para no chocar las
    /// piernas (también corriendo). Golpeando, la mano sigue el tajo (<see cref="Swings"/>) y el
    /// cuerpo lo acompaña. Cuando cambia el golpe (empieza, se encadena o se corta) la mano sale
    /// de donde estaba.
    /// </summary>
    private void BladeArm(Pose p, Vector3 pelvis, float ph, float r, float move, float still, float breath, float h)
    {
        var shoulder = pelvis + new Vector3(0, _shoulderY, _s.D.ShoulderW);
        float run = r * move;
        float swing = -Lerp(0.5f, 0.9f, r) * move * MathF.Cos(ph) * _armK;
        var pos = shoulder + new Vector3(Lerp(1.0f, 0.9f, run) * _armK + swing, (-3.5f + 0.15f * breath * still) * _armK, Lerp(0.75f, 0.9f, run) * _armK);
        var rot = Grip(Aim(Lerp(28, 38, run), Lerp(-30, -24, run)), Vector3.UnitY);
        var hint = new Vector3(-1, -0.3f, 0.6f);

        Swing sw = null;
        var act = _act != null && Swings.TryGetValue(_act.Id, out sw) ? _act : null;
        _smearOn = false;
        if (act != null)
        {
            float w = SwingPose(p, sw, act, _actT, pelvis, ref pos, ref rot, out _smearOn);
            hint = Vector3.Lerp(hint, sw.Hint, w);
        }

        // Continuidad: al cambiar de golpe (o volver a empezar el mismo) la mano parte de donde estaba.
        if (!_armInit) { _armPos = _armFromPos = pos; _armRot = _armFromRot = rot; _armInit = true; }
        if (act != _armState || (act != null && _actT < _armStateT - 1e-4f))
        {
            _armFromPos = _armPos;
            _armFromRot = _armRot;
            _armSince = 0;
            _armBlend = act != null ? MathF.Max(0.05f, act.HitStart * 0.9f) : 0.12f;
            _armState = act;
        }
        _armStateT = _actT;
        _armSince += h;
        float k = Smooth(0, _armBlend, _armSince);
        _armPos = Vector3.Lerp(_armFromPos, pos, k);
        _armRot = Quaternion.Slerp(_armFromRot, rot, k);

        // La mano nunca atraviesa el cuerpo: cerca del eje de la cadera se corre hacia afuera.
        var target = _armPos;
        var out2 = new Vector2(target.X - pelvis.X, target.Z - pelvis.Z);
        float body = (_s.D.WaistS + 0.75f * _s.D.Limb) * 1.05f;
        if (out2.Length() < body && target.Y < pelvis.Y + _shoulderY * 1.07f)
        {
            out2 = out2.LengthSquared() < 1e-4f ? Vector2.UnitY : Vector2.Normalize(out2);
            target = new Vector3(pelvis.X + out2.X * body, target.Y, pelvis.Z + out2.Y * body);
        }
        p.Arms[0] = new Ik { On = true, Target = target, Hint = hint, Rot = _armRot };
        // Con IK la mano toma la rotación pedida (en el espacio del personaje).
        p[Skeleton.Arm(1, 2)] = Vector3.Zero;
    }

    /// <summary>Una mano por IK hasta <paramref name="at"/>, con el arma apuntando a <paramref name="dir"/> y las caras hacia <paramref name="face"/>.</summary>
    private void Hold(Pose p, int side, Vector3 at, Vector3 dir, Vector3 face, Vector3 hint)
    {
        p.Arms[Pose.SideIndex(side)] = new Ik { On = true, Target = at, Hint = hint, Rot = Grip(dir, face) };
        p[Skeleton.Arm(side, 2)] = Vector3.Zero;
    }

    /// <summary>
    /// Las tres llaves de un golpe hecho a mano: de la postura a la carga hasta que empieza el golpe
    /// (<paramref name="load"/>), de la carga al golpe (sale de una vez y frena: <paramref name="strike"/>)
    /// y de vuelta a la postura al final (<paramref name="back"/>).
    /// </summary>
    private static void Keyed(float t, float hs, float he, float T, out float load, out float strike, out float back)
    {
        load = Smooth(0, MathF.Max(hs, 1e-3f), t);
        float u = Math.Clamp((t - hs) / MathF.Max(he - hs + 0.04f, 1e-3f), 0, 1);
        strike = t < hs ? 0 : 1 - MathF.Pow(1 - u, 3);
        back = Smooth(he + 0.04f, MathF.Max(T, he + 0.05f), t);
    }

    private static Vector3 Keys(Vector3 rest, Vector3 load, Vector3 hit, float l, float s, float b) =>
        Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(rest, load, l), hit, s), rest, b);

    private static Vector3 KeysDir(Vector3 rest, Vector3 load, Vector3 hit, float l, float s, float b)
    {
        var v = Keys(rest, load, hit, l, s, b);
        return v.LengthSquared() > 1e-6f ? Vector3.Normalize(v) : rest;
    }

    /// <summary>
    /// Dos dagas. En guardia baja: los puños adelante a la altura de la cintura, las hojas hacia
    /// adelante y un poco afuera (caras arriba); corriendo bajan y apuntan atrás, pegadas al cuerpo.
    /// Las estocadas (una con cada mano) cargan junto a la cadera y salen derecho al pecho del
    /// blanco con el hombro adelante; el cierre en cruz levanta las dos hojas afuera y las baja
    /// cruzándose por delante, la izquierda un instante después.
    /// </summary>
    private void DaggerArms(Pose p, Vector3 pelvis, float ph, float r, float move, float still, float breath)
    {
        float run = r * move;
        var act = _act != null && _act.Family == WeaponFamily.Dagger ? _act : null;
        float twist = 0, lean = 0, drive = 0;
        for (int side = 1; side >= -1; side -= 2)
        {
            var shoulder = pelvis + new Vector3(0, _shoulderY, side * _s.D.ShoulderW);
            float swing = -side * Lerp(0.4f, 0.9f, r) * move * MathF.Cos(ph) * _armK;
            var at = shoulder + new Vector3(Lerp(1.35f, 0.85f, run) * _armK + swing, (-3.0f - 0.35f * run + 0.12f * breath * still) * _armK, side * Lerp(0.5f, 0.75f, run) * _armK);
            var dir = Aim(side * Lerp(22, 12, run), Lerp(-6, -60, run));
            if (act != null)
            {
                float t = _actT, hs = act.HitStart, he = act.HitEnd, T = act.Duration;
                if (act.Id == ClipId.Flurry)
                {
                    float tt = t - (side > 0 ? 0 : 0.03f);
                    Keyed(tt, hs, he, T, out float l, out float st, out float b);
                    var high = shoulder + new Vector3(1.0f, -0.2f, side * 0.95f) * _armK;
                    var low = shoulder + new Vector3(2.2f, -2.9f, -side * 1.35f) * _armK;
                    var mid = (high + low) / 2 + new Vector3(0.8f * _armK, 0, 0);
                    var arc = (1 - st) * (1 - st) * high + 2 * (1 - st) * st * mid + st * st * low;
                    var p1 = Vector3.Lerp(at, high, l);
                    at = Vector3.Lerp(st > 0 ? arc : p1, at, b);
                    dir = KeysDir(dir, Aim(side * 55, 45), Aim(-side * 35, -35), l, st, b);
                    float w = st * (1 - b);
                    lean += 5 * w;
                    drive += 0.25f * w;
                }
                else if ((act.Id == ClipId.Stab1) == (side > 0))
                {
                    Keyed(t, hs, he, T, out float l, out float st, out float b);
                    var load = shoulder + new Vector3(0.1f, -3.3f, side * 0.6f) * _armK;
                    var hit = shoulder + new Vector3(2.6f, -1.3f, -side * 0.35f) * _armK;
                    at = Keys(at, load, hit, l, st, b);
                    dir = KeysDir(dir, Aim(side * 8, 10), Aim(-side * 4, -3), l, st, b);
                    float w = st * (1 - b);
                    twist += side * (26 * w - 8 * l * (1 - st));
                    lean += 7 * w;
                    drive += 0.5f * w;
                }
                else
                {
                    // La otra mano se queda en guardia, un poco atrás.
                    Keyed(t, hs, he, T, out float l, out _, out float b);
                    at += new Vector3(-0.35f * _armK * l * (1 - b), 0.2f * _armK * l * (1 - b), 0);
                }
            }
            Hold(p, side, at, dir, Vector3.UnitY, new Vector3(-1, -0.4f, side * 0.7f));
        }
        p[Bone.Pelvis] += new Vector3(0, twist * 0.45f, 0);
        p[Bone.Spine] += new Vector3(0, twist * 0.6f, -lean);
        p[Bone.Head] += new Vector3(0, -twist * 0.9f, lean * 0.6f);
        p.Move[(int)Bone.Pelvis] += new Vector3(drive, 0, 0) * _k;
        // La estela sigue a la mano derecha: la estocada de derecha y el cierre en cruz.
        _smearOn = act != null && act.Id != ClipId.Stab2 && _actT >= act.HitStart && _actT <= act.HitEnd + 0.05f;
    }

    /// <summary>
    /// Los golpes del cayado, con las dos manos: la punta larga va adelante (el puño derecho arriba
    /// de la vara, la mano izquierda más abajo) y barre baja, a la altura de las rodillas; el golpe
    /// de arriba baja la vara entera sobre el piso delante.
    /// </summary>
    private static readonly Dictionary<ClipId, Swing> StaffSwings = new()
    {
        [ClipId.StaffSweep1] = new(110, -115, Tilted(-10, false), new(0.9f, 2.6f, 0.4f), 1.7f, new(-0.5f, -0.4f, 1), 12, 0.5f),
        [ClipId.StaffSweep2] = new(-110, 115, Tilted(-6, false), new(0.9f, 2.5f, 0.2f), 1.7f, new(-0.6f, -0.6f, 0.8f), 12, 0.5f),
        [ClipId.StaffBash] = new(125, -30, Tilted(12, true), new(0.7f, 3.4f, 0.5f), 1.9f, new(0, 0.5f, 1), 22, 0.9f),
    };

    /// <summary>
    /// El cayado en la derecha, como un bastón: el puño a la altura de la cintura, adelante y
    /// afuera; la punta de abajo apoyada delante del pie y la de arriba (la curva) un poco hacia
    /// atrás. Corriendo se inclina más. Pegando (<see cref="StaffSwings"/>) la agarra también con la
    /// izquierda, más abajo, y barre con la punta larga.
    /// </summary>
    private void StaffArm(Pose p, Vector3 pelvis, float ph, float r, float move, float still, float breath)
    {
        float run = r * move;
        var shoulder = pelvis + new Vector3(0, _shoulderY, _s.D.ShoulderW);
        float swing = -Lerp(0.25f, 0.5f, r) * move * MathF.Cos(ph) * _armK;
        var at = shoulder + new Vector3(Lerp(1.35f, 1.1f, run) * _armK + swing, (Lerp(-2.95f, -3.3f, run) + 0.1f * breath * still) * _armK, Lerp(0.72f, 0.65f, run) * _armK);
        var rot = Grip(Aim(0, Lerp(103, 128, run)), -Vector3.UnitX);
        var hint = new Vector3(-1, -0.2f, 0.9f);
        var act = _act != null && _act.Family == WeaponFamily.Staff ? _act : null;
        float w = 0;
        if (act != null && StaffSwings.TryGetValue(act.Id, out var sw))
        {
            w = SwingPose(p, sw, act, _actT, pelvis, ref at, ref rot, out _smearOn, reverse: true, droop: act.Id == ClipId.StaffBash ? 0.15f : 0.5f);
            hint = Vector3.Lerp(hint, sw.Hint, w);
        }
        p.Arms[0] = new Ik { On = true, Target = at, Hint = hint, Rot = rot };
        p[Skeleton.Arm(1, 2)] = Vector3.Zero;
        if (w > 0.2f)
        {
            // La izquierda agarra la vara más abajo (el puño sobre el eje de la vara).
            var grip = at + Vector3.Transform(new Vector3((-2.7f - Fist.X) * _k, 0, 0), rot);
            p.Arms[Pose.SideIndex(-1)] = new Ik { On = true, Target = grip, Hint = new Vector3(-0.6f, -0.5f, -1), Rot = rot };
            p[Skeleton.Arm(-1, 2)] = Vector3.Zero;
        }
    }

    /// <summary>
    /// El cetro en la derecha, erguido: el puño a la altura de la cintura, adelante, y la punta
    /// (el cráneo, con el pico hacia adelante) arriba; corriendo se inclina hacia adelante. La
    /// descarga carga atrás (o cruzada, la de revés) y apunta el cráneo al blanco de una vez. En la
    /// maldición (<see cref="GestureKind.Curse"/>) lo levanta bien alto y lo baja apuntando al piso.
    /// </summary>
    private void ScepterArm(Pose p, Vector3 pelvis, float ph, float r, float move, float still, float breath)
    {
        float run = r * move;
        var shoulder = pelvis + new Vector3(0, _shoulderY, _s.D.ShoulderW);
        float swing = -Lerp(0.35f, 0.8f, r) * move * MathF.Cos(ph) * _armK;
        var at = shoulder + new Vector3(Lerp(1.2f, 1.0f, run) * _armK + swing, (Lerp(-3.0f, -3.3f, run) + 0.12f * breath * still) * _armK, Lerp(0.7f, 0.8f, run) * _armK);
        var dir = Aim(Lerp(12, 4, run), Lerp(62, 28, run));
        float twist = 0, lean = 0;
        var act = _act != null && _act.Family == WeaponFamily.Scepter ? _act : null;
        if (act != null)
        {
            Keyed(_actT, act.HitStart, act.HitEnd, act.Duration, out float l, out float st, out float b);
            bool rev = act.Id == ClipId.ScepterCast2;
            var load = shoulder + (rev ? new Vector3(0.7f, -1.9f, -1.1f) : new Vector3(0.1f, -0.6f, 0.7f)) * _armK;
            var hit = shoulder + (rev ? new Vector3(2.4f, -1.5f, 0.6f) : new Vector3(2.5f, -1.2f, 0.1f)) * _armK;
            at = Keys(at, load, hit, l, st, b);
            dir = KeysDir(dir, rev ? Aim(-60, 30) : Aim(25, 75), Aim(rev ? 12 : 0, 8), l, st, b);
            float w = st * (1 - b);
            twist = (rev ? -14 : 18) * w - (rev ? -8 : 10) * l * (1 - st);
            lean = 6 * w;
        }
        if (CurrentGesture == GestureKind.Curse)
        {
            float u = _gestureT / _gestureDur;
            var high = shoulder + new Vector3(1.0f, 1.3f, 0.2f) * _armK;
            var low = shoulder + new Vector3(2.3f, -2.2f, 0.1f) * _armK;
            float up = Smooth(0, 0.35f, u), down = Smooth(0.45f, 0.62f, u), rel = Smooth(0.8f, 1, u);
            at = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(at, high, up), low, down), at, rel);
            var g = Vector3.Lerp(Vector3.Lerp(dir, Aim(0, 88), up), Aim(0, -40), down);
            dir = Vector3.Normalize(Vector3.Lerp(Vector3.Normalize(g), dir, rel));
        }
        Hold(p, 1, at, dir, Vector3.UnitX, new Vector3(-1, -0.3f, 0.7f));
        p[Bone.Pelvis] += new Vector3(0, twist * 0.4f, 0);
        p[Bone.Spine] += new Vector3(0, twist * 0.55f, -lean);
        p[Bone.Head] += new Vector3(0, -twist * 0.8f, lean * 0.6f);
    }

    // ------------------------------------------------------------------ mostrar un arma recién tomada

    /// <summary>
    /// Mostrar el arma recién tomada (la mano del arma, encima de la postura de su familia).
    /// <list type="bullet">
    /// <item>Al mirarla (<see cref="GestureKind.Inspect"/>) la trae adelante del pecho, con la hoja
    /// hacia arriba y de cara, y vuelve.</item>
    /// <item>Al alzarla (<see cref="GestureKind.Raise"/>), del pecho la sube bien alta sobre la cabeza
    /// con el brazo estirado y la punta al cielo, y la otra mano se abre hacia el costado. La tiene
    /// ahí arriba (en <see cref="RaiseApex"/> llega) y al final la baja.</item>
    /// </list>
    /// El cuerpo (mirarla, arquearse, las puntas de pie) va en <see cref="GesturePose"/>.
    /// </summary>
    private void Present(Pose p, Vector3 pelvis)
    {
        float u = Math.Clamp(_gestureT / _gestureDur, 0, 1);
        bool raise = _gesture == GestureKind.Raise;
        var shR = pelvis + new Vector3(0, _shoulderY, _s.D.ShoulderW);
        // De dónde parte la mano: lo que pidió la postura del arma (o el brazo colgando).
        var rest = p.Arms[0].On ? p.Arms[0].Target : shR + new Vector3(0.3f, -3.1f, 0.25f) * _armK;
        var restRot = p.Arms[0].On && p.Arms[0].Rot is { } rr ? rr : Grip(Aim(0, -70), Vector3.UnitX);
        var chest = shR + new Vector3(1.7f, -1.3f, -0.8f) * _armK;
        var high = shR + new Vector3(0.35f, 3.7f, 0.05f) * _armK;
        float toChest = Smooth(0, raise ? 0.28f : 0.35f, u);
        float up = raise ? Smooth(0.3f, RaiseApex, u) : 0;
        float back = Smooth(raise ? 0.86f : 0.72f, 1, u);
        var at = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(rest, chest, toChest), high, up), rest, back);
        var rot = Quaternion.Slerp(Quaternion.Slerp(Quaternion.Slerp(restRot, Grip(Aim(0, 62), Vector3.UnitX), toChest), Grip(Aim(0, 89), Vector3.UnitX), up), restRot, back);
        var hint = Vector3.Lerp(new Vector3(-0.2f, -1, 0.8f), new Vector3(0.2f, 0, 1), up);
        p.Arms[0] = new Ik { On = true, Target = at, Hint = hint, Rot = rot };
        p[Skeleton.Arm(1, 2)] = Vector3.Zero;
        if (!raise) return;
        // La otra mano, abierta hacia el costado y arriba (suelta lo que agarraba: la vara del cayado).
        float open = up * (1 - back);
        if (open > 0.3f) p.Arms[1].On = false;
        var lSh = Skeleton.Arm(-1, 0);
        var lEl = Skeleton.Arm(-1, 1);
        p[lSh] = Vector3.Lerp(p[lSh], ArmEuler(new Vector3(118, 0, 28), -1), open);
        p[lEl] = Vector3.Lerp(p[lEl], new Vector3(0, 0, 18), open);
    }

    /// <summary>En qué parte del gesto de alzar el arma llega arriba (de 0 a 1 de su duración).</summary>
    public const float RaiseApex = 0.45f;

    // ------------------------------------------------------------------ abrir un cofre

    /// <summary>En qué parte del gesto de abrir un cofre (<see cref="GestureKind.Heave"/>) salta la tapa (de 0 a 1 de su duración).</summary>
    public const float HeaveBurst = 0.66f;
    /// <summary>
    /// Dónde está el borde de la tapa de un cofre, adelante del que lo abre (en unidades del mundo,
    /// desde los pies: adelante y arriba). La escena pone al que abre a esa distancia del cofre.
    /// </summary>
    public const float LidReach = 2.6f, LidHeight = 4.4f;

    /// <summary>
    /// Abrir un cofre pesado (<see cref="GestureKind.Heave"/>): agachado, las dos manos van al borde
    /// de la tapa; hace fuerza (la tapa sube apenas y los brazos tiemblan) y en
    /// <see cref="HeaveBurst"/> la tira para arriba: las manos suben por encima de la cabeza y el cuerpo
    /// se echa atrás. Al final, los brazos bajan.
    /// </summary>
    private void Heave(Pose p, Vector3 pelvis)
    {
        float u = Math.Clamp(_gestureT / _gestureDur, 0, 1);
        float reach = Smooth(0, 0.18f, u), strain = Smooth(0.18f, HeaveBurst - 0.03f, u);
        float fling = Smooth(HeaveBurst - 0.03f, HeaveBurst + 0.07f, u), rest = Smooth(0.82f, 1, u);
        float shake = MathF.Sin(_gestureT * 60) * 0.12f * strain * (1 - fling);
        for (int k = 0; k < 2; k++)
        {
            int side = Side(k), arm = Pose.SideIndex(side);
            var sh = pelvis + new Vector3(0, _shoulderY, side * _s.D.ShoulderW);
            var hang = p.Arms[arm].On ? p.Arms[arm].Target : sh + new Vector3(0.3f, -3.1f, side * 0.25f) * _armK;
            // El borde de la tapa (sube un poco mientras hace fuerza) y arriba de la cabeza.
            var lid = new Vector3(LidReach, LidHeight + 0.8f * strain + shake, side * 1.5f);
            var high = sh + new Vector3(1.2f, 2.6f, side * 0.9f) * _armK;
            var at = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(hang, lid, reach), high, fling), hang, rest);
            p.Arms[arm] = new Ik { On = true, Target = at, Hint = new Vector3(-0.3f, -1, side * 1.1f), Rot = p.Arms[arm].Rot };
        }
    }

    // ------------------------------------------------------------------ tomar el brebaje

    /// <summary>Las partes del gesto de tomar (fracciones de su duración): la boca en los labios, los tragos, y cuando la baja.</summary>
    public const float DrinkAtLips = 0.36f, DrinkDone = 0.72f;

    /// <summary>
    /// Tomar el brebaje de Ossa (<see cref="GestureKind.Drink"/>), con la mano izquierda (la derecha sigue con el
    /// arma): la baja al cinto a buscar la calabaza, la trae adelante del pecho, se la lleva a la boca y la
    /// empina (la boca en los labios, el fondo arriba) mientras traga; después la baja de golpe y la guarda. El
    /// cuerpo (echar la cabeza atrás, los tragos, el escalofrío) va en <see cref="GesturePose"/>.
    /// </summary>
    private void DrinkArm(Pose p, Vector3 pelvis)
    {
        float u = Math.Clamp(_gestureT / _gestureDur, 0, 1);
        var shL = pelvis + new Vector3(0, _shoulderY, -_s.D.ShoulderW);
        var rest = p.Arms[1].On ? p.Arms[1].Target : shL + new Vector3(0.3f, -3.1f, -0.25f) * _armK;
        var restRot = p.Arms[1].On && p.Arms[1].Rot is { } rr ? rr : Grip(Aim(0, -70), -Vector3.UnitZ);
        var belt = shL + new Vector3(0.35f, -3.35f, 0.15f) * _armK;
        var chest = shL + new Vector3(1.55f, -1.55f, 0.75f) * _armK;
        // La boca, con la cabeza echada atrás (se corre atrás y arriba); la mano queda arriba de la cara y la
        // calabaza baja empinada hasta los labios.
        var lips = new Vector3(0.75f * _k, pelvis.Y + _shoulderY + 1.3f * _k, -0.1f * _k);
        var drink = lips + new Vector3(-0.45f, 1.45f, -0.25f) * _k;
        float toBelt = Smooth(0, 0.14f, u), toChest = Smooth(0.14f, 0.28f, u), toLips = Smooth(0.26f, DrinkAtLips, u);
        float down = Smooth(DrinkDone, 0.8f, u), stow = Smooth(0.8f, 0.94f, u), back = Smooth(0.92f, 1, u);
        // Los tragos: la mano empina un poco más en cada uno.
        float sip = toLips * (1 - down) * MathF.Max(0, MathF.Sin((u - DrinkAtLips) * MathF.PI * 3 / (DrinkDone - DrinkAtLips)));
        var at = rest;
        at = Vector3.Lerp(at, belt, toBelt);
        at = Vector3.Lerp(at, chest, toChest);
        at = Vector3.Lerp(at, drink + new Vector3(-0.1f, 0.15f, 0) * (sip * _k), toLips);
        at = Vector3.Lerp(at, chest + new Vector3(-0.2f, -0.5f, 0) * _armK, down);
        at = Vector3.Lerp(at, belt, stow);
        at = Vector3.Lerp(at, rest, back);
        // La calabaza: de pie en la mano (la boca arriba), acostada adelante del pecho, empinada en los labios.
        var upright = Grip(Aim(0, 80), -Vector3.UnitZ);
        var level = Grip(new Vector3(0.2f, 0.35f, 1), Vector3.UnitY);
        var tilted = Grip(Vector3.Normalize(new Vector3(-0.95f, -0.85f - 0.25f * sip, 0.35f)), -Vector3.UnitZ);
        var rot = Quaternion.Slerp(restRot, upright, toBelt);
        rot = Quaternion.Slerp(rot, level, toChest);
        rot = Quaternion.Slerp(rot, tilted, toLips);
        rot = Quaternion.Slerp(rot, level, down);
        rot = Quaternion.Slerp(rot, upright, stow);
        rot = Quaternion.Slerp(rot, restRot, back);
        var hint = Vector3.Lerp(new Vector3(-0.3f, -1, -0.6f), new Vector3(0.2f, -0.5f, -1.2f), toLips * (1 - down));
        p.Arms[1] = new Ik { On = true, Target = at, Hint = hint, Rot = rot };
        p[Skeleton.Arm(-1, 2)] = Vector3.Zero;
    }

    // ------------------------------------------------------------------ gestos de habilidad

    private GestureKind _gesture;
    private float _gestureT = 10, _gestureDur = 1;

    /// <summary>El gesto de habilidad en curso (ninguno si ya terminó).</summary>
    public GestureKind CurrentGesture => _gestureT < _gestureDur ? _gesture : GestureKind.None;

    /// <summary>Cuánto lleva el gesto en curso (segundos de animación: se frena con la cámara lenta y el golpe).</summary>
    public float GestureTime => _gestureT;

    /// <summary>Hace un gesto de habilidad durante <paramref name="duration"/> segundos (encima de lo que esté haciendo).</summary>
    public void Gesture(GestureKind kind, float duration)
    {
        if (_rag != null || _mode != Mode.Normal || _down) return;
        _gesture = kind;
        _gestureT = 0;
        _gestureDur = MathF.Max(0.05f, duration);
    }

    /// <summary>
    /// Los gestos de las habilidades, encima de la pose: la embestida (el hombro izquierdo adelante,
    /// agachado y empujando), el grito (el pecho afuera, la cabeza atrás y los brazos abiertos), el
    /// tiro del atado de hierbas con la izquierda (atrás y arriba, y adelante) y la maldición (el
    /// cuerpo acompaña el cetro: atrás al levantarlo, adelante al bajarlo).
    /// </summary>
    private void GesturePose(Pose p, float h)
    {
        if (_gestureT >= _gestureDur) return;
        _gestureT += h;
        float u = Math.Clamp(_gestureT / _gestureDur, 0, 1);
        float w = Smooth(0, 0.15f, u) * (1 - Smooth(0.75f, 1, u));
        var lSh = Skeleton.Arm(-1, 0);
        var lEl = Skeleton.Arm(-1, 1);
        switch (_gesture)
        {
            case GestureKind.Charge:
                p[Bone.Pelvis] += new Vector3(0, -25 * w, -10 * w);
                p[Bone.Spine] += new Vector3(0, -30 * w, -26 * w);
                p[Bone.Head] += new Vector3(0, 40 * w, 20 * w);
                p[lSh] = Vector3.Lerp(p[lSh], ArmEuler(new Vector3(10, 30, 25), -1), w);
                p[lEl] = Vector3.Lerp(p[lEl], new Vector3(0, 0, 110), w);
                p.Move[(int)Bone.Pelvis] += new Vector3(0.6f, -0.9f, 0) * (_k * w);
                break;
            case GestureKind.Roar:
                p[Bone.Spine] += new Vector3(0, 0, 14 * w);
                p[Bone.Head] += new Vector3(0, 0, 26 * w);
                foreach (int side in new[] { 1, -1 })
                {
                    p[Skeleton.Arm(side, 0)] = Vector3.Lerp(p[Skeleton.Arm(side, 0)], ArmEuler(new Vector3(58, 0, -12), side), w);
                    p[Skeleton.Arm(side, 1)] = Vector3.Lerp(p[Skeleton.Arm(side, 1)], new Vector3(0, 0, 35), w);
                }
                p.Move[(int)Bone.Pelvis] += new Vector3(-0.3f, -0.6f, 0) * (_k * w);
                break;
            case GestureKind.Throw:
            {
                float wind = Smooth(0, 0.45f, u), fling = Smooth(0.45f, 0.62f, u);
                var sh = Vector3.Lerp(Vector3.Lerp(p[lSh], ArmEuler(new Vector3(25, 0, -70), -1), wind), ArmEuler(new Vector3(15, 0, 120), -1), fling);
                p[lSh] = Vector3.Lerp(p[lSh], sh, w);
                p[lEl] = Vector3.Lerp(p[lEl], new Vector3(0, 0, Lerp(Lerp(p[lEl].Z, 100, wind), 15, fling)), w);
                p[Bone.Spine] += new Vector3(0, (18 * wind - 34 * fling) * w, -6 * fling * w);
                p[Bone.Head] += new Vector3(0, (-14 * wind + 26 * fling) * w, 0);
                break;
            }
            case GestureKind.Inspect:
            {
                // La mira: la cabeza baja hacia la mano y el torso se inclina apenas.
                float look = Smooth(0, 0.35f, u) * (1 - Smooth(0.72f, 1, u));
                p[Bone.Head] += new Vector3(0, -8 * look, -16 * look);
                p[Bone.Spine] += new Vector3(0, -6 * look, -4 * look);
                break;
            }
            case GestureKind.Raise:
            {
                // Primero la mira en el pecho; al alzarla se arquea, la mira desde abajo y sube en puntas de pie.
                float chest = Smooth(0, 0.28f, u) * (1 - Smooth(0.3f, 0.42f, u));
                float up = Smooth(0.3f, RaiseApex + 0.02f, u) * (1 - Smooth(0.86f, 1, u));
                p[Bone.Head] += new Vector3(0, -8 * chest, -14 * chest + 26 * up);
                p[Bone.Spine] += new Vector3(0, 0, 12 * up);
                p.Move[(int)Bone.Pelvis] += new Vector3(-0.15f, 0.3f, 0) * (_k * up);
                break;
            }
            case GestureKind.Heave:
            {
                // Agachado sobre la tapa, temblando de la fuerza; al abrirla, se echa atrás y mira arriba.
                float reach = Smooth(0, 0.18f, u), fling = Smooth(HeaveBurst - 0.03f, HeaveBurst + 0.07f, u), rest = Smooth(0.82f, 1, u);
                float bend = reach * (1 - fling), back = fling * (1 - rest);
                float strain = Smooth(0.18f, HeaveBurst - 0.03f, u) * (1 - fling);
                float tremble = MathF.Sin(_gestureT * 57) * strain;
                p.Move[(int)Bone.Pelvis] += new Vector3(-0.5f * bend - 0.4f * back, -2.6f * bend + 0.2f * back, 0) * _k;
                p[Bone.Pelvis] += new Vector3(0, 0, -10 * bend);
                p[Bone.Spine] += new Vector3(1.6f * tremble, 0, -26 * bend + 14 * back);
                p[Bone.Head] += new Vector3(0, 0, -6 * bend + 8 * strain + 20 * back);
                break;
            }
            case GestureKind.Drink:
            {
                // Mira la calabaza al sacarla; al tomar echa la cabeza atrás (y el pecho), traga tres veces;
                // al bajarla le corre un escalofrío (tiembla, se encoge de hombros) y agacha la cabeza.
                float look = Smooth(0.1f, 0.22f, u) * (1 - Smooth(0.26f, DrinkAtLips, u));
                float tilt = Smooth(0.28f, DrinkAtLips + 0.04f, u) * (1 - Smooth(DrinkDone, 0.8f, u));
                float gulp = tilt * MathF.Max(0, MathF.Sin((u - DrinkAtLips) * MathF.PI * 6 / (DrinkDone - DrinkAtLips)));
                float shiver = Smooth(DrinkDone, 0.78f, u) * (1 - Smooth(0.9f, 1, u));
                float bow = Smooth(0.78f, 0.9f, u) * (1 - Smooth(0.95f, 1, u));
                float tremble = MathF.Sin(_gestureT * 47) * shiver;
                p[Bone.Head] += new Vector3(1.5f * tremble, -12 * look, -16 * look + 26 * tilt - 4 * gulp - 14 * bow);
                p[Bone.Spine] += new Vector3(2.4f * tremble, -4 * look, 9 * tilt - 8 * shiver - 4 * bow);
                p.Move[(int)Bone.Pelvis] += new Vector3(-0.15f * tilt, -0.35f * shiver - 0.2f * bow, 0) * _k;
                foreach (int side in new[] { 1, -1 })
                    p[Skeleton.Arm(side, 0)] += new Vector3(-side * 6 * shiver, 0, 0);
                break;
            }
            case GestureKind.Execute:
                ExecuteBody(p, u);
                break;
            case GestureKind.Curse:
            {
                float up = Smooth(0, 0.35f, u), down = Smooth(0.45f, 0.62f, u);
                p[Bone.Spine] += new Vector3(0, 0, (8 * up - 18 * down) * w);
                p[Bone.Head] += new Vector3(0, 0, (10 * up - 14 * down) * w);
                p.Move[(int)Bone.Pelvis] += new Vector3(0.2f * down, -0.5f * down, 0) * (_k * w);
                break;
            }
        }
    }

    /// <summary>
    /// Brazos con arco (en la mano derecha). En reposo cuelga al costado con la vara casi
    /// vertical. Tensando, el cuerpo se pone de perfil (lo gira la simulación) y todo se arma sobre
    /// la línea de la flecha, que pasa por delante de la cara: el anclaje es la mejilla izquierda
    /// y el arco queda adelante sobre esa línea, con la vara vertical, el brazo derecho estirado
    /// hacia el blanco. La mano izquierda agarra la cuerda y la trae hasta la mejilla según la
    /// tensión, con el codo hacia atrás; la cabeza mira al blanco. Al soltar, la mano sigue de
    /// largo hacia atrás y después todo baja a la postura de reposo.
    /// </summary>
    private void BowArms(Pose p, Vector3 pelvis, float h)
    {
        bool aiming = _bowDrawing || _bowSince < 0.3f;
        _bowUp = Damp(_bowUp, aiming ? 1 : 0, aiming ? 20 : 7, h);
        if (_bowDrawing) _bowHeld += h;
        float up = _bowUp;
        float rel = Wrap(_bowAim - Yaw);
        var aim = new Vector3(MathF.Cos(rel), 0, MathF.Sin(rel));
        var shoulderR = pelvis + new Vector3(0, _shoulderY, _s.D.ShoulderW);
        var shoulderL = pelvis + new Vector3(0, _shoulderY, -_s.D.ShoulderW);

        var holdPos = shoulderR + new Vector3(0.6f, -3.6f, 0.75f) * _armK;
        var holdRot = Grip(new Vector3(0.25f, -1, 0.1f), Vector3.UnitZ);
        var stave = Vector3.Normalize(Vector3.UnitY + aim * 0.1f);
        var bowRot = Grip(stave, -Vector3.Normalize(Vector3.Cross(stave, aim)));
        // La línea de la flecha: del anclaje (junto a la mandíbula, delante y arriba del hombro que
        // tensa) hacia el blanco; el puño del arco queda sobre ella, a un largo de flecha menos la punta.
        var anchor = shoulderL + aim * (0.45f * _armK) + new Vector3(0.6f, 0.85f, 0) * _armK;
        var grip = anchor + aim * (7.4f * _armK);
        var bowPos = grip - Vector3.Transform(Fist, bowRot);
        var pos = Vector3.Lerp(holdPos, bowPos, up);
        var rot = Quaternion.Slerp(holdRot, bowRot, up);
        // El codo del arco apenas flexionado, hacia afuera y abajo.
        p.Arms[0] = new Ik { On = true, Target = pos, Hint = Vector3.Lerp(new Vector3(-0.4f, -1, 0.6f), new Vector3(0.2f, -1, 0) - aim * 0.3f, up), Rot = rot };
        p[Skeleton.Arm(1, 2)] = Vector3.Zero;

        // La mano de la cuerda: agarra en la muesca y la trae hasta el anclaje según la tensión;
        // al soltar sigue de largo hacia atrás. El codo apunta hacia atrás (lejos del blanco).
        if (up > 0.01f)
        {
            var nock = pos + Vector3.Transform(StringRest, rot);
            float draw = _bowDrawing ? Smooth(0, 1, _bowDraw) : _bowDraw;
            var fist = Vector3.Lerp(nock, anchor, _bowDrawing ? 0.06f + 0.94f * draw : draw);
            if (!_bowDrawing) fist -= aim * (1.4f * _armK * Smooth(0, 0.12f, _bowSince));
            // Los dedos enganchan la cuerda hacia el blanco, la muñeca atrás (como la mano del arco, con el pulgar arriba).
            var drawRot = Grip(Vector3.UnitY, -Vector3.Cross(Vector3.UnitY, aim));
            var hand = fist - Vector3.Transform(Fist, drawRot);
            var rest = shoulderL + new Vector3(0.3f, -3.9f, -0.4f) * _armK;
            // El codo va atrás, en la línea de la flecha y apenas más alto que el hombro.
            var elbow = shoulderL - aim * (2.6f * _armK) + new Vector3(0.1f, 0.9f, 0) * _armK;
            var target = Vector3.Lerp(rest, hand, up);
            var hint = Vector3.Lerp(new Vector3(-1, -0.3f, -0.7f), elbow - (shoulderL + target) * 0.5f, up);
            p.Arms[1] = new Ik { On = true, Target = target, Hint = hint, Rot = drawRot };
            p[Skeleton.Arm(-1, 2)] = Vector3.Zero;
        }
        float deg = rel * 180 / MathF.PI * up;
        // El torso queda de perfil (los hombros en la línea de la flecha); sólo la cabeza mira al blanco.
        p[Bone.Spine] += new Vector3(0, -deg * 0.04f, 0);
        p[Bone.Head] += new Vector3(0, -deg * 0.85f, -4 * up);
    }

    /// <summary>
    /// Ubica la muesca del arco: en reposo sobre la cuerda recta (y al soltar vibra un instante);
    /// tensando, en el puño de la mano de la cuerda, con su eje X apuntando a la empuñadura (por
    /// donde sale la flecha).
    /// </summary>
    private void PlaceNock(float h)
    {
        var hand = Bones[(int)Bone.HandR];
        var rest = Matrix4x4.CreateTranslation(StringRest) * hand;
        // (Rematando a quemarropa, la flecha también va en la cuerda: ver Animator.Execute.cs.)
        bool exec = ExecNocked(out _);
        if (MainHand != WeaponFamily.Bow || _mask.NoWeapon || !(_bowDrawing || exec))
        {
            if (MainHand == WeaponFamily.Bow && _bowSince < 0.25f)
            {
                // La cuerda vibra al soltar.
                var back = Vector3.Normalize(new Vector3(rest.M21, rest.M22, rest.M23));
                rest.Translation += back * (0.5f * _armK * MathF.Sin(_bowSince * 90) * MathF.Exp(-_bowSince * 18));
            }
            Bones[(int)Bone.Nock] = rest;
            return;
        }
        var fist = Vector3.Transform(Fist, Bones[(int)Bone.HandL]);
        float grab = exec ? 1 : Smooth(0, 0.1f, _bowHeld);
        var at = Vector3.Lerp(rest.Translation, fist, grab);
        var grip = Vector3.Transform(Fist, hand);
        var x = grip - at;
        x = x.LengthSquared() > 1e-4f ? Vector3.Normalize(x) : Vector3.UnitX;
        var z = Vector3.Cross(x, Vector3.UnitY);
        z = z.LengthSquared() > 1e-4f ? Vector3.Normalize(z) : Vector3.UnitZ;
        var y = Vector3.Cross(z, x);
        Bones[(int)Bone.Nock] = new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, at.X, at.Y, at.Z, 1);
    }

    /// <summary>
    /// Un tajo: la mano recorre un arco alrededor del pecho, en el plano de adelante (U) y de
    /// <see cref="Side"/> (V), y la hoja apunta hacia afuera del arco con el filo por delante.
    /// Ángulos en grados desde adelante hacia V: dónde se carga y hasta dónde sigue de largo.
    /// <see cref="Center"/> es relativo a la cadera; <see cref="Lean"/> y <see cref="Crouch"/>,
    /// cuánto se inclina y baja el cuerpo al pegar.
    /// </summary>
    private sealed record Swing(float Windup, float Follow, Vector3 Side, Vector3 Center, float Radius, Vector3 Hint, float Lean, float Crouch);

    /// <summary>V de un tajo: la derecha levantada <paramref name="deg"/> grados, o (vertical) arriba corrido hacia la derecha.</summary>
    private static Vector3 Tilted(float deg, bool vertical)
    {
        float t = deg * MathF.PI / 180;
        return vertical ? new Vector3(0, MathF.Cos(t), MathF.Sin(t)) : new Vector3(0, MathF.Sin(t), MathF.Cos(t));
    }

    /// <summary>
    /// Los tajos de las armas de hoja (los comparten todas las espadas): de derecha a izquierda
    /// bajando, de revés subiendo y, para cerrar el combo, con la espada levantada de arriba
    /// hacia abajo. Los arcos pasan por delante del cuerpo.
    /// </summary>
    private static readonly Dictionary<ClipId, Swing> Swings = new()
    {
        [ClipId.Slash1] = new(120, -100, Tilted(40, false), new(1.2f, 2.9f, 0.8f), 2.6f, new(-0.5f, -0.4f, 1), 12, 0.35f),
        [ClipId.Slash2] = new(-115, 105, Tilted(32, false), new(1.1f, 2.7f, 0.7f), 2.6f, new(-0.6f, -0.6f, 0.8f), 12, 0.35f),
        [ClipId.Slash3] = new(115, -60, Tilted(20, true), new(0.8f, 3.2f, 1.0f), 2.8f, new(0, 0.5f, 1), 20, 0.8f),
        // El goblin: un hachazo en diagonal de arriba hacia abajo.
        [ClipId.GoblinChop] = new(125, -70, Tilted(35, true), new(0.9f, 3.0f, 1.0f), 2.6f, new(0, 0.5f, 1), 18, 0.7f),
        // El familiar: un tajo bajo, casi horizontal, de derecha a izquierda con el brazo entero.
        [ClipId.FamiliarSlash] = new(118, -98, Tilted(10, false), new(1.3f, 2.1f, 0.7f), 3.0f, new(-0.5f, -0.6f, 1), 12, 0.45f),
    };

    /// <summary>
    /// Un puñetazo: con qué brazo (0 derecho, 1 izquierdo), el hombro y el codo al cargar y al
    /// pegar, cuánto gira el torso en cada momento (grados; positivo lleva adelante el hombro del
    /// que pega), cuánto se inclina y cuánto avanza la cadera al pegar. El hombro va "hacia afuera,
    /// hacia adentro, adelante": vale para los dos lados (ver <see cref="ArmEuler"/>).
    /// </summary>
    private sealed record Punch(int Arm, Vector3 Load, float LoadElbow, Vector3 Hit, float HitElbow, float LoadTwist, float HitTwist, float Lean, float Drive);

    /// <summary>
    /// El combo sin arma: jab de izquierda (derecho al frente, el brazo entero), cross de derecha
    /// (con toda la cadera) y gancho de izquierda: el codo afuera a la altura del hombro y el brazo
    /// barre hacia adentro con el giro del cuerpo.
    /// </summary>
    private static readonly Dictionary<ClipId, Punch> Punches = new()
    {
        [ClipId.Punch1] = new(1, new(16, 10, 42), 118, new(6, 9, 86), 4, -6, 18, 4, 0.3f),
        [ClipId.Punch2] = new(0, new(18, 14, 30), 132, new(4, 13, 88), 3, -10, 30, 7, 0.55f),
        [ClipId.Hook] = new(1, new(84, -20, 8), 100, new(84, 76, 8), 88, -22, 40, 3, 0.45f),
    };

    /// <summary>La guardia (hombro como en <see cref="Punch"/> y codo): la izquierda adelante, la derecha cerca de la cara.</summary>
    private static readonly (Vector3 shoulder, float elbow)[] Guard = { (new(16, 16, 38), 126), (new(14, 12, 50), 106) };

    /// <summary>Hombro "hacia afuera, hacia adentro, adelante" a los grados de la pose para el lado <paramref name="side"/>.</summary>
    private static Vector3 ArmEuler(Vector3 n, int side) => new(-side * n.X, side * n.Y, n.Z);

    /// <summary>
    /// Brazos sin arma. Después de pegar queda un rato en guardia (los puños a la altura de la
    /// cara; corriendo, más abajo) y se relaja de a poco. Cada puñetazo carga apenas, sale de una
    /// vez hasta estirar el brazo (con el giro del torso y la cadera que avanza) y vuelve a la
    /// guardia; cada brazo lleva su propio reloj, así el jab termina de volver mientras sale el cross.
    /// </summary>
    private void FistArms(Pose p, float h, float move)
    {
        var now = _act != null && Punches.TryGetValue(_act.Id, out var pn) ? pn : null;
        if (now != null && (_act != _fistAct || _actT < _fistActT - 1e-4f))
        {
            _fistClip[now.Arm] = _act;
            _fistT[now.Arm] = _actT;
        }
        _fistAct = _act;
        _fistActT = _actT;
        for (int k = 0; k < 2; k++) _fistT[k] = now != null && now.Arm == k && _fistClip[k] == _act ? _actT : _fistT[k] + h;
        _sinceFist = now != null ? 0 : _sinceFist + h;
        bool fighting = _sinceFist < 1.3f;
        _guard = Damp(_guard, fighting ? 1 - 0.6f * move * (now == null ? 1 : 0) : 0, fighting ? 22 : 3.5f, h);

        float twist = 0, lean = 0, drive = 0;
        for (int k = 0; k < 2; k++)
        {
            int side = Side(k);
            var sh = Skeleton.Arm(side, 0);
            var el = Skeleton.Arm(side, 1);
            var baseSh = Vector3.Lerp(p[sh], ArmEuler(Guard[k].shoulder, side), _guard);
            float baseEl = Lerp(p[el].Z, Guard[k].elbow, _guard);
            var outSh = baseSh;
            float outEl = baseEl;
            var c = _fistClip[k];
            if (c != null && _fistT[k] < c.Duration && Punches.TryGetValue(c.Id, out var pu))
            {
                float t = _fistT[k], hs = c.HitStart, he = c.HitEnd, T = c.Duration;
                var load = ArmEuler(pu.Load, side);
                var hit = ArmEuler(pu.Hit, side);
                float tw, strike;
                if (t < hs)
                {
                    float u = Smooth(0, hs, t);
                    outSh = Vector3.Lerp(baseSh, load, u);
                    outEl = Lerp(baseEl, pu.LoadElbow, u);
                    tw = pu.LoadTwist * u;
                    strike = 0;
                }
                else if (t < he)
                {
                    // Sale de una vez y frena al estirarse.
                    float u = 1 - MathF.Pow(1 - (t - hs) / (he - hs), 3);
                    outSh = Vector3.Lerp(load, hit, u);
                    outEl = Lerp(pu.LoadElbow, pu.HitElbow, u);
                    tw = Lerp(pu.LoadTwist, pu.HitTwist, u);
                    strike = u;
                }
                else
                {
                    // Un instante estirado y vuelve rápido a la guardia (si no, con el golpe siguiente
                    // quedan los dos brazos estirados).
                    float u = Smooth(he + 0.02f, MathF.Min(T, he + 0.15f), t);
                    outSh = Vector3.Lerp(hit, baseSh, u);
                    outEl = Lerp(pu.HitElbow, baseEl, u);
                    tw = pu.HitTwist * (1 - u);
                    strike = 1 - u;
                }
                twist += side * tw;
                lean += pu.Lean * strike;
                drive += pu.Drive * strike;
            }
            p[sh] = outSh;
            p[el] = new Vector3(0, 0, outEl);
        }
        // El torso gira con el golpe (la cabeza sigue mirando adelante); en guardia, un poco agachado.
        p[Bone.Pelvis] += new Vector3(0, twist * 0.45f, 0);
        p[Bone.Spine] += new Vector3(0, twist * 0.6f, -lean - 4 * _guard);
        p[Bone.Head] += new Vector3(0, -twist * 0.9f, lean * 0.6f + 3 * _guard);
        p.Move[(int)Bone.Pelvis] += new Vector3(drive, -0.3f * _guard, 0) * _k;
    }

    /// <summary>
    /// Una pose clave de levantarse del piso, a los <see cref="T"/> segundos (medidas humanas,
    /// espacio del personaje): dónde está la cadera y cómo gira, columna y cabeza, los brazos
    /// (hombro y codo, grados de la pose) y los pies (tobillo, hacia dónde va la rodilla e
    /// inclinación del pie).
    /// </summary>
    private sealed record RiseKey(float T, Vector3 Pelvis, Vector3 PelvisRot, Vector3 Spine, Vector3 Head,
        Vector3 ArmR, float ElbowR, Vector3 ArmL, float ElbowL,
        Vector3 FootR, Vector3 KneeR, float PitchR, Vector3 FootL, Vector3 KneeL, float PitchL);

    /// <summary>
    /// Levantarse: boca arriba con la cara de costado; levanta la cabeza y encoge una pierna; se
    /// sienta apoyado en las manos; rueda sobre un pie y la otra rodilla; empuja con la mano en la
    /// rodilla y trae el pie de atrás; y se para (la última clave se funde con la postura de siempre).
    /// </summary>
    private static readonly RiseKey[] RiseKeys =
    {
        new(0, new(-4.2f, 1.05f, 0), new(0, 0, 86), new(0, 0, 2), new(0, 24, -6),
            new(-18, 0, -8), 6, new(22, 0, -8), 10,
            new(3.4f, 0.55f, 1.25f), new(0.1f, 1, 0), 70, new(3.2f, 0.55f, -1.35f), new(0.1f, 1, 0), 70),
        // Se lleva la mano a la frente y levanta la cabeza, encogiendo una pierna.
        new(0.5f, new(-4.2f, 1.05f, 0), new(0, 0, 84), new(0, 0, -6), new(0, 10, -34),
            new(-30, 0, 75), 125, new(18, 0, -6), 12,
            new(0.6f, 0.6f, 1.3f), new(0.3f, 1, 0.2f), 15, new(3.2f, 0.55f, -1.35f), new(0.1f, 1, 0), 70),
        // Mareado: la cabeza gira hacia el otro lado.
        new(0.85f, new(-4.2f, 1.05f, 0), new(0, 0, 82), new(0, 0, -8), new(0, -14, -38),
            new(-30, 0, 72), 128, new(18, 0, -2), 16,
            new(0.7f, 0.6f, 1.3f), new(0.3f, 1, 0.2f), 15, new(2.6f, 0.55f, -1.35f), new(0.2f, 1, 0), 60),
        // Sentado, apoyado en las manos.
        new(1.3f, new(-3.9f, 1.35f, 0), new(0, 0, 30), new(0, 0, -18), new(0, -4, 8),
            new(-18, 0, -20), 12, new(18, 0, -20), 12,
            new(0.4f, 0.6f, 1.4f), new(0.5f, 1, 0.3f), 10, new(-0.2f, 0.6f, -1.5f), new(0.5f, 1, -0.3f), 10),
        // Sobre el pie derecho y la rodilla izquierda, la mano en la rodilla.
        new(1.85f, new(-1.4f, 3.6f, 0.3f), new(0, -10, -8), new(0, 6, -22), new(0, 0, 24),
            new(-10, 0, 40), 30, new(14, 0, 25), 10,
            new(1.4f, 0.7f, 1.3f), new(1, 0.6f, 0.2f), 0, new(-4.6f, 0.6f, -1.1f), new(1, -0.6f, 0), -60),
        // Empuja para arriba y trae el pie de atrás.
        new(2.4f, new(-0.5f, 6.9f, 0.15f), new(0, -4, -12), new(0, 3, -16), new(0, 0, 18),
            new(-8, 0, 20), 25, new(10, 0, 12), 20,
            new(0.6f, 0.7f, 1.3f), new(1, 0.3f, 0.1f), 0, new(-1.8f, 1.8f, -1.0f), new(1, 0, 0), -10),
    };

    /// <summary>
    /// Tirado o levantándose: la pose sale de las claves de <see cref="RiseKeys"/> (suave entre una
    /// y otra) y al final se funde con la que ya armó <see cref="BuildPose"/> (los pies en su lugar).
    /// </summary>
    private void RisePose(Pose p)
    {
        float t = MathF.Max(0, _riseT);
        int i = 0;
        while (i < RiseKeys.Length - 2 && t >= RiseKeys[i + 1].T) i++;
        var a = RiseKeys[i];
        var b = RiseKeys[i + 1];
        float u = Smooth(a.T, b.T, t);
        Vector3 V(Vector3 x, Vector3 y) => Vector3.Lerp(x, y, u);
        float F(float x, float y) => Lerp(x, y, u);
        float w = 1 - Smooth(RiseKeys[^1].T, RiseTime, t);

        var pelvis = V(a.Pelvis, b.Pelvis) * _k;
        p.Move[(int)Bone.Pelvis] = Vector3.Lerp(p.Move[(int)Bone.Pelvis], pelvis - new Vector3(0, _s.PelvisY, 0), w);
        p[Bone.Pelvis] = Vector3.Lerp(p[Bone.Pelvis], V(a.PelvisRot, b.PelvisRot), w);
        p[Bone.Spine] = Vector3.Lerp(p[Bone.Spine], V(a.Spine, b.Spine), w);
        p[Bone.Head] = Vector3.Lerp(p[Bone.Head], V(a.Head, b.Head), w);
        foreach (int side in new[] { 1, -1 })
        {
            bool r = side > 0;
            var sh = Skeleton.Arm(side, 0);
            var el = Skeleton.Arm(side, 1);
            p[sh] = Vector3.Lerp(p[sh], r ? V(a.ArmR, b.ArmR) : V(a.ArmL, b.ArmL), w);
            p[el] = Vector3.Lerp(p[el], new Vector3(0, 0, r ? F(a.ElbowR, b.ElbowR) : F(a.ElbowL, b.ElbowL)), w);
            ref var arm = ref p.Arms[Pose.SideIndex(side)];
            if (w > 0.5f) arm.On = false;
            ref var leg = ref p.Legs[Pose.SideIndex(side)];
            var foot = (r ? V(a.FootR, b.FootR) : V(a.FootL, b.FootL)) * _k;
            var knee = r ? V(a.KneeR, b.KneeR) : V(a.KneeL, b.KneeL);
            float pitch = r ? F(a.PitchR, b.PitchR) : F(a.PitchL, b.PitchL);
            leg.On = true;
            leg.Target = Vector3.Lerp(leg.Target, foot, w);
            leg.Hint = Vector3.Lerp(leg.Hint, knee, w);
            leg.End = Vector3.Lerp(leg.End, new Vector3(0, -side * 8, pitch), w);
            leg.Rot = null;
        }
    }

    /// <summary>
    /// Sacudón al recibir un golpe: en un instante el cuerpo se va hacia donde lo empujan (se
    /// echa atrás si le pegan de frente, se ladea si le pegan de costado), baja y abre los
    /// brazos; después vuelve solo.
    /// </summary>
    private void Recoil(Pose p, Matrix4x4 toChar)
    {
        if (_hurtT >= 0.8f) return;
        float k = _hurtT < 0.04f ? _hurtT / 0.04f : MathF.Exp(-(_hurtT - 0.04f) * 7);
        var push = Vector3.Transform(new Vector3(_hurtPush.X, 0, _hurtPush.Y), toChar);
        p[Bone.Pelvis] += new Vector3(6 * push.Z * k, 0, -8 * push.X * k);
        p[Bone.Spine] += new Vector3(14 * push.Z * k, 0, -26 * push.X * k);
        p[Bone.Head] += new Vector3(8 * push.Z * k, 0, -16 * push.X * k);
        p.Move[(int)Bone.Pelvis] += new Vector3(0, -0.65f * k * _k, 0);
        foreach (int side in new[] { 1, -1 })
            p[Skeleton.Arm(side, 0)] += new Vector3(-side * 25 * k, 0, 0);
    }

    /// <summary>
    /// Decapitado: se le aflojan las piernas y cae de rodillas (con un rebote), el torso se
    /// vence un poco hacia adelante y tambalea; los brazos cuelgan.
    /// </summary>
    private void KneelPose(Pose p)
    {
        float t = _modeT;
        float k = t < 0.28f ? Smooth(0, 0.28f, t) : 1 + 0.05f * MathF.Sin((t - 0.28f) * 22) * MathF.Exp(-(t - 0.28f) * 6);
        // Cae hasta apoyar las rodillas: la cadera queda a un muslo (un poco inclinado) del piso.
        float drop = _s.PelvisY - _s.HipDown - _s.Thigh * 0.93f - 0.2f * _k;
        p.Move[(int)Bone.Pelvis] = new Vector3(-0.4f * k * _k, -drop * k, 0);
        p[Bone.Pelvis] = new Vector3(0, 0, -6 * k);
        p[Bone.Spine] = new Vector3(3 * MathF.Sin(t * 5), 0, -14 * k);
        foreach (int side in new[] { 1, -1 })
        {
            p[Skeleton.Leg(side, 0)] = new Vector3(-side * 5, 0, 6 * k);
            p[Skeleton.Leg(side, 1)] = new Vector3(0, 0, -95 * k);
            p[Skeleton.Leg(side, 2)] = new Vector3(0, 0, 35 * k);
            p[Skeleton.Arm(side, 0)] = new Vector3(-side * (10 + 8 * k), 0, 12 * k);
            p[Skeleton.Arm(side, 1)] = new Vector3(0, 0, 15);
        }
    }

    /// <summary>
    /// Sin piernas: se desploma boca abajo y tira de sí mismo con los brazos, uno y otro, con la
    /// cabeza levantada mirando hacia adelante. Los brazos siguen manoteando aunque no avance.
    /// </summary>
    private void CrawlPose(Pose p, float h)
    {
        float down = Smooth(0, 0.3f, _modeT);
        _crawlPh += h * (0.7f + _vel.Length() * 0.25f);
        float ph = _crawlPh * MathF.Tau;
        p.Local[(int)Bone.Root] = Quaternion.CreateFromAxisAngle(-Vector3.UnitZ, 1.42f * down);
        p.Move[(int)Bone.Root] = new Vector3(-0.9f * _s.PelvisY * down, (0.55f + 0.5f * _s.D.ChestF) * down, 0);
        p[Bone.Spine] = new Vector3(0, 6 * MathF.Sin(ph), 8 * down);
        p[Bone.Head] = new Vector3(0, -6 * MathF.Sin(ph), 38 * down);
        foreach (int side in new[] { 1, -1 })
        {
            float s = MathF.Sin(ph + (side > 0 ? 0 : MathF.PI));
            p[Skeleton.Arm(side, 0)] = new Vector3(-side * 22, 0, Lerp(10, 150 + 28 * s, down));
            p[Skeleton.Arm(side, 1)] = new Vector3(0, 0, 25 + 30 * MathF.Max(0, -s));
            p[Skeleton.Leg(side, 0)] = new Vector3(-side * 8, 0, -4 + 5 * s);
            p[Skeleton.Leg(side, 1)] = new Vector3(0, 0, -15);
        }
    }

    /// <summary>
    /// Pose de un tajo en el segundo <paramref name="t"/> (<paramref name="pos"/> y
    /// <paramref name="rot"/> traen la postura y salen con la mano del tajo):
    /// <list type="bullet">
    /// <item>Carga hasta que empieza el golpe activo: la mano va atrás y se sigue enroscando, la
    /// muñeca deja la hoja más atrás todavía y el cuerpo gira hacia ese lado.</item>
    /// <item>El golpe sale de una vez y frena de a poco hasta el final del recorrido, sin
    /// detenerse en el medio. La hoja viene arrastrada y pasa a la mano (latigazo), el brazo se
    /// estira y el cuerpo se tira adelante.</item>
    /// <item>Al final vuelve a la postura.</item>
    /// </list>
    /// El otro brazo contrapesa. Devuelve cuánto pesa el tajo sobre la postura (0..1);
    /// <paramref name="smear"/> dice si la hoja deja estela.
    /// </summary>
    private float SwingPose(Pose p, Swing sw, ClipDef clip, float t, Vector3 pelvis, ref Vector3 pos, ref Quaternion rot, out bool smear, bool reverse = false, float droop = 0)
    {
        float hs = clip.HitStart, he = clip.HitEnd, T = clip.Duration;
        float settle = he + (T - he) * 0.5f;
        float dir = MathF.Sign(sw.Follow - sw.Windup);
        float load = sw.Windup - dir * 14;
        float a, wrist, extend = 0;
        if (t < hs)
        {
            float u = Smooth(0, hs, t);
            a = Lerp(sw.Windup, load, u);
            wrist = -18 * u;
        }
        else
        {
            float u = Math.Clamp((t - hs) / (settle - hs), 0, 1);
            a = Lerp(load, sw.Follow, 1 - MathF.Pow(1 - u, 3));
            wrist = Lerp(-18, 14, Smooth(0, 0.55f, u));
            extend = MathF.Sin(MathF.PI * MathF.Min(1, u * 1.8f));
        }
        float ra = a * MathF.PI / 180, rb = (a + dir * wrist) * MathF.PI / 180;
        var radial = new Vector3(MathF.Cos(ra), 0, 0) + sw.Side * MathF.Sin(ra);
        var blade = new Vector3(MathF.Cos(rb), 0, 0) + sw.Side * MathF.Sin(rb);
        var center = new Vector3(sw.Center.X * _armK, sw.Center.Y * _shoulderY / 3.6f, sw.Center.Z * _armK);
        var arcPos = pelvis + center + radial * (sw.Radius * _armK * (1 + 0.18f * extend));
        // Al revés (el cayado): lo que barre es la punta larga, del otro lado del puño, un poco caída.
        var arcRot = Grip(reverse ? -blade + new Vector3(0, droop, 0) : blade, Vector3.Cross(Vector3.UnitX, sw.Side));

        float back = Smooth(settle, T, t);
        pos = Vector3.Lerp(arcPos, pos, back);
        rot = Quaternion.Slerp(arcRot, rot, back);
        float w = Smooth(0, MathF.Max(hs * 0.7f, 0.01f), t) * (1 - back);
        float coil = Smooth(0, hs, t) * (1 - Smooth(hs, he, t));
        float strike = Smooth(hs * 0.6f, hs, t) * (1 - Smooth(he, T, t));
        smear = t >= hs && t <= settle;

        // El torso gira hacia el lado de la mano (la cabeza sigue mirando al frente): cuenta sólo
        // lo que la mano se corre de costado, así en el golpe de arriba hacia abajo casi no gira.
        // Al cargar se echa un poco atrás; al pegar se tira adelante, baja y avanza la cadera.
        float turn = radial.Z * w;
        p[Bone.Pelvis] += new Vector3(0, -18 * turn, 0);
        p[Bone.Spine] += new Vector3(0, -30 * turn, 5 * coil - sw.Lean * strike);
        p[Bone.Head] += new Vector3(0, 44 * turn, sw.Lean * 0.6f * strike - 3 * coil);
        p.Move[(int)Bone.Pelvis] += new Vector3(0.45f * strike, -sw.Crouch * 1.3f * (0.5f * coil + strike), 0) * _k;
        // El otro brazo: adelante mientras carga, atrás y abierto al pegar.
        var left = Skeleton.Arm(-1, 0);
        var leftGoal = Vector3.Lerp(new Vector3(18, 0, 25), new Vector3(30, 0, -28), Smooth(hs * 0.8f, he, t));
        p[left] = Vector3.Lerp(p[left], leftGoal, w);
        p[Skeleton.Arm(-1, 1)] = Vector3.Lerp(p[Skeleton.Arm(-1, 1)], new Vector3(0, 0, 40), w);
        return w;
    }

    /// <summary>
    /// Hacia dónde apunta la hoja, en el espacio del personaje: <paramref name="side"/> grados
    /// desde adelante hacia la derecha (180 = atrás) y <paramref name="pitch"/> grados hacia arriba.
    /// </summary>
    internal static Vector3 Aim(float side, float pitch)
    {
        float a = side * MathF.PI / 180, b = pitch * MathF.PI / 180;
        return new Vector3(MathF.Cos(b) * MathF.Cos(a), MathF.Sin(b), MathF.Cos(b) * MathF.Sin(a));
    }

    /// <summary>
    /// Rotación de la mano que empuña (el mango en su eje X) para que la hoja apunte a
    /// <paramref name="dir"/> con las caras hacia <paramref name="face"/>. En la postura las caras
    /// miran hacia arriba: la cámara mira desde arriba, así se ve el ancho de la hoja y no sólo
    /// el filo. En un tajo miran fuera del plano del golpe, así el filo va por delante.
    /// </summary>
    internal static Quaternion Grip(Vector3 dir, Vector3 face)
    {
        var x = Vector3.Normalize(dir);
        var z = face - x * Vector3.Dot(face, x);
        z = z.LengthSquared() < 1e-4f ? Vector3.UnitZ : Vector3.Normalize(z);
        var y = Vector3.Cross(z, x);
        return Quaternion.CreateFromRotationMatrix(new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, 0, 1));
    }

    // ------------------------------------------------------------------ movimiento secundario

    private void UpdateSprings(float h)
    {
        var world = Matrix4x4.CreateRotationY(-Yaw) * Matrix4x4.CreateTranslation(Root);
        RunChain(_hair, Bone.Head, _s.D.HairBackRoot, world, h);
        RunChain(_tail, Bone.Head, _s.D.TailRoot, world, h);
        RunChain(_scarf, Bone.Pelvis, _s.D.ScarfRoot, world, h);
    }

    private void RunChain(SpringChain c, Bone parent, Vector3 rootOffset, Matrix4x4 world, float h)
    {
        var pm = Bones[(int)parent] * world;
        var anchor = Vector3.Transform(rootOffset, pm);
        var (q1, q2) = c.Step(pm, anchor, h);
        Pose.Local[(int)c.B1] = q1;
        Pose.Local[(int)c.B2] = q2;
    }

    /// <summary>Escala de la pierna y del brazo respecto de las humanas (ver <see cref="_k"/>).</summary>
    internal float LegScale => _k;
    internal float ArmScale => _armK;
    /// <summary>Un número al azar (0..1) de este personaje.</summary>
    internal float Random01() => Rand();
    /// <summary>El hombro de un lado (+1 derecho) en el espacio del personaje, con la cadera en <paramref name="pelvis"/>.</summary>
    internal Vector3 ShoulderAt(int side, Vector3 pelvis) => pelvis + new Vector3(0, _shoulderY, side * _s.D.ShoulderW);

    /// <summary>Pie derecho (0) o izquierdo (1): posición en el mundo y si está apoyado (para pruebas y depuración).</summary>
    public (Vector3 pos, bool planted) FootState(int k) => (_feet[k].Pos, !_feet[k].Swing);
    public float Phase => _phase;
    /// <summary>La fase de la brazada (0..1: tira hasta 0,45) mientras nada.</summary>
    public float SwimPhase => _swimPh;
    public float Speed => _speed;
}
