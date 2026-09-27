using Jaqueca.Client.Game;
using Jaqueca.Client.World;
using Microsoft.Xna.Framework;

namespace Jaqueca.Tests;

/// <summary>
/// Cómo se mueve Ernesto en el living de verdad (el mismo armado que el juego, sin GPU): que llegue a los
/// muebles, que las paredes frenen, que deslizarse, el dash, el salto en la pared y el rebote del golpe
/// al piso hagan lo que prometen.
/// </summary>
public class MovementTests
{
    private const float Dt = 1 / 60f;

    private static Solids Level()
    {
        var l = new Living();
        l.Assemble();
        return l.Solids;
    }

    /// <summary>Corre <paramref name="seconds"/> con lo que pide <paramref name="want"/> (según el tiempo) y devuelve lo más alto que llegó.</summary>
    private static float Run(Player p, Solids s, float seconds, Func<float, Intent> want)
    {
        float top = p.Feet.Y;
        for (float t = 0; t < seconds; t += Dt)
        {
            p.Update(want(t), s, Dt);
            top = MathF.Max(top, p.Feet.Y);
        }
        return top;
    }

    private static Intent Idle => new() { Weapon = -1 };

    [Fact]
    public void SaltaMasAltoQueElAsientoDeUnaSilla()
    {
        var s = Level();
        var p = new Player(new Vector3(0, 0, 60), 0);
        Run(p, s, 0.2f, _ => Idle);
        float top = Run(p, s, 1.2f, t => new Intent { Weapon = -1, Jump = t < Dt, JumpHeld = true });
        Assert.True(top > 18, $"el salto llega a {top:0.0} (el asiento está a 16)");
        Assert.True(p.Grounded, "vuelve a apoyar");
    }

    [Fact]
    public void CaeSobreLaMesa()
    {
        var s = Level();
        var p = new Player(new Vector3(-65, 60, -28), 0);
        Run(p, s, 1.5f, _ => Idle);
        Assert.True(p.Grounded);
        Assert.InRange(p.Feet.Y, 29.5f, 30.5f);
    }

    [Fact]
    public void DeLaSillaSaltaALaMesa()
    {
        var s = Level();
        // Parado en la silla de adelante de la mesa (asiento a 16), mirando al sur (hacia la mesa).
        var p = new Player(new Vector3(-84, 16, -60), MathHelper.PiOver2);
        Run(p, s, 0.3f, _ => Idle);
        Assert.InRange(p.Feet.Y, 15.5f, 16.5f);
        // Salta hacia adelante y suelta cuando ya está sobre la mesa.
        Run(p, s, 1.2f, t => new Intent { Weapon = -1, Jump = t < Dt, JumpHeld = true, Move = new Vector2(0, t < 0.3f ? 1 : 0) });
        Assert.True(p.Grounded);
        Assert.True(p.Feet.Y > 29, $"terminó a {p.Feet.Y:0.0} (la mesa está a 30)");
    }

    [Fact]
    public void LasParedesFrenan()
    {
        var s = Level();
        // Corre hacia el norte desde el medio (entre la mesa y el modular), con dash y todo.
        var p = new Player(new Vector3(70, 0, 0), -MathHelper.PiOver2);
        Run(p, s, 4, t => new Intent { Weapon = -1, Move = new Vector2(0, 1), Dash = (int)(t * 2) != (int)((t - Dt) * 2) });
        Assert.True(p.Feet.Z >= -Living.D + Player.Radius - 0.01f, $"llegó a z = {p.Feet.Z:0.00}");
    }

    [Fact]
    public void DeslizarseNoPierdeVelocidad()
    {
        var s = Level();
        var p = new Player(new Vector3(-20, 0, 80), -MathHelper.PiOver2);
        Run(p, s, 0.2f, _ => Idle);
        Run(p, s, 0.7f, _ => new Intent { Weapon = -1, Crouch = true, Move = new Vector2(0, 1) });
        Assert.True(p.Sliding);
        float speed = new Vector2(p.Vel.X, p.Vel.Z).Length();
        Assert.True(speed >= Player.SlideSpeed * 0.95f, $"va a {speed:0}");
        Assert.True(p.Eye < Player.EyeHeight - 3, "se agacha");
    }

    [Fact]
    public void SaltarDeslizandoseLlegaMasLejos()
    {
        float Distance(bool slide)
        {
            var s = Level();
            var p = new Player(new Vector3(-20, 0, 90), -MathHelper.PiOver2);
            Run(p, s, 0.2f, _ => Idle);
            Run(p, s, 0.5f, _ => new Intent { Weapon = -1, Crouch = slide, Move = new Vector2(0, 1) });
            float z0 = p.Feet.Z;
            Run(p, s, 0.02f, _ => new Intent { Weapon = -1, Jump = true, JumpHeld = true, Crouch = slide, Move = new Vector2(0, 1) });
            for (int i = 0; i < 200 && !p.Grounded; i++) p.Update(new Intent { Weapon = -1, JumpHeld = true, Move = new Vector2(0, 1) }, s, Dt);
            return z0 - p.Feet.Z;
        }
        float plain = Distance(false), slid = Distance(true);
        Assert.True(slid > plain * 1.3f, $"deslizándose {slid:0}, corriendo {plain:0}");
    }

    [Fact]
    public void ElDashRecorreLargoYGastaUnaCarga()
    {
        var s = Level();
        var p = new Player(new Vector3(0, 0, 60), -MathHelper.PiOver2);
        Run(p, s, 0.2f, _ => Idle);
        float z0 = p.Feet.Z;
        Run(p, s, Player.DashTime, t => new Intent { Weapon = -1, Dash = t < Dt, Move = new Vector2(0, 1) });
        Assert.True(z0 - p.Feet.Z > 38, $"recorrió {z0 - p.Feet.Z:0.0}");
        Assert.InRange(p.Stamina, Player.MaxStamina - 1.05f, Player.MaxStamina - 0.8f);
    }

    [Fact]
    public void SeImpulsaEnLaParedTresVeces()
    {
        var s = Level();
        // Pegado a la pared oeste, en el aire.
        var p = new Player(new Vector3(-Living.W + Player.Radius + 0.5f, 40, -100), MathHelper.Pi);
        int jumps = 0;
        for (int i = 0; i < 5; i++)
        {
            // Vuelve contra la pared y salta.
            p.Feet = new Vector3(-Living.W + Player.Radius + 0.5f, 40, -100);
            p.Vel = new Vector3(-20, -10, 0);
            var ev = p.Update(new Intent { Weapon = -1, Jump = true, JumpHeld = true }, s, Dt);
            if (ev.HasFlag(MoveEvent.WallJumped)) { jumps++; Assert.True(p.Vel.X > 50 && p.Vel.Y > 50, "sale para afuera y para arriba"); }
            Run(p, s, 0.05f, _ => Idle);
        }
        Assert.Equal(Player.MaxWallJumps, jumps);
    }

    [Fact]
    public void ElGolpeAlPisoRebota()
    {
        float Jump(bool slam)
        {
            var s = Level();
            var p = new Player(new Vector3(0, slam ? 60 : 0, 40), 0);
            if (slam)
            {
                Run(p, s, 0.05f, _ => Idle);
                p.Update(new Intent { Weapon = -1, CrouchPressed = true, Crouch = true }, s, Dt);
                for (int i = 0; i < 120 && !p.Grounded; i++) p.Update(Idle, s, Dt);
            }
            else Run(p, s, 0.1f, _ => Idle);
            var ev = p.Update(new Intent { Weapon = -1, Jump = true, JumpHeld = true }, s, Dt);
            Assert.True(ev.HasFlag(MoveEvent.Jumped));
            return p.Vel.Y;
        }
        Assert.True(Jump(true) > Jump(false) + 30);
    }
}
