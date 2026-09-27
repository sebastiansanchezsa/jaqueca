using System.Numerics;
using Jaqueca.Figures.Rig;

namespace Jaqueca.Figures.Model;

/// <summary>
/// Primitiva 3D colgada de un hueso. Se intersecta en su propio espacio local: el rayo del
/// píxel se lleva ahí con la inversa de (Local × hueso × cámara), así los huesos pueden
/// escalarse (squash &amp; stretch) sin tocar las fórmulas.
/// </summary>
public abstract class Shape
{
    public Bone Bone;
    /// <summary>Forma → hueso.</summary>
    public Matrix4x4 Local = Matrix4x4.Identity;
    public int Mat, Part;
    /// <summary>Formas finas (cuerdas, astas): con un solo subpíxel alcanza para pintar.</summary>
    public bool Thin;
    /// <summary>
    /// Sólo se dibuja como estela de un golpe: una copia por cada posición pasada de su hueso
    /// (ver <see cref="Render.Rasterizer.RenderLive"/>), nunca en la pose actual.
    /// </summary>
    public bool Smear;
    /// <summary>Es del arma (se cae aparte si le cortan la mano).</summary>
    public bool Weapon;
    /// <summary>
    /// De qué pieza del equipo puesto es (armadura, reliquia, mochila; su ID de Items.Gear), o null
    /// si no es equipo: así cada pieza se puede dibujar sola (su ícono, o resaltada en el inventario).
    /// </summary>
    public string Gear;
    /// <summary>Muñón: sólo se dibuja si este hueso se cortó de su padre.</summary>
    public Bone? CapFor;
    /// <summary>
    /// Cuerda: la forma es un tramo de largo 1 sobre su eje Y que se estira de
    /// <see cref="CordFrom"/> (en su hueso) a <see cref="CordTo"/> (en <see cref="CordBone"/>),
    /// así puede unir dos huesos (la cuerda del arco, de las puntas a la mano que tensa).
    /// </summary>
    public Bone? CordBone;
    public Vector3 CordFrom, CordTo;
    /// <summary>Forma de flecha: no se dibuja con la figura sino en cada flecha adjunta (clavada o en la cuerda).</summary>
    public bool Arrow;
    /// <summary>Repinta el material según la posición en el hueso: (punto, material) → material.</summary>
    public Func<Vector3, int, int> Paint;
    /// <summary>Recorta la forma: sólo cuenta donde devuelve true (punto en el espacio del hueso).</summary>
    public Func<Vector3, bool> Mask;
    /// <summary>
    /// Dónde está el origen del hueso en reposo, en el espacio de su "dueño" (p. ej. la cabeza
    /// para un mechón). Máscara, pintura y textura reciben el punto + Origin: el dibujo del pelo
    /// queda igual aunque cuelgue de un hueso secundario.
    /// </summary>
    public Vector3 Origin;
    /// <summary>Si está, la forma se dibuja sólo cuando devuelve true (lo que se rompe o aparece: el alcaide que se va deshaciendo).</summary>
    public Func<bool> Show;
    /// <summary>
    /// Si está, reemplaza a <see cref="Local"/> en cada cuadro: una parte que se mueve sobre su
    /// hueso sin tener hueso propio (las costillas del alcaide, que se abren como una reja).
    /// </summary>
    public Func<Matrix4x4> Pivot;

    /// <summary>La matriz forma → hueso de este cuadro.</summary>
    public Matrix4x4 LocalNow => Pivot?.Invoke() ?? Local;

    /// <summary>Copia superficial (para unir capas en una figura con índices corridos).</summary>
    public Shape Copy() => (Shape)MemberwiseClone();

    public abstract Vector3 BoundCenter { get; }
    public abstract float BoundRadius { get; }

    /// <summary>
    /// Hasta dónde llega la forma (con la matriz forma → cámara <paramref name="m"/>) sobre los ejes
    /// <paramref name="right"/> y <paramref name="up"/>: el rectángulo de pantalla donde hay que
    /// trazarla. Por defecto, el de la esfera envolvente; cada forma afina el suyo (una cápsula
    /// larga y cruzada no necesita todo el cuadrado de su esfera).
    /// </summary>
    public virtual void Span(Matrix4x4 m, Vector3 right, Vector3 up, out float x0, out float x1, out float y0, out float y1)
    {
        var c = Vector3.Transform(BoundCenter, m);
        float r = BoundRadius * MaxScale(m);
        float cx = Vector3.Dot(c, right), cy = Vector3.Dot(c, up);
        x0 = cx - r; x1 = cx + r; y0 = cy - r; y1 = cy + r;
    }

    protected static float MaxScale(Matrix4x4 m)
    {
        float a = new Vector3(m.M11, m.M12, m.M13).Length();
        float b = new Vector3(m.M21, m.M22, m.M23).Length();
        float c = new Vector3(m.M31, m.M32, m.M33).Length();
        return MathF.Max(a, MathF.Max(b, c));
    }

    /// <summary>La parte lineal de la matriz aplicada a un vector columna (para medir un eje del mundo en el espacio de la forma).</summary>
    protected static Vector3 Col(Matrix4x4 m, Vector3 e) => new(m.M11 * e.X + m.M12 * e.Y + m.M13 * e.Z, m.M21 * e.X + m.M22 * e.Y + m.M23 * e.Z, m.M31 * e.X + m.M32 * e.Y + m.M33 * e.Z);

    /// <summary>Intersección en espacio local: t de entrada y de salida con sus normales.</summary>
    public abstract bool Hit(Vector3 o, Vector3 d, out float t0, out Vector3 n0, out float t1, out Vector3 n1);

    /// <summary>Sólo la entrada (lo común: la salida hace falta únicamente si una máscara recorta la forma).</summary>
    public virtual bool HitFront(Vector3 o, Vector3 d, out float t0, out Vector3 n0) => Hit(o, d, out t0, out n0, out _, out _);
}

public sealed class Ellipsoid : Shape
{
    public Vector3 C, R;

    public override Vector3 BoundCenter => C;
    public override float BoundRadius => MathF.Max(R.X, MathF.Max(R.Y, R.Z));

    /// <summary>Exacto: sobre un eje e del mundo, el elipsoide se extiende |R ∘ (L·e)| a cada lado de su centro.</summary>
    public override void Span(Matrix4x4 m, Vector3 right, Vector3 up, out float x0, out float x1, out float y0, out float y1)
    {
        var c = Vector3.Transform(C, m);
        float hx = (R * Col(m, right)).Length(), hy = (R * Col(m, up)).Length();
        float cx = Vector3.Dot(c, right), cy = Vector3.Dot(c, up);
        x0 = cx - hx; x1 = cx + hx; y0 = cy - hy; y1 = cy + hy;
    }

    public override bool Hit(Vector3 o, Vector3 d, out float t0, out Vector3 n0, out float t1, out Vector3 n1)
    {
        var oo = (o - C) / R;
        var dd = d / R;
        float a = Vector3.Dot(dd, dd), b = Vector3.Dot(oo, dd), c = Vector3.Dot(oo, oo) - 1;
        float disc = b * b - a * c;
        t0 = t1 = 0; n0 = n1 = default;
        if (disc < 0) return false;
        float sq = MathF.Sqrt(disc);
        t0 = (-b - sq) / a;
        t1 = (-b + sq) / a;
        var r2 = R * R;
        n0 = (o + d * t0 - C) / r2;
        n1 = (o + d * t1 - C) / r2;
        return true;
    }
}

/// <summary>Cápsula cónica: esferas de radio Ra en A y Rb en B unidas por un cono tangente.</summary>
public sealed class RoundCone : Shape
{
    public Vector3 A, B;
    public float Ra, Rb;

    public override Vector3 BoundCenter => (A + B) * 0.5f;
    public override float BoundRadius => Vector3.Distance(A, B) * 0.5f + MathF.Max(Ra, Rb);

    /// <summary>La caja de las dos esferas de las puntas (el cono entre ellas no se sale).</summary>
    public override void Span(Matrix4x4 m, Vector3 right, Vector3 up, out float x0, out float x1, out float y0, out float y1)
    {
        var a = Vector3.Transform(A, m);
        var b = Vector3.Transform(B, m);
        float k = MaxScale(m), ra = Ra * k, rb = Rb * k;
        float ax = Vector3.Dot(a, right), bx = Vector3.Dot(b, right), ay = Vector3.Dot(a, up), by = Vector3.Dot(b, up);
        x0 = MathF.Min(ax - ra, bx - rb); x1 = MathF.Max(ax + ra, bx + rb);
        y0 = MathF.Min(ay - ra, by - rb); y1 = MathF.Max(ay + ra, by + rb);
    }

    /// <summary>Distancia con signo exacta (Íñigo Quílez, "round cone").</summary>
    public float Sd(Vector3 p)
    {
        var ba = B - A;
        float l2 = Vector3.Dot(ba, ba);
        if (l2 < 1e-8f) return Vector3.Distance(p, A) - Ra;
        float rr = Ra - Rb, a2 = l2 - rr * rr, il2 = 1 / l2;
        var pa = p - A;
        float y = Vector3.Dot(pa, ba), z = y - l2;
        var q = pa * l2 - ba * y;
        float x2 = Vector3.Dot(q, q), y2 = y * y * l2, z2 = z * z * l2;
        float k = MathF.Sign(rr) * rr * rr * x2;
        if (MathF.Sign(z) * a2 * z2 > k) return MathF.Sqrt(x2 + z2) * il2 - Rb;
        if (MathF.Sign(y) * a2 * y2 < k) return MathF.Sqrt(x2 + y2) * il2 - Ra;
        return (MathF.Sqrt(x2 * a2 * il2) + y * rr) * il2 - Ra;
    }

    private Vector3 Grad(Vector3 p)
    {
        const float e = 0.004f;
        return new Vector3(
            Sd(p + new Vector3(e, 0, 0)) - Sd(p - new Vector3(e, 0, 0)),
            Sd(p + new Vector3(0, e, 0)) - Sd(p - new Vector3(0, e, 0)),
            Sd(p + new Vector3(0, 0, e)) - Sd(p - new Vector3(0, 0, e)));
    }

    public override bool Hit(Vector3 o, Vector3 d, out float t0, out Vector3 n0, out float t1, out Vector3 n1)
    {
        t0 = t1 = 0; n0 = n1 = default;
        float len = d.Length();
        var dn = d / len;
        // Entrada y salida de la esfera envolvente; se avanza con sphere tracing desde cada punta.
        var c = BoundCenter;
        float r = BoundRadius + 0.01f;
        var oc = o - c;
        float b = Vector3.Dot(oc, dn), cc = Vector3.Dot(oc, oc) - r * r, disc = b * b - cc;
        if (disc < 0) return false;
        float sq = MathF.Sqrt(disc), ta = -b - sq, tb = -b + sq;

        if (!HitFront(o, d, out t0, out n0)) return false;
        March(o, dn, tb, ta, -1, out float hb);
        t1 = hb / len; n1 = Grad(o + dn * hb);
        return true;
    }

    /// <summary>
    /// La entrada, exacta (Íñigo Quílez, "rounded cone intersection"): el cono tangente o una de
    /// las dos esferas. Mucho más barato que avanzar a pasos (lo grande, como los brazos del
    /// alcaide, cubre miles de subpíxeles).
    /// </summary>
    public override bool HitFront(Vector3 o, Vector3 d, out float t0, out Vector3 n0)
    {
        t0 = 0; n0 = default;
        float len = d.Length();
        var rd = d / len;
        var ba = B - A;
        var oa = o - A;
        var ob = o - B;
        float rr = Ra - Rb;
        float m0 = Vector3.Dot(ba, ba), m1 = Vector3.Dot(ba, oa), m2 = Vector3.Dot(ba, rd), m3 = Vector3.Dot(rd, oa);
        float m5 = Vector3.Dot(oa, oa), m6 = Vector3.Dot(ob, rd), m7 = Vector3.Dot(ob, ob);
        if (m0 < 1e-8f)
        {
            // Una esfera sola.
            float hs = m3 * m3 - m5 + Ra * Ra;
            if (hs < 0) return false;
            float ts = -m3 - MathF.Sqrt(hs);
            t0 = ts / len; n0 = oa + rd * ts;
            return true;
        }
        float d2 = m0 - rr * rr;
        float k2 = d2 - m2 * m2;
        float k1 = d2 * m3 - m1 * m2 + m2 * rr * Ra;
        float k0 = d2 * m5 - m1 * m1 + m1 * rr * Ra * 2 - m0 * Ra * Ra;
        float h = k1 * k1 - k0 * k2;
        if (h < 0) return false;
        if (MathF.Abs(k2) > 1e-9f)
        {
            float t = (-MathF.Sqrt(h) - k1) / k2;
            float y = m1 - Ra * rr + t * m2;
            if (y > 0 && y < d2)
            {
                t0 = t / len;
                n0 = (oa + rd * t) * d2 - ba * y;
                return true;
            }
        }
        float h1 = m3 * m3 - m5 + Ra * Ra, h2 = m6 * m6 - m7 + Rb * Rb;
        if (MathF.Max(h1, h2) < 0) return false;
        float best = float.MaxValue;
        if (h1 > 0)
        {
            float t = -m3 - MathF.Sqrt(h1);
            best = t;
            n0 = oa + rd * t;
        }
        if (h2 > 0)
        {
            float t = -m6 - MathF.Sqrt(h2);
            if (t < best) { best = t; n0 = ob + rd * t; }
        }
        t0 = best / len;
        return true;
    }

    private bool March(Vector3 o, Vector3 dn, float from, float to, int dir, out float t)
    {
        t = from;
        for (int i = 0; i < 64; i++)
        {
            float s = Sd(o + dn * t);
            if (s < 0.001f) return true;
            t += s * dir;
            if (dir > 0 ? t > to : t < to) return false;
        }
        return false;
    }
}

/// <summary>Caja orientada con los ejes del hueso (hojas, mangos, placas).</summary>
public sealed class Box : Shape
{
    public Vector3 C, Half;

    public override Vector3 BoundCenter => C;
    public override float BoundRadius => Half.Length();

    /// <summary>Exacto: sobre un eje e, la caja se extiende Σ |Half_i · (L·e)_i| a cada lado del centro.</summary>
    public override void Span(Matrix4x4 m, Vector3 right, Vector3 up, out float x0, out float x1, out float y0, out float y1)
    {
        var c = Vector3.Transform(C, m);
        float hx = Vector3.Dot(Half, Vector3.Abs(Col(m, right))), hy = Vector3.Dot(Half, Vector3.Abs(Col(m, up)));
        float cx = Vector3.Dot(c, right), cy = Vector3.Dot(c, up);
        x0 = cx - hx; x1 = cx + hx; y0 = cy - hy; y1 = cy + hy;
    }

    public override bool Hit(Vector3 o, Vector3 d, out float t0, out Vector3 n0, out float t1, out Vector3 n1)
    {
        t0 = float.NegativeInfinity; t1 = float.PositiveInfinity; n0 = n1 = default;
        var lo = C - Half - o;
        var hi = C + Half - o;
        for (int ax = 0; ax < 3; ax++)
        {
            float dv = ax == 0 ? d.X : ax == 1 ? d.Y : d.Z;
            float l = ax == 0 ? lo.X : ax == 1 ? lo.Y : lo.Z;
            float h = ax == 0 ? hi.X : ax == 1 ? hi.Y : hi.Z;
            var axis = ax == 0 ? Vector3.UnitX : ax == 1 ? Vector3.UnitY : Vector3.UnitZ;
            if (MathF.Abs(dv) < 1e-9f)
            {
                if (l > 0 || h < 0) return false;
                continue;
            }
            float ta = l / dv, tb = h / dv;
            var na = -axis; var nb = axis;
            if (ta > tb) { (ta, tb) = (tb, ta); (na, nb) = (nb, na); }
            if (ta > t0) { t0 = ta; n0 = na; }
            if (tb < t1) { t1 = tb; n1 = nb; }
            if (t0 > t1) return false;
        }
        return true;
    }
}
