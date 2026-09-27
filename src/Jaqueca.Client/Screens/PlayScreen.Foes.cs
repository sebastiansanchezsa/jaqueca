using Jaqueca.Audio;
using Jaqueca.Client.Game;
using Jaqueca.Client.World;
using Jaqueca.Figures.Rig;
using Microsoft.Xna.Framework;
using NVec3 = System.Numerics.Vector3;

namespace Jaqueca.Client.Screens;

/// <summary>
/// Los pensamientos en el living: aparecen por oleadas (se los oye y se los ve crecer del piso), pelean,
/// mueren y quedan tirados (los cuerpos no se van: se los puede seguir despedazando). Cuando no queda
/// ninguno, suena la campana y viene la siguiente, más grande.
/// </summary>
public sealed partial class PlayScreen : IArena
{
    private readonly List<Foe> _foes = new();
    private readonly List<Piece> _pieces = new();
    private int _wave, _seed = 1;
    private float _waveT = 1.5f;
    private bool _waveOn;
    private string _banner;
    private float _bannerT;
    private float _deadT;

    public Player Player => _p;
    public Solids Solids => _level.Solids;
    public Audio.Sfx Sfx => _game.Sfx;
    public IReadOnlyList<Foe> Foes => _foes;

    private void StartFoes()
    {
        var o = _game.Options;
        if (o.NoEnemies) return;
        if (o.TestFoes)
        {
            // Prueba: delante de Ernesto, a la vista.
            var fwd = _p.FlatForward; var right = _p.FlatRight;
            int n = 0;
            for (int i = 0; i < Math.Max(0, o.TestNeighbors); i++, n++) Spawn(FoeKind.Neighbor, _p.Feet + fwd * (30 + 14 * (n / 3)) + right * ((n % 3) - 1) * 16, instant: true);
            for (int i = 0; i < Math.Max(0, o.TestTeachers); i++, n++) Spawn(FoeKind.Teacher, _p.Feet + fwd * (34 + 14 * (n / 3)) + right * ((n % 3) - 1) * 16, instant: true);
            return;
        }
    }

    private Foe Spawn(FoeKind kind, Vector3 at, bool instant = false)
    {
        at.Y = MathF.Max(0, _level.Solids.GroundAt(at.X, at.Z, at.Y + 40, 3));
        var toP = _p.Feet - at;
        var f = new Foe(kind, _art, _level.Solids, at, MathF.Atan2(toP.Z, toP.X), _seed++) { Frozen = _game.Options.Frozen };
        if (instant) { f.State = kind == FoeKind.Neighbor ? Foe.Mode.Chase : Foe.Mode.Keep; f.SpawnT = 1; }
        else
        {
            _game.Sfx.Play(Sound.Spawn, N(at + new Vector3(0, 8, 0)), 0.8f);
            _r.Lights.Flash(at + new Vector3(0, 10, 0), 60, new Vector3(1.2f, 0.4f, 0.6f), 0.9f);
        }
        _foes.Add(f);
        return f;
    }

    /// <summary>Las oleadas: qué trae cada una (la cuarta en adelante crece sola).</summary>
    private (int neighbors, int teachers) WaveOf(int w) => w switch
    {
        1 => (3, 0),
        2 => (2, 2),
        3 => (4, 3),
        _ => (3 + w, 2 + w / 2),
    };

    private void UpdateWaves(float dt)
    {
        var o = _game.Options;
        if (o.NoEnemies || o.TestFoes || _p.Health <= 0) return;
        bool alive = false;
        foreach (var f in _foes) if (!f.Dead) { alive = true; break; }
        if (_waveOn && !alive)
        {
            _waveOn = false;
            _waveT = 3;
            _game.Sfx.Flat(Sound.WaveClear, 0.7f);
            Banner("SE CALMÓ... POR AHORA");
        }
        if (!_waveOn)
        {
            _waveT -= dt;
            if (_waveT <= 0)
            {
                _wave++;
                _waveOn = true;
                var (nb, te) = WaveOf(_wave);
                Banner(_wave == 1 ? "EL LIVING DE LA ABUELA" : "OLEADA " + _wave);
                // Aparecen de a poco, en los lugares más lejos de Ernesto.
                var spots = new List<Vector3>(_level.Spawns);
                spots.Sort((a, b) => Vector3.DistanceSquared(b, _p.Feet).CompareTo(Vector3.DistanceSquared(a, _p.Feet)));
                int k = 0;
                for (int i = 0; i < nb; i++) _pending.Add((0.35f * k, FoeKind.Neighbor, spots[k++ % spots.Count] + Jitter()));
                for (int i = 0; i < te; i++) _pending.Add((0.35f * k, FoeKind.Teacher, spots[k++ % spots.Count] + Jitter()));
            }
        }
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            var (t, kind, at) = _pending[i];
            t -= dt;
            if (t <= 0) { Spawn(kind, at); _pending.RemoveAt(i); }
            else _pending[i] = (t, kind, at);
        }
    }

    private readonly List<(float t, FoeKind kind, Vector3 at)> _pending = new();

    private Vector3 Jitter() => new(Rnd(-10, 10), 0, Rnd(-10, 10));

    private void Banner(string text) { _banner = text; _bannerT = 3; }

    private void UpdateFoes(float dt)
    {
        float wdt = _freeze > 0 ? 0 : dt;
        UpdateWaves(dt);
        _bannerT -= dt;
        if (wdt <= 0) return;
        using (Perf.Time("pensamientos"))
        {
            foreach (var f in _foes)
            {
                f.Think(this, wdt);
                f.Animate(wdt);
                // Los muñones chorrean.
                for (int b = 0; b < f.Bleeding.Length; b++)
                {
                    if (f.Bleeding[b] <= 0) continue;
                    f.Bleeding[b] -= wdt;
                    if (f.Anim.Wound((Bone)b) is { } w && _brng.Next(2) == 0)
                    {
                        var at = new Vector3(w.at.X, w.at.Y, w.at.Z);
                        var d = new Vector3(w.dir.X, w.dir.Y, w.dir.Z);
                        AddDrop(at, (d + RndDir() * 0.3f) * Rnd(30, 70) * MathF.Min(1, f.Bleeding[b]), Rnd(0.2f, 0.4f), DropKind.Blood, BloodFly);
                    }
                }
                // El cuerpo que cae suena.
                if (f.Dead && f.Anim.TakeImpact() is float imp && imp > 80) _game.Sfx.Play(Sound.BodyFall, N(f.Chest), MathF.Min(1, imp / 250), gap: 0.1f);
            }
            foreach (var pc in _pieces)
            {
                pc.Step(wdt);
                if (pc.Bleed > 0 && pc.Gib.Wound is { } w && _brng.Next(3) == 0)
                {
                    var at = new Vector3(w.at.X, w.at.Y, w.at.Z);
                    AddDrop(at, new Vector3(w.dir.X, w.dir.Y, w.dir.Z) * Rnd(20, 50), Rnd(0.2f, 0.35f), DropKind.Blood, BloodFly);
                }
                if (!pc.Landed && pc.Gib.Body.Body.TakeImpact() > 60) { pc.Landed = true; _game.Sfx.Play(Sound.GibLand, N(pc.Gib.Root), 0.6f, gap: 0.05f); }
            }
        }
    }

    // ------------------------------------------------------------------ la pelea

    /// <summary>Un pensamiento le pegó a Ernesto.</summary>
    public void FoeHits(Foe who, float damage, Vector3 at, Vector3 push)
    {
        if (_p.Health <= 0) return;
        if (!_p.Hurt(damage, push)) return;
        Hurt(damage, at);
        if (who.Kind == FoeKind.Neighbor) _game.Sfx.Play(Sound.DrillHit, N(at), 1);
    }

    private float _hurtT;

    /// <summary>Lo que se siente al recibir: el borde rojo, el sacudón, el quejido, y se pierde estilo.</summary>
    private void Hurt(float damage, Vector3 from)
    {
        _hurtT = 1;
        _shake = MathF.Max(_shake, 0.6f + damage / 30);
        _game.Sfx.Play(Sound.Hurt, N(_p.EyePos), 0.8f, gap: 0.2f, flat: true);
        _styleT = MathF.Max(0, _styleT - 60);
        // La propia sangre también salpica (pero no cura).
        for (int i = 0; i < 10; i++) AddDrop(_p.EyePos + _r.Cam.Forward * 4 + RndDir(), RndDir() * Rnd(20, 60), Rnd(0.2f, 0.4f), DropKind.Blood, BloodFly, 2);
    }

    /// <summary>Se murió un pensamiento: el estilo, la cuenta.</summary>
    private void Killed(Foe f, bool head, Weapon w)
    {
        f.StopDrill(this);
        int pts = 60;
        string why = w switch
        {
            Weapon.Kick => "+ CHANCLETAZO",
            Weapon.Slam => "+ APLASTADO",
            Weapon.Chalk => "+ SU PROPIA TIZA",
            _ => head ? "+ ¡A LA CABEZA!" : "+ UNO MENOS",
        };
        if (head) pts += 40;
        if (w is Weapon.Kick or Weapon.Slam) pts += 30;
        if (!_p.Grounded) { pts += 30; Style(30, "+ EN EL AIRE"); }
        if (!f.Grounded) { pts += 20; Style(20, "+ AL VUELO"); }
        Style(pts, why);
        // Varios seguidos.
        _multi = _time - _lastKill < 1.2f ? _multi + 1 : 1;
        _lastKill = _time;
        if (_multi == 2) Style(40, "+ DE A DOS");
        else if (_multi == 3) Style(80, "+ DE A TRES");
        else if (_multi > 3) Style(120, "+ MASACRE X" + _multi);
    }

    private int _multi;
    private float _lastKill = -10;

    /// <summary>Se tiró de cabeza contra el piso: la onda empuja para arriba (y lastima) lo que está cerca.</summary>
    private void Slammed()
    {
        float power = _p.SlamPower;
        _game.Sfx.Play(Sound.Slam, N(_p.Feet), 1, flat: true);
        _shake = MathF.Max(_shake, 0.7f + MathF.Min(1, power / 80));
        _dip = 1.2f;
        float radius = 30 + MathF.Min(30, power * 0.3f);
        for (int i = 0; i < 28; i++)
        {
            float a = i * MathF.Tau / 28;
            var d = new Vector3(MathF.Cos(a), 0.2f, MathF.Sin(a));
            AddDrop(_p.Feet + d * 3, d * Rnd(60, 120), Rnd(0.5f, 1), DropKind.Dust, new Color(150, 110, 80), 0.8f);
        }
        foreach (var f in _foes)
        {
            if (f.Dead || f.Spawning) continue;
            var d = f.Pos - _p.Feet;
            if (new Vector2(d.X, d.Z).Length() > radius || MathF.Abs(d.Y) > 14) continue;
            var push = Vector3.Normalize(new Vector3(d.X, 0, d.Z) + new Vector3(0.001f, 0, 0)) * 60 + new Vector3(0, 150, 0);
            Damage(f, Bone.Spine, 1, Vector3.UnitY, f.Chest, Weapon.Slam);
            if (!f.Dead) f.Knock(push, 0.8f);
        }
    }

    /// <summary>Ernesto se murió: la jaqueca ganó. A los tres segundos, de nuevo (todo limpio).</summary>
    private void Died(float dt)
    {
        if (_deadT == 0)
        {
            Banner("TE GANÓ LA JAQUECA");
            _bannerT = 3.5f;
            _game.Sfx.Play(Sound.Groan, N(_p.EyePos), 0.9f, flat: true);
        }
        _deadT += dt;
        _dip = MathF.Min(4, _dip + dt * 3);
        if (_deadT < 3.5f) return;
        foreach (var f in _foes) f.StopDrill(this);
        _foes.Clear();
        _pieces.Clear();
        _pending.Clear();
        _chalks.Clear();
        _dropN = 0;
        _decals.Clear();
        _p.Health = _p.MaxHealth;
        _p.Feet = _level.Start;
        _p.Vel = Vector3.Zero;
        _p.Yaw = _level.StartYaw;
        _p.Pitch = 0;
        _wave = 0;
        _waveOn = false;
        _waveT = 1.5f;
        _deadT = 0;
        _styleT = 0;
        StartFoes();
    }

    // ------------------------------------------------------------------ dibujar

    private void DrawFoes()
    {
        var cam = _r.Cam;
        foreach (var f in _foes)
        {
            if (!cam.Sees(f.Pos + new Vector3(0, 8, 0), f.Art.Mesh.Radius + 6)) continue;
            _r.Figures.Add(f.Draw());
            // Apareciendo: un resplandor rosado en el piso y polvo que sube.
            if (f.Spawning)
            {
                float k = 1 - f.SpawnT;
                _r.Glow.Billboard(f.Pos + new Vector3(0, 8 * f.SpawnT, 0), cam.Right, cam.Up, 10 * k + 2, new Color(255, 120, 170, (int)(200 * k)), -cam.Forward);
            }
        }
        foreach (var pc in _pieces)
        {
            if (!cam.Sees(pc.Gib.Root, 10)) continue;
            _r.Figures.Add(pc.Draw());
        }
    }
}
