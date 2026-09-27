using Jaqueca.Audio;
using Jaqueca.Client.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Jaqueca.Client.Screens;

/// <summary>
/// El estilo y lo que se ve encima: la mira, la vida y las cargas de dash (abajo a la izquierda), el arma
/// (abajo a la derecha), el medidor de estilo (arriba a la derecha) y los carteles. El estilo sube
/// haciendo cosas (matar en el aire, a la cabeza, devolver tizas, variar) y baja solo; recibir un golpe
/// lo baja de una. Sus rangos son los de un dolor de cabeza que empeora.
/// </summary>
public sealed partial class PlayScreen
{
    /// <summary>Los rangos: de una molestia a la muerte cerebral.</summary>
    private static readonly string[] Ranks = { "MOLESTIA", "PUNTADA", "PALPITACIÓN", "JAQUECA", "MIGRAÑA", "ANEURISMA", "A.C.V.", "MUERTE CEREBRAL" };
    private static readonly float[] RankAt = { 0, 60, 150, 260, 390, 540, 710, 900 };
    private static readonly Color[] RankColor =
    {
        new(170, 170, 170), new(120, 190, 220), new(110, 210, 140), new(230, 210, 90), new(240, 150, 60), new(240, 80, 70), new(220, 60, 160), new(255, 255, 255),
    };

    private float _styleT;
    private int _rank;
    private readonly List<(string text, float t)> _styleLog = new();

    private void Style(float points, string text)
    {
        _styleT = MathF.Min(RankAt[^1] + 150, _styleT + points);
        _styleLog.Insert(0, (text, 2.6f));
        if (_styleLog.Count > 7) _styleLog.RemoveAt(_styleLog.Count - 1);
    }

    private void UpdateStyle(float dt)
    {
        int rank = 0;
        for (int i = 0; i < RankAt.Length; i++) if (_styleT >= RankAt[i]) rank = i;
        // Baja solo, más rápido cuanto más arriba está.
        _styleT = MathF.Max(0, _styleT - dt * (10 + rank * 9));
        if (rank > _rank) _game.Sfx.Flat(Sound.StyleUp, 0.5f, 1 + rank * 0.04f);
        _rank = rank;
        for (int i = _styleLog.Count - 1; i >= 0; i--)
        {
            var (t, left) = _styleLog[i];
            left -= dt;
            if (left <= 0) _styleLog.RemoveAt(i); else _styleLog[i] = (t, left);
        }
        _hurtT = MathF.Max(0, _hurtT - dt * 1.8f);
    }

    /// <summary>El golpe tiñe el borde; la vida baja separa los colores (la puntada).</summary>
    private void HurtPost()
    {
        var post = _r.Post;
        float low = _p.Health < 35 ? (35 - _p.Health) / 35 : 0;
        post.Hurt = new Vector4(0.55f, 0.02f, 0.06f, MathF.Min(0.8f, _hurtT * 0.7f + low * 0.35f + (_p.Health <= 0 ? 0.6f : 0)));
        post.Aberration = _hurtT * 2.5f + low * 1.5f;
        post.Wobble = low * 0.5f + (_p.Health <= 0 ? 0.8f : 0);
        post.Saturation = 1.08f - low * 0.4f;
        post.Flash = _healGlow > 0 ? new Vector4(0.9f, 0.1f, 0.12f, _healGlow * 0.12f) : Vector4.Zero;
    }

    private void Rect(SpriteBatch sb, Rectangle dest, int scale, int x, int y, int w, int h, Color c) =>
        sb.Draw(_px, new Rectangle(dest.X + x * scale, dest.Y + y * scale, w * scale, h * scale), c);

    private void Text(SpriteBatch sb, Rectangle dest, int scale, string s, int x, int y, Color c, int size = 1, bool center = false, bool right = false)
    {
        int w = _font.Measure(s, size);
        float px = center ? x - w / 2f : right ? x - w : x;
        _font.Draw(sb, s, new Vector2(dest.X + px * scale, dest.Y + y * scale), c, new Color(20, 8, 12), scale: size * scale);
    }

    private void DrawHud(SpriteBatch sb, RenderTarget2D target, Rectangle dest, int scale)
    {
        _gd.SetRenderTarget(target);
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        int W = JaquecaGame.LowW, H = JaquecaGame.LowH;

        // La mira: cuatro rayitas y el punto; con el tiro cargado, se cierra.
        if (_p.Health > 0)
        {
            int cx = W / 2, cy = H / 2;
            int gap = 3 + (int)((1 - _charge) * 2);
            var cc = _charge >= 1 ? new Color(255, 220, 120) : new Color(250, 245, 240);
            var ink = new Color(20, 6, 10, 200);
            foreach (var (x, y, w, h) in new[] { (cx, cy, 1, 1), (cx - gap - 4, cy, 4, 1), (cx + gap + 1, cy, 4, 1), (cx, cy - gap - 4, 1, 4), (cx, cy + gap + 1, 1, 4) })
                Rect(sb, dest, scale, x - 1, y - 1, w + 2, h + 2, ink);
            Rect(sb, dest, scale, cx, cy, 1, 1, cc);
            Rect(sb, dest, scale, cx - gap - 4, cy, 4, 1, cc);
            Rect(sb, dest, scale, cx + gap + 1, cy, 4, 1, cc);
            Rect(sb, dest, scale, cx, cy - gap - 4, 1, 4, cc);
            Rect(sb, dest, scale, cx, cy + gap + 1, 1, 4, cc);
            // La carga del tiro que atraviesa: una barrita debajo.
            if (_charge > 0) Rect(sb, dest, scale, cx - 8, cy + gap + 8, (int)(17 * _charge), 2, cc);
        }

        // La vida y las cargas de dash.
        int bx = 14, by = H - 34;
        Rect(sb, dest, scale, bx - 2, by - 2, 124, 26, new Color(18, 8, 12, 190));
        Text(sb, dest, scale, "SANGRE", bx, by - 1, new Color(210, 170, 160));
        float hp = Math.Clamp(_p.Health / _p.MaxHealth, 0, 1);
        Rect(sb, dest, scale, bx, by + 10, 120, 6, new Color(60, 12, 18));
        Rect(sb, dest, scale, bx, by + 10, (int)(120 * hp), 6, _healGlow > 0.1f ? new Color(255, 90, 90) : new Color(200, 24, 36));
        Text(sb, dest, scale, ((int)MathF.Ceiling(MathF.Max(0, _p.Health))).ToString(), bx + 118, by - 1, Color.White, right: true);
        for (int i = 0; i < Player.MaxStamina; i++)
        {
            float k = Math.Clamp(_p.Stamina - i, 0, 1);
            Rect(sb, dest, scale, bx + i * 41, by + 18, 38, 3, new Color(30, 40, 60));
            Rect(sb, dest, scale, bx + i * 41, by + 18, (int)(38 * k), 3, k >= 1 ? new Color(120, 200, 255) : new Color(70, 110, 150));
        }

        // El arma.
        string wn = _weapon == 0 ? "REVÓLVER DE CEBITA" : "ESCOPETA DEL ABUELO";
        Text(sb, dest, scale, wn, W - 14, H - 22, new Color(230, 220, 200), right: true);
        Text(sb, dest, scale, _weapon == 0 ? "1" : "2", W - 14, H - 34, new Color(150, 130, 120), right: true);

        // El estilo: el rango, la barra hasta el próximo y lo último que sumó.
        if (_styleT > 0.5f || _styleLog.Count > 0)
        {
            int sx = W - 14, sy = 14;
            var col = RankColor[_rank];
            Text(sb, dest, scale, Ranks[_rank], sx, sy, col, 2, right: true);
            float lo = RankAt[_rank], hi = _rank + 1 < RankAt.Length ? RankAt[_rank + 1] : RankAt[^1] + 150;
            float k = Math.Clamp((_styleT - lo) / (hi - lo), 0, 1);
            Rect(sb, dest, scale, sx - 110, sy + 24, 110, 3, new Color(30, 20, 26, 200));
            Rect(sb, dest, scale, sx - 110, sy + 24, (int)(110 * k), 3, col);
            for (int i = 0; i < _styleLog.Count; i++)
            {
                var (t, left) = _styleLog[i];
                var c = new Color(240, 230, 210) * MathF.Min(1, left * 2);
                Text(sb, dest, scale, t, sx, sy + 32 + i * 11, c, right: true);
            }
        }

        // La oleada y los carteles.
        if (_waveOn)
        {
            int alive = 0;
            foreach (var f in _foes) if (!f.Dead) alive++;
            alive += _pending.Count;
            Text(sb, dest, scale, $"OLEADA {_wave}  ·  QUEDAN {alive}", 14, 14, new Color(210, 180, 190));
        }
        if (_bannerT > 0 && _banner != null)
        {
            float a = MathF.Min(1, _bannerT * 1.5f);
            Text(sb, dest, scale, _banner, W / 2, 70, new Color(245, 225, 215) * a, 2, center: true);
        }

        if (_game.Options.Debug)
        {
            Text(sb, dest, scale, $"{_game.Fps:0} fps  pensamientos {_foes.Count}  pedazos {_pieces.Count}  gotas {_dropN}", 14, 30, Color.White);
            Text(sb, dest, scale, $"pies {_p.Feet.X:0},{_p.Feet.Y:0},{_p.Feet.Z:0}  vel {new Vector2(_p.Vel.X, _p.Vel.Z).Length():0}", 14, 42, Color.White);
        }
        sb.End();
        if (_game.Options.Debug && (int)(_time * 1) != (int)((_time - 1 / 60f) * 1)) Console.WriteLine("[cuadro] " + Perf.Report());
    }

    // ------------------------------------------------------------------ el piloto automático

    private float _autoT, _autoJump, _autoStrafe = 1;

    /// <summary>
    /// Ernesto juega solo (para capturas y pruebas): apunta al pensamiento vivo más cercano, dispara,
    /// cambia de arma según la distancia, patea las tizas que se le vienen, se mueve de costado, salta y
    /// hace dash de a ratos.
    /// </summary>
    private Intent AutoIntent(float dt)
    {
        var it = new Intent { Weapon = -1 };
        _autoT += dt;
        Foe best = null;
        float bd = float.MaxValue;
        foreach (var f in _foes)
        {
            if (f.Dead || f.Spawning) continue;
            float d = Vector3.DistanceSquared(f.Pos, _p.Feet);
            if (d < bd) { bd = d; best = f; }
        }
        if (best != null)
        {
            var aim = (_autoT % 3 < 1.5f ? best.HeadCenter : best.Chest) - _p.EyePos;
            float yaw = MathF.Atan2(aim.Z, aim.X), pitch = MathF.Atan2(aim.Y, new Vector2(aim.X, aim.Z).Length());
            it.Yaw = Figures.Anim.Animator.Wrap(yaw - _p.Yaw) * MathF.Min(1, dt * 12);
            it.Pitch = (pitch - _p.Pitch) * MathF.Min(1, dt * 12);
            float dist = MathF.Sqrt(bd);
            it.Weapon = dist < 45 ? 1 : 0;
            bool onTarget = MathF.Abs(Figures.Anim.Animator.Wrap(yaw - _p.Yaw)) < 0.08f;
            it.Fire = onTarget && _cooldown <= 0;
            it.Move = new Vector2(_autoStrafe, dist > 60 ? 1 : dist < 25 ? -1 : 0);
        }
        if (_autoT > _autoJump) { it.Jump = true; it.JumpHeld = true; _autoJump = _autoT + 1.3f + (float)_brng.NextDouble() * 2; if (_brng.Next(3) == 0) it.Dash = true; }
        if (_brng.NextDouble() < dt * 0.5f) _autoStrafe = -_autoStrafe;
        // Una tiza que viene: patada.
        foreach (var c in _chalks)
            if (!c.Returned && Vector3.Distance(c.Pos, _p.EyePos) < 18 && _kickCool <= 0) { it.Kick = true; break; }
        return it;
    }
}
