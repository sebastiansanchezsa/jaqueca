using Jaqueca.Client.Render;
using Jaqueca.Client.World;
using Jaqueca.Figures.Anim;
using Jaqueca.Figures.Physics;
using Jaqueca.Figures.Rig;
using Jaqueca.Look;
using Microsoft.Xna.Framework;
using NVec2 = System.Numerics.Vector2;
using NVec3 = System.Numerics.Vector3;

namespace Jaqueca.Client.Game;

/// <summary>
/// Un pensamiento intrusivo en el living: su cuerpo lo anima el <see cref="Animator"/> del motor (la
/// marcha con los pies clavados al piso, los golpes, el muñeco de trapo al morir, los cortes) y lo
/// dibuja su malla (ver <see cref="FoeArt"/>). Lo que piensa está en <see cref="Brain"/>.
/// </summary>
public sealed partial class Foe
{
    public readonly FoeKind Kind;
    public readonly Animator Anim;
    public readonly FoeArt.Kind Art;
    public readonly Matrix[] Pal = new Matrix[FigureMesh.MaxBones];
    private readonly Solids _world;

    /// <summary>Los pies en el mundo (la simulación; el cuerpo que se ve lo sigue).</summary>
    public Vector3 Pos;
    public Vector3 Vel;
    /// <summary>Hacia dónde quiere mirar.</summary>
    public float Yaw;
    public float Hp, MaxHp;
    /// <summary>El destello blanco del golpe (1 recién golpeado, baja solo).</summary>
    public float Flash;
    public bool Dead => Anim.Dead;
    /// <summary>Segundos desde que murió (para no seguir gastando en lo que ya está quieto).</summary>
    public float DeadTime;
    /// <summary>Tocando el piso (salta a los muebles; en el aire no camina).</summary>
    public bool Grounded = true;
    /// <summary>La altura de referencia para buscar el piso (lo de más arriba es techo o mueble).</summary>
    private float _refY;
    public float Radius => Kind == FoeKind.Neighbor ? 4.2f : 3.2f;
    public float Height => Kind == FoeKind.Neighbor ? 17.5f : 16f;

    public Foe(FoeKind kind, FoeArt art, Solids world, Vector3 at, float yaw, int seed)
    {
        Kind = kind;
        Art = art.Of(kind);
        _world = world;
        Pos = at;
        Yaw = yaw;
        _refY = at.Y + 3;
        MaxHp = Hp = kind == FoeKind.Neighbor ? 5 : 3;
        Anim = new Animator(Art.Skel, new NVec2(at.X, at.Z), yaw, seed)
        {
            Ground = GroundAt,
            Walls = world.Walls,
            MainHand = kind == FoeKind.Neighbor ? WeaponFamily.Dagger : WeaponFamily.None,
            BladeTip = Figures.Content.Thoughts.DrillTip,
            Hunch = kind == FoeKind.Neighbor ? 0.25f : 0,
            Convulse = 1.2f,
        };
        _rng = new Random(seed);
    }

    private float GroundAt(float x, float z) => _world.FloorBelow(x, z, _refY);

    /// <summary>Un punto de un hueso en el mundo (la cabeza, la mano del taladro).</summary>
    public Vector3 Point(Bone b, NVec3 local)
    {
        var p = Anim.WorldPoint(b, local);
        return new Vector3(p.X, p.Y, p.Z);
    }

    /// <summary>El centro del pecho (a donde apunta el que le tira).</summary>
    public Vector3 Chest => Point(Bone.Spine, new NVec3(0, 1.8f, 0));
    public Vector3 HeadCenter => Point(Bone.Head, Art.Dims.HeadC * Art.Scale);

    /// <summary>Avanza el cuerpo: la simulación ya movió <see cref="Pos"/>; el animador lo sigue.</summary>
    public void Animate(float dt)
    {
        Flash = MathF.Max(0, Flash - dt * 6);
        if (Dead)
        {
            DeadTime += dt;
            // Muerto: el piso se busca desde donde está el cuerpo (si cae de la mesa, cae al piso).
            var pelvis = Anim.WorldPoint(Bone.Pelvis, NVec3.Zero);
            _refY = pelvis.Y + 3;
            if (Anim.Still && DeadTime > 3) return;
        }
        else _refY = Pos.Y + 3;
        Anim.Update(new NVec2(Pos.X, Pos.Z), new NVec2(Vel.X, Vel.Z), Yaw, dt);
    }

    /// <summary>Lo que se dibuja en este cuadro (los huesos al mundo, lo cortado oculto, el destello).</summary>
    public FigureDraw Draw()
    {
        var world = Anim.WorldMatrix;
        // Apareciendo: crece desde el piso.
        if (Spawning)
        {
            float s = SpawnT * SpawnT * (3 - 2 * SpawnT);
            world = System.Numerics.Matrix4x4.CreateScale(MathF.Max(0.02f, s)) * world;
        }
        var bones = Art.Mesh.Pose(Anim.Bones, world, Anim.Mask);
        Array.Copy(bones, Pal, Pal.Length);
        return new FigureDraw { Mesh = Art.Mesh, Bones = Pal, Mask = Anim.Mask, Flash = new Vector4(1, 0.95f, 0.9f, Flash * 0.85f), CastsShadow = true };
    }

    private readonly Random _rng;
}

/// <summary>
/// Un pedazo que se cortó (un brazo, una cabeza, el taladro que se cayó): su física es la del motor
/// (<see cref="Gib"/>) y se dibuja con la malla de su dueño, mostrando sólo lo suyo.
/// </summary>
public sealed class Piece
{
    public readonly Gib Gib;
    public readonly FigureMesh Mesh;
    public readonly Matrix[] Pal = new Matrix[FigureMesh.MaxBones];
    private readonly Solids _world;
    private float _refY;
    /// <summary>Cuánto sangra todavía por el corte (segundos).</summary>
    public float Bleed = 1.6f;
    public bool Landed;

    public Piece(Gib gib, FigureMesh mesh, Solids world, float y)
    {
        Gib = gib;
        Mesh = mesh;
        _world = world;
        _refY = y;
        gib.Body.Body.Ground = (x, z) => _world.FloorBelow(x, z, _refY);
        gib.Body.Body.Walls = world.Walls;
    }

    public void Step(float dt)
    {
        var c = Gib.Body.Center;
        _refY = c.Y + 2;
        int n = Math.Max(1, (int)MathF.Ceiling(dt / Animator.Step));
        for (int i = 0; i < n; i++) Gib.Step(dt / n);
        Bleed = MathF.Max(0, Bleed - dt);
    }

    public FigureDraw Draw()
    {
        var w = System.Numerics.Matrix4x4.CreateRotationY(-Gib.Yaw) * System.Numerics.Matrix4x4.CreateTranslation(Gib.Root);
        var bones = Mesh.Pose(Gib.Bones, w, Gib.Mask);
        Array.Copy(bones, Pal, Pal.Length);
        return new FigureDraw { Mesh = Mesh, Bones = Pal, Mask = Gib.Mask, CastsShadow = true };
    }
}
