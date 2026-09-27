using System.Numerics;
using Jaqueca.Figures.Model;

namespace Jaqueca.Figures.Physics;

/// <summary>
/// La tela de un personaje en movimiento (ver <see cref="ClothDef"/>), simulada en el mundo: la fila de
/// arriba va cosida a su hueso y cada punto de abajo es un resorte con freno hacia donde estaría en reposo
/// (más firme cerca de la costura: la prenda no pierde su forma). Como tiene peso, cuando el cuerpo arranca,
/// frena o gira, la tela se queda atrás y se hamaca; la tela no se estira (se puede fruncir) y las piernas
/// y el piso la empujan: al caminar la rodilla levanta la falda en vez de atravesarla.
/// <para>
/// El resorte y el freno se integran con la velocidad de cada punto y no dependen de cuántos cuadros por
/// segundo haya. Lo que acomoda la tela (el largo, los choques) mueve el punto sin darle velocidad: así
/// nada rebota ni vibra (un empujón de la pierna no se vuelve un sacudón).
/// </para>
/// </summary>
public sealed class ClothSim
{
    /// <summary>La gravedad en unidades del mundo (un humano mide 16 u, ~1,80 m).</summary>
    public const float Gravity = 90;
    private const int Iterations = 2;

    public readonly ClothDef Def;
    /// <summary>Los puntos en el espacio del personaje y sus normales (lo que se dibuja).</summary>
    public readonly Vector3[] Local, Normal;
    /// <summary>A qué escala se dibuja la figura (la tela se arma a la medida de referencia).</summary>
    public float Scale => _scale;
    /// <summary>Ya tiene puntos (antes del primer paso se dibuja en reposo).</summary>
    public bool Ready { get; private set; }

    private readonly Vector3[] _pos, _vel, _target, _targetBefore;
    private readonly float[] _lenV, _lenH;
    private readonly float _scale;

    /// <summary>Un tramo con radio contra el que choca la tela (una pierna, el tronco), en el mundo.</summary>
    public readonly record struct Capsule(Vector3 A, Vector3 B, float R);

    public ClothSim(ClothDef def, float scale)
    {
        Def = def;
        _scale = scale;
        int n = def.Count;
        Local = new Vector3[n];
        Normal = new Vector3[n];
        _pos = new Vector3[n];
        _vel = new Vector3[n];
        _target = new Vector3[n];
        _targetBefore = new Vector3[n];
        _lenV = new float[n];
        _lenH = new float[n];
        for (int r = 0; r <= def.Rows; r++)
        for (int k = 0; k < def.Cols; k++)
        {
            int i = def.Index(k, r);
            if (r < def.Rows) _lenV[i] = Vector3.Distance(def.Rest[i], def.Rest[def.Index(k, r + 1)]) * scale;
            if (def.Ring || k < def.Cols - 1) _lenH[i] = Vector3.Distance(def.Rest[i], def.Rest[def.Index((k + 1) % def.Cols, r)]) * scale;
        }
    }

    /// <summary>Vuelve a ponerla en reposo en el próximo paso (se teletransportó el personaje).</summary>
    public void Reset() => Ready = false;

    /// <param name="anchor">El hueso de la costura en el mundo.</param>
    /// <param name="toChar">Del mundo al espacio del personaje (para dibujar).</param>
    /// <param name="ground">La altura del piso en (x, z) del mundo.</param>
    /// <param name="water">La superficie del agua honda donde está (null: no hay): abajo flota y se frena.</param>
    public void Step(Matrix4x4 anchor, Matrix4x4 toChar, float h, ReadOnlySpan<Capsule> colliders, Func<float, float, float> ground, float? water)
    {
        var d = Def;
        int n = d.Count, cols = d.Cols;
        if (h <= 0) return;
        for (int i = 0; i < n; i++) _target[i] = Vector3.Transform(d.Rest[i] * _scale, anchor);
        // Recién puesta, o el cuerpo apareció en otro lado de golpe: arranca en reposo.
        float jump = 8 * _scale;
        if (!Ready || Vector3.DistanceSquared(_target[0], _pos[0]) > jump * jump)
        {
            Array.Copy(_target, _pos, n);
            Array.Copy(_target, _targetBefore, n);
            Array.Clear(_vel);
            Ready = true;
        }

        // ---- el resorte hacia la forma de reposo, con su freno (relativo a cómo se mueve el cuerpo).
        // La forma de reposo ya es la tela colgando: la gravedad sólo tira de lo que el cuerpo inclinó
        // (tirado en el piso, la falda cae; parado, conserva su forma).
        var up = Vector3.Normalize(new Vector3(anchor.M21, anchor.M22, anchor.M23));
        float tilt = 1 - Math.Clamp(up.Y, 0, 1);
        float weight = MathF.Max(0.2f, d.Weight);
        float k0 = (60 + 1400 * d.Stiff) / weight;
        for (int i = 0; i < n; i++)
        {
            var tv = (_target[i] - _targetBefore[i]) / h;
            if (i < cols) { _pos[i] = _target[i]; _vel[i] = tv; continue; }
            float v = (i / cols) / (float)d.Rows;
            float k = k0 * (1 - 0.55f * v);
            float c = 2 * d.Damp * MathF.Sqrt(k);
            var p = _pos[i];
            bool wet = water is { } lvl && p.Y < lvl;
            var acc = (_target[i] - p) * k - (_vel[i] - tv) * (wet ? c * 2.5f : c);
            acc.Y -= wet ? -Gravity * 0.1f : Gravity * tilt;
            _vel[i] += acc * h;
            _pos[i] = p + _vel[i] * h;
        }

        for (int it = 0; it < Iterations; it++)
        {
            // ---- a lo ancho no se estira (se puede fruncir).
            for (int r = 1; r <= d.Rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (!d.Ring && c == cols - 1) continue;
                int a = d.Index(c, r), b = d.Index((c + 1) % cols, r);
                Keep(a, b, _lenH[a]);
            }
            // ---- a lo largo de la caída tampoco: cada fila cuelga de la de arriba y no se aleja más que
            // el largo de la tela (de arriba abajo, así nunca queda estirada; si la empujan, se frunce).
            for (int r = 0; r < d.Rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int a = d.Index(c, r), b = d.Index(c, r + 1);
                var ab = _pos[b] - _pos[a];
                float dist = ab.Length(), len = _lenV[a];
                if (dist > len && dist > 1e-5f) _pos[b] = _pos[a] + ab * (len / dist);
            }
            // ---- las piernas (y el tronco) la empujan afuera; el piso la sostiene.
            for (int i = cols; i < n; i++)
            {
                var p = _pos[i];
                // Hacia dónde es "afuera" para este punto: lejos del hueso de la costura, de costado.
                var outward = _target[i] - anchor.Translation;
                outward.Y = 0;
                bool radial = outward.LengthSquared() > 1e-4f;
                if (radial) outward = Vector3.Normalize(outward);
                foreach (var cap in colliders)
                {
                    var ab = cap.B - cap.A;
                    float l2 = ab.LengthSquared();
                    float t = l2 > 1e-6f ? Math.Clamp(Vector3.Dot(p - cap.A, ab) / l2, 0, 1) : 0;
                    var q = cap.A + ab * t;
                    var off = p - q;
                    if (radial)
                    {
                        // La pierna la empuja sólo hacia afuera, sobre la línea del punto: lo que la pierna tapa de
                        // esa línea (más cuanto más de frente la tiene) queda del lado de afuera. El empujón crece
                        // y se apaga de a poco cuando la rodilla avanza o se corre: la tela no salta, y el frente
                        // de la falda queda siempre delante de las rodillas.
                        float along = Vector3.Dot(off, outward);
                        float lat2 = (off - outward * along).LengthSquared();
                        if (lat2 >= cap.R * cap.R) continue;
                        float surface = MathF.Sqrt(cap.R * cap.R - lat2);
                        if (along >= surface) continue;
                        p += outward * (surface - along);
                        float into = Vector3.Dot(_vel[i], outward);
                        if (into < 0) _vel[i] -= outward * into;
                        continue;
                    }
                    float dist = off.Length();
                    if (dist >= cap.R || dist < 1e-4f) continue;
                    p = q + off / dist * cap.R;
                }
                float floor = ground(p.X, p.Z) + 0.12f;
                if (p.Y < floor)
                {
                    p.Y = floor;
                    // Apoya en el piso: no sigue cayendo y roza (se frena de costado).
                    var v = _vel[i];
                    float rub = MathF.Exp(-10 * h);
                    _vel[i] = new Vector3(v.X * rub, MathF.Max(0, v.Y), v.Z * rub);
                }
                _pos[i] = p;
            }
        }

        Array.Copy(_target, _targetBefore, n);
        for (int i = 0; i < n; i++) Local[i] = Vector3.Transform(_pos[i], toChar);
        Normals(Local, Normal, d);
    }

    /// <summary>Si dos vecinos de la misma fila se alejan más que su largo, se acercan (juntos, por partes iguales).</summary>
    private void Keep(int a, int b, float len)
    {
        var ab = _pos[b] - _pos[a];
        float dist = ab.Length();
        if (dist <= len || dist < 1e-5f) return;
        var fix = ab * ((dist - len) / dist * 0.5f);
        _pos[a] += fix;
        _pos[b] -= fix;
    }

    /// <summary>Las normales de la grilla (de los vecinos de cada punto), en el espacio en que estén los puntos.</summary>
    public static void Normals(Vector3[] p, Vector3[] n, ClothDef d)
    {
        int cols = d.Cols;
        for (int r = 0; r <= d.Rows; r++)
        for (int c = 0; c < cols; c++)
        {
            int l = d.Ring ? (c + cols - 1) % cols : Math.Max(0, c - 1), rr = d.Ring ? (c + 1) % cols : Math.Min(cols - 1, c + 1);
            int up = Math.Max(0, r - 1), dn = Math.Min(d.Rows, r + 1);
            var u = p[d.Index(rr, r)] - p[d.Index(l, r)];
            var v = p[d.Index(c, dn)] - p[d.Index(c, up)];
            var nn = Vector3.Cross(u, v);
            n[d.Index(c, r)] = nn.LengthSquared() > 1e-10f ? Vector3.Normalize(nn) : Vector3.UnitX;
        }
    }
}
