using Jaqueca.Audio;
using Jaqueca.Client.Game;
using Jaqueca.Client.Render;
using Jaqueca.Figures.Content;
using Jaqueca.Figures.Rig;
using Microsoft.Xna.Framework;
using NVec2 = System.Numerics.Vector2;
using NVec3 = System.Numerics.Vector3;

namespace Jaqueca.Client.Screens;

/// <summary>
/// Las armas de Ernesto. El revólver de cebita: preciso, a la cabeza revienta; con el botón derecho
/// apretado carga un tiro que atraviesa a todos los que estén en la línea. La escopeta del abuelo: de
/// cerca despedaza. La patada con la pantufla (F): empuja lejos y, si le llega algo tirado (una tiza),
/// lo devuelve más fuerte a quien lo tiró. Los tiros pegan en los huesos (cabeza, torso, brazos, piernas):
/// un miembro puede salir volando, y los cuerpos tirados también se pueden seguir despedazando.
/// </summary>
public sealed partial class PlayScreen
{
    private int _weapon;
    private float _cooldown, _kickCool, _charge, _freeze, _flashT, _pumpT = 10;
    private bool _charging;
    private Mixer.Voice _chargeVoice;

    private struct Trace { public Vector3 A, B; public float Life, Max, Width; public Color Color; }
    private readonly List<Trace> _tracers = new();

    /// <summary>Una tiza en el aire (la tiró la maestra, o Ernesto se la devolvió de una patada).</summary>
    private struct Chalk { public Vector3 Pos, Vel; public bool Returned; public float Spin, Life; public Foe From; }
    private readonly List<Chalk> _chalks = new();

    private struct Hit { public Foe Foe; public Bone Bone; public float T; public Vector3 Point; }
    private readonly List<Hit> _hits = new();
    private readonly List<Hit> _pierce = new();

    // ------------------------------------------------------------------ disparar

    private void UpdateCombat(in Intent it, float dt)
    {
        _cooldown -= dt;
        _kickCool -= dt;
        _flashT -= dt;
        _freeze -= dt;
        if (_p.Health <= 0) return;

        if (it.Weapon >= 0 && it.Weapon != _weapon)
        {
            _weapon = it.Weapon;
            _vm.Switch(_weapon);
            _charging = false;
            _charge = 0;
            _game.Sfx.Stop(_chargeVoice, 0.05f);
            _chargeVoice = null;
            _game.Sfx.Play(Sound.Switch, N(_p.EyePos), 0.5f, flat: true);
            _cooldown = MathF.Max(_cooldown, 0.22f);
        }

        bool ready = _cooldown <= 0 && !_vm.Switching;
        if (_weapon == 0)
        {
            if (it.Fire && ready && !_charging) Revolver(pierce: false);
            // El tiro cargado: se aprieta el derecho, se carga, y sale al soltar (si llegó a cargarse).
            if (it.AltHeld && ready && !_charging) { _charging = true; _charge = 0; _chargeVoice = _game.Sfx.Play(Sound.RevolverCharge, N(_p.EyePos), 0.55f, flat: true); }
            if (_charging)
            {
                _charge = MathF.Min(1, _charge + dt / 0.75f);
                if (!it.AltHeld)
                {
                    _charging = false;
                    if (_charge >= 1) Revolver(pierce: true);
                    else { _game.Sfx.Stop(_chargeVoice, 0.05f); }
                    _chargeVoice = null;
                    _charge = 0;
                }
            }
        }
        else if (it.Fire && ready) Shotgun();
        _vm.Charge = _charge;

        if (_pumpT < 10)
        {
            _pumpT += dt;
            if (_pumpT >= 0.3f) { _game.Sfx.Play(Sound.ShotgunPump, N(_p.EyePos), 0.6f, flat: true); _pumpT = 10; }
        }

        if (it.Kick && _kickCool <= 0) Kick();
        if (_kickPending > 0) { _kickPending -= dt; if (_kickPending <= 0) KickLands(); }

        UpdateChalks(dt);
        for (int i = _tracers.Count - 1; i >= 0; i--)
        {
            var t = _tracers[i];
            t.Life -= dt;
            if (t.Life <= 0) _tracers.RemoveAt(i); else _tracers[i] = t;
        }
    }

    private Vector3 AimDir => FpsCamera.Dir(_p.Yaw, _p.Pitch);

    private void MuzzleFlash(float size)
    {
        _flashT = 0.06f;
        _flashSize = size;
        var at = _p.EyePos + AimDir * 6;
        _r.Lights.Flash(at, 90 * size, new Vector3(1.4f, 1.0f, 0.55f) * size, 0.09f);
    }

    private float _flashSize = 1;

    private void Revolver(bool pierce)
    {
        _cooldown = pierce ? 0.6f : 0.36f;
        var o = _p.EyePos;
        var d = AimDir;
        _game.Sfx.Play(pierce ? Sound.RevolverPierce : Sound.Revolver, N(o), pierce ? 1 : 0.85f, flat: true);
        _vm.Fire(pierce ? 7 : 3.5f);
        MuzzleFlash(pierce ? 1.6f : 1);
        _shake = MathF.Max(_shake, pierce ? 0.9f : 0.3f);
        float maxT = 900;
        // ¿Le da a una tiza en el aire? (se rompe en el aire).
        float chalkT = RayChalk(o, d, maxT, out int chalk);
        _level.Solids.Raycast(o, d, maxT, out float wallT, out var wallN, out byte stuff);
        if (pierce)
        {
            // Atraviesa a todos los que estén en la línea hasta la pared.
            RayFoes(o, d, wallT, _pierce);
            foreach (var h in _pierce) Damage(h.Foe, h.Bone, 3.5f, d, h.Point, Weapon.Pierce);
            if (_pierce.Count >= 2) Style(40 * _pierce.Count, _pierce.Count == 2 ? "+ BROCHETA" : "+ BROCHETA X" + _pierce.Count);
            if (chalk >= 0 && chalkT < wallT) BreakChalk(chalk, true);
            Tracer(_vm.Muzzle(_r.Cam), o + d * wallT, 0.35f, 1.6f, new Color(255, 230, 150));
        }
        else
        {
            float foeT = RayFoe(o, d, wallT, out var hit) ? hit.T : float.MaxValue;
            if (chalk >= 0 && chalkT < foeT && chalkT < wallT) { BreakChalk(chalk, true); wallT = chalkT; }
            else if (foeT < wallT) { Damage(hit.Foe, hit.Bone, 1, d, hit.Point, Weapon.Revolver); wallT = foeT; }
            else Impact(o + d * wallT, wallN, stuff);
            Tracer(_vm.Muzzle(_r.Cam), o + d * wallT, 0.12f, 0.6f, new Color(255, 220, 160));
        }
        if (pierce) Impact(o + d * wallT, wallN, stuff);
    }

    private void Shotgun()
    {
        _cooldown = 0.95f;
        _pumpT = 0;
        var o = _p.EyePos;
        var fwd = AimDir;
        _game.Sfx.Play(Sound.Shotgun, N(o), 1, flat: true);
        _vm.Fire(7);
        MuzzleFlash(1.8f);
        _shake = MathF.Max(_shake, 0.8f);
        var right = _r.Cam.Right; var up = _r.Cam.Up;
        _hits.Clear();
        for (int i = 0; i < 12; i++)
        {
            // Los perdigones: un cono de ~6 grados, más apretado en el medio.
            float a = Rnd(0, MathF.Tau), rr = MathF.Sqrt(Rnd(0, 1)) * 0.1f;
            var d = Vector3.Normalize(fwd + right * MathF.Cos(a) * rr + up * MathF.Sin(a) * rr);
            _level.Solids.Raycast(o, d, 700, out float wallT, out var wallN, out byte stuff);
            float chalkT = RayChalk(o, d, wallT, out int chalk);
            if (chalk >= 0) { BreakChalk(chalk, true); continue; }
            if (RayFoe(o, d, wallT, out var hit))
            {
                // De cerca pega más (y despedaza); de lejos, casi nada.
                float k = Math.Clamp(1.4f - hit.T / 90, 0.25f, 1.3f);
                Damage(hit.Foe, hit.Bone, 0.55f * k, d, hit.Point, Weapon.Shotgun);
                if (i % 3 == 0) Tracer(_vm.Muzzle(_r.Cam), hit.Point, 0.08f, 0.35f, new Color(255, 200, 140));
            }
            else
            {
                Impact(o + d * wallT, wallN, stuff, quiet: i % 3 != 0);
                if (i % 3 == 0) Tracer(_vm.Muzzle(_r.Cam), o + d * wallT, 0.08f, 0.35f, new Color(255, 200, 140));
            }
        }
        // El empujón de la escopeta: los que recibieron salen para atrás.
        foreach (var f in _foes)
            if (!f.Dead && f.ShotThisFrame > 0)
            {
                var push = Vector3.Normalize(new Vector3(f.Pos.X - o.X, 0, f.Pos.Z - o.Z)) * MathF.Min(160, f.ShotThisFrame * 60);
                f.Knock(push + new Vector3(0, 30, 0), 0.35f);
            }
        foreach (var f in _foes) f.ShotThisFrame = 0;
    }

    private void Tracer(Vector3 a, Vector3 b, float life, float width, Color c) => _tracers.Add(new Trace { A = a, B = b, Life = life, Max = life, Width = width, Color = c });

    /// <summary>Donde pega un tiro en el lugar: el agujero, el polvo (o las astillas) y el ruido.</summary>
    private void Impact(Vector3 at, Vector3 n, byte stuff, bool quiet = false)
    {
        if (n == Vector3.Zero) return;
        _decals.Add(at, n, Rnd(0.5f, 0.8f), Rnd(0, 6.28f), new Color(30, 22, 20));
        var col = stuff switch { 1 => new Color(200, 190, 160), 2 => new Color(120, 40, 44), 3 => new Color(170, 60, 70), _ => new Color(120, 80, 50) };
        Puff(at, n, quiet ? 3 : 7, DropKind.Dust, col);
        Puff(at, n, quiet ? 1 : 4, DropKind.Spark, Color.White);
        if (!quiet) _game.Sfx.Play(Sound.Ricochet, N(at), 0.5f, gap: 0.05f);
    }

    // ------------------------------------------------------------------ la patada

    private float _kickPending;

    private void Kick()
    {
        _kickCool = 0.5f;
        _vm.Kick();
        _game.Sfx.Play(Sound.Kick, N(_p.EyePos), 0.7f, flat: true);
        _kickPending = 0.11f;
    }

    /// <summary>El momento en que la pantufla llega: empuja lo que tiene adelante y devuelve lo que viene volando.</summary>
    private void KickLands()
    {
        var o = _p.EyePos;
        var d = AimDir;
        bool any = false;
        // Lo que viene volando: vuelve por donde apunta Ernesto, más rápido.
        for (int i = 0; i < _chalks.Count; i++)
        {
            var c = _chalks[i];
            if (c.Returned) continue;
            var to = c.Pos - o;
            float dist = to.Length();
            if (dist > 20 || Vector3.Dot(to / MathF.Max(dist, 0.01f), d) < 0.35f) continue;
            // Si tiene a quién devolvérsela, va a él.
            var dir = d;
            if (c.From != null && !c.From.Dead)
            {
                var tgt = c.From.Chest - c.Pos;
                if (Vector3.Dot(Vector3.Normalize(tgt), d) > 0.5f) dir = Vector3.Normalize(tgt);
            }
            c.Vel = dir * 420;
            c.Returned = true;
            _chalks[i] = c;
            _game.Sfx.Play(Sound.Parry, N(c.Pos), 0.9f, flat: true);
            Style(80, "+ ¡DEVUELTA!");
            _freeze = 0.1f;
            Heal(15);
            any = true;
        }
        // Los pensamientos cerca y adelante: salen volando.
        foreach (var f in _foes)
        {
            if (f.Dead || f.Spawning) continue;
            var to = f.Chest - o;
            float dist = to.Length();
            if (dist > 24 || Vector3.Dot(to / MathF.Max(dist, 0.01f), d) < 0.5f) continue;
            var push = Vector3.Normalize(new Vector3(d.X, 0, d.Z)) * 220 + new Vector3(0, 70, 0);
            Damage(f, Bone.Spine, 1.2f, d, f.Chest, Weapon.Kick);
            if (!f.Dead) f.Knock(push, 0.7f);
            _game.Sfx.Play(Sound.KickHit, N(f.Chest), 0.9f);
            _freeze = MathF.Max(_freeze, 0.06f);
            any = true;
        }
        if (any) _shake = MathF.Max(_shake, 0.5f);
    }

    // ------------------------------------------------------------------ golpes a los pensamientos

    private enum Weapon { Revolver, Pierce, Shotgun, Kick, Chalk, Slam }

    /// <summary>
    /// Le pega a un pensamiento (vivo o tirado) en un hueso: resta vida, sangra (y cura a Ernesto si está
    /// cerca), a veces le arranca el miembro, y si se muere, lo tira con el empujón del golpe.
    /// </summary>
    private void Damage(Foe f, Bone bone, float dmg, Vector3 dir, Vector3 at, Weapon w)
    {
        bool head = bone == Bone.Head;
        bool wasDead = f.Dead;
        if (!wasDead && f.Spawning) return;
        if (head && w is Weapon.Revolver or Weapon.Pierce) dmg *= 3;
        f.ShotThisFrame += dmg;
        Bleed(at, -dir * 0.3f + dir * 0.7f, MathF.Min(2.5f, dmg * (head ? 1.2f : 0.7f)));
        _game.Sfx.Play(Sound.HitFlesh, N(at), 0.7f, gap: 0.04f);
        if (wasDead)
        {
            // Un cuerpo tirado: se sigue despedazando (y salpica).
            if (Gore.Cuttable.Contains(bone) && !f.Anim.IsCut(bone) && (w == Weapon.Shotgun ? _brng.Next(4) == 0 : dmg >= 1)) Sever(f, bone, dir);
            return;
        }
        f.Hp -= dmg;
        f.Flash = 1;
        f.Anim.Hurt(new NVec2(dir.X, dir.Z));
        f.Stagger(dmg >= 1 ? 0.25f : 0.1f);
        // Los miembros: la escopeta de cerca y el tiro que atraviesa arrancan; el revólver, a veces.
        bool limb = bone != Bone.Spine && bone != Bone.Pelvis && bone != Bone.Head;
        float cutChance = w switch { Weapon.Pierce => 0.7f, Weapon.Shotgun => 0.12f, Weapon.Revolver => 0.3f, _ => 0 };
        if (limb && Gore.Cuttable.Contains(bone) && !f.Anim.IsCut(bone) && (float)_brng.NextDouble() < cutChance)
        {
            Sever(f, bone, dir);
            Style(20, "+ DESPIECE");
        }
        if (f.Hp <= 0) Kill(f, bone, dir, w);
    }

    private void Kill(Foe f, Bone bone, Vector3 dir, Weapon w)
    {
        bool head = bone == Bone.Head;
        // A la cabeza con el revólver: la cabeza revienta (sale volando, con todo lo que tenía adentro).
        if (head && w is Weapon.Revolver or Weapon.Pierce && !f.Anim.IsCut(Bone.Head))
        {
            Sever(f, Bone.Head, dir + new Vector3(0, 0.6f, 0));
            Bleed(f.HeadCenter, Vector3.UnitY, 3);
            _game.Sfx.Play(Sound.Headshot, N(f.HeadCenter), 1);
        }
        // La escopeta de cerca: se lleva un par de pedazos.
        if (w == Weapon.Shotgun && Vector3.Distance(f.Chest, _p.EyePos) < 45)
        {
            foreach (var b in new[] { Bone.UpperArmL, Bone.ForearmR, Bone.ShinL, Bone.Head })
                if (_brng.Next(2) == 0 && !f.Anim.IsCut(b)) Sever(f, b, dir);
            Bleed(f.Chest, dir, 2.5f);
        }
        var push = new NVec2(dir.X, dir.Z) * (w == Weapon.Shotgun ? 190 : w == Weapon.Kick ? 240 : 90);
        f.Anim.Die(push, w is Weapon.Shotgun or Weapon.Kick ? 60 : 25);
        _game.Sfx.Play(Sound.Groan, N(f.Chest), 0.5f, gap: 0.2f);
        Killed(f, head, w);
    }

    /// <summary>Le arranca el miembro de <paramref name="bone"/>: el pedazo sale volando y el muñón chorrea.</summary>
    private void Sever(Foe f, Bone bone, Vector3 dir)
    {
        var gibs = f.Anim.Sever(bone, new NVec2(dir.X, dir.Z) * 70, _brng.Next());
        foreach (var g in gibs) _pieces.Add(new Piece(g, f.Art.Mesh, _level.Solids, f.Pos.Y + 4));
        var at = f.Anim.Wound(bone) is { } wnd ? new Vector3(wnd.at.X, wnd.at.Y, wnd.at.Z) : f.Chest;
        Bleed(at, dir, 1.6f);
        f.Bleeding[(int)bone] = 2.2f;
        _game.Sfx.Play(Sound.Dismember, N(at), 0.9f, gap: 0.05f);
    }

    // ------------------------------------------------------------------ rayos contra los cuerpos

    private struct Capsule { public Bone Bone; public Vector3 A, B; public float R; }
    private readonly Capsule[] _caps = new Capsule[12];

    /// <summary>Las cápsulas de golpe de un cuerpo (lo cortado no está).</summary>
    private int Capsules(Foe f)
    {
        int n = 0;
        float k = f.Art.Scale * f.Art.Dims.Limb, s = f.Art.Scale;
        Vector3 J(Bone b) => f.Point(b, NVec3.Zero);
        void Add(Bone b, Vector3 a, Vector3 c, float r) { if (!f.Anim.IsCut(b)) _caps[n++] = new Capsule { Bone = b, A = a, B = c, R = r }; }
        var head = f.HeadCenter;
        if (!f.Anim.IsCut(Bone.Head)) _caps[n++] = new Capsule { Bone = Bone.Head, A = head, B = head + new Vector3(0, 0.3f, 0), R = 1.25f * s };
        var pelvis = J(Bone.Pelvis);
        var neck = f.Point(Bone.Spine, new NVec3(0, f.Art.Dims.ShoulderUp * s, 0));
        _caps[n++] = new Capsule { Bone = Bone.Spine, A = pelvis, B = neck, R = (f.Kind == FoeKind.Neighbor ? 2.5f : 1.8f) * s };
        foreach (int side in new[] { 1, -1 })
        {
            var ua = Skeleton.Arm(side, 0); var fa = Skeleton.Arm(side, 1); var ha = Skeleton.Arm(side, 2);
            Add(ua, J(ua), J(fa), 0.75f * k);
            Add(fa, J(fa), f.Point(ha, new NVec3(0, -0.9f * s, 0)), 0.65f * k);
            var th = Skeleton.Leg(side, 0); var sh = Skeleton.Leg(side, 1); var ft = Skeleton.Leg(side, 2);
            Add(th, J(th), J(sh), 0.95f * k);
            Add(sh, J(sh), J(ft), 0.75f * k);
        }
        return n;
    }

    private static bool RayCapsule(Vector3 o, Vector3 d, Vector3 a, Vector3 b, float r, out float t)
    {
        // Íñigo Quílez: intersección rayo-cápsula.
        t = 0;
        var ba = b - a; var oa = o - a;
        float baba = Vector3.Dot(ba, ba), bard = Vector3.Dot(ba, d), baoa = Vector3.Dot(ba, oa), rdoa = Vector3.Dot(d, oa), oaoa = Vector3.Dot(oa, oa);
        float A = baba - bard * bard, B = baba * rdoa - baoa * bard, C = baba * oaoa - baoa * baoa - r * r * baba;
        float h = B * B - A * C;
        if (h >= 0 && A > 1e-8f)
        {
            float tt = (-B - MathF.Sqrt(h)) / A;
            float y = baoa + tt * bard;
            if (y > 0 && y < baba && tt >= 0) { t = tt; return true; }
            // Las tapas.
            var oc = y <= 0 ? oa : o - b;
            B = Vector3.Dot(d, oc); C = Vector3.Dot(oc, oc) - r * r;
            h = B * B - C;
            if (h > 0) { tt = -B - MathF.Sqrt(h); if (tt >= 0) { t = tt; return true; } }
            return false;
        }
        // Rayo paralelo al eje: como esfera en la punta más cercana.
        var oc2 = oa;
        float b2 = Vector3.Dot(d, oc2), c2 = Vector3.Dot(oc2, oc2) - r * r, h2 = b2 * b2 - c2;
        if (h2 < 0) return false;
        t = -b2 - MathF.Sqrt(h2);
        return t >= 0;
    }

    private bool RayFoe(Vector3 o, Vector3 d, float maxT, out Hit hit)
    {
        hit = default;
        float best = maxT;
        foreach (var f in _foes)
        {
            if (f.Spawning) continue;
            // Lejos del rayo: ni se mira.
            var c = f.Dead ? f.Point(Bone.Pelvis, NVec3.Zero) : f.Pos + new Vector3(0, f.Height * 0.5f, 0);
            var oc = c - o;
            float along = Vector3.Dot(oc, d);
            if (along < -20 || along > best + 20) continue;
            if ((oc - d * along).LengthSquared() > 22 * 22) continue;
            int n = Capsules(f);
            for (int i = 0; i < n; i++)
            {
                ref var cp = ref _caps[i];
                if (RayCapsule(o, d, cp.A, cp.B, cp.R, out float t) && t < best)
                {
                    best = t;
                    hit = new Hit { Foe = f, Bone = cp.Bone, T = t, Point = o + d * t };
                }
            }
        }
        return hit.Foe != null;
    }

    /// <summary>Todos los cuerpos que corta el rayo (el primer hueso de cada uno), del más cercano al más lejano.</summary>
    private void RayFoes(Vector3 o, Vector3 d, float maxT, List<Hit> into)
    {
        into.Clear();
        foreach (var f in _foes)
        {
            if (f.Spawning) continue;
            int n = Capsules(f);
            Hit best = default;
            float bt = maxT;
            for (int i = 0; i < n; i++)
            {
                ref var cp = ref _caps[i];
                if (RayCapsule(o, d, cp.A, cp.B, cp.R, out float t) && t < bt) { bt = t; best = new Hit { Foe = f, Bone = cp.Bone, T = t, Point = o + d * t }; }
            }
            if (best.Foe != null) into.Add(best);
        }
        into.Sort((a, b) => a.T.CompareTo(b.T));
    }

    // ------------------------------------------------------------------ las tizas

    /// <summary>La maestra tira una tiza desde <paramref name="from"/> para que caiga en <paramref name="target"/>.</summary>
    public void ThrowChalk(Foe who, Vector3 from, Vector3 target, float speed = 170)
    {
        var d = target - from;
        float flat = new Vector2(d.X, d.Z).Length();
        float t = Math.Clamp(flat / speed, 0.25f, 1.6f);
        const float g = ChalkGravity;
        var v = new Vector3(d.X / t, (d.Y + 0.5f * g * t * t) / t, d.Z / t);
        _chalks.Add(new Chalk { Pos = from, Vel = v, From = who, Life = 5, Spin = Rnd(0, 6) });
        _game.Sfx.Play(Sound.ChalkThrow, N(from), 0.7f);
    }

    private const float ChalkGravity = 110;

    private void UpdateChalks(float dt)
    {
        for (int i = _chalks.Count - 1; i >= 0; i--)
        {
            var c = _chalks[i];
            c.Life -= dt;
            if (!c.Returned) c.Vel.Y -= ChalkGravity * dt;
            var d = c.Vel * dt;
            float len = d.Length();
            bool broke = c.Life <= 0;
            if (!broke && len > 0 && _level.Solids.Raycast(c.Pos, d / len, len, out float t, out var n, out _))
            {
                c.Pos += d / len * t;
                Puff(c.Pos, n, 10, DropKind.Dust, new Color(236, 232, 222));
                _decals.Add(c.Pos, n, Rnd(0.8f, 1.4f), Rnd(0, 6), new Color(210, 206, 196));
                _game.Sfx.Play(Sound.ChalkBreak, N(c.Pos), 0.6f);
                broke = true;
            }
            if (!broke)
            {
                c.Pos += d;
                c.Spin += dt * 14;
                if (!c.Returned)
                {
                    // Contra Ernesto.
                    var to = c.Pos - (_p.Feet + new Vector3(0, _p.BodyHeight * 0.5f, 0));
                    if (MathF.Abs(to.Y) < _p.BodyHeight * 0.5f + 1 && new Vector2(to.X, to.Z).Length() < Player.Radius + 1.2f)
                    {
                        if (_p.Hurt(12, Vector3.Normalize(c.Vel) * 30)) Hurt(12, c.Pos);
                        Puff(c.Pos, -Vector3.Normalize(c.Vel), 12, DropKind.Dust, new Color(236, 232, 222));
                        _game.Sfx.Play(Sound.ChalkBreak, N(c.Pos), 0.8f);
                        broke = true;
                    }
                }
                else
                {
                    // Devuelta: le pega al primero que encuentre.
                    foreach (var f in _foes)
                    {
                        if (f.Dead || f.Spawning) continue;
                        if (Vector3.DistanceSquared(f.Chest, c.Pos) < 16 || Vector3.DistanceSquared(f.HeadCenter, c.Pos) < 6)
                        {
                            bool head = Vector3.DistanceSquared(f.HeadCenter, c.Pos) < 6;
                            Damage(f, head ? Bone.Head : Bone.Spine, 5, Vector3.Normalize(c.Vel), c.Pos, Weapon.Chalk);
                            Puff(c.Pos, -Vector3.Normalize(c.Vel), 14, DropKind.Dust, new Color(236, 232, 222));
                            _game.Sfx.Play(Sound.ChalkBreak, N(c.Pos), 0.9f);
                            if (f.Dead) Style(60, "+ SU PROPIA TIZA");
                            broke = true;
                            break;
                        }
                    }
                }
            }
            if (broke) _chalks.RemoveAt(i); else _chalks[i] = c;
        }
    }

    /// <summary>El primer tiza que corta el rayo (índice, o -1) y a qué distancia.</summary>
    private float RayChalk(Vector3 o, Vector3 d, float maxT, out int index)
    {
        index = -1;
        float best = maxT;
        for (int i = 0; i < _chalks.Count; i++)
        {
            var oc = _chalks[i].Pos - o;
            float along = Vector3.Dot(oc, d);
            if (along < 0 || along > best) continue;
            // Las tizas son chicas, pero se les apunta con más margen (que pegarles sea posible).
            if ((oc - d * along).LengthSquared() < 2.2f * 2.2f) { best = along; index = i; }
        }
        return best;
    }

    private void BreakChalk(int i, bool shot)
    {
        var c = _chalks[i];
        Puff(c.Pos, -Vector3.Normalize(c.Vel), 16, DropKind.Dust, new Color(236, 232, 222));
        _game.Sfx.Play(Sound.ChalkBreak, N(c.Pos), 0.8f);
        if (shot) Style(50, "+ TIZA ROTA");
        _chalks.RemoveAt(i);
    }

    // ------------------------------------------------------------------ dibujar

    private void DrawCombat()
    {
        var cam = _r.Cam;
        // Las tizas: un palito blanco que gira.
        foreach (var c in _chalks)
        {
            var dir = c.Vel.LengthSquared() > 1 ? Vector3.Normalize(c.Vel) : Vector3.UnitX;
            var side = Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitY) + new Vector3(0.001f, 0, 0));
            var spun = Vector3.Transform(side, Quaternion.CreateFromAxisAngle(dir, c.Spin));
            var a = c.Pos - spun * 0.9f; var b = c.Pos + spun * 0.9f;
            var w = Vector3.Cross(spun, dir) * 0.28f;
            var up = Vector3.Normalize(Vector3.Cross(spun, w) + new Vector3(0, 0.001f, 0));
            var col = c.Returned ? new Color(255, 250, 220) : new Color(236, 232, 222);
            _r.Extra.Quad(a - w, b - w, b + w, a + w, up, col);
            _r.Extra.Quad(a - up * 0.28f, b - up * 0.28f, b + up * 0.28f, a + up * 0.28f, w, col);
            if (c.Returned) _r.Glow.Billboard(c.Pos, cam.Right, cam.Up, 1.6f, new Color(255, 230, 160, 120), -cam.Forward);
        }
        // Los trazos de los tiros: una tira que mira a la cámara.
        foreach (var t in _tracers)
        {
            float k = t.Life / t.Max;
            var d = t.B - t.A;
            var side = Vector3.Cross(d, cam.Forward);
            if (side.LengthSquared() < 1e-6f) continue;
            side = Vector3.Normalize(side) * t.Width * k;
            var col = new Color(t.Color.R, t.Color.G, t.Color.B, (byte)(255 * k));
            _r.Glow.Quad(t.A - side, t.B - side, t.B + side, t.A + side, -cam.Forward, col);
        }
        // El fogonazo en la punta del caño: una estrella de puntas finas (girada al azar) y el centro chico.
        if (_flashT > 0)
        {
            var m = _vm.Muzzle(cam) + AimDir * 0.4f;
            float s = _flashSize * (0.7f + Rnd(0, 0.35f));
            float rot = Rnd(0, MathF.PI);
            var n = -cam.Forward;
            for (int k = 0; k < 4; k++)
            {
                float a = rot + k * MathF.PI / 4;
                var dir = cam.Right * MathF.Cos(a) + cam.Up * MathF.Sin(a);
                var side = Vector3.Cross(dir, cam.Forward) * 0.09f * s;
                float len = s * (k % 2 == 0 ? 1.1f : 0.6f);
                _r.ViewGlow.Quad(m - dir * len - side, m + dir * len - side, m + dir * len + side, m - dir * len + side, n, new Color(255, 190, 90, 230));
            }
            _r.ViewGlow.Billboard(m, cam.Right, cam.Up, s * 0.28f, new Color(255, 245, 210, 255), n);
            _r.ViewGlow.Billboard(m + AimDir * 0.5f, cam.Right, cam.Up, s * 0.18f, new Color(255, 220, 150, 200), n);
        }
    }
}
