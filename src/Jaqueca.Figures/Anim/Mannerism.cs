using System.Numerics;
using Jaqueca.Anim;

namespace Jaqueca.Figures.Anim;

/// <summary>
/// Lo que el animador sabe de este paso, para las criaturas que agregan lo suyo encima de la
/// animación de siempre. Espacio del personaje salvo donde dice "mundo".
/// </summary>
public sealed class Motion
{
    /// <summary>Segundos desde que existe y largo de este paso.</summary>
    public float Time, Step;
    /// <summary>Fase de la marcha (0..1, sacada del pie derecho) y fracción del ciclo con cada pie apoyado.</summary>
    public float Phase, Duty;
    /// <summary>Cuánto corre (0 camina, 1 corre), cuánto está en marcha (0 parado, 1 andando) y su velocidad suavizada.</summary>
    public float Run, Move, Speed;
    /// <summary>Posición y velocidad en el piso (mundo x, z).</summary>
    public Vector2 Pos, Vel;
    /// <summary>Hacia dónde mira el cuerpo, qué tan rápido gira y hacia dónde quiere mirar (radianes, mundo).</summary>
    public float Yaw, YawVel, WantYaw;
    /// <summary>Origen en el mundo y la cadera en el espacio del personaje (ya con la marcha).</summary>
    public Vector3 Root, Pelvis;
    /// <summary>Golpe en curso (null = ninguno) y cuánto lleva.</summary>
    public ClipDef Act;
    public float ActT;
    /// <summary>Hace cuánto lo golpearon y hacia dónde lo empujaron.</summary>
    public float HurtT;
    public Vector3 HurtPush;
    /// <summary>Respiración (0..1).</summary>
    public float Breath;
    /// <summary>En el aire (ver <see cref="Animator.Leap"/>): cuánto del salto lleva (0..1) y hace cuánto aterrizó.</summary>
    public bool Airborne;
    public float AirU, SinceLand;
}

/// <summary>
/// Los gestos propios de una criatura: se montan sobre la marcha, los golpes y el reposo del
/// <see cref="Animator"/> (que valen para cualquier cuerpo) y pueden pisar lo que quieran de la
/// pose. Cada criatura tiene su instancia (guarda su estado: hacia dónde mira, espasmos...).
/// </summary>
public abstract class Mannerism
{
    /// <summary>Antes de armar la pose: puede cambiar lo que el animador usa para armarla (encorvado, agachado).</summary>
    public virtual void Prepare(Animator a, Motion m) { }

    /// <summary>Con la pose base armada (marcha, brazos, IK de las piernas), antes de resolverla.</summary>
    public abstract void Pose(Animator a, Rig.Pose p, Motion m);

    /// <summary>
    /// Después de los resortes: puede pisar la rotación de los huesos secundarios (una mandíbula,
    /// un manojo de llaves). También se llama muerto, con <paramref name="dead"/>.
    /// </summary>
    public virtual void Secondary(Animator a, Rig.Pose p, Motion m, bool dead) { }

    /// <summary>
    /// La matriz de un hueso del tronco (cadera, columna, cabeza o el arranque de un brazo o una
    /// pierna) con las rotaciones de la pose, antes de resolverla: dónde quedan de verdad el
    /// hombro o la cabeza de un cuerpo encorvado, para apuntar las manos.
    /// </summary>
    protected static Matrix4x4 Trunk(Rig.Skeleton s, Rig.Pose p, Rig.Bone b)
    {
        int i = (int)b;
        var rot = p.Local[i] is { } q ? Matrix4x4.CreateFromQuaternion(q) : Rig.Pose.Euler(p.Rot[i]);
        var local = Matrix4x4.CreateScale(p.Scale[i]) * rot * Matrix4x4.CreateTranslation(s.Offset[i] + p.Move[i]);
        return b == Rig.Bone.Root ? local : local * Trunk(s, p, Rig.Skeleton.Parent[i]);
    }

    /// <summary>Rotación con los ejes X e Y (casi) dados, para la mano de una IK.</summary>
    protected static Quaternion Basis(Vector3 x, Vector3 y)
    {
        x = Vector3.Normalize(x);
        var z = Vector3.Cross(x, y);
        z = z.LengthSquared() < 1e-6f ? Vector3.UnitZ : Vector3.Normalize(z);
        y = Vector3.Cross(z, x);
        return Quaternion.CreateFromRotationMatrix(new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, 0, 1));
    }

    protected static float Smooth(float e0, float e1, float x) { float t = Math.Clamp((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t); }
    protected static float Lerp(float a, float b, float t) => a + (b - a) * t;
    protected static float Wrap(float a) => Animator.Wrap(a);
    /// <summary>Sigue a <paramref name="goal"/> con un resorte exponencial de <paramref name="rate"/> por segundo.</summary>
    protected static float Toward(float cur, float goal, float rate, float h) => cur + (goal - cur) * (1 - MathF.Exp(-rate * h));
}
