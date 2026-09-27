using Jaqueca.Client.Render;
using Microsoft.Xna.Framework;

namespace Jaqueca.Client.World;

/// <summary>
/// Las piezas con que se arman los lugares: cajas que se ven y frenan (con la oclusión horneada al pie:
/// lo que toca el piso se oscurece abajo), tornos (patas de mesa, la lámpara, un florero), tablas y
/// cuadros. Todo va a un <see cref="MeshBuilder"/> (lo que se ve) y a <see cref="Solids"/> (lo que frena).
/// </summary>
public sealed class Kit
{
    public readonly MeshBuilder B = new();
    public readonly Solids S;

    public Kit(Solids solids) { S = solids; }

    /// <summary>Oclusión de un punto: más oscuro cuanto más cerca del piso (lo que está al pie de algo).</summary>
    private static float Ao(float y, float floor) => MathHelper.Lerp(0.62f, 1f, Math.Clamp((y - floor) / 9f, 0, 1));

    private void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, float mat, float floor, float edge = 1)
    {
        var n = Vector3.Cross(b - a, c - a);
        if (n.LengthSquared() < 1e-10f) return;
        n.Normalize();
        B.Material = mat;
        B.Edge = edge;
        bool vertical = MathF.Abs(n.Y) < 0.5f;
        int ia = B.Vertex(a, n, col, vertical ? Ao(a.Y, floor) : 1);
        int ib = B.Vertex(b, n, col, vertical ? Ao(b.Y, floor) : 1);
        int ic = B.Vertex(c, n, col, vertical ? Ao(c.Y, floor) : 1);
        int id = B.Vertex(d, n, col, vertical ? Ao(d.Y, floor) : 1);
        B.Index(ia, ib, ic);
        B.Index(ia, ic, id);
    }

    /// <summary>
    /// Una caja entre <paramref name="min"/> y <paramref name="max"/>: se ve (con otro color y material en
    /// la tapa, si se pide) y, si <paramref name="solid"/>, frena. <paramref name="floor"/>: la altura del
    /// piso donde apoya (para oscurecer el pie).
    /// </summary>
    public void Block(Vector3 min, Vector3 max, Color col, float mat, byte stuff = 0, Color? top = null, float topMat = -1, bool solid = true, float floor = 0, bool bottom = false)
    {
        var t = top ?? col;
        float tm = topMat < 0 ? mat : topMat;
        Vector3 P(float x, float y, float z) => new(x == 0 ? min.X : max.X, y == 0 ? min.Y : max.Y, z == 0 ? min.Z : max.Z);
        Face(P(0, 1, 0), P(0, 1, 1), P(1, 1, 1), P(1, 1, 0), t, tm, floor);          // arriba
        if (bottom) Face(P(0, 0, 1), P(0, 0, 0), P(1, 0, 0), P(1, 0, 1), col, mat, floor);
        Face(P(0, 0, 1), P(1, 0, 1), P(1, 1, 1), P(0, 1, 1), col, mat, floor);        // sur (+Z)
        Face(P(1, 0, 0), P(0, 0, 0), P(0, 1, 0), P(1, 1, 0), col, mat, floor);        // norte
        Face(P(1, 0, 1), P(1, 0, 0), P(1, 1, 0), P(1, 1, 1), col, mat, floor);        // este
        Face(P(0, 0, 0), P(0, 0, 1), P(0, 1, 1), P(0, 1, 0), col, mat, floor);        // oeste
        if (solid) S.Box(min, max, stuff);
    }

    /// <summary>Una rampa que sube hacia <paramref name="dir"/> (1 +X, 2 −X, 3 +Z, 4 −Z): se ve y se pisa.</summary>
    public void Ramp(Vector3 min, Vector3 max, byte dir, Color col, float mat, byte stuff = 0)
    {
        var s = new Solid { Min = min, Max = max, Ramp = dir, Stuff = stuff };
        float Y(float x, float z) => s.TopAt(x, z);
        var a = new Vector3(min.X, Y(min.X, min.Z), min.Z);
        var b = new Vector3(min.X, Y(min.X, max.Z), max.Z);
        var c = new Vector3(max.X, Y(max.X, max.Z), max.Z);
        var d = new Vector3(max.X, Y(max.X, min.Z), min.Z);
        Face(a, b, c, d, col, mat, min.Y);
        // Los costados: del piso a la tapa (triángulos donde la tapa baja a cero).
        void Side(Vector3 p, Vector3 q)
        {
            var p0 = new Vector3(p.X, min.Y, p.Z); var q0 = new Vector3(q.X, min.Y, q.Z);
            if (p.Y - min.Y < 0.01f && q.Y - min.Y < 0.01f) return;
            Face(p0, q0, q, p, col, mat, min.Y);
        }
        Side(b, a); Side(c, b); Side(d, c); Side(a, d);
        S.Add(s);
    }

    /// <summary>
    /// Un torno: un perfil (altura, radio) que gira alrededor de un eje vertical en (x, z), con
    /// <paramref name="sides"/> caras (patas torneadas, la lámpara de pie, un florero).
    /// </summary>
    public void Lathe(float x, float z, float y0, (float y, float r)[] profile, Color col, float mat, int sides = 8, bool solid = false, byte stuff = 0, float floor = 0)
    {
        B.Material = mat;
        B.Edge = 1;
        for (int k = 0; k + 1 < profile.Length; k++)
        {
            var (ya, ra) = profile[k];
            var (yb, rb) = profile[k + 1];
            float slope = (ra - rb) / MathF.Max(0.01f, yb - ya);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * MathF.Tau / sides, a1 = (i + 1) * MathF.Tau / sides;
                var d0 = new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0));
                var d1 = new Vector3(MathF.Cos(a1), 0, MathF.Sin(a1));
                var n0 = Vector3.Normalize(d0 + Vector3.UnitY * slope);
                var n1 = Vector3.Normalize(d1 + Vector3.UnitY * slope);
                var p0 = new Vector3(x, y0 + ya, z) + d0 * ra;
                var p1 = new Vector3(x, y0 + ya, z) + d1 * ra;
                var q1 = new Vector3(x, y0 + yb, z) + d1 * rb;
                var q0 = new Vector3(x, y0 + yb, z) + d0 * rb;
                int i0 = B.Vertex(p0, n0, col, Ao(p0.Y, floor)), i1 = B.Vertex(p1, n1, col, Ao(p1.Y, floor));
                int j1 = B.Vertex(q1, n1, col, Ao(q1.Y, floor)), j0 = B.Vertex(q0, n0, col, Ao(q0.Y, floor));
                B.Index(i0, j0, j1);
                B.Index(i0, j1, i1);
            }
        }
        // La tapa de arriba.
        var (yt, rt) = profile[^1];
        if (rt > 0.05f)
        {
            int c = B.Vertex(new Vector3(x, y0 + yt, z), Vector3.UnitY, col);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * MathF.Tau / sides, a1 = (i + 1) * MathF.Tau / sides;
                int i0 = B.Vertex(new Vector3(x + MathF.Cos(a0) * rt, y0 + yt, z + MathF.Sin(a0) * rt), Vector3.UnitY, col);
                int i1 = B.Vertex(new Vector3(x + MathF.Cos(a1) * rt, y0 + yt, z + MathF.Sin(a1) * rt), Vector3.UnitY, col);
                B.Index(c, i1, i0);
            }
        }
        if (solid)
        {
            float rmax = 0;
            foreach (var (_, r) in profile) rmax = MathF.Max(rmax, r);
            S.Box(new Vector3(x - rmax * 0.8f, y0, z - rmax * 0.8f), new Vector3(x + rmax * 0.8f, y0 + yt, z + rmax * 0.8f), stuff);
        }
    }

    /// <summary>Un bulto redondo (un elipsoide de radios <paramref name="r"/>) que se ve pero no frena: la carne, un almohadón.</summary>
    public void Blob(Vector3 c, Vector3 r, Color col, float mat, int seg = 10)
    {
        B.Material = mat;
        B.Edge = 1;
        int rings = seg / 2 + 1;
        var idx = new int[rings + 1, seg + 1];
        for (int i = 0; i <= rings; i++)
        {
            float th = MathF.PI * i / rings;
            for (int j = 0; j <= seg; j++)
            {
                float ph = MathF.Tau * j / seg;
                var d = new Vector3(MathF.Sin(th) * MathF.Cos(ph), MathF.Cos(th), MathF.Sin(th) * MathF.Sin(ph));
                var n = Vector3.Normalize(d / r);
                idx[i, j] = B.Vertex(c + d * r, n, col);
            }
        }
        for (int i = 0; i < rings; i++)
        for (int j = 0; j < seg; j++)
        {
            B.Index(idx[i, j], idx[i + 1, j + 1], idx[i + 1, j]);
            B.Index(idx[i, j], idx[i, j + 1], idx[i + 1, j + 1]);
        }
    }

    /// <summary>Un cuadrado plano pegado a una pared (un cuadro, la pantalla del televisor): sin volumen, sin contorno propio.</summary>
    public void Panel(Vector3 center, Vector3 right, Vector3 up, float w, float h, Color col, float mat = 0, float emissive = 0)
    {
        var n = Vector3.Normalize(Vector3.Cross(right, up));
        var r = right * (w / 2); var u = up * (h / 2);
        var c = new Color(col.R, col.G, col.B, (byte)(255 * (1 - emissive)));
        Face(center - r - u, center + r - u, center + r + u, center - r + u, c, mat, -100, 0.5f);
    }
}
