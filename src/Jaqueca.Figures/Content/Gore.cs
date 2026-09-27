using System.Numerics;
using Jaqueca.Figures.Model;
using Jaqueca.Figures.Rig;
using Jaqueca.Sprites;
using static Jaqueca.Figures.Model.Figure;

namespace Jaqueca.Figures.Content;

/// <summary>
/// Muñones: una tapa de carne en cada articulación que se puede cortar (cuello, hombros,
/// codos, caderas, rodillas), del lado del cuerpo y del lado del pedazo. Sólo se dibujan donde
/// hubo un corte (ver <see cref="Render.RenderMask"/>).
/// </summary>
public static class Gore
{
    public const uint Flesh = 0x9A1C1C;

    /// <summary>Lo que se puede cortar: el hueso que se va (con todo lo que cuelga de él).</summary>
    public static readonly Bone[] Cuttable =
    {
        Bone.Head, Bone.UpperArmR, Bone.UpperArmL, Bone.ForearmR, Bone.ForearmL,
        Bone.ThighR, Bone.ThighL, Bone.ShinR, Bone.ShinL,
    };

    public static void AddStumps(Figure f, Skeleton s)
    {
        int flesh = f.AddMat("carne", MatChannel.Fixed, Flesh, lift: 0.1f);
        float limb = s.D.Limb;
        foreach (var b in Cuttable)
        {
            var parent = Skeleton.Parent[(int)b];
            var at = s.Offset[(int)b];
            float r = b switch
            {
                Bone.Head => 0.5f * limb,
                Bone.UpperArmR or Bone.UpperArmL => 0.56f * limb,
                Bone.ForearmR or Bone.ForearmL => 0.4f * limb,
                Bone.ThighR or Bone.ThighL => 0.72f * limb,
                _ => 0.48f * limb,
            };
            // Del lado del cuerpo, un poco hacia afuera de la articulación para que asome sobre la ropa.
            var outward = b switch
            {
                Bone.Head => V(0, 0.2f, 0),
                Bone.UpperArmR or Bone.UpperArmL => V(0, 0.1f, MathF.Sign(at.Z) * 0.3f),
                Bone.ThighR or Bone.ThighL => V(0, -0.3f, MathF.Sign(at.Z) * 0.2f),
                _ => V(0, 0.12f, 0),
            };
            int part = f.Part();
            f.Ellipsoid(parent, at + outward, V(r, r * 0.55f, r), flesh, part).CapFor = b;
            // Del lado del pedazo, en el arranque del hueso cortado (la cabeza: abajo del cuello).
            var end = b == Bone.Head ? V(0, -0.3f, 0) : V(0, -0.1f, 0);
            f.Ellipsoid(b, end, V(r, r * 0.55f, r), flesh, f.Part()).CapFor = b;
        }
    }
}
