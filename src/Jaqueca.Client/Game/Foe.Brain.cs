using Jaqueca.Anim;
using Jaqueca.Audio;
using Jaqueca.Client.World;
using Jaqueca.Figures.Anim;
using Jaqueca.Figures.Rig;
using Microsoft.Xna.Framework;
using NVec3 = System.Numerics.Vector3;

namespace Jaqueca.Client.Game;

/// <summary>Lo que un pensamiento necesita saber del lugar y de la pelea.</summary>
public interface IArena
{
    Player Player { get; }
    Solids Solids { get; }
    Audio.Sfx Sfx { get; }
    IReadOnlyList<Foe> Foes { get; }
    /// <summary>La maestra tira una tiza.</summary>
    void ThrowChalk(Foe who, Vector3 from, Vector3 target, float speed);
    /// <summary>Un pensamiento le pega a Ernesto (el taladro).</summary>
    void FoeHits(Foe who, float damage, Vector3 at, Vector3 push);
}

/// <summary>
/// Lo que piensa un pensamiento. El vecino: persigue, se prepara (el taladro acelera: se lo oye venir),
/// embiste de golpe y, si pega, taladra; si Ernesto se sube a un mueble, salta atrás de él. La maestra:
/// guarda distancia dando vueltas, chista (se la oye antes de que tire) y tira tizas con puntería (le
/// apunta a donde Ernesto va a estar). Un golpe fuerte los frena; una patada o la escopeta los hace volar.
/// </summary>
public sealed partial class Foe
{
    public enum Mode : byte { Spawn, Chase, Windup, Lunge, Recover, Leap, Keep, Throw, Stagger, Flying }

    public Mode State = Mode.Spawn;
    public float T;
    /// <summary>Apareciendo (0..1): todavía no pelea ni recibe.</summary>
    public float SpawnT;
    public bool Spawning => State == Mode.Spawn;
    /// <summary>Cuánto daño le hizo el último disparo (la escopeta suma sus perdigones para empujar).</summary>
    public float ShotThisFrame;
    /// <summary>Cuánto sigue chorreando cada muñón (segundos por hueso cortado).</summary>
    public readonly float[] Bleeding = new float[(int)Bone.Count];
    /// <summary>Quieto (--quietos: para mirarlo).</summary>
    public bool Frozen;

    private float _cool, _stun, _orbit = 1, _throwAt;
    private bool _hitDone, _thrown;
    private Vector3 _lungeDir, _target;
    private Mixer.Voice _drill;
    private static readonly ClipDef Stab = ClipDefs.Get(ClipId.Stab1);

    public const float SpawnTime = 0.9f;

    /// <summary>Lo empujan (una patada, la escopeta, el golpe al piso): sale volando y queda aturdido.</summary>
    public void Knock(Vector3 push, float stun)
    {
        if (Dead || Spawning) return;
        Vel = push;
        Grounded = false;
        State = Mode.Flying;
        _stun = stun;
        T = 0;
    }

    /// <summary>Un golpe lo frena un momento (corta lo que estaba preparando; no una embestida ya lanzada).</summary>
    public void Stagger(float t)
    {
        if (Dead || Spawning || State is Mode.Lunge or Mode.Flying or Mode.Leap) return;
        State = Mode.Stagger;
        T = 0;
        _stun = t;
    }

    public void Think(IArena a, float dt)
    {
        if (Dead) { StopDrill(a); return; }
        T += dt;
        _cool -= dt;
        if (State == Mode.Spawn)
        {
            SpawnT = MathF.Min(1, T / SpawnTime);
            if (SpawnT >= 1) { State = Kind == FoeKind.Neighbor ? Mode.Chase : Mode.Keep; T = 0; _cool = 0.5f + (float)_rng.NextDouble(); }
            Move(a, Vector3.Zero, dt);
            return;
        }
        if (Frozen) { Move(a, Vector3.Zero, dt); return; }
        var p = a.Player;
        var to = p.Feet - Pos;
        var flat = new Vector3(to.X, 0, to.Z);
        float dist = flat.Length();
        var dir = dist > 0.01f ? flat / dist : Vector3.UnitX;
        if (State is not (Mode.Lunge or Mode.Flying)) Yaw = Turn(Yaw, MathF.Atan2(dir.Z, dir.X), dt * 8);

        if (Kind == FoeKind.Neighbor) Neighbor(a, dir, dist, to.Y, dt);
        else Teacher(a, dir, dist, dt);
        Drill(a);
    }

    private static float Turn(float from, float to, float max)
    {
        float d = Animator.Wrap(to - from);
        return from + Math.Clamp(d, -max, max);
    }

    // ------------------------------------------------------------------ el vecino

    private void Neighbor(IArena a, Vector3 dir, float dist, float dy, float dt)
    {
        const float Speed = 60;
        switch (State)
        {
            case Mode.Chase:
                // Ernesto arriba de un mueble y cerca: salta atrás de él.
                if (dy > 9 && dist < 70 && Grounded && a.Player.Grounded && _cool <= 0) { Leap(a.Player.Feet, dist); break; }
                if (dist < 24 && MathF.Abs(dy) < 9 && _cool <= 0)
                {
                    State = Mode.Windup; T = 0;
                    a.Sfx.Play(Sound.NeighborGrunt, N(Chest), 0.8f, gap: 0.3f);
                    break;
                }
                Move(a, dir * Speed, dt);
                break;
            case Mode.Windup:
                Anim.SetAction(Stab, Stab.HitStart * 0.9f * MathF.Min(1, T / 0.45f));
                Move(a, dir * 8, dt);
                if (T >= 0.45f) { State = Mode.Lunge; T = 0; _lungeDir = dir; _hitDone = false; }
                break;
            case Mode.Lunge:
                Anim.SetAction(Stab, Stab.HitStart + (Stab.HitEnd - Stab.HitStart) * MathF.Min(1, T / 0.3f));
                Move(a, _lungeDir * 185, dt);
                if (!_hitDone)
                {
                    var tip = Point(Bone.HandR, Figures.Content.Thoughts.DrillTip * Art.Scale);
                    var pc = a.Player.Feet + new Vector3(0, a.Player.BodyHeight * 0.55f, 0);
                    var d = pc - Pos;
                    bool close = new Vector2(d.X, d.Z).Length() < Radius + Player.Radius + 5 && MathF.Abs(d.Y - 7) < 12;
                    if (close || Vector3.Distance(tip, pc) < 6)
                    {
                        _hitDone = true;
                        a.FoeHits(this, 22, tip, _lungeDir * 150 + new Vector3(0, 40, 0));
                    }
                }
                if (T >= 0.3f) { State = Mode.Recover; T = 0; }
                break;
            case Mode.Recover:
                Anim.SetAction(Stab, Stab.HitEnd + (Stab.Duration - Stab.HitEnd) * MathF.Min(1, T / 0.6f));
                Move(a, dir * 15, dt);
                if (T >= 0.65f) { Anim.SetAction(null, 0); State = Mode.Chase; T = 0; _cool = 0.4f + (float)_rng.NextDouble() * 0.6f; }
                break;
            case Mode.Leap:
                Move(a, new Vector3(Vel.X, 0, Vel.Z), dt, keepVelocity: true);
                if (Grounded && T > 0.15f) { State = Mode.Chase; T = 0; _cool = 0.6f; }
                break;
            case Mode.Stagger:
                Anim.SetAction(null, 0);
                Move(a, Vector3.Zero, dt);
                if (T >= _stun) { State = Mode.Chase; T = 0; }
                break;
            case Mode.Flying:
                Anim.SetAction(null, 0);
                Move(a, new Vector3(Vel.X, 0, Vel.Z), dt, keepVelocity: true);
                if (Grounded && T > 0.1f) { State = Mode.Stagger; T = 0; Vel = Vector3.Zero; }
                break;
            default:
                State = Mode.Chase;
                break;
        }
    }

    /// <summary>Salta para caer donde está Ernesto (arriba de la mesa, del sillón).</summary>
    private void Leap(Vector3 target, float dist)
    {
        const float g = 260;
        float t = Math.Clamp(0.45f + dist / 160, 0.5f, 0.9f);
        var d = target - Pos;
        Vel = new Vector3(d.X / t, (d.Y + 0.5f * g * t * t) / t, d.Z / t);
        Grounded = false;
        State = Mode.Leap;
        T = 0;
        Anim.Leap(t, 4);
    }

    /// <summary>El taladro suena mientras vive: más fuerte y más agudo cuando se prepara y embiste.</summary>
    private void Drill(IArena a)
    {
        if (Kind != FoeKind.Neighbor) return;
        _drill ??= a.Sfx.Loop(Sound.Drill, N(Chest), 0.3f, Bus.Effects, fadeIn: 0.4f);
        if (_drill == null) return;
        _drill.At = N(Point(Bone.HandR, Figures.Content.Thoughts.DrillTip * Art.Scale));
        bool angry = State is Mode.Windup or Mode.Lunge;
        _drill.Gain = angry ? 0.75f : 0.3f;
        _drill.Pitch = State == Mode.Windup ? 1 + 0.5f * MathF.Min(1, T / 0.45f) : angry ? 1.5f : 1;
    }

    public void StopDrill(IArena a)
    {
        if (_drill == null) return;
        a.Sfx.Stop(_drill, 0.3f);
        _drill = null;
    }

    // ------------------------------------------------------------------ la maestra

    private void Teacher(IArena a, Vector3 dir, float dist, float dt)
    {
        const float Speed = 52;
        var side = new Vector3(-dir.Z, 0, dir.X) * _orbit;
        switch (State)
        {
            case Mode.Keep:
            {
                // A distancia: si está muy cerca se aleja, si está lejos se acerca; si no, da vueltas.
                var want = dist < 55 ? -dir * Speed + side * 20 : dist > 125 ? dir * Speed + side * 15 : side * Speed * 0.8f;
                if (_rng.NextDouble() < dt * 0.3f) _orbit = -_orbit;
                Move(a, want, dt);
                if (_cool <= 0 && dist < 220 && Sees(a))
                {
                    State = Mode.Throw; T = 0; _thrown = false;
                    _throwAt = 0.55f;
                    Anim.Gesture(GestureKind.Throw, 0.8f);
                    a.Sfx.Play(Sound.Shush, N(HeadCenter), 0.9f, gap: 0.2f);
                }
                break;
            }
            case Mode.Throw:
                Move(a, Vector3.Zero, dt);
                if (!_thrown && T >= _throwAt)
                {
                    _thrown = true;
                    var hand = Point(Bone.HandR, (Figures.Content.WeaponModels.Fist + new NVec3(0.8f, 0, 0)) * Art.Scale);
                    // Le apunta a donde va a estar (con un poco de error: no es infalible).
                    var p = a.Player;
                    float flight = Math.Clamp(Vector3.Distance(hand, p.EyePos) / 170, 0.25f, 1.4f);
                    var aim = p.Feet + new Vector3(0, 8, 0) + new Vector3(p.Vel.X, 0, p.Vel.Z) * flight * 0.75f;
                    aim += new Vector3((float)_rng.NextDouble() - 0.5f, 0, (float)_rng.NextDouble() - 0.5f) * 6;
                    a.ThrowChalk(this, hand, aim, 170);
                }
                if (T >= 0.85f) { State = Mode.Keep; T = 0; _cool = 1.8f + (float)_rng.NextDouble() * 1.4f; }
                break;
            case Mode.Stagger:
                Move(a, Vector3.Zero, dt);
                if (T >= _stun) { State = Mode.Keep; T = 0; }
                break;
            case Mode.Flying:
                Move(a, new Vector3(Vel.X, 0, Vel.Z), dt, keepVelocity: true);
                if (Grounded && T > 0.1f) { State = Mode.Stagger; T = 0; Vel = Vector3.Zero; }
                break;
            default:
                State = Mode.Keep;
                break;
        }
    }

    /// <summary>¿Ve a Ernesto (nada en el medio entre su cabeza y la de él)?</summary>
    private bool Sees(IArena a)
    {
        var from = HeadCenter;
        var d = a.Player.EyePos - from;
        float len = d.Length();
        return !a.Solids.Raycast(from, d / len, len, out _, out _, out _);
    }

    // ------------------------------------------------------------------ moverse

    /// <summary>
    /// Camina hacia <paramref name="want"/> (velocidad horizontal): acelera, cae si no tiene piso, choca
    /// con los muebles y con los otros pensamientos. <paramref name="keepVelocity"/>: en el aire (un salto,
    /// un empujón), sigue con lo que traía.
    /// </summary>
    private void Move(IArena a, Vector3 want, float dt, bool keepVelocity = false)
    {
        if (!keepVelocity)
        {
            var h = new Vector3(Vel.X, 0, Vel.Z);
            h = Player.MoveTowards(h, want, (Grounded ? 500 : 150) * dt);
            Vel = new Vector3(h.X, Vel.Y, h.Z);
        }
        if (!Grounded) Vel.Y -= 260 * dt;
        // Se separa de los otros (no se enciman) y de Ernesto (no se le mete adentro).
        foreach (var o in a.Foes)
        {
            if (o == this || o.Dead || o.Spawning) continue;
            var d = new Vector2(Pos.X - o.Pos.X, Pos.Z - o.Pos.Z);
            float min = Radius + o.Radius;
            float l = d.Length();
            if (l < min && l > 0.01f && MathF.Abs(Pos.Y - o.Pos.Y) < 12) { var push = d / l * (min - l) * 6; Vel += new Vector3(push.X, 0, push.Y); }
        }
        var pp = a.Player.Feet;
        var dp = new Vector2(Pos.X - pp.X, Pos.Z - pp.Z);
        float pl = dp.Length(), pmin = Radius + Player.Radius;
        if (pl < pmin && pl > 0.01f && MathF.Abs(Pos.Y - pp.Y) < 12) { var push = dp / pl * (pmin - pl); Pos += new Vector3(push.X, 0, push.Y); }

        Pos = a.Solids.Move(Pos, Radius, Height, Vel * dt, 5, Grounded && Vel.Y <= 0, out bool grounded, out bool bonk, out var wall);
        if (bonk && Vel.Y > 0) Vel.Y = 0;
        if (wall != Vector3.Zero) { float into = Vector3.Dot(Vel, wall); if (into < 0) Vel -= wall * into; }
        Grounded = grounded && Vel.Y <= 0.01f;
        if (Grounded && Vel.Y < 0) Vel.Y = 0;
    }

    private static System.Numerics.Vector3 N(Vector3 v) => new(v.X, v.Y, v.Z);
}
