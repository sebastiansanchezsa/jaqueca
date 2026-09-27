using System.Numerics;
using Jaqueca.Anim;

namespace Jaqueca.Figures.Rig;

/// <summary>
/// Poses de cada clip, frame por frame. Los clips se escriben en código con funciones de la
/// fase o con poses clave interpoladas; la cantidad de frames sale de <see cref="ClipDefs"/>.
/// </summary>
public static partial class Clips
{
    public delegate Pose Frame(Skeleton s, int frame, int frames);

    private static readonly Dictionary<ClipId, Frame> _clips = new()
    {
        [ClipId.Idle] = Idle,
        [ClipId.Run] = Run,
    };

    public static bool Has(ClipId id) => _clips.ContainsKey(id);

    public static Pose Get(ClipId id, Skeleton s, int frame) => _clips[id](s, frame, ClipDefs.Get(id).Frames);

    private const float Deg = MathF.PI / 180;

    private static float Sin(float deg) => MathF.Sin(deg * Deg);
    private static float Cos(float deg) => MathF.Cos(deg * Deg);

    /// <summary>Pies apoyados donde caen en reposo, con la punta un poco hacia afuera.</summary>
    private static void Plant(Pose p, Skeleton s, float spread = 1.1f, float fwdR = 0, float fwdL = 0, float turnOut = 8)
    {
        foreach (int side in new[] { 1, -1 })
        {
            float fwd = side > 0 ? fwdR : fwdL;
            p.Legs[Pose.SideIndex(side)] = new Ik
            {
                On = true,
                Target = new Vector3(fwd, s.AnkleY, side * s.D.HipW * spread),
                Hint = new Vector3(1, 0, side * 0.12f),
                End = new Vector3(0, -side * turnOut, 0),
            };
        }
    }

    /// <summary>Brazos relajados: un poco separados del cuerpo y con el codo apenas flexionado.</summary>
    private static void ArmsRest(Pose p, float abd = 9, float swing = 3, float elbow = 12)
    {
        foreach (int side in new[] { 1, -1 })
        {
            p[Skeleton.Arm(side, 0)] = new Vector3(-side * abd, 0, swing);
            p[Skeleton.Arm(side, 1)] = new Vector3(0, 0, elbow);
            p[Skeleton.Arm(side, 2)] = new Vector3(-side * 4, 0, 6);
        }
    }

    // ------------------------------------------------------------------ base

    /// <summary>Respiración: el cuerpo baja 1 px flexionando las rodillas, el pecho se abre, los hombros suben.</summary>
    private static Pose Idle(Skeleton s, int f, int n)
    {
        float ph = 360f * f / n;
        float breath = (1 - Cos(ph)) * 0.5f; // 0 arriba, 1 abajo
        var p = new Pose();
        p.Move[(int)Bone.Pelvis] = new Vector3(0, -0.28f - 0.44f * breath, 0);
        p[Bone.Pelvis] = new Vector3(0, 4, 0);
        p[Bone.Spine] = new Vector3(0, -4, -3 + 2 * breath);
        p.Scale[(int)Bone.Spine] = new Vector3(1, 1 + 0.015f * (1 - breath), 1 + 0.02f * (1 - breath));
        p[Bone.Head] = new Vector3(0, 2, 2 - 3 * breath);
        ArmsRest(p, abd: 9 - breath, swing: 4 + 2 * breath, elbow: 12 + 5 * breath);
        Plant(p, s, spread: 1.15f, fwdR: 0.35f, fwdL: -0.25f);
        return p;
    }

    /// <summary>
    /// Carrera de 8 frames. Fase φ de la pierna derecha: π/2 contacto adelante, π pasa por
    /// debajo cargando el peso, 3π/2 impulso atrás, 0 recogida (talón arriba). La izquierda
    /// va a φ + π y los brazos al revés que las piernas.
    /// </summary>
    private static Pose Run(Skeleton s, int f, int n)
    {
        float ph = 360f * f / n;
        var p = new Pose();
        // Punto más bajo apenas después del contacto; más alto en el vuelo.
        float bob = (1 + Cos(2 * ph - 40)) * 0.5f;
        p.Move[(int)Bone.Pelvis] = new Vector3(0, -0.15f - 0.55f * bob, 0);
        p[Bone.Pelvis] = new Vector3(0, 7 * Sin(ph), 0);
        p[Bone.Spine] = new Vector3(0, -11 * Sin(ph), -13);
        p[Bone.Head] = new Vector3(0, 8 * Sin(ph), 9);

        foreach (int side in new[] { 1, -1 })
        {
            float q = side > 0 ? ph : ph + 180;
            float thigh = 8 + 44 * Sin(q);
            float c = Cos(q);
            float knee = 18 + 82 * MathF.Pow(MathF.Max(0, c), 1.4f) + 16 * MathF.Max(0, -c);
            p[Skeleton.Leg(side, 0)] = new Vector3(-side * 3, 0, thigh);
            p[Skeleton.Leg(side, 1)] = new Vector3(0, 0, -knee);
            // Pie: en el impulso la punta queda abajo; en la recogida sube con el talón.
            p[Skeleton.Leg(side, 2)] = new Vector3(0, 0, knee * 0.45f - thigh * 0.35f + 6 * Sin(q + 180));

            float arm = -side * 40 * Sin(ph);
            p[Skeleton.Arm(side, 0)] = new Vector3(-side * 12, 0, arm + 6);
            p[Skeleton.Arm(side, 1)] = new Vector3(0, 0, 72 + 14 * MathF.Max(0, arm) / 40);
            p[Skeleton.Arm(side, 2)] = new Vector3(-side * 6, 0, 10);
        }
        return p;
    }
}
