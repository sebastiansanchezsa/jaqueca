using System.Numerics;

namespace Jaqueca.Figures.Rig;

/// <summary>Objetivo de IK de dos huesos (pierna o brazo), en espacio del personaje.</summary>
public struct Ik
{
    public bool On;
    /// <summary>Dónde tiene que quedar el tobillo o la muñeca.</summary>
    public Vector3 Target;
    /// <summary>Hacia dónde apunta la rodilla o el codo.</summary>
    public Vector3 Hint;
    /// <summary>Rotación del pie o la mano en espacio del personaje (grados X, Y, Z).</summary>
    public Vector3 End;
    /// <summary>Rotación del pie o la mano en espacio del personaje, ya armada (si está, reemplaza a <see cref="End"/>).</summary>
    public Quaternion? Rot;

    public static Ik Lerp(in Ik a, in Ik b, float t) => new()
    {
        On = t < 0.5f ? a.On : b.On,
        Target = Vector3.Lerp(a.Target, b.Target, t),
        Hint = Vector3.Lerp(a.Hint, b.Hint, t),
        End = Vector3.Lerp(a.End, b.End, t),
        Rot = a.Rot is { } qa && b.Rot is { } qb ? Quaternion.Slerp(qa, qb, t) : t < 0.5f ? a.Rot : b.Rot,
    };
}

/// <summary>
/// Una pose: rotaciones por hueso en grados (X = hacia el costado, Y = giro sobre el eje del
/// hueso, Z = adelante/atrás), desplazamientos y escalas por articulación, más IK opcional
/// para piernas y brazos. En un miembro que cuelga, +Z lo lleva hacia adelante; en la
/// columna y la cabeza, +Z inclina hacia atrás.
/// </summary>
public sealed class Pose
{
    private const int N = (int)Bone.Count;

    public readonly Vector3[] Rot = new Vector3[N];
    public readonly Vector3[] Move = new Vector3[N];
    public readonly Vector3[] Scale = Enumerable.Repeat(Vector3.One, N).ToArray();
    /// <summary>IK por lado: índice 0 = derecho, 1 = izquierdo.</summary>
    public readonly Ik[] Legs = new Ik[2], Arms = new Ik[2];
    /// <summary>Rotación local calculada afuera (resortes del pelo y el pañuelo); reemplaza a <see cref="Rot"/>.</summary>
    public readonly Quaternion?[] Local = new Quaternion?[N];

    public ref Vector3 this[Bone b] => ref Rot[(int)b];

    public static int SideIndex(int side) => side > 0 ? 0 : 1;

    /// <summary>Vuelve a la pose de reposo (para reusar el objeto frame a frame).</summary>
    public void Reset()
    {
        Array.Clear(Rot); Array.Clear(Move);
        Array.Fill(Scale, Vector3.One);
        Array.Clear(Legs); Array.Clear(Arms);
    }

    public Pose Clone()
    {
        var p = new Pose();
        Array.Copy(Rot, p.Rot, N); Array.Copy(Move, p.Move, N); Array.Copy(Scale, p.Scale, N);
        Array.Copy(Legs, p.Legs, 2); Array.Copy(Arms, p.Arms, 2);
        Array.Copy(Local, p.Local, N);
        return p;
    }

    public static Pose Lerp(Pose a, Pose b, float t)
    {
        var p = new Pose();
        for (int i = 0; i < N; i++)
        {
            p.Rot[i] = Vector3.Lerp(a.Rot[i], b.Rot[i], t);
            p.Move[i] = Vector3.Lerp(a.Move[i], b.Move[i], t);
            p.Scale[i] = Vector3.Lerp(a.Scale[i], b.Scale[i], t);
        }
        for (int s = 0; s < 2; s++)
        {
            p.Legs[s] = Ik.Lerp(a.Legs[s], b.Legs[s], t);
            p.Arms[s] = Ik.Lerp(a.Arms[s], b.Arms[s], t);
        }
        return p;
    }

    public static Matrix4x4 Euler(Vector3 deg) =>
        Matrix4x4.CreateRotationX(deg.X * MathF.PI / 180) *
        Matrix4x4.CreateRotationZ(deg.Z * MathF.PI / 180) *
        Matrix4x4.CreateRotationY(deg.Y * MathF.PI / 180);

    /// <summary>Matrices de cada hueso (hueso → espacio del personaje).</summary>
    public Matrix4x4[] Solve(Skeleton s) => Solve(s, new Matrix4x4[N]);

    /// <summary>Resuelve sobre un arreglo existente (sin reservar memoria: se usa cada frame).</summary>
    public Matrix4x4[] Solve(Skeleton s, Matrix4x4[] m)
    {
        Array.Clear(m);
        for (int i = 0; i < N; i++)
        {
            var b = (Bone)i;
            if (m[i] != default) continue; // lo resolvió una IK
            var rot = Local[i] is { } q ? Matrix4x4.CreateFromQuaternion(q) : Euler(Rot[i]);
            var local = Matrix4x4.CreateScale(Scale[i]) * rot * Matrix4x4.CreateTranslation(s.Offset[i] + Move[i]);
            m[i] = b == Bone.Root ? local : local * m[(int)Skeleton.Parent[i]];

            for (int side = 1; side >= -1; side -= 2)
            {
                if (b == Bone.Pelvis && Legs[SideIndex(side)].On)
                    TwoBone(s, m, Skeleton.Leg(side, 0), s.Thigh, s.Shin, Legs[SideIndex(side)]);
                if (b == Bone.Spine && Arms[SideIndex(side)].On)
                    TwoBone(s, m, Skeleton.Arm(side, 0), s.UpperArm, s.Forearm, Arms[SideIndex(side)]);
            }
        }
        return m;
    }

    /// <summary>
    /// IK analítica de dos huesos: ubica la articulación del medio en el plano que forman el
    /// objetivo y la pista, y arma las matrices de los tres huesos (el tercero con la
    /// rotación pedida en espacio del personaje).
    /// </summary>
    private void TwoBone(Skeleton s, Matrix4x4[] m, Bone first, float l1, float l2, in Ik ik)
    {
        int b1 = (int)first, b2 = b1 + 1, b3 = b1 + 2;
        var parent = m[(int)Skeleton.Parent[b1]];
        var t1 = s.Offset[b1] + Move[b1];
        var root = Vector3.Transform(t1, parent);

        var d = ik.Target - root;
        float dist = Math.Clamp(d.Length(), MathF.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.002f);
        var dir = d.LengthSquared() < 1e-8f ? -Vector3.UnitY : Vector3.Normalize(d);
        var end = root + dir * dist;

        var hint = ik.Hint - dir * Vector3.Dot(ik.Hint, dir);
        if (hint.LengthSquared() < 1e-6f) hint = Vector3.UnitX - dir * dir.X;
        hint = Vector3.Normalize(hint);
        float a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist);
        float h = MathF.Sqrt(MathF.Max(0, l1 * l1 - a * a));
        var mid = root + dir * a + hint * h;

        // La normal del plano de flexión queda del lado derecho del padre, igual que el eje Z
        // de los huesos en reposo: así rodilla y codo conservan su orientación natural.
        var n = Vector3.Normalize(Vector3.Cross(dir, hint));
        if (Vector3.Dot(n, LateralOf(parent)) < 0) n = -n;

        var r1 = Frame(Vector3.Normalize(root - mid), n);
        var r2 = Frame(Vector3.Normalize(mid - end), n);
        var r3 = ik.Rot is { } q ? Matrix4x4.CreateFromQuaternion(q) : Euler(ik.End);

        m[b1] = r1 * Matrix4x4.CreateTranslation(root);
        m[b2] = r2 * Matrix4x4.CreateTranslation(mid);
        m[b3] = Matrix4x4.CreateScale(Scale[b3]) * Euler(Rot[b3]) * r3 * Matrix4x4.CreateTranslation(end);
    }

    private static Vector3 LateralOf(Matrix4x4 m) => Vector3.Normalize(new Vector3(m.M31, m.M32, m.M33));

    /// <summary>Base ortonormal con el eje Y dado y Z lo más parecido posible a <paramref name="z"/>.</summary>
    private static Matrix4x4 Frame(Vector3 y, Vector3 z)
    {
        z = Vector3.Normalize(z - y * Vector3.Dot(z, y));
        var x = Vector3.Cross(y, z);
        return new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, 0, 1);
    }
}
