using Jaqueca.Audio;
using Jaqueca.Client.Game;
using Jaqueca.Client.Render;
using Jaqueca.Client.Ui;
using Jaqueca.Client.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Jaqueca.Client.Screens;

/// <summary>
/// La partida: Ernesto en un recuerdo (por ahora, el living de la abuela) contra las oleadas de
/// pensamientos. Está repartida por tema: el movimiento y la cámara acá; las armas en
/// PlayScreen.Combat.cs; los pensamientos y las oleadas en PlayScreen.Foes.cs; la sangre en
/// PlayScreen.Blood.cs; el estilo y el HUD en PlayScreen.Hud.cs.
/// </summary>
public sealed partial class PlayScreen : IScreen, IDisposable
{
    private readonly JaquecaGame _game;
    private readonly GraphicsDevice _gd;
    private readonly Renderer _r;
    private readonly FoeArt _art;
    private readonly Living _level = new();
    private readonly Player _p;
    private readonly ViewModel _vm;
    private readonly PixelFont _font;
    private readonly Texture2D _px;
    private float _time;
    private float _mouseSens = 0.0026f;
    /// <summary>Cuánto se hunde la cámara al caer y el sacudón (tiros, golpes al piso).</summary>
    private float _dip, _shake, _roll;
    private Mixer.Voice _slideVoice, _roomVoice;
    private float _clockT;
    private int _clockTick;

    public PlayScreen(JaquecaGame game)
    {
        _game = game;
        _gd = game.GraphicsDevice;
        _r = new Renderer(_gd, game.Content);
        _art = new FoeArt(_gd);
        _font = new PixelFont(_gd);
        _px = new Texture2D(_gd, 1, 1);
        _px.SetData(new[] { Color.White });

        _r.Static.Add(_level.Build(_gd));
        _r.Lights.Items.AddRange(_level.Lights);
        _r.ShadowFocus = Vector3.Zero;
        _r.Shadow.Extent = 420;
        // La luz de adentro de la cabeza: rosada y tibia desde arriba (no hay techo), el ambiente violeta.
        _r.Sky.Fixed = new Sky.Ambience(new Vector3(0.74f, 0.57f, 0.5f), new Vector3(0.33f, 0.28f, 0.34f), new Vector3(0.19f, 0.13f, 0.13f), new Vector3(0.12f, 0.1f, 0.13f), 58);
        _r.Decals = _decals;

        var o = game.Options;
        var start = o.At is { } at ? new Vector3(at.X, 0, at.Y) : _level.Start;
        start.Y = _level.Solids.GroundAt(start.X, start.Z, 200, Player.Radius);
        if (start.Y < 0) start.Y = 0;
        _p = new Player(start, o.Look is { } look ? MathHelper.ToRadians(look) : _level.StartYaw);
        if (o.LookUp is { } up) _p.Pitch = MathHelper.ToRadians(up);
        _vm = new ViewModel(_art) { Shown = o.Weapon };
        _vm.Switch(o.Weapon);
        _weapon = o.Weapon;
        // Las capturas y el piloto automático quieren los sonidos listos desde el principio.
        if (o.ShotTime > 0 || o.Seq > 0 || o.AutoPlay) Audio.Sounds.Wait();
        StartFoes();
    }

    public void Update(float dt)
    {
        _time += dt;
        _r.Time = _time;
        var input = _game.Input;
        var it = _game.Options.AutoPlay ? AutoIntent(dt) : ReadIntent(input);

        // El golpe que congela un instante (la patada que pega, la tiza devuelta): el mundo se para, la cabeza no.
        float wdt = _freeze > 0 ? 0 : dt;
        var ev = _p.Health > 0 ? _p.Update(it, _level.Solids, wdt) : MoveEvent.None;
        if (_p.Health > 0) Moved(ev, dt);
        UpdateCombat(it, dt);
        UpdateFoes(dt);
        UpdateBlood(dt);
        UpdateStyle(dt);
        _r.Lights.Update(dt);
        if (_game.Options.Immortal) _p.Health = _p.MaxHealth;
        if (_p.Health <= 0) Died(dt);

        float speed = new Vector2(_p.Vel.X, _p.Vel.Z).Length();
        _vm.Update(dt, speed, _p.Grounded, new Vector2(it.Yaw, it.Pitch) * 8);

        // El sonido: los oídos en la cabeza de Ernesto.
        var mx = _game.Sound.Mixer;
        mx.Listener = N(_p.EyePos);
        mx.ListenerRight = N(_r.Cam.Right);
        mx.ListenerForward = N(_r.Cam.Forward);
        Ambience(dt);
        if (input.Pressed(Keys.Escape)) _game.Exit();
    }

    private static System.Numerics.Vector3 N(Vector3 v) => new(v.X, v.Y, v.Z);

    private Intent ReadIntent(Engine.InputState input)
    {
        var it = new Intent { Weapon = -1 };
        if (input.Down(Keys.W)) it.Move.Y += 1;
        if (input.Down(Keys.S)) it.Move.Y -= 1;
        if (input.Down(Keys.D)) it.Move.X += 1;
        if (input.Down(Keys.A)) it.Move.X -= 1;
        it.Jump = input.Pressed(Keys.Space);
        it.JumpHeld = input.Down(Keys.Space);
        it.Dash = input.Pressed(Keys.LeftShift) || input.Pressed(Keys.RightShift);
        it.Crouch = input.Down(Keys.LeftControl) || input.Down(Keys.C);
        it.CrouchPressed = input.Pressed(Keys.LeftControl) || input.Pressed(Keys.C);
        it.Fire = input.LeftPressed;
        it.FireHeld = input.LeftDown;
        it.Alt = input.RightPressed;
        it.AltHeld = input.RightDown;
        it.AltReleased = input.RightReleased;
        it.Kick = input.Pressed(Keys.F) || input.Pressed(Keys.V);
        if (input.Pressed(Keys.D1)) it.Weapon = 0;
        if (input.Pressed(Keys.D2)) it.Weapon = 1;
        if (input.ScrollDelta != 0) it.Weapon = 1 - _weapon;
        it.Yaw = input.MouseDelta.X * _mouseSens;
        it.Pitch = -input.MouseDelta.Y * _mouseSens;
        // El mando: el palo izquierdo camina, el derecho mira.
        var pad = input.Pad;
        if (pad.IsConnected)
        {
            it.Move += new Vector2(pad.ThumbSticks.Left.X, pad.ThumbSticks.Left.Y);
            it.Yaw += pad.ThumbSticks.Right.X * 0.05f;
            it.Pitch += pad.ThumbSticks.Right.Y * 0.035f;
            it.Jump |= input.PadPressed(Buttons.A);
            it.JumpHeld |= input.PadDown(Buttons.A);
            it.Dash |= input.PadPressed(Buttons.LeftShoulder);
            it.Crouch |= input.PadDown(Buttons.B);
            it.CrouchPressed |= input.PadPressed(Buttons.B);
            it.Fire |= pad.Triggers.Right > 0.5f && !_padFire;
            it.FireHeld |= pad.Triggers.Right > 0.5f;
            it.Alt |= pad.Triggers.Left > 0.5f && !_padAlt;
            it.AltHeld |= pad.Triggers.Left > 0.5f;
            it.AltReleased |= pad.Triggers.Left <= 0.5f && _padAlt;
            it.Kick |= input.PadPressed(Buttons.RightShoulder);
            if (input.PadPressed(Buttons.Y)) it.Weapon = 1 - _weapon;
            _padFire = pad.Triggers.Right > 0.5f;
            _padAlt = pad.Triggers.Left > 0.5f;
        }
        return it;
    }

    private bool _padFire, _padAlt;

    /// <summary>El sonido y la cámara de lo que hizo moviéndose.</summary>
    private void Moved(MoveEvent ev, float dt)
    {
        var sfx = _game.Sfx;
        var feet = _p.Feet;
        if (ev.HasFlag(MoveEvent.Step)) sfx.Play(Sound.StepWood, feet, 0.55f);
        if (ev.HasFlag(MoveEvent.Jumped)) sfx.Play(Sound.Jump, feet, 0.6f);
        if (ev.HasFlag(MoveEvent.WallJumped)) sfx.Play(Sound.WallJump, feet, 0.8f);
        if (ev.HasFlag(MoveEvent.Dashed)) sfx.Play(Sound.Dash, _p.EyePos, 0.8f, flat: true);
        if (ev.HasFlag(MoveEvent.Landed) && !ev.HasFlag(MoveEvent.SlamLand))
        {
            sfx.Play(Sound.Land, feet, Math.Clamp(_p.LandImpact / 250, 0.25f, 1));
            _dip = MathF.Max(_dip, Math.Clamp(_p.LandImpact / 300, 0, 1.2f));
            _vm.Land(_p.LandImpact);
        }
        if (ev.HasFlag(MoveEvent.SlamLand)) Slammed();
        if (ev.HasFlag(MoveEvent.SlideStart)) _slideVoice ??= sfx.Loop(Sound.Slide, N(feet), 0.7f, Bus.Effects, fadeIn: 0.05f);
        if (ev.HasFlag(MoveEvent.SlideEnd) || (!_p.Sliding && _slideVoice != null)) { sfx.Stop(_slideVoice, 0.12f); _slideVoice = null; }
        if (_slideVoice != null) _slideVoice.At = N(feet);
        _dip = MathF.Max(0, _dip - dt * 4);
        _shake = MathF.Max(0, _shake - dt * 3);
        // La cabeza se inclina al ir de costado y al deslizarse.
        float side = Vector3.Dot(_p.Vel, _p.FlatRight) / Player.RunSpeed;
        float want = -side * 0.035f + (_p.Sliding ? -0.07f : 0);
        _roll += (want - _roll) * MathF.Min(1, dt * 10);
    }

    /// <summary>La casa suena: la heladera y el televisor, y el reloj de pie que no para.</summary>
    private void Ambience(float dt)
    {
        _roomVoice ??= _game.Sfx.Loop(Sound.RoomTone, N(_level.TvAt), 0.5f);
        _clockT += dt;
        if (_clockT >= 1)
        {
            _clockT -= 1;
            _game.Sfx.Play(Sound.Clock, N(_level.ClockAt), 0.45f, pitch: _clockTick++ % 2 == 0 ? 1 : 0.94f, spread: 0.01f, bus: Bus.Ambience);
        }
    }

    public void Draw(SpriteBatch sb, RenderTarget2D target, Rectangle dest, int scale)
    {
        var cam = _r.Cam;
        float bobY = _p.Grounded ? -MathF.Abs(MathF.Sin(_time * 9)) * 0.25f * Math.Clamp(new Vector2(_p.Vel.X, _p.Vel.Z).Length() / Player.RunSpeed, 0, 1) : 0;
        var shake = _shake > 0 ? new Vector3(MathF.Sin(_time * 91), MathF.Sin(_time * 77), 0) * _shake * 0.6f : Vector3.Zero;
        cam.Eye = _p.EyePos + new Vector3(0, bobY - _dip * 2.2f, 0) + shake;
        cam.Yaw = _p.Yaw;
        cam.Pitch = _p.Pitch;
        cam.Roll = _roll;
        // El dash abre el campo de visión (la velocidad).
        float want = 72 + (_p.Dashing ? 8 : 0) + (_p.Sliding ? 4 : 0);
        cam.Fov += (want - cam.Fov) * 0.25f;
        cam.Rebuild();

        _r.ClearFrame();
        DrawFoes();
        DrawBlood();
        DrawCombat();
        _vm.Draw(_r, _time);
        HurtPost();
        using (Perf.Time("render")) _r.Draw(sb, target, dest, scale);
        DrawHud(sb, target, dest, scale);
    }

    public void Dispose()
    {
        _r.Dispose();
        _art.Dispose();
        _font.Dispose();
        _px.Dispose();
    }
}
