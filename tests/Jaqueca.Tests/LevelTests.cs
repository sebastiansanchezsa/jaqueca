using Jaqueca.Audio;
using Jaqueca.Client.Game;
using Jaqueca.Client.World;
using Jaqueca.Figures.Content;
using Microsoft.Xna.Framework;

namespace Jaqueca.Tests;

/// <summary>El living, los tiros contra los muebles, las figuras y los sonidos.</summary>
public class LevelTests
{
    private static Living Built()
    {
        var l = new Living();
        l.Assemble();
        return l;
    }

    [Fact]
    public void LosPensamientosAparecenEnElPisoYLibres()
    {
        var l = Built();
        foreach (var at in l.Spawns)
        {
            Assert.InRange(l.Solids.GroundAt(at.X, at.Z, 5, 4), -0.01f, 0.51f);
            // Parado ahí, nada lo empuja.
            var moved = l.Solids.Move(at, 4.2f, 17.5f, Vector3.Zero, 5, true, out bool grounded, out _, out _);
            Assert.True(grounded);
            Assert.True(Vector3.Distance(moved, at) < 0.6f, $"en {at} lo corre a {moved}");
        }
    }

    [Fact]
    public void ErnestoArrancaLibre()
    {
        var l = Built();
        var moved = l.Solids.Move(l.Start, Player.Radius, Player.Height, Vector3.Zero, 4.5f, true, out bool grounded, out _, out _);
        Assert.True(grounded);
        Assert.True(Vector3.Distance(moved, l.Start) < 0.6f);
    }

    [Fact]
    public void ElTiroPegaEnLaPared()
    {
        var l = Built();
        // Del medio hacia el oeste, a la altura de los ojos, por encima de todo: pega en la pared oeste.
        Assert.True(l.Solids.Raycast(new Vector3(40, 60, 40), -Vector3.UnitX, 900, out float t, out var n, out _));
        Assert.InRange(40 - t, -Living.W - 0.01f, -Living.W + 0.01f);
        Assert.Equal(Vector3.UnitX, n);
        // Hacia abajo, el piso.
        Assert.True(l.Solids.Raycast(new Vector3(0, 30, 30), -Vector3.UnitY, 100, out t, out n, out _));
        Assert.InRange(t, 29.4f, 30.01f);
    }

    [Fact]
    public void LosPedazosNoAtraviesanLosMuebles()
    {
        var l = Built();
        // Un punto que viene de costado y entra en la base del modular sale por donde entró.
        var prev = new System.Numerics.Vector3(0, 8, -D + 25);
        var into = new System.Numerics.Vector3(0, 8, -D + 20);
        var got = l.Solids.Walls(into, prev, 0.5f);
        Assert.True(got.Z >= -D + 22, $"quedó en z = {got.Z:0.0}");
    }

    private const float D = Living.D;

    [Fact]
    public void LasFigurasSeArman()
    {
        foreach (var fig in new[] { Thoughts.Neighbor(), Thoughts.Teacher(), Ernesto.Revolver(), Ernesto.Shotgun(), Ernesto.Leg() })
        {
            Assert.NotEmpty(fig.Shapes);
            foreach (var s in fig.Shapes) Assert.InRange(s.Mat, 0, fig.Mats.Count - 1);
        }
    }

    [Fact]
    public void TodosLosSonidosSuenan()
    {
        foreach (var id in Enum.GetValues<Sound>())
        {
            var data = Recipes.Make(id, 0);
            Assert.True(data.Length > 100, $"{id} está vacío");
            float peak = 0;
            foreach (var x in data)
            {
                Assert.False(float.IsNaN(x) || float.IsInfinity(x), $"{id} tiene un valor roto");
                peak = MathF.Max(peak, MathF.Abs(x));
            }
            Assert.True(peak > 0.05f && peak <= 1.0001f, $"{id}: pico {peak}");
        }
    }
}
