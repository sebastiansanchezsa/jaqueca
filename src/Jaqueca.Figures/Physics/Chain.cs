using System.Numerics;

namespace Jaqueca.Figures.Physics;

/// <summary>
/// Una cadena en el mundo (la del carcelero): puntos por Verlet unidos por tramos que no se
/// estiran pero sí se aflojan (la cadena cuelga y se amontona), con gravedad, roce contra el
/// piso (se arrastra) y contra los muros. Cualquiera de las dos puntas se puede fijar (la mano,
/// el pecho de alguien enganchado); el largo se puede cambiar en el momento (se suelta al tirar
/// el garfio, se recoge al arrastrar). La punta del final es pesada: el garfio.
/// </summary>
public sealed class Chain
{
    public Vector3[] Pos { get; private set; }
    public Vector3[] Prev { get; private set; }
    private float[] _inv;
    /// <summary>Largo total (cada tramo mide Length / (puntos - 1)).</summary>
    public float Length;
    /// <summary>Puntas fijas (null = sueltas).</summary>
    public Vector3? Start, End;
    public Func<float, float, float> Ground = (_, _) => 0;
    /// <summary>Los muros (ver <see cref="VerletBody.Walls"/>; null = ninguno).</summary>
    public Func<Vector3, Vector3, float, Vector3> Walls;
    public float Gravity = 170, Damping = 0.992f, Friction = 0.45f, Radius = 0.22f;
    public int Iterations = 14;
    /// <summary>Tiene el garfio en la punta del final (una cadena cortada lo pierde de un lado).</summary>
    public bool Hook = true;

    public int Count => Pos.Length;
    public float Segment => Length / (Pos.Length - 1);
    public Vector3 Head => Pos[0];
    public Vector3 Tail => Pos[^1];

    /// <summary>Una cadena de <paramref name="points"/> puntos colgando derecha hacia abajo desde <paramref name="from"/>.</summary>
    public Chain(int points, Vector3 from, float length, float hookWeight = 3)
    {
        Pos = new Vector3[points];
        Prev = new Vector3[points];
        _inv = new float[points];
        Length = length;
        for (int i = 0; i < points; i++)
        {
            Pos[i] = Prev[i] = from - Vector3.UnitY * (length * i / (points - 1));
            _inv[i] = 1;
        }
        _inv[^1] = 1 / hookWeight;
    }

    private Chain(Vector3[] pos, Vector3[] prev, float[] inv, float length)
    {
        Pos = pos;
        Prev = prev;
        _inv = inv;
        Length = length;
    }

    /// <summary>Le suma velocidad a un punto (unidades por segundo, con el paso <paramref name="h"/>).</summary>
    public void Push(int i, Vector3 velocity, float h) => Prev[i] -= velocity * h;

    /// <summary>Velocidad de un punto (unidades por segundo) con el paso <paramref name="h"/>.</summary>
    public Vector3 Velocity(int i, float h) => (Pos[i] - Prev[i]) / h;

    public void Step(float h)
    {
        var g = new Vector3(0, -Gravity * h * h, 0);
        for (int i = 0; i < Pos.Length; i++)
        {
            var v = (Pos[i] - Prev[i]) * Damping;
            Prev[i] = Pos[i];
            Pos[i] += v + g;
        }
        Pin();
        float seg = Segment;
        for (int it = 0; it < Iterations; it++)
        {
            for (int i = 0; i + 1 < Pos.Length; i++)
            {
                float wa = Weight(i), wb = Weight(i + 1), w = wa + wb;
                if (w <= 0) continue;
                var d = Pos[i + 1] - Pos[i];
                float dist = d.Length();
                if (dist <= seg || dist < 1e-5f) continue;
                var corr = d * ((dist - seg) / dist / w);
                Pos[i] += corr * wa;
                Pos[i + 1] -= corr * wb;
            }
            Pin();
            Floor();
        }
        if (Walls != null)
            for (int i = 0; i < Pos.Length; i++)
            {
                if (Weight(i) == 0) continue;
                var c = Walls(Pos[i], Prev[i], Radius);
                if (c.X == Pos[i].X && c.Z == Pos[i].Z) continue;
                Pos[i] = new Vector3(c.X, Pos[i].Y, c.Z);
                Prev[i] = new Vector3(c.X, Prev[i].Y, c.Z);
            }
    }

    private float Weight(int i) => (i == 0 && Start != null) || (i == Pos.Length - 1 && End != null) ? 0 : _inv[i];

    private void Pin()
    {
        if (Start is { } s) Pos[0] = s;
        if (End is { } e) Pos[^1] = e;
    }

    /// <summary>Contra el piso: no lo atraviesa y el roce frena lo que se arrastra.</summary>
    private void Floor()
    {
        for (int i = 0; i < Pos.Length; i++)
        {
            float floor = Ground(Pos[i].X, Pos[i].Z) + Radius;
            if (Pos[i].Y >= floor) continue;
            Pos[i].Y = floor;
            Prev[i].Y = MathF.Min(Prev[i].Y, floor);
            Prev[i].X += (Pos[i].X - Prev[i].X) * Friction * 0.25f;
            Prev[i].Z += (Pos[i].Z - Prev[i].Z) * Friction * 0.25f;
        }
    }

    /// <summary>
    /// La corta en el punto <paramref name="at"/>: ésta se queda con el tramo del principio (sin
    /// garfio) y devuelve el resto, suelto, con el garfio y el impulso que traía.
    /// </summary>
    public Chain Cut(int at)
    {
        at = Math.Clamp(at, 1, Pos.Length - 2);
        float seg = Segment;
        int n = Pos.Length - at;
        var rest = new Chain(Pos[at..], Prev[at..], _inv[at..], seg * (n - 1))
        {
            Ground = Ground, Walls = Walls, Hook = Hook, End = End, Gravity = Gravity, Friction = Friction, Radius = Radius,
        };
        Pos = Pos[..(at + 1)];
        Prev = Prev[..(at + 1)];
        _inv = _inv[..(at + 1)];
        _inv[^1] = 1;
        Length = seg * at;
        Hook = false;
        End = null;
        return rest;
    }

    /// <summary>El punto de la cadena más cercano a <paramref name="p"/> en el piso (x, z), y a qué distancia.</summary>
    public (int index, float dist) Nearest(Vector2 p)
    {
        int best = 0;
        float bd = float.MaxValue;
        for (int i = 0; i < Pos.Length; i++)
        {
            float d = Vector2.Distance(new Vector2(Pos[i].X, Pos[i].Z), p);
            if (d < bd) { bd = d; best = i; }
        }
        return (best, bd);
    }

    /// <summary>
    /// Eslabones a lo largo de la cadena, uno cada <paramref name="link"/> unidades: dónde está el
    /// centro, hacia dónde va y si está de canto (se alternan, como en una cadena de verdad).
    /// </summary>
    public IEnumerable<(Vector3 at, Vector3 dir, bool edge)> Links(float link)
    {
        float carry = link * 0.5f;
        int k = 0;
        for (int i = 0; i + 1 < Pos.Length; i++)
        {
            var a = Pos[i];
            var d = Pos[i + 1] - a;
            float len = d.Length();
            if (len < 1e-4f) continue;
            var dir = d / len;
            float s = carry;
            while (s < len)
            {
                yield return (a + dir * s, dir, k++ % 2 == 1);
                s += link;
            }
            carry = s - len;
        }
    }
}
