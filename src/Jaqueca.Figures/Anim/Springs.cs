using System.Numerics;
using Jaqueca.Figures.Rig;

namespace Jaqueca.Figures.Anim;

/// <summary>
/// Cadena de dos tramos con inercia (verlet a paso fijo): pelo largo, coleta, punta del
/// pañuelo. Los puntos viven en el mundo, así cuando el personaje corre o gira se quedan
/// atrás y se balancean. Un resorte suave los devuelve a su forma de reposo y los límites
/// de ángulo evitan que atraviesen la cabeza o el pecho.
/// </summary>
public sealed class SpringChain
{
    public readonly Bone B1, B2;
    public readonly float Len1, Len2;
    /// <summary>Cuánto se abre como mucho respecto de su dirección de reposo (radianes).</summary>
    public float MaxAngle = 0.9f;
    /// <summary>Límite en el espacio del hueso padre: componente X (hacia adelante) mínima y máxima de cada tramo.</summary>
    public float MinX = -1, MaxX = 1;
    public float Stiffness = 70, Damping = 4.5f, Gravity = 140;

    private Vector3 _p1, _p2, _q1, _q2;
    private float _hPrev;
    private bool _init;

    public SpringChain(Bone b1, Bone b2, float len1, float len2)
    {
        B1 = b1; B2 = b2; Len1 = len1; Len2 = len2;
    }

    /// <summary>Vuelve a arrancar colgando en reposo (después de mover el cuerpo de golpe).</summary>
    public void Reset()
    {
        _init = false;
        _hPrev = 0;
    }

    /// <summary>
    /// Un paso. <paramref name="parent"/> es la matriz del hueso padre en el mundo;
    /// <paramref name="anchor"/> la posición de la raíz de la cadena. Devuelve las rotaciones
    /// locales de los dos huesos.
    /// </summary>
    public (Quaternion, Quaternion) Step(Matrix4x4 parent, Vector3 anchor, float h)
    {
        var down = -Vector3.Normalize(new Vector3(parent.M21, parent.M22, parent.M23));
        var rest1 = anchor + down * Len1;
        var rest2 = rest1 + down * Len2;
        if (!_init)
        {
            _p1 = _q1 = rest1;
            _p2 = _q2 = rest2;
            _init = true;
        }
        float keep = MathF.Exp(-Damping * h);
        var g = new Vector3(0, -Gravity, 0) * h * h;
        // Verlet con paso variable: la velocidad del paso anterior se escala al paso actual.
        float ratio = _hPrev > 0 ? h / _hPrev : 1;
        _hPrev = h;
        Integrate(ref _p1, ref _q1, rest1, keep * ratio, g, h);
        Integrate(ref _p2, ref _q2, rest2, keep * ratio, g, h);

        // Largos fijos y límites de ángulo (en el espacio del padre).
        Matrix4x4.Invert(Rot(parent), out var toParent);
        var d1 = Limit(Vector3.Normalize(_p1 - anchor), toParent, parent);
        _p1 = anchor + d1 * Len1;
        var d2 = Vector3.Normalize(_p2 - _p1);
        // El segundo tramo no se dobla más de MaxAngle respecto del primero.
        float c = Vector3.Dot(d1, d2);
        if (c < MathF.Cos(MaxAngle)) d2 = Vector3.Normalize(Vector3.Lerp(d1, d2, 0.5f));
        d2 = Limit(d2, toParent, parent);
        _p2 = _p1 + d2 * Len2;

        // Rotaciones locales: llevan el eje de reposo (-Y) a la dirección simulada.
        var l1 = Vector3.Normalize(Vector3.TransformNormal(d1, toParent));
        var q1 = FromTo(-Vector3.UnitY, l1);
        var w1 = Matrix4x4.CreateFromQuaternion(q1) * Rot(parent);
        Matrix4x4.Invert(w1, out var to1);
        var l2 = Vector3.Normalize(Vector3.TransformNormal(d2, to1));
        var q2 = FromTo(-Vector3.UnitY, l2);
        return (q1, q2);
    }

    private void Integrate(ref Vector3 p, ref Vector3 prev, Vector3 rest, float keep, Vector3 g, float h)
    {
        var v = (p - prev) * keep;
        prev = p;
        p += v + g + (rest - p) * (Stiffness * h * h);
    }

    private Vector3 Limit(Vector3 dir, Matrix4x4 toParent, Matrix4x4 parent)
    {
        var l = Vector3.TransformNormal(dir, toParent);
        l = Vector3.Normalize(l);
        // Cono alrededor del reposo (-Y del padre).
        float ang = MathF.Acos(Math.Clamp(-l.Y, -1, 1));
        if (ang > MaxAngle)
        {
            var side = new Vector3(l.X, 0, l.Z);
            side = side.LengthSquared() < 1e-6f ? Vector3.UnitX : Vector3.Normalize(side);
            l = side * MathF.Sin(MaxAngle) - Vector3.UnitY * MathF.Cos(MaxAngle);
        }
        if (l.X < MinX || l.X > MaxX)
        {
            l.X = Math.Clamp(l.X, MinX, MaxX);
            l = Vector3.Normalize(l);
        }
        return Vector3.Normalize(Vector3.TransformNormal(l, Rot(parent)));
    }

    private static Matrix4x4 Rot(Matrix4x4 m)
    {
        var x = Vector3.Normalize(new Vector3(m.M11, m.M12, m.M13));
        var y = Vector3.Normalize(new Vector3(m.M21, m.M22, m.M23));
        var z = Vector3.Normalize(new Vector3(m.M31, m.M32, m.M33));
        return new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, 0, 1);
    }

    /// <summary>Rotación mínima que lleva el vector a al b.</summary>
    public static Quaternion FromTo(Vector3 a, Vector3 b)
    {
        float d = Vector3.Dot(a, b);
        if (d < -0.9999f)
        {
            var axis = Vector3.Cross(Vector3.UnitX, a);
            if (axis.LengthSquared() < 1e-6f) axis = Vector3.Cross(Vector3.UnitZ, a);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
        }
        var c = Vector3.Cross(a, b);
        return Quaternion.Normalize(new Quaternion(c, 1 + d));
    }
}
