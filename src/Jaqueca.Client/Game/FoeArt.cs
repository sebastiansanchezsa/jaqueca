using Jaqueca.Client.Render;
using Jaqueca.Figures.Content;
using Jaqueca.Figures.Model;
using Jaqueca.Figures.Rig;
using Jaqueca.Look;
using Microsoft.Xna.Framework.Graphics;

namespace Jaqueca.Client.Game;

/// <summary>Los pensamientos que hay (lo nuevo, al final).</summary>
public enum FoeKind : byte { Neighbor, Teacher }

/// <summary>
/// Las mallas de los pensamientos y de lo que tiene Ernesto en la mano, armadas una vez al empezar (de
/// las figuras del motor: Figures.Content.Thoughts y Ernesto) y compartidas por todos los de su clase.
/// </summary>
public sealed class FoeArt : IDisposable
{
    public sealed class Kind
    {
        public FigureMesh Mesh;
        public Dims Dims;
        public float Scale;
        public Skeleton Skel => new(Build.Robust, Dims.Scaled(Scale));
    }

    public readonly Kind Neighbor, Teacher;
    public readonly FigureMesh Revolver, Shotgun, Leg;

    public FoeArt(GraphicsDevice gd)
    {
        Neighbor = Make(gd, Thoughts.Neighbor(), Thoughts.NeighborDims, 1.1f);
        Teacher = Make(gd, Thoughts.Teacher(), Thoughts.TeacherDims, 1.0f);
        Revolver = FigureMesh.Build(gd, Ernesto.Revolver(), detail: 1.6f);
        Shotgun = FigureMesh.Build(gd, Ernesto.Shotgun(), detail: 1.6f);
        Leg = FigureMesh.Build(gd, Ernesto.Leg(), detail: 1.4f);
    }

    private static Kind Make(GraphicsDevice gd, Figure fig, Dims dims, float scale)
    {
        // Los muñones se ubican con el esqueleto de referencia (como el resto de la figura).
        Gore.AddStumps(fig, new Skeleton(Build.Robust, dims));
        return new Kind { Mesh = FigureMesh.Build(gd, fig, scale: scale), Dims = dims, Scale = scale };
    }

    public Kind Of(FoeKind k) => k == FoeKind.Neighbor ? Neighbor : Teacher;

    public void Dispose()
    {
        Neighbor.Mesh.Dispose();
        Teacher.Mesh.Dispose();
        Revolver.Dispose();
        Shotgun.Dispose();
        Leg.Dispose();
    }
}
