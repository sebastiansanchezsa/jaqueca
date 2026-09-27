using Jaqueca.Client.World;
using Microsoft.Xna.Framework;

namespace Jaqueca.Client.Game;

/// <summary>Lo que quiere hacer Ernesto en este cuadro (lo arma el teclado y el mouse, o el piloto automático).</summary>
public struct Intent
{
    /// <summary>x a la derecha, y hacia adelante (-1..1).</summary>
    public Vector2 Move;
    public bool Jump, JumpHeld, Dash, Crouch, CrouchPressed;
    public bool Fire, FireHeld, Alt, AltHeld, AltReleased, Kick;
    /// <summary>El arma que eligió (-1 = no cambió).</summary>
    public int Weapon;
    /// <summary>Cuánto gira la cabeza (radianes).</summary>
    public float Yaw, Pitch;
}

/// <summary>Lo que pasó moviéndose (para el sonido, la cámara y la pelea).</summary>
[Flags]
public enum MoveEvent
{
    None = 0, Jumped = 1, Landed = 2, Dashed = 4, SlideStart = 8, SlideEnd = 16, SlamStart = 32, SlamLand = 64, WallJumped = 128, Step = 256,
}

/// <summary>
/// Ernesto en pijama: cómo se mueve adentro de su cabeza (rápido y en el aire, como en ULTRAKILL).
/// Corre fuerte y frena en seco; salta; hace dash (tres cargas que vuelven solas; mientras dura no lo
/// tocan); se desliza por el piso sin perder velocidad (y si salta deslizándose, sale volando largo);
/// en el aire, agacharse es tirarse de cabeza contra el piso (el golpe empuja lo que está cerca y, si
/// salta justo al caer, rebota más alto); y se impulsa en las paredes (tres veces antes de tocar el piso).
/// Es un cilindro parado para chocar (ver <see cref="Solids.Move"/>).
/// </summary>
public sealed class Player
{
    public const float Radius = 3.2f, Height = 14.5f, EyeHeight = 13f, SlideHeight = 7f, SlideEye = 5.5f;
    public const float RunSpeed = 100, GroundAccel = 1100, AirAccel = 420, Gravity = 330, JumpSpeed = 118, MaxFall = 520;
    public const float DashSpeed = 290, DashTime = 0.16f, DashRecharge = 1.1f, SlideSpeed = 175, SlamSpeed = 640;
    public const int MaxStamina = 3, MaxWallJumps = 3;

    public Vector3 Feet, Vel;
    public float Yaw, Pitch;
    public bool Grounded { get; private set; }
    public bool Sliding { get; private set; }
    public bool Slamming { get; private set; }
    public bool Dashing => _dashT > 0;
    /// <summary>Cargas de dash (0..3, con decimales mientras se recarga).</summary>
    public float Stamina = MaxStamina;
    public float Health = 100, MaxHealth = 100;
    /// <summary>Qué tan fuerte cayó en el último golpe al piso (para la onda del golpe).</summary>
    public float SlamPower { get; private set; }
    /// <summary>Qué tan fuerte aterrizó (para que la cámara se hunda).</summary>
    public float LandImpact { get; private set; }
    /// <summary>La altura de los ojos, suave (se agacha al deslizarse).</summary>
    public float Eye { get; private set; } = EyeHeight;
    public Vector3 EyePos => Feet + new Vector3(0, Eye, 0);
    public Vector3 Forward => Render.FpsCamera.Dir(Yaw, Pitch);
    public Vector3 FlatForward => new(MathF.Cos(Yaw), 0, MathF.Sin(Yaw));
    public Vector3 FlatRight => new(-MathF.Sin(Yaw), 0, MathF.Cos(Yaw));
    /// <summary>La altura del cuerpo ahora (deslizándose es la mitad).</summary>
    public float BodyHeight => Sliding ? SlideHeight : Height;

    private float _dashT, _coyote, _jumpBuffer, _sinceSlam = 10, _slamFrom, _stepDist, _airTime;
    private int _wallJumps;
    private Vector3 _dashDir, _slideDir;

    public Player(Vector3 feet, float yaw) { Feet = feet; Yaw = yaw; }

    public MoveEvent Update(in Intent it, Solids world, float dt)
    {
        var ev = MoveEvent.None;
        Yaw += it.Yaw;
        Pitch = Math.Clamp(Pitch + it.Pitch, -1.5f, 1.5f);
        var fwd = FlatForward;
        var right = FlatRight;
        var wish = fwd * it.Move.Y + right * it.Move.X;
        if (wish.LengthSquared() > 1) wish.Normalize();

        Stamina = MathF.Min(MaxStamina, Stamina + dt / DashRecharge);
        _coyote = Grounded ? 0.12f : _coyote - dt;
        _jumpBuffer = it.Jump ? 0.12f : _jumpBuffer - dt;
        _sinceSlam += dt;

        // ---- dash
        if (it.Dash && Stamina >= 1 && !Dashing)
        {
            Stamina -= 1;
            _dashDir = wish.LengthSquared() > 0.01f ? Vector3.Normalize(wish) : fwd;
            _dashT = DashTime;
            Sliding = false;
            Slamming = false;
            ev |= MoveEvent.Dashed;
        }

        // ---- deslizarse (agachado en el piso) y el golpe (agachado en el aire)
        if (it.CrouchPressed && !Grounded && _coyote <= 0 && !Slamming && !Dashing)
        {
            Slamming = true;
            _slamFrom = Feet.Y;
            Vel = new Vector3(0, -SlamSpeed, 0);
            ev |= MoveEvent.SlamStart;
        }
        if (it.Crouch && Grounded && !Sliding && !Dashing)
        {
            Sliding = true;
            var h = new Vector3(Vel.X, 0, Vel.Z);
            _slideDir = wish.LengthSquared() > 0.01f ? Vector3.Normalize(wish) : h.LengthSquared() > 1 ? Vector3.Normalize(h) : fwd;
            ev |= MoveEvent.SlideStart;
        }
        if (Sliding && (!it.Crouch || !Grounded))
        {
            Sliding = false;
            ev |= MoveEvent.SlideEnd;
        }

        if (Dashing)
        {
            _dashT -= dt;
            Vel = _dashDir * DashSpeed;
            // Saltar en el dash: sale disparado largo (el dash se corta, la velocidad queda).
            if (_jumpBuffer > 0 && (Grounded || _coyote > 0))
            {
                _dashT = 0;
                Vel = _dashDir * 230 + new Vector3(0, JumpSpeed * 0.8f, 0);
                _jumpBuffer = 0; _coyote = 0;
                ev |= MoveEvent.Jumped;
            }
            else if (_dashT <= 0)
            {
                // Al terminar, se frena hasta un poco más que correr.
                var h = new Vector3(Vel.X, 0, Vel.Z);
                if (h.Length() > RunSpeed * 1.25f) h = Vector3.Normalize(h) * RunSpeed * 1.25f;
                Vel = new Vector3(h.X, 0, h.Z);
            }
        }
        else if (Slamming)
        {
            Vel = new Vector3(0, -SlamSpeed, 0);
        }
        else if (Sliding)
        {
            // Se desliza a velocidad pareja; mirar hacia un costado la tuerce un poco.
            float turn = Math.Clamp(it.Move.X, -1, 1) * 1.2f * dt;
            _slideDir = Vector3.Transform(_slideDir, Matrix.CreateRotationY(-turn));
            float sp = MathF.Max(SlideSpeed, new Vector3(Vel.X, 0, Vel.Z).Length() * 0.995f);
            Vel = new Vector3(_slideDir.X * sp, Vel.Y, _slideDir.Z * sp);
        }
        else if (Grounded)
        {
            var h = new Vector3(Vel.X, 0, Vel.Z);
            var target = wish * RunSpeed;
            // Más rápido que correr (venía de un dash o de deslizarse): se frena de a poco hacia correr.
            float accel = h.Length() > RunSpeed * 1.05f ? 600 : GroundAccel;
            h = MoveTowards(h, target, accel * dt);
            Vel = new Vector3(h.X, Vel.Y, h.Z);
        }
        else
        {
            // En el aire: suma hacia donde quiere ir, pero no le saca la velocidad que traía.
            if (wish.LengthSquared() > 0.01f)
            {
                var dir = Vector3.Normalize(wish);
                float along = Vector3.Dot(new Vector3(Vel.X, 0, Vel.Z), dir);
                float add = MathF.Min(AirAccel * dt, MathF.Max(0, RunSpeed - along));
                Vel += dir * add;
            }
        }

        // ---- saltos
        if (!Dashing && !Slamming && _jumpBuffer > 0)
        {
            if (Grounded || _coyote > 0)
            {
                float vy = JumpSpeed;
                // Rebote: saltar justo después del golpe al piso sube según de qué altura venía.
                if (_sinceSlam < 0.18f) vy += Math.Clamp(SlamPower * 0.9f, 0, 90);
                // Deslizándose: sale largo y bajo, con toda la velocidad.
                if (Sliding)
                {
                    var h = new Vector3(Vel.X, 0, Vel.Z);
                    Vel = new Vector3(h.X * 1.05f, vy * 0.85f, h.Z * 1.05f);
                    Sliding = false;
                    ev |= MoveEvent.SlideEnd;
                }
                else Vel.Y = vy;
                Grounded = false;
                _coyote = 0; _jumpBuffer = 0;
                ev |= MoveEvent.Jumped;
            }
            else if (_wallJumps < MaxWallJumps && world.WallNear(Feet, Radius, Height, 2.5f, out var n))
            {
                // Contra la pared: sale para afuera y para arriba (lo que venía de costado se conserva a medias).
                var h = new Vector3(Vel.X, 0, Vel.Z);
                h -= n * Vector3.Dot(h, n);
                Vel = h * 0.6f + n * 115 + new Vector3(0, JumpSpeed * 0.95f, 0);
                _wallJumps++;
                _jumpBuffer = 0;
                ev |= MoveEvent.WallJumped;
            }
        }

        if (!Dashing && !Grounded && !Slamming) Vel.Y = MathF.Max(Vel.Y - Gravity * dt, -MaxFall);
        if (!it.JumpHeld && Vel.Y > 40 && !Dashing && !Slamming) Vel.Y -= Gravity * 0.8f * dt;

        // ---- mover y chocar
        bool was = Grounded;
        float fallSpeed = -Vel.Y;
        var before = Feet;
        Feet = world.Move(Feet, Radius, BodyHeight, Vel * dt, 4.5f, Grounded && Vel.Y <= 0, out bool grounded, out bool bonk, out var wall);
        if (bonk && Vel.Y > 0) Vel.Y = 0;
        if (wall != Vector3.Zero)
        {
            float into = Vector3.Dot(Vel, wall);
            if (into < 0) Vel -= wall * into;
        }
        Grounded = grounded && Vel.Y <= 0.01f;
        if (Grounded)
        {
            if (Vel.Y < 0) Vel.Y = 0;
            _wallJumps = 0;
            if (!was)
            {
                LandImpact = fallSpeed;
                ev |= MoveEvent.Landed;
                if (Slamming)
                {
                    Slamming = false;
                    SlamPower = MathF.Max(0, _slamFrom - Feet.Y) + 20;
                    _sinceSlam = 0;
                    ev |= MoveEvent.SlamLand;
                }
            }
            // Pasos: por distancia recorrida en el piso.
            if (!Sliding)
            {
                _stepDist += new Vector2(Feet.X - before.X, Feet.Z - before.Z).Length();
                if (_stepDist > 26) { _stepDist = 0; ev |= MoveEvent.Step; }
            }
            _airTime = 0;
        }
        else _airTime += dt;

        float wantEye = Sliding ? SlideEye : EyeHeight;
        Eye += (wantEye - Eye) * MathF.Min(1, dt * 18);
        return ev;
    }

    /// <summary>Segundos que lleva en el aire.</summary>
    public float AirTime => _airTime;

    public static Vector3 MoveTowards(Vector3 a, Vector3 b, float maxStep)
    {
        var d = b - a;
        float len = d.Length();
        return len <= maxStep || len < 1e-5f ? b : a + d / len * maxStep;
    }

    /// <summary>Le pegan: resta vida (no mientras hace dash) y lo empuja.</summary>
    public bool Hurt(float damage, Vector3 push)
    {
        if (Dashing) return false;
        Health -= damage;
        Vel += push;
        return true;
    }
}
