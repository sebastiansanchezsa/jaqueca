using Jaqueca.Audio;
using Jaqueca.Client.Render;
using Microsoft.Xna.Framework;

namespace Jaqueca.Client.Screens;

/// <summary>
/// La sangre (y lo demás que vuela: polvo de tiza, chispas, astillas). Las gotas vuelan con su peso y,
/// donde tocan algo, dejan la mancha pegada (el parquet, las paredes, el sillón); lo cortado sigue
/// chorreando un rato. La sangre cura: la que le salpica a Ernesto (cerca de la cabeza) le devuelve vida,
/// así que la pelea se gana de cerca.
/// </summary>
public sealed partial class PlayScreen
{
    private enum DropKind : byte { Blood, Dust, Spark, Chunk }

    private struct Drop
    {
        public Vector3 Pos, Vel;
        public float Life, Size;
        public DropKind Kind;
        public Color Color;
    }

    private const int MaxDrops = 3000;
    private readonly Drop[] _drops = new Drop[MaxDrops];
    private int _dropN;
    private readonly DecalRing _decals = new();
    private readonly Random _brng = new(5);
    private float _healGlow, _healSoundT;

    private static readonly Color BloodFly = new(150, 12, 20), BloodDark = new(86, 6, 14), BloodPool = new(70, 4, 10);

    private float Rnd(float a, float b) => a + (b - a) * (float)_brng.NextDouble();
    private Vector3 RndDir() { var v = new Vector3(Rnd(-1, 1), Rnd(-1, 1), Rnd(-1, 1)); return v.LengthSquared() < 1e-4f ? Vector3.UnitY : Vector3.Normalize(v); }

    private void AddDrop(Vector3 at, Vector3 vel, float size, DropKind kind, Color col, float life = 3)
    {
        if (_dropN >= MaxDrops)
        {
            // Lleno: la gota nueva reemplaza una al azar (las viejas ya casi cayeron).
            int k = _brng.Next(MaxDrops);
            _drops[k] = new Drop { Pos = at, Vel = vel, Size = size, Kind = kind, Color = col, Life = life };
            return;
        }
        _drops[_dropN++] = new Drop { Pos = at, Vel = vel, Size = size, Kind = kind, Color = col, Life = life };
    }

    /// <summary>
    /// Un golpe que saca sangre en <paramref name="at"/> hacia <paramref name="dir"/>: <paramref name="amount"/>
    /// de 0 (un rasguño) a 3 (una cabeza que revienta). Si Ernesto está cerca, lo cura.
    /// </summary>
    private void Bleed(Vector3 at, Vector3 dir, float amount)
    {
        int n = (int)(8 + amount * 26);
        for (int i = 0; i < n; i++)
        {
            var v = (dir * Rnd(0.2f, 1.2f) + RndDir() * Rnd(0.2f, 0.9f)) * Rnd(40, 110) * (0.6f + amount * 0.25f);
            v.Y += Rnd(10, 60);
            AddDrop(at + RndDir() * 0.5f, v, Rnd(0.12f, 0.28f) * (1 + amount * 0.1f), DropKind.Blood, BloodFly, 4);
        }
        // Pedacitos de carne en lo fuerte.
        if (amount >= 1.5f)
            for (int i = 0; i < (int)(amount * 5); i++)
                AddDrop(at, (dir + RndDir()) * Rnd(40, 120) + new Vector3(0, Rnd(30, 80), 0), Rnd(0.35f, 0.7f), DropKind.Chunk, new Color(120, 30, 34), 6);
        // Curarse con la sangre de otro: de cerca, mucho; de lejos, nada.
        float d = Vector3.Distance(at, _p.EyePos);
        if (d < 30 && _p.Health > 0)
        {
            float heal = amount * 9 * (1 - d / 30);
            Heal(heal);
        }
    }

    private void Heal(float amount)
    {
        if (amount <= 0 || _p.Health >= _p.MaxHealth) return;
        _p.Health = MathF.Min(_p.MaxHealth, _p.Health + amount);
        _healGlow = MathF.Min(1, _healGlow + amount / 20);
        if (_healSoundT <= 0) { _game.Sfx.Play(Sound.Heal, N(_p.EyePos), 0.5f, flat: true); _healSoundT = 0.25f; }
    }

    /// <summary>Polvo (tiza, yeso) o chispas donde pega algo.</summary>
    private void Puff(Vector3 at, Vector3 n, int count, DropKind kind, Color col)
    {
        for (int i = 0; i < count; i++)
        {
            var v = (n * Rnd(0.3f, 1) + RndDir() * 0.8f) * (kind == DropKind.Spark ? Rnd(80, 200) : Rnd(15, 50));
            AddDrop(at + n * 0.3f, v, kind == DropKind.Spark ? Rnd(0.1f, 0.2f) : Rnd(0.18f, 0.42f), kind, col, kind == DropKind.Spark ? 0.3f : Rnd(0.6f, 1.2f));
        }
    }

    private void UpdateBlood(float dt)
    {
        _healGlow = MathF.Max(0, _healGlow - dt * 1.5f);
        _healSoundT -= dt;
        var eye = _p.EyePos;
        for (int i = _dropN - 1; i >= 0; i--)
        {
            ref var d = ref _drops[i];
            d.Life -= dt;
            float g = d.Kind switch { DropKind.Dust => 20, DropKind.Spark => 120, _ => 300 };
            d.Vel.Y -= g * dt;
            if (d.Kind == DropKind.Dust) d.Vel *= 1 - MathF.Min(1, dt * 3);
            var next = d.Pos + d.Vel * dt;
            bool gone = d.Life <= 0;
            if (!gone && d.Kind is DropKind.Blood or DropKind.Chunk)
            {
                // La sangre que salpica la cara de Ernesto también cura (un poquito cada gota).
                if (d.Kind == DropKind.Blood && Vector3.DistanceSquared(next, eye) < 7 * 7 && _p.Health > 0) { Heal(0.35f); gone = true; }
                else if (_level.Solids.Inside(next, out var n, out var at) || next.Y < 0)
                {
                    if (next.Y < 0) { n = Vector3.UnitY; at = new Vector3(next.X, 0, next.Z); }
                    // La mancha: más grande si venía rápido, estirada hacia donde iba.
                    float speed = d.Vel.Length();
                    float size = d.Size * (d.Kind == DropKind.Chunk ? 2.4f : 2.8f + MathF.Min(3, speed / 60));
                    float spin = MathF.Atan2(d.Vel.Z, d.Vel.X);
                    _decals.Add(at, n, size, spin, _brng.Next(3) == 0 ? BloodPool : BloodDark, 1 + MathF.Min(1.5f, speed / 150));
                    if (d.Kind == DropKind.Chunk && _brng.Next(3) == 0) _game.Sfx.Play(Sound.BloodDrip, N(at), 0.3f, gap: 0.08f);
                    gone = true;
                }
            }
            else if (!gone && d.Kind == DropKind.Dust && next.Y < 0) { next.Y = 0; d.Vel = Vector3.Zero; }
            if (gone) { _drops[i] = _drops[--_dropN]; continue; }
            d.Pos = next;
        }
    }

    /// <summary>Una gota en el aire: estirada hacia donde va (una rayita, no un cuadrado).</summary>
    private void Streak(Vector3 p, Vector3 vel, float size, Color col)
    {
        var cam = _r.Cam;
        var v = vel - cam.Forward * Vector3.Dot(vel, cam.Forward);
        float sp = v.Length();
        if (sp < 1) { _r.Flat.Billboard(p, cam.Right, cam.Up, size, col, -cam.Forward); return; }
        var dir = v / sp;
        var side = Vector3.Cross(dir, cam.Forward) * size;
        var len = dir * (size + MathF.Min(sp * 0.012f, 1.6f));
        _r.Flat.Quad(p - len - side, p + len - side, p + len + side, p - len + side, -cam.Forward, col);
    }

    private void DrawBlood()
    {
        var cam = _r.Cam;
        var right = cam.Right;
        var up = cam.Up;
        var n = -cam.Forward;
        for (int i = 0; i < _dropN; i++)
        {
            ref var d = ref _drops[i];
            if (!cam.Sees(d.Pos, d.Size + 1)) continue;
            switch (d.Kind)
            {
                case DropKind.Spark:
                    _r.Glow.Billboard(d.Pos, right, up, d.Size, new Color(255, 200, 120, 255), n);
                    break;
                case DropKind.Dust:
                    _r.Flat.Billboard(d.Pos, right, up, d.Size * MathF.Min(1, d.Life * 1.5f), d.Color, n);
                    break;
                case DropKind.Blood:
                    Streak(d.Pos, d.Vel, d.Size, d.Color);
                    break;
                default:
                    _r.Flat.Billboard(d.Pos, right, up, d.Size, d.Color, n);
                    break;
            }
        }
    }
}
