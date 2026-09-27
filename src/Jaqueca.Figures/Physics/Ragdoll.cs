using System.Numerics;
using Jaqueca.Figures.Rig;

namespace Jaqueca.Figures.Physics;

/// <summary>
/// Cuerpo de partículas por Verlet: puntos con radio unidos por distancias fijas, con gravedad,
/// piso con fricción y amortiguación. Los enlaces con <c>Min</c> sólo impiden que dos puntos se
/// acerquen más de eso (topes de codos, rodillas y cuello). Cuando todo queda quieto un rato,
/// se duerme y deja de simularse.
/// </summary>
public sealed class VerletBody
{
    public readonly Vector3[] Pos, Prev;
    public readonly float[] Radius;
    public readonly List<(int a, int b, float len, bool min)> Links = new();
    public Func<float, float, float> Ground = (_, _) => 0;
    /// <summary>
    /// Los muros y lo construido (null = nada): recibe dónde está un punto, dónde estaba y su
    /// radio, y devuelve dónde puede estar (lo empuja afuera de un muro, o lo devuelve si se iba a
    /// caer al vacío). Así un cuerpo tirado de un golpe no atraviesa las paredes.
    /// </summary>
    public Func<Vector3, Vector3, float, Vector3> Walls;
    public float Gravity = 170, Damping = 0.996f, Friction = 0.55f, Bounce = 0.25f;
    public int Iterations = 8;
    public bool Asleep { get; private set; }
    private float _still, _impact, _h = 1 / 120f;

    /// <summary>El golpe más fuerte contra el piso desde la última vez que se preguntó (unidades por segundo): para el ruido de lo que cae.</summary>
    public float TakeImpact()
    {
        float v = _impact;
        _impact = 0;
        return v;
    }

    public VerletBody(int n)
    {
        Pos = new Vector3[n];
        Prev = new Vector3[n];
        Radius = new float[n];
    }

    /// <summary>Une dos puntos a la distancia que tienen ahora (o, con <paramref name="minFraction"/>, les pone un tope a esa fracción).</summary>
    public void Link(int a, int b, float minFraction = 0)
    {
        float d = Vector3.Distance(Pos[a], Pos[b]);
        Links.Add((a, b, minFraction > 0 ? d * minFraction : d, minFraction > 0));
    }

    /// <summary>Lo despierta (algo lo movió de afuera: un cuervo que se lo lleva).</summary>
    public void Wake()
    {
        Asleep = false;
        _still = 0;
    }

    public void Step(float h)
    {
        if (Asleep) return;
        _h = h;
        var g = new Vector3(0, -Gravity * h * h, 0);
        for (int i = 0; i < Pos.Length; i++)
        {
            var v = (Pos[i] - Prev[i]) * Damping;
            Prev[i] = Pos[i];
            Pos[i] += v + g;
        }
        for (int it = 0; it < Iterations; it++)
        {
            foreach (var (a, b, len, min) in Links)
            {
                var d = Pos[b] - Pos[a];
                float dist = d.Length();
                if (dist < 1e-5f || (min && dist >= len)) continue;
                var corr = d * (0.5f * (dist - len) / dist);
                Pos[a] += corr;
                Pos[b] -= corr;
            }
            Collide();
        }
        // Contra los muros: una vez por paso (lo que choca pierde la velocidad de costado).
        if (Walls != null)
        {
            for (int i = 0; i < Pos.Length; i++)
            {
                var c = Walls(Pos[i], Prev[i], Radius[i]);
                if (c.X == Pos[i].X && c.Z == Pos[i].Z) continue;
                Pos[i] = new Vector3(c.X, Pos[i].Y, c.Z);
                Prev[i] = new Vector3(c.X, Prev[i].Y, c.Z);
            }
            Collide();
        }
        // Se duerme cuando nada se movió en un rato.
        float moved = 0;
        for (int i = 0; i < Pos.Length; i++) moved = MathF.Max(moved, (Pos[i] - Prev[i]).LengthSquared());
        _still = moved < 0.02f * 0.02f ? _still + h : 0;
        if (_still > 0.5f) Asleep = true;
    }

    /// <summary>Contra el piso: no lo atraviesa, rebota un poco y el roce frena lo que se desliza.</summary>
    private void Collide()
    {
        for (int i = 0; i < Pos.Length; i++)
        {
            float floor = Ground(Pos[i].X, Pos[i].Z) + Radius[i];
            if (Pos[i].Y >= floor) continue;
            float vy = Pos[i].Y - Prev[i].Y;
            if (vy < 0) _impact = MathF.Max(_impact, -vy / _h);
            Pos[i].Y = floor;
            Prev[i].Y = floor + vy * Bounce;
            Prev[i].X += (Pos[i].X - Prev[i].X) * Friction;
            Prev[i].Z += (Pos[i].Z - Prev[i].Z) * Friction;
        }
    }

    /// <summary>Le suma una velocidad a un punto (unidades por segundo, con el paso <paramref name="h"/>).</summary>
    public void Push(int i, Vector3 velocity, float h) => Prev[i] -= velocity * h;
}

/// <summary>
/// Muñeco de trapo sobre los huesos del personaje (todo el cuerpo al morir, o sólo el pedazo
/// que se cortó). Pone partículas en las articulaciones, las une como el esqueleto (el torso y
/// la cabeza rígidos, brazos y piernas con topes para no doblarse del todo) y después arma la
/// matriz de cada hueso con sus partículas. Al empezar guarda la diferencia entre ese armado y el
/// hueso real: así pasar de la animación al muñeco no da ningún salto.
/// </summary>
public sealed class Ragdoll
{
    // Partículas: dónde van (hueso y punto en el hueso) y su radio.
    public enum P { Pelvis, Chest, HeadTop, Face, ShoulderR, ElbowR, WristR, GripR, ShoulderL, ElbowL, WristL, GripL, HipR, KneeR, AnkleR, ToeR, HipL, KneeL, AnkleL, ToeL, Tip, Count }

    /// <summary>Dónde va cada partícula (hueso, punto y radio) para un cuerpo con esas medidas: la cabeza sale de su cráneo.</summary>
    private static (Bone bone, Vector3 at, float r)[] Where(Dims d)
    {
        var top = d.HeadC + new Vector3(0, d.HeadR.Y * 0.35f, 0);
        var face = d.HeadC + new Vector3(d.HeadR.X * 0.75f, -d.HeadR.Y * 0.45f, 0);
        float l = d.Limb;
        return new (Bone, Vector3, float)[]
        {
            (Bone.Pelvis, Vector3.Zero, 0.95f * l), (Bone.Head, Vector3.Zero, 0.9f * l), (Bone.Head, top, d.HeadR.Y * 0.95f), (Bone.Head, face, d.HeadR.X * 0.55f),
            (Bone.UpperArmR, Vector3.Zero, 0.6f * l), (Bone.ForearmR, Vector3.Zero, 0.42f * l), (Bone.HandR, Vector3.Zero, 0.32f), (Bone.HandR, new(1.2f, -0.6f, 0), 0.32f),
            (Bone.UpperArmL, Vector3.Zero, 0.6f * l), (Bone.ForearmL, Vector3.Zero, 0.42f * l), (Bone.HandL, Vector3.Zero, 0.32f), (Bone.HandL, new(1.2f, -0.6f, 0), 0.32f),
            (Bone.ThighR, Vector3.Zero, 0.7f * l), (Bone.ShinR, Vector3.Zero, 0.48f * l), (Bone.FootR, Vector3.Zero, 0.38f), (Bone.FootR, new(1.6f, -0.4f, 0), 0.34f),
            (Bone.ThighL, Vector3.Zero, 0.7f * l), (Bone.ShinL, Vector3.Zero, 0.48f * l), (Bone.FootL, Vector3.Zero, 0.38f), (Bone.FootL, new(1.6f, -0.4f, 0), 0.34f),
            (Bone.HandR, Vector3.Zero, 0.3f), // la punta del arma (se ubica aparte)
        };
    }

    // Cada hueso se arma con: origen, un eje (origen → a) y una referencia (−1 = se transporta del paso anterior).
    private static readonly Dictionary<Bone, (P o, P a, P r)> Frames = new()
    {
        [Bone.Pelvis] = (P.Pelvis, P.Chest, P.HipR),
        [Bone.Spine] = (P.Pelvis, P.Chest, P.ShoulderR),
        [Bone.Head] = (P.Chest, P.HeadTop, P.Face),
        [Bone.UpperArmR] = (P.ShoulderR, P.ElbowR, P.Count), [Bone.ForearmR] = (P.ElbowR, P.WristR, P.Count), [Bone.HandR] = (P.WristR, P.GripR, P.Count),
        [Bone.UpperArmL] = (P.ShoulderL, P.ElbowL, P.Count), [Bone.ForearmL] = (P.ElbowL, P.WristL, P.Count), [Bone.HandL] = (P.WristL, P.GripL, P.Count),
        [Bone.ThighR] = (P.HipR, P.KneeR, P.Count), [Bone.ShinR] = (P.KneeR, P.AnkleR, P.Count), [Bone.FootR] = (P.AnkleR, P.ToeR, P.Count),
        [Bone.ThighL] = (P.HipL, P.KneeL, P.Count), [Bone.ShinL] = (P.KneeL, P.AnkleL, P.Count), [Bone.FootL] = (P.AnkleL, P.ToeL, P.Count),
    };

    public readonly VerletBody Body;
    private readonly int[] _index = new int[(int)P.Count];
    private readonly List<(Bone bone, int o, int a, int r)> _frames = new();
    private readonly List<Matrix4x4> _bind = new();
    private readonly List<Vector3> _ref = new();

    /// <summary>Los huesos que mueve este muñeco.</summary>
    public IEnumerable<Bone> Bones => _frames.Select(f => f.bone);

    /// <summary>
    /// Arma el muñeco con los huesos <paramref name="bones"/> tal como están ahora
    /// (<paramref name="world"/>) y como estaban un paso antes (<paramref name="before"/>, así
    /// sigue con el impulso que traía). <paramref name="weaponTip"/>: si sólo es el arma, dónde
    /// está su punta en el hueso de la mano (se simula como una barra rígida).
    /// </summary>
    public Ragdoll(IReadOnlyCollection<Bone> bones, Matrix4x4[] world, Matrix4x4[] before, Func<float, float, float> ground, Vector3? weaponTip = null, Dims dims = null)
    {
        var where = Where(dims ?? Dims.Of(Look.Build.Slim));
        Array.Fill(_index, -1);
        var used = new List<P>();
        void Use(P p) { if (_index[(int)p] < 0) { _index[(int)p] = used.Count; used.Add(p); } }
        bool weapon = weaponTip != null;
        if (weapon) { Use(P.WristR); Use(P.GripR); Use(P.Tip); }
        else
            foreach (var b in bones)
                if (Frames.TryGetValue(b, out var f)) { Use(f.o); Use(f.a); if (f.r != P.Count) Use(f.r); }

        Body = new VerletBody(used.Count) { Ground = ground };
        for (int i = 0; i < used.Count; i++)
        {
            var (bone, at, r) = where[(int)used[i]];
            var local = used[i] == P.Tip ? weaponTip.Value : used[i] == P.GripR && weapon ? new Vector3(0, -1.4f, 0) : at;
            Body.Pos[i] = Vector3.Transform(local, world[(int)bone]);
            Body.Prev[i] = Vector3.Transform(local, before[(int)bone]);
            Body.Radius[i] = r;
        }

        // Enlaces: todo lo que existe de cada grupo rígido, más los huesos y sus topes.
        void Rigid(params P[] group)
        {
            for (int i = 0; i < group.Length; i++)
            for (int j = i + 1; j < group.Length; j++)
                if (Has(group[i]) && Has(group[j])) Body.Link(I(group[i]), I(group[j]));
        }
        void Bone2(P a, P b) { if (Has(a) && Has(b)) Body.Link(I(a), I(b)); }
        void Stop(P a, P b, float f) { if (Has(a) && Has(b)) Body.Link(I(a), I(b), f); }
        if (weapon) Rigid(P.WristR, P.GripR, P.Tip);
        else
        {
            Rigid(P.Pelvis, P.Chest, P.ShoulderR, P.ShoulderL, P.HipR, P.HipL);
            Rigid(P.Chest, P.HeadTop, P.Face);
            Stop(P.HeadTop, P.ShoulderR, 0.85f);
            Stop(P.HeadTop, P.ShoulderL, 0.85f);
            Stop(P.HeadTop, P.Pelvis, 0.85f);
            foreach (var (s, e, w, g) in new[] { (P.ShoulderR, P.ElbowR, P.WristR, P.GripR), (P.ShoulderL, P.ElbowL, P.WristL, P.GripL), (P.HipR, P.KneeR, P.AnkleR, P.ToeR), (P.HipL, P.KneeL, P.AnkleL, P.ToeL) })
            {
                Bone2(s, e);
                Bone2(e, w);
                Rigid(e, w, g);
                Stop(s, w, 0.55f);
            }
        }

        foreach (var b in bones)
        {
            (P o, P a, P r) f;
            if (weapon) { if (b != Bone.HandR) continue; f = (P.GripR, P.Tip, P.WristR); }
            else if (!Frames.TryGetValue(b, out f)) continue;
            int r = f.r == P.Count ? -1 : I(f.r);
            _frames.Add((b, I(f.o), I(f.a), r));
            // Referencia inicial para los que se transportan: el costado del hueso real.
            var w = world[(int)b];
            _ref.Add(Vector3.Normalize(new Vector3(w.M31, w.M32, w.M33)));
            var frame = FrameOf(_frames.Count - 1);
            Matrix4x4.Invert(frame, out var inv);
            _bind.Add(w * inv);
        }
    }

    private bool Has(P p) => _index[(int)p] >= 0;
    private int I(P p) => _index[(int)p];

    /// <summary>Le da un empujón (unidades por segundo): más fuerte arriba, así el cuerpo se vence y no sólo se desliza.</summary>
    public void Push(Vector3 velocity, float h, float topBias = 0.6f)
    {
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var p in Body.Pos) { lo = MathF.Min(lo, p.Y); hi = MathF.Max(hi, p.Y); }
        for (int i = 0; i < Body.Pos.Length; i++)
        {
            float up = hi > lo ? (Body.Pos[i].Y - lo) / (hi - lo) : 1;
            Body.Push(i, velocity * (1 - topBias + topBias * 2 * up), h);
        }
    }

    /// <summary>Lo que se sacude en una convulsión: manos, pies y cabeza.</summary>
    private static readonly P[] Twitchy = { P.WristR, P.WristL, P.GripL, P.AnkleR, P.AnkleL, P.ToeR, P.ToeL, P.HeadTop, P.Face, P.ElbowL, P.KneeR };

    /// <summary>
    /// Una sacudida de convulsión: una de las puntas del cuerpo (elegida con <paramref name="pick"/>,
    /// 0..1) salta a <paramref name="speed"/> hacia un costado y un poco arriba (con
    /// <paramref name="a"/> y <paramref name="b"/> al azar). Despierta al muñeco si dormía.
    /// </summary>
    public void Jolt(float pick, float a, float b, float speed, float h)
    {
        int n = 0;
        foreach (var t in Twitchy) if (Has(t)) n++;
        if (n == 0) return;
        int k = Math.Min(n - 1, (int)(pick * n));
        foreach (var t in Twitchy)
        {
            if (!Has(t)) continue;
            if (k-- > 0) continue;
            float ang = a * MathF.Tau;
            var dir = Vector3.Normalize(new Vector3(MathF.Cos(ang), 0.6f + b, MathF.Sin(ang)));
            Body.Wake();
            Body.Push(I(t), dir * speed, h);
            return;
        }
    }

    /// <summary>Le da un giro (radianes por segundo alrededor de su centro), para los pedazos que salen volando.</summary>
    public void Spin(Vector3 axisSpeed, float h)
    {
        var c = Center;
        for (int i = 0; i < Body.Pos.Length; i++) Body.Push(i, Vector3.Cross(axisSpeed, Body.Pos[i] - c), h);
    }

    public Vector3 Center
    {
        get
        {
            var c = Vector3.Zero;
            foreach (var p in Body.Pos) c += p;
            return c / Body.Pos.Length;
        }
    }

    public void Step(float h) => Body.Step(h);

    /// <summary>Escribe las matrices de sus huesos (en el mundo).</summary>
    public void Write(Matrix4x4[] world)
    {
        for (int i = 0; i < _frames.Count; i++) world[(int)_frames[i].bone] = _bind[i] * FrameOf(i);
    }

    private Matrix4x4 FrameOf(int i)
    {
        var (_, o, a, r) = _frames[i];
        var pos = Body.Pos;
        var y = pos[a] - pos[o];
        y = y.LengthSquared() < 1e-8f ? Vector3.UnitY : Vector3.Normalize(y);
        var z = r >= 0 ? pos[r] - pos[o] : _ref[i];
        z -= y * Vector3.Dot(z, y);
        if (z.LengthSquared() < 1e-6f)
        {
            z = Vector3.Cross(y, MathF.Abs(y.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
        }
        z = Vector3.Normalize(z);
        _ref[i] = z;
        var x = Vector3.Cross(y, z);
        return new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, pos[o].X, pos[o].Y, pos[o].Z, 1);
    }
}
