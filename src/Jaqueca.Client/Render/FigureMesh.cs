using Jaqueca.Figures.Model;
using Jaqueca.Figures.Render;
using Jaqueca.Figures.Rig;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NMat = System.Numerics.Matrix4x4;
using NVec3 = System.Numerics.Vector3;

namespace Jaqueca.Client.Render;

/// <summary>
/// Una figura del motor (las mismas primitivas colgadas de huesos con que Inquisition arma sus
/// personajes) pasada a triángulos una sola vez, cada vértice en el espacio de su hueso. Se anima en
/// la GPU: al dibujar se le pasan las matrices de los huesos de este cuadro (ver
/// <see cref="Pose(NMat[], NMat, RenderMask)"/>) y el vertex shader lleva cada vértice con la de su
/// hueso. Lo que la máscara oculta (un miembro cortado, el cuerpo entero en un pedazo) se achica a un
/// punto; los muñones y el arma son tramos aparte de los índices, que se dibujan o no.
/// Máscaras y pintura se evalúan en el centro de cada triángulo y la textura del material (el tono que
/// corre) en cada vértice; los estampados (los ojos) son cuadraditos pegados a la superficie.
/// </summary>
public sealed class FigureMesh : IDisposable
{
    public const int MaxBones = 24;

    public VertexBuffer Vb;
    public IndexBuffer Ib;
    /// <summary>Tramos de los índices: el cuerpo, el arma y el muñón de cada hueso que se puede cortar.</summary>
    public (int start, int tris) Main, Weapon;
    public readonly (int start, int tris)[] Caps = new (int, int)[(int)Bone.Count];
    /// <summary>Radio que abarca la figura desde su origen (para no dibujar lo que no se ve).</summary>
    public float Radius;

    private readonly Matrix[] _pal = new Matrix[MaxBones];

    private sealed class Builder
    {
        public readonly List<WorldVertex> V = new();
        public readonly List<int> I = new();
        public float Reach;

        public int Vertex(NVec3 p, NVec3 n, int bone, int row, float bias, bool fixedTone = false, float glow = 0)
        {
            Reach = MathF.Max(Reach, p.Length());
            V.Add(new WorldVertex(new Vector3(p.X, p.Y, p.Z), new Vector3(n.X, n.Y, n.Z),
                new Color((byte)bone, (byte)(fixedTone ? 1 : 0), (byte)0, (byte)255), new Vector4(row, 1, bias, glow)));
            return V.Count - 1;
        }
    }

    /// <summary>
    /// Arma la malla de <paramref name="fig"/>. <paramref name="color"/> da el color de cada material
    /// (para los que no son de color fijo: piel, pelo, acento); <paramref name="scale"/> achica o agranda
    /// las formas (la figura se arma a la medida de referencia y se anima a otra); <paramref name="detail"/>
    /// multiplica cuántas caras tiene cada forma.
    /// </summary>
    public static FigureMesh Build(GraphicsDevice gd, Figure fig, Func<Mat, uint> color = null, float scale = 1, float detail = 1)
    {
        var rows = new int[fig.Mats.Count];
        for (int i = 0; i < rows.Length; i++)
        {
            var d = fig.Mats[i].Def;
            rows[i] = FigurePalette.Row(color?.Invoke(fig.Mats[i]) ?? d.Rgb, d.Shiny, d.Lift, d.Flat);
        }
        var main = new Builder();
        var weapon = new List<int>();
        var caps = new List<int>[(int)Bone.Count];
        var body = new List<int>();
        var tris = new List<(NVec3 a, NVec3 b, NVec3 c)>();

        foreach (var s in fig.Shapes)
        {
            if (s.Smear || s.Arrow || s.CordBone != null) continue;
            tris.Clear();
            var local = s.Pivot != null ? s.Pivot() : s.Local;
            NVec3 center;
            bool twoSided = false;
            switch (s)
            {
                case Ellipsoid e: center = e.C; Sphere(tris, e.C, e.R, detail); break;
                case RoundCone rc: center = (rc.A + rc.B) * 0.5f; Capsule(tris, rc, detail); break;
                case Box bx: center = bx.C; BoxTris(tris, bx.C, bx.Half); break;
                case ClothTri ct:
                    center = default;
                    twoSided = true;
                    tris.Add((ct.Cloth.Rest[ct.A], ct.Cloth.Rest[ct.B], ct.Cloth.Rest[ct.C]));
                    break;
                default: continue;
            }
            var target = s.CapFor is { } cap ? caps[(int)cap] ??= new List<int>() : s.Weapon ? weapon : body;
            var mat = fig.Mats[s.Mat];
            foreach (var (ta, tb, tc) in tris)
            {
                var mid = (ta + tb + tc) / 3;
                var pb = NVec3.Transform(mid, local) + s.Origin;
                if (s.Mask != null && !s.Mask(pb)) continue;
                int m = s.Paint != null ? s.Paint(pb, s.Mat) : s.Mat;
                if (m < 0 || m >= rows.Length) m = s.Mat;
                var mm = fig.Mats[m];
                // Normales suaves: la de la superficie de cada primitiva en ese punto.
                NVec3 N(NVec3 p) => s switch
                {
                    Ellipsoid e => NVec3.Normalize((p - e.C) / (e.R * e.R)),
                    RoundCone r => CapsuleNormal(r, p),
                    _ => NVec3.Normalize(NVec3.Cross(tb - ta, tc - ta)),
                };
                var na = N(ta); var nb = N(tb); var nc = N(tc);
                if (s is Box || s is ClothTri) na = nb = nc = FaceNormal(ta, tb, tc, center, s is ClothTri);
                int Add(NVec3 p, NVec3 n)
                {
                    var q = NVec3.Transform(p, local);
                    var qn = NVec3.Normalize(NVec3.TransformNormal(n, local));
                    float bias = 0;
                    if (mm.Tex != null)
                    {
                        var pp = q + s.Origin;
                        bias = ((mm.Tex(pp, 2) - 2) + (mm.Tex(pp, 3) - 3)) * 0.5f;
                    }
                    return main.Vertex(q * scale, qn, (int)s.Bone, rows[m], bias);
                }
                int ia = Add(ta, na), ib = Add(tb, nb), ic = Add(tc, nc);
                // Antihorario visto desde afuera (el lado de la normal).
                var fn = NVec3.Cross(tb - ta, tc - ta);
                bool flip = NVec3.Dot(fn, na + nb + nc) < 0;
                if (flip) (ib, ic) = (ic, ib);
                target.Add(ia); target.Add(ib); target.Add(ic);
                if (twoSided)
                {
                    int ja = Add(ta, -na), jb = Add(tb, -nb), jc = Add(tc, -nc);
                    target.Add(ja); target.Add(flip ? jb : jc); target.Add(flip ? jc : jb);
                }
            }
        }

        // Los estampados (ojos, cejas, boca): un cuadradito por píxel, pegado a la superficie.
        foreach (var st in fig.Stamps)
        {
            var n = NVec3.Normalize(st.Normal);
            var up = NVec3.UnitY - n * NVec3.Dot(NVec3.UnitY, n);
            if (up.LengthSquared() < 1e-4f) up = NVec3.UnitX;
            up = NVec3.Normalize(up);
            var right = NVec3.Normalize(NVec3.Cross(up, n));
            const float px = 0.19f;
            var at = st.At + n * 0.05f;
            foreach (var (dx, dy, pm, tone) in st.Pixels)
            {
                if (pm < 0 || pm >= rows.Length) continue;
                var c = at + right * (dx * px) - up * (dy * px);
                var h = px * 0.5f;
                int a = main.Vertex((c - right * h + up * h) * scale, n, (int)st.Bone, rows[pm], tone, fixedTone: true);
                int b = main.Vertex((c + right * h + up * h) * scale, n, (int)st.Bone, rows[pm], tone, fixedTone: true);
                int cc = main.Vertex((c + right * h - up * h) * scale, n, (int)st.Bone, rows[pm], tone, fixedTone: true);
                int d = main.Vertex((c - right * h - up * h) * scale, n, (int)st.Bone, rows[pm], tone, fixedTone: true);
                body.Add(a); body.Add(b); body.Add(cc);
                body.Add(a); body.Add(cc); body.Add(d);
            }
        }

        var fm = new FigureMesh();
        var all = new List<int>(body.Count + weapon.Count);
        fm.Main = (0, body.Count / 3);
        all.AddRange(body);
        fm.Weapon = (all.Count, weapon.Count / 3);
        all.AddRange(weapon);
        for (int b = 0; b < caps.Length; b++)
        {
            if (caps[b] == null) continue;
            fm.Caps[b] = (all.Count, caps[b].Count / 3);
            all.AddRange(caps[b]);
        }
        fm.Radius = main.Reach * scale + 2;
        fm.Vb = new VertexBuffer(gd, typeof(WorldVertex), main.V.Count, BufferUsage.WriteOnly);
        fm.Vb.SetData(main.V.ToArray());
        if (main.V.Count < 65536)
        {
            var s16 = new short[all.Count];
            for (int i = 0; i < all.Count; i++) s16[i] = (short)all[i];
            fm.Ib = new IndexBuffer(gd, IndexElementSize.SixteenBits, s16.Length, BufferUsage.WriteOnly);
            fm.Ib.SetData(s16);
        }
        else
        {
            fm.Ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, all.Count, BufferUsage.WriteOnly);
            fm.Ib.SetData(all.ToArray());
        }
        return fm;
    }

    private static NVec3 FaceNormal(NVec3 a, NVec3 b, NVec3 c, NVec3 center, bool cloth)
    {
        var n = NVec3.Normalize(NVec3.Cross(b - a, c - a));
        if (!cloth && NVec3.Dot(n, (a + b + c) / 3 - center) < 0) n = -n;
        return n;
    }

    private static NVec3 CapsuleNormal(RoundCone r, NVec3 p)
    {
        // La normal de un cono redondeado: la de la esfera que se desliza por el eje (con el radio que tiene ahí).
        var ab = r.B - r.A;
        float l2 = ab.LengthSquared();
        if (l2 < 1e-8f) return NVec3.Normalize(p - r.A);
        float len = MathF.Sqrt(l2);
        var ax = ab / len;
        // La esfera que toca p: su centro en el eje, corrido según la pendiente del cono.
        float slope = (r.Ra - r.Rb) / len;
        float along = NVec3.Dot(p - r.A, ax);
        var radial = p - r.A - ax * along;
        float rl = radial.Length();
        float t = Math.Clamp((along + rl * slope) / len, 0, 1);
        var c = r.A + ab * t;
        var n = p - c;
        return n.LengthSquared() > 1e-10f ? NVec3.Normalize(n) : ax;
    }

    private static void Sphere(List<(NVec3, NVec3, NVec3)> tris, NVec3 c, NVec3 r, float detail)
    {
        float big = MathF.Max(r.X, MathF.Max(r.Y, r.Z));
        int lon = Math.Clamp((int)((8 + big * 7) * detail), 8, 32), lat = Math.Max(5, lon / 2);
        NVec3 P(int i, int j)
        {
            float th = i * MathF.Tau / lon, ph = -MathF.PI / 2 + j * MathF.PI / lat;
            return c + new NVec3(MathF.Cos(ph) * MathF.Cos(th) * r.X, MathF.Sin(ph) * r.Y, MathF.Cos(ph) * MathF.Sin(th) * r.Z);
        }
        for (int j = 0; j < lat; j++)
        for (int i = 0; i < lon; i++)
        {
            var a = P(i, j); var b = P(i + 1, j); var cc = P(i + 1, j + 1); var d = P(i, j + 1);
            if (j > 0) tris.Add((a, b, cc));
            if (j < lat - 1) tris.Add((a, cc, d));
        }
    }

    /// <summary>Cono redondeado: la esfera de A hasta la línea tangente y de ahí la de B.</summary>
    private static void Capsule(List<(NVec3, NVec3, NVec3)> tris, RoundCone rc, float detail)
    {
        var axis = rc.B - rc.A;
        float len = axis.Length();
        if (len < 1e-4f) { Sphere(tris, rc.A, new NVec3(rc.Ra), detail); return; }
        var ax = axis / len;
        var up = MathF.Abs(ax.Y) > 0.9f ? NVec3.UnitX : NVec3.UnitY;
        var u = NVec3.Normalize(NVec3.Cross(ax, up));
        var v = NVec3.Cross(ax, u);
        float big = MathF.Max(rc.Ra, rc.Rb);
        int seg = Math.Clamp((int)((8 + big * 8) * detail), 8, 24);
        int capRings = Math.Max(2, seg / 4);
        float t0 = MathF.Asin(Math.Clamp((rc.Ra - rc.Rb) / len, -1, 1));
        var rings = new List<(NVec3 c, float r)>();
        for (int k = 0; k <= capRings; k++)
        {
            float ph = -MathF.PI / 2 + (t0 + MathF.PI / 2) * k / capRings;
            if (k == 0) ph += 0.001f;
            rings.Add((rc.A + ax * (rc.Ra * MathF.Sin(ph)), rc.Ra * MathF.Cos(ph)));
        }
        // El tramo largo del medio, con anillos intermedios (así la luz no se estira en una sola cara).
        int mids = Math.Clamp((int)(len / 1.2f), 0, 6);
        var ta = rings[^1];
        var tb = (c: rc.B + ax * (rc.Rb * MathF.Sin(t0)), r: rc.Rb * MathF.Cos(t0));
        for (int k = 1; k <= mids; k++)
        {
            float f = k / (float)(mids + 1);
            rings.Add((NVec3.Lerp(ta.c, tb.c, f), ta.r + (tb.r - ta.r) * f));
        }
        for (int k = 0; k <= capRings; k++)
        {
            float ph = t0 + (MathF.PI / 2 - t0) * k / capRings;
            if (k == capRings) ph -= 0.001f;
            rings.Add((rc.B + ax * (rc.Rb * MathF.Sin(ph)), rc.Rb * MathF.Cos(ph)));
        }
        NVec3 P(int ring, int i)
        {
            float th = i * MathF.Tau / seg;
            var (c, r) = rings[ring];
            return c + (u * MathF.Cos(th) + v * MathF.Sin(th)) * r;
        }
        for (int k = 0; k < rings.Count - 1; k++)
        for (int i = 0; i < seg; i++)
        {
            var a = P(k, i); var b = P(k, i + 1); var c = P(k + 1, i + 1); var d = P(k + 1, i);
            if ((a - b).LengthSquared() > 1e-8f) tris.Add((a, b, c));
            if ((c - d).LengthSquared() > 1e-8f) tris.Add((a, c, d));
        }
    }

    private static void BoxTris(List<(NVec3, NVec3, NVec3)> tris, NVec3 c, NVec3 h)
    {
        NVec3 P(int x, int y, int z) => c + new NVec3(x * h.X, y * h.Y, z * h.Z);
        void Q(NVec3 a, NVec3 b, NVec3 cc, NVec3 d) { tris.Add((a, b, cc)); tris.Add((a, cc, d)); }
        Q(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));
        Q(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1));
        Q(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1));
        Q(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1));
        Q(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1));
        Q(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1));
    }

    // ------------------------------------------------------------------ dibujar

    /// <summary>
    /// Las matrices de los huesos de este cuadro (del hueso al mundo): las de la figura por
    /// <paramref name="world"/>. Lo que <paramref name="mask"/> oculta se achica a un punto (no se ve).
    /// Devuelve el arreglo para el shader (el mismo en cada llamada: se usa enseguida).
    /// </summary>
    public Matrix[] Pose(NMat[] bones, NMat world, RenderMask mask)
    {
        for (int i = 0; i < MaxBones; i++)
        {
            if (i >= bones.Length) { _pal[i] = Matrix.Identity; continue; }
            var m = bones[i] * world;
            if (mask != null && mask.Hidden[i]) m = NMat.CreateScale(0) * NMat.CreateTranslation(m.Translation);
            _pal[i] = X(m);
        }
        return _pal;
    }

    public static Matrix X(NMat m) => new(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);

    /// <summary>Dibuja lo que muestra <paramref name="mask"/>: el cuerpo, el arma si la tiene y los muñones de lo cortado.</summary>
    public void Draw(GraphicsDevice gd, RenderMask mask)
    {
        gd.SetVertexBuffer(Vb);
        gd.Indices = Ib;
        bool onlyWeapon = mask?.OnlyWeapon == true;
        if (!onlyWeapon && Main.tris > 0) gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, Main.start, Main.tris);
        if ((mask == null || !mask.NoWeapon) && Weapon.tris > 0) gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, Weapon.start, Weapon.tris);
        if (mask == null || onlyWeapon) return;
        for (int b = 0; b < Caps.Length; b++)
            if (mask.Cut[b] && Caps[b].tris > 0) gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, Caps[b].start, Caps[b].tris);
    }

    public void Dispose()
    {
        Vb?.Dispose();
        Ib?.Dispose();
    }
}
