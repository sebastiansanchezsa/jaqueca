using System.Numerics;
using Jaqueca.Figures.Render;
using Jaqueca.Figures.Rig;

namespace Jaqueca.Figures.Physics;

/// <summary>Qué es un pedazo: un miembro (con el arma si la tenía en la mano), el miembro sin el arma, o el arma sola.</summary>
public enum GibKind { Limb, NoWeapon, Weapon }

/// <summary>
/// Un pedazo cortado (o el arma que se cayó): su muñeco de trapo, qué se dibuja de la figura
/// del personaje (todo oculto salvo sus huesos, con el muñón en el corte) y sus huesos listos
/// para dibujar alrededor de su propio origen, que sigue al pedazo sobre el piso.
/// </summary>
public sealed class Gib
{
    public readonly Ragdoll Body;
    public readonly RenderMask Mask = new();
    public readonly GibKind Kind;
    /// <summary>Dónde se cortó (el hueso que arranca el pedazo).</summary>
    public readonly Bone At;
    /// <summary>Giro con que se dibuja (el del personaje al cortarse).</summary>
    public readonly float Yaw;
    public Vector3 Root { get; private set; }
    public readonly Matrix4x4[] Bones = new Matrix4x4[(int)Bone.Count];
    public float Age { get; private set; }
    public bool Asleep => Body.Body.Asleep;

    private readonly Matrix4x4[] _world = new Matrix4x4[(int)Bone.Count];
    private readonly List<(int bone, int parent, Matrix4x4 local)> _hang = new();
    private readonly Func<float, float, float> _ground;

    /// <summary>
    /// Arma el pedazo con los huesos <paramref name="bones"/> como están en el mundo ahora y un
    /// paso antes; <paramref name="charBones"/> son los del personaje (para lo que cuelga sin
    /// física, como el pelo de una cabeza cortada).
    /// </summary>
    public Gib(IReadOnlyList<Bone> bones, Bone at, GibKind kind, Matrix4x4[] world, Matrix4x4[] before, Matrix4x4[] charBones, float yaw, Vector3 bladeTip, Func<float, float, float> ground, Dims dims = null)
    {
        At = at;
        Kind = kind;
        Yaw = yaw;
        _ground = ground;
        Body = kind == GibKind.Weapon
            ? new Ragdoll(new[] { Bone.HandR }, world, before, ground, bladeTip)
            : new Ragdoll(bones, world, before, ground, dims: dims);
        Body.Body.Friction = 0.7f;
        Body.Body.Bounce = 0.35f;
        var moved = Body.Bones.ToHashSet();
        Array.Fill(Mask.Hidden, true);
        foreach (var b in bones)
        {
            Mask.Hidden[(int)b] = false;
            if (moved.Contains(b)) continue;
            // Lo que no tiene física (pelo, coleta) queda fijo a su padre, como estaba.
            int parent = (int)Skeleton.Parent[(int)b];
            Matrix4x4.Invert(charBones[parent], out var inv);
            _hang.Add(((int)b, parent, charBones[(int)b] * inv));
        }
        if (kind == GibKind.Weapon) Mask.OnlyWeapon = true;
        else
        {
            Mask.Cut[(int)at] = true;
            Mask.NoWeapon = kind == GibKind.NoWeapon;
        }
        Write();
    }

    public void Step(float h)
    {
        Age += h;
        if (Body.Body.Asleep) return;
        Body.Step(h);
        Write();
    }

    private int _anchor = -1;

    /// <summary>
    /// Lo lleva algo agarrado (un cuervo con las patas): la partícula más cercana al corte queda
    /// en <paramref name="grip"/>, moviéndose a <paramref name="vel"/>, y el resto cuelga con su
    /// física. Al dejar de llamarlo, cae con el impulso que traía.
    /// </summary>
    public void Carry(Vector3 grip, Vector3 vel, float h)
    {
        var b = Body.Body;
        b.Wake();
        if (_anchor < 0)
        {
            var cut = _world[(int)At].Translation;
            float best = float.MaxValue;
            for (int i = 0; i < b.Pos.Length; i++)
            {
                float d = Vector3.DistanceSquared(b.Pos[i], cut);
                if (d < best) { best = d; _anchor = i; }
            }
        }
        b.Pos[_anchor] = grip;
        b.Prev[_anchor] = grip - vel * h;
        Write();
    }

    /// <summary>El corte en el mundo y hacia dónde sale la sangre (el arma no sangra).</summary>
    public (Vector3 at, Vector3 dir)? Wound
    {
        get
        {
            if (Kind == GibKind.Weapon) return null;
            var m = _world[(int)At];
            var up = Vector3.Normalize(new Vector3(m.M21, m.M22, m.M23));
            // Los miembros cuelgan hacia −Y: el corte (hacia su padre) queda en +Y; la cabeza, abajo.
            return (m.Translation, At == Bone.Head ? -up : up);
        }
    }

    private void Write()
    {
        Body.Write(_world);
        var c = Body.Center;
        Root = new Vector3(c.X, _ground(c.X, c.Z), c.Z);
        var w = Matrix4x4.CreateRotationY(-Yaw) * Matrix4x4.CreateTranslation(Root);
        Matrix4x4.Invert(w, out var toChar);
        foreach (var b in Body.Bones) Bones[(int)b] = _world[(int)b] * toChar;
        foreach (var (b, parent, local) in _hang) Bones[b] = local * Bones[parent];
    }
}
