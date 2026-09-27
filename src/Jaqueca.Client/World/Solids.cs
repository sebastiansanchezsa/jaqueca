using Microsoft.Xna.Framework;

namespace Jaqueca.Client.World;

/// <summary>
/// Un sólido del lugar: una caja alineada con los ejes, o una rampa (la misma caja con la tapa
/// inclinada: sube hacia un lado). Es lo que frena, lo que se pisa y contra lo que pegan las balas.
/// </summary>
public struct Solid
{
    public Vector3 Min, Max;
    /// <summary>0 caja; rampa que sube hacia 1 +X, 2 −X, 3 +Z, 4 −Z.</summary>
    public byte Ramp;
    /// <summary>De qué es (para el sonido y las chispas del tiro): 0 madera, 1 yeso, 2 tela, 3 carne.</summary>
    public byte Stuff;

    /// <summary>La altura de la tapa en (x, z) (en una caja, la de arriba).</summary>
    public float TopAt(float x, float z)
    {
        if (Ramp == 0) return Max.Y;
        float t = Ramp switch
        {
            1 => (x - Min.X) / (Max.X - Min.X),
            2 => (Max.X - x) / (Max.X - Min.X),
            3 => (z - Min.Z) / (Max.Z - Min.Z),
            _ => (Max.Z - z) / (Max.Z - Min.Z),
        };
        return Min.Y + (Max.Y - Min.Y) * Math.Clamp(t, 0, 1);
    }
}

/// <summary>
/// Las colisiones del lugar: sólidos en una grilla (cada celda sabe qué sólidos la tocan), así los
/// cuerpos, los pedazos y los tiros sólo miran lo que tienen cerca. Los cuerpos son cilindros parados
/// (los pies, un radio y una altura): se deslizan por las paredes, suben escalones bajos, pisan las
/// tapas y las rampas y chocan la cabeza con lo de arriba.
/// </summary>
public sealed class Solids
{
    public const float Cell = 24;
    public readonly List<Solid> All = new();
    /// <summary>Los bordes del lugar (x, z): nada sale de acá (ni un cuerpo, ni un pedazo).</summary>
    public Rectangle Bounds;
    private List<int>[] _grid;
    private int _gw, _gh;
    private float _gx, _gz;
    private int[] _seen = Array.Empty<int>();
    private int _stamp;
    private readonly List<int> _near = new();

    public void Add(Solid s) => All.Add(s);

    public void Box(Vector3 min, Vector3 max, byte stuff = 0) => All.Add(new Solid { Min = min, Max = max, Stuff = stuff });

    /// <summary>Arma la grilla (después de agregar todo).</summary>
    public void Build()
    {
        _gx = Bounds.Left - Cell; _gz = Bounds.Top - Cell;
        _gw = (int)MathF.Ceiling((Bounds.Width + 2 * Cell) / Cell) + 1;
        _gh = (int)MathF.Ceiling((Bounds.Height + 2 * Cell) / Cell) + 1;
        _grid = new List<int>[_gw * _gh];
        for (int i = 0; i < _grid.Length; i++) _grid[i] = new List<int>();
        for (int i = 0; i < All.Count; i++)
        {
            var s = All[i];
            int x0 = CellX(s.Min.X), x1 = CellX(s.Max.X), z0 = CellZ(s.Min.Z), z1 = CellZ(s.Max.Z);
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++) _grid[z * _gw + x].Add(i);
        }
        _seen = new int[All.Count];
    }

    private int CellX(float x) => Math.Clamp((int)MathF.Floor((x - _gx) / Cell), 0, _gw - 1);
    private int CellZ(float z) => Math.Clamp((int)MathF.Floor((z - _gz) / Cell), 0, _gh - 1);

    /// <summary>Los sólidos que tocan el rectángulo (x, z) dado (en <see cref="_near"/>; sin repetir).</summary>
    private List<int> Near(float x0, float z0, float x1, float z1)
    {
        _near.Clear();
        _stamp++;
        if (_stamp == int.MaxValue) { Array.Clear(_seen); _stamp = 1; }
        int cx0 = CellX(x0), cx1 = CellX(x1), cz0 = CellZ(z0), cz1 = CellZ(z1);
        for (int z = cz0; z <= cz1; z++)
        for (int x = cx0; x <= cx1; x++)
            foreach (int i in _grid[z * _gw + x])
            {
                if (_seen[i] == _stamp) continue;
                _seen[i] = _stamp;
                _near.Add(i);
            }
        return _near;
    }

    private static Vector2 Closest(in Solid s, float x, float z) => new(Math.Clamp(x, s.Min.X, s.Max.X), Math.Clamp(z, s.Min.Z, s.Max.Z));

    /// <summary>
    /// La tapa más alta bajo un círculo de radio <paramref name="r"/> en (x, z) que no pase de
    /// <paramref name="below"/> (lo que está arriba de eso es un techo, no un piso). -1000 si no hay nada.
    /// </summary>
    public float GroundAt(float x, float z, float below, float r = 0)
    {
        float best = -1000;
        foreach (int i in Near(x - r, z - r, x + r, z + r))
        {
            var s = All[i];
            var c = Closest(s, x, z);
            if (Vector2.DistanceSquared(c, new Vector2(x, z)) > r * r + 1e-4f) continue;
            float top = s.TopAt(c.X, c.Y);
            if (top <= below && top > best) best = top;
        }
        return best;
    }

    /// <summary>
    /// Mueve un cilindro parado (pies en <paramref name="feet"/>) por <paramref name="delta"/>: de a pasos
    /// cortos se desliza contra lo que frena y sube lo que es más bajo que <paramref name="stepUp"/>. Dice
    /// si terminó apoyado, si pegó la cabeza y contra qué pared se apoyó (la normal, o cero).
    /// </summary>
    public Vector3 Move(Vector3 feet, float radius, float height, Vector3 delta, float stepUp, bool wasGrounded, out bool grounded, out bool bonk, out Vector3 wall)
    {
        wall = Vector3.Zero;
        bonk = false;
        var p = feet;
        var hd = new Vector2(delta.X, delta.Z);
        int steps = Math.Max(1, (int)MathF.Ceiling(hd.Length() / (radius * 0.45f)));
        var step = hd / steps;
        for (int k = 0; k < steps; k++)
        {
            p.X += step.X; p.Z += step.Y;
            Push(ref p, radius, height, stepUp, ref wall);
        }
        // Lo vertical: el piso (lo que queda debajo, hasta un escalón arriba de los pies) y el techo.
        float oldY = p.Y;
        p.Y += delta.Y;
        float reach = MathF.Max(oldY, p.Y) + (wasGrounded ? stepUp : 0.5f);
        float ground = GroundAt(p.X, p.Z, reach, radius * 0.7f);
        grounded = false;
        if (p.Y <= ground + 0.02f) { p.Y = ground; grounded = true; }
        // Bajando una rampa o un escalón, se queda pegado al piso (no salta de a pasitos).
        else if (wasGrounded && delta.Y <= 0 && p.Y - ground < stepUp) { p.Y = ground; grounded = true; }
        if (delta.Y > 0)
        {
            float head = p.Y + height;
            foreach (int i in Near(p.X - radius, p.Z - radius, p.X + radius, p.Z + radius))
            {
                var s = All[i];
                var c = Closest(s, p.X, p.Z);
                if (Vector2.DistanceSquared(c, new Vector2(p.X, p.Z)) > radius * radius * 0.5f) continue;
                if (s.Min.Y >= oldY + height - 0.01f && s.Min.Y < head)
                {
                    p.Y = s.Min.Y - height;
                    bonk = true;
                    head = p.Y + height;
                }
            }
        }
        Clamp(ref p, radius);
        return p;
    }

    /// <summary>Saca el cilindro de lo que frena (de costado), anotando contra qué pared quedó.</summary>
    private void Push(ref Vector3 p, float radius, float height, float stepUp, ref Vector3 wall)
    {
        for (int pass = 0; pass < 2; pass++)
        foreach (int i in Near(p.X - radius, p.Z - radius, p.X + radius, p.Z + radius))
        {
            var s = All[i];
            if (s.Min.Y >= p.Y + height - 0.01f) continue;
            var c = Closest(s, p.X, p.Z);
            float top = s.TopAt(c.X, c.Y);
            if (top <= p.Y + stepUp) continue;
            var d = new Vector2(p.X - c.X, p.Z - c.Y);
            float dist = d.Length();
            if (dist >= radius) continue;
            Vector2 n;
            if (dist > 1e-4f) n = d / dist;
            else
            {
                // Adentro: sale por el lado más cercano.
                float l = p.X - s.Min.X, r = s.Max.X - p.X, b = p.Z - s.Min.Z, f = s.Max.Z - p.Z;
                float m = MathF.Min(MathF.Min(l, r), MathF.Min(b, f));
                n = m == l ? -Vector2.UnitX : m == r ? Vector2.UnitX : m == b ? -Vector2.UnitY : Vector2.UnitY;
                dist = -m;
            }
            p.X += n.X * (radius - dist);
            p.Z += n.Y * (radius - dist);
            wall = new Vector3(n.X, 0, n.Y);
        }
    }

    private void Clamp(ref Vector3 p, float r)
    {
        p.X = Math.Clamp(p.X, Bounds.Left + r, Bounds.Right - r);
        p.Z = Math.Clamp(p.Z, Bounds.Top + r, Bounds.Bottom - r);
    }

    /// <summary>¿Hay una pared pegada (a menos de <paramref name="gap"/>) al cilindro? La normal que sale de ella.</summary>
    public bool WallNear(Vector3 feet, float radius, float height, float gap, out Vector3 normal)
    {
        normal = Vector3.Zero;
        float best = float.MaxValue;
        float r = radius + gap;
        foreach (int i in Near(feet.X - r, feet.Z - r, feet.X + r, feet.Z + r))
        {
            var s = All[i];
            if (s.Min.Y >= feet.Y + height || s.TopAt(Math.Clamp(feet.X, s.Min.X, s.Max.X), Math.Clamp(feet.Z, s.Min.Z, s.Max.Z)) <= feet.Y + 2) continue;
            var c = Closest(s, feet.X, feet.Z);
            var d = new Vector2(feet.X - c.X, feet.Z - c.Y);
            float dist = d.Length();
            if (dist < r && dist < best && dist > 1e-4f)
            {
                best = dist;
                normal = new Vector3(d.X / dist, 0, d.Y / dist);
            }
        }
        // Los bordes del lugar también son pared.
        if (feet.X - Bounds.Left < r) { normal = Vector3.UnitX; best = 0; }
        else if (Bounds.Right - feet.X < r) { normal = -Vector3.UnitX; best = 0; }
        else if (feet.Z - Bounds.Top < r) { normal = Vector3.UnitZ; best = 0; }
        else if (Bounds.Bottom - feet.Z < r) { normal = -Vector3.UnitZ; best = 0; }
        return best < float.MaxValue;
    }

    /// <summary>
    /// El primer sólido que corta el rayo (<paramref name="o"/> + t·<paramref name="d"/>, d de largo 1)
    /// antes de <paramref name="maxT"/>: dónde, con qué normal y de qué es.
    /// </summary>
    public bool Raycast(Vector3 o, Vector3 d, float maxT, out float t, out Vector3 normal, out byte stuff)
    {
        t = maxT; normal = Vector3.Zero; stuff = 0;
        bool hit = false;
        // Se recorre la grilla a lo largo del rayo, de a tramos del tamaño de una celda.
        float walked = 0;
        while (walked < t)
        {
            float seg = MathF.Min(Cell, t - walked);
            var a = o + d * walked;
            var b = o + d * (walked + seg);
            foreach (int i in Near(MathF.Min(a.X, b.X), MathF.Min(a.Z, b.Z), MathF.Max(a.X, b.X), MathF.Max(a.Z, b.Z)))
            {
                var s = All[i];
                if (RayBox(o, d, s, out float ti, out var ni) && ti < t && ti >= 0)
                {
                    t = ti; normal = ni; stuff = s.Stuff; hit = true;
                }
            }
            if (hit) break;
            walked += seg;
        }
        return hit;
    }

    private static bool RayBox(Vector3 o, Vector3 d, in Solid s, out float t, out Vector3 n)
    {
        t = 0; n = Vector3.Zero;
        float t0 = float.NegativeInfinity, t1 = float.PositiveInfinity;
        var n0 = Vector3.Zero;
        for (int ax = 0; ax < 3; ax++)
        {
            float oo = ax == 0 ? o.X : ax == 1 ? o.Y : o.Z;
            float dd = ax == 0 ? d.X : ax == 1 ? d.Y : d.Z;
            float lo = ax == 0 ? s.Min.X : ax == 1 ? s.Min.Y : s.Min.Z;
            float hi = ax == 0 ? s.Max.X : ax == 1 ? s.Max.Y : s.Max.Z;
            var axis = ax == 0 ? Vector3.UnitX : ax == 1 ? Vector3.UnitY : Vector3.UnitZ;
            if (MathF.Abs(dd) < 1e-9f)
            {
                if (oo < lo || oo > hi) return false;
                continue;
            }
            float ta = (lo - oo) / dd, tb = (hi - oo) / dd;
            var na = -axis;
            if (ta > tb) { (ta, tb) = (tb, ta); na = axis; }
            if (ta > t0) { t0 = ta; n0 = na; }
            if (tb < t1) t1 = tb;
            if (t0 > t1) return false;
        }
        if (t1 < 0) return false;
        t = MathF.Max(t0, 0);
        n = n0;
        if (s.Ramp == 0) return true;
        // Rampa: lo que cae por encima de la tapa inclinada no la toca; se busca dónde corta la tapa.
        var p = o + d * t;
        if (p.Y <= s.TopAt(p.X, p.Z) + 0.01f) return true;
        var up = s.Ramp switch
        {
            1 => new Vector3(-(s.Max.Y - s.Min.Y), s.Max.X - s.Min.X, 0),
            2 => new Vector3(s.Max.Y - s.Min.Y, s.Max.X - s.Min.X, 0),
            3 => new Vector3(0, s.Max.Z - s.Min.Z, -(s.Max.Y - s.Min.Y)),
            _ => new Vector3(0, s.Max.Z - s.Min.Z, s.Max.Y - s.Min.Y),
        };
        up.Normalize();
        var onPlane = new Vector3(s.Ramp is 1 or 2 ? (s.Ramp == 1 ? s.Min.X : s.Max.X) : 0, s.Min.Y, s.Ramp is 3 or 4 ? (s.Ramp == 3 ? s.Min.Z : s.Max.Z) : 0);
        float den = Vector3.Dot(up, d);
        if (MathF.Abs(den) < 1e-6f) return false;
        float tp = Vector3.Dot(up, onPlane - o) / den;
        if (tp < t || tp > t1) return false;
        t = tp; n = up;
        return true;
    }

    /// <summary>
    /// ¿El punto está adentro de un sólido? Con la normal de la cara más cercana y el punto sobre ella (para
    /// pegar una mancha donde entró una gota).
    /// </summary>
    public bool Inside(Vector3 p, out Vector3 normal, out Vector3 surface)
    {
        normal = Vector3.Zero; surface = p;
        foreach (int i in Near(p.X, p.Z, p.X, p.Z))
        {
            var s = All[i];
            if (p.X < s.Min.X || p.X > s.Max.X || p.Z < s.Min.Z || p.Z > s.Max.Z || p.Y < s.Min.Y) continue;
            float top = s.TopAt(p.X, p.Z);
            if (p.Y > top) continue;
            // La cara más cercana (arriba incluida).
            float best = top - p.Y;
            normal = Vector3.UnitY; surface = new Vector3(p.X, top, p.Z);
            float d;
            if ((d = p.X - s.Min.X) < best) { best = d; normal = -Vector3.UnitX; surface = new Vector3(s.Min.X, p.Y, p.Z); }
            if ((d = s.Max.X - p.X) < best) { best = d; normal = Vector3.UnitX; surface = new Vector3(s.Max.X, p.Y, p.Z); }
            if ((d = p.Z - s.Min.Z) < best) { best = d; normal = -Vector3.UnitZ; surface = new Vector3(p.X, p.Y, s.Min.Z); }
            if ((d = s.Max.Z - p.Z) < best) { best = d; normal = Vector3.UnitZ; surface = new Vector3(p.X, p.Y, s.Max.Z); }
            if ((d = p.Y - s.Min.Y) < best) { normal = -Vector3.UnitY; surface = new Vector3(p.X, s.Min.Y, p.Z); }
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ para los muñecos de trapo

    /// <summary>El piso bajo (x, z) para algo que está a la altura <paramref name="y"/> (lo de arriba no cuenta).</summary>
    public float FloorBelow(float x, float z, float y) => MathF.Max(GroundAt(x, z, y + 1.5f), 0);

    /// <summary>
    /// Un punto que se metió en un sólido de costado sale por el lado más cercano (y no se va del lugar):
    /// los cuerpos y los pedazos no atraviesan paredes ni muebles.
    /// </summary>
    public System.Numerics.Vector3 Walls(System.Numerics.Vector3 p, System.Numerics.Vector3 prev, float r)
    {
        var q = new Vector3(p.X, p.Y, p.Z);
        foreach (int i in Near(q.X - r, q.Z - r, q.X + r, q.Z + r))
        {
            var s = All[i];
            if (q.Y < s.Min.Y - r || q.Y > s.TopAt(Math.Clamp(q.X, s.Min.X, s.Max.X), Math.Clamp(q.Z, s.Min.Z, s.Max.Z)) - 0.6f) continue;
            // Si venía de arriba (cayó sobre la tapa), el piso se encarga.
            if (prev.Y >= s.TopAt(Math.Clamp(prev.X, s.Min.X, s.Max.X), Math.Clamp(prev.Z, s.Min.Z, s.Max.Z)) - 0.3f) continue;
            var c = Closest(s, q.X, q.Z);
            var d = new Vector2(q.X - c.X, q.Z - c.Y);
            float dist = d.Length();
            if (dist >= r) continue;
            if (dist > 1e-4f) { q.X = c.X + d.X / dist * r; q.Z = c.Y + d.Y / dist * r; }
            else
            {
                float l = q.X - s.Min.X, rr = s.Max.X - q.X, b = q.Z - s.Min.Z, f = s.Max.Z - q.Z;
                float m = MathF.Min(MathF.Min(l, rr), MathF.Min(b, f));
                if (m == l) q.X = s.Min.X - r; else if (m == rr) q.X = s.Max.X + r; else if (m == b) q.Z = s.Min.Z - r; else q.Z = s.Max.Z + r;
            }
        }
        Clamp(ref q, r);
        return new System.Numerics.Vector3(q.X, q.Y, q.Z);
    }
}
