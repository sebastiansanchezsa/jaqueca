using System.Numerics;
using Jaqueca.Look;

namespace Jaqueca.Figures.Rig;

/// <summary>
/// Huesos del personaje. Espacio del personaje: +X adelante, +Y arriba, +Z su derecha; los
/// pies apoyan en y = 0. En reposo brazos y piernas cuelgan (su eje es -Y), y columna y
/// cabeza apuntan hacia arriba (+Y).
/// </summary>
public enum Bone : byte
{
    Root, Pelvis, Spine, Head,
    UpperArmR, ForearmR, HandR,
    UpperArmL, ForearmL, HandL,
    ThighR, ShinR, FootR,
    ThighL, ShinL, FootL,
    // La cuerda del arco donde va la flecha: la ubica el animador (en la mano que tensa, o en la cuerda en reposo).
    Nock,
    // Secundarios (se mueven con inercia): pelo largo, coleta y la faja de la cintura. Cuelgan (-Y).
    HairBack1, HairBack2, Tail1, Tail2, Scarf1, Scarf2,
    Count
}

/// <summary>
/// Medidas de un cuerpo (unidades de mundo; un humano mide 16, ~1,80 m): largos de los huesos,
/// anchos, radios del torso, grosor de los miembros y la cabeza. Los humanos tienen
/// proporciones realistas (~7 cabezas); las criaturas traen las suyas.
/// </summary>
public sealed class Dims
{
    // Largos (de la articulación padre a la hija). Humano: cadera a 8,35, rodilla a 4,5,
    // tobillo a 0,7, hombros a 12,9, base del cuello a 13,15 y coronilla a 16.
    public float PelvisY = 8.7f, SpineUp = 0.9f, NeckUp = 3.55f, ShoulderUp = 3.3f;
    public float UpperArm = 2.9f, Forearm = 2.45f, HipDown = 0.35f, Thigh = 3.85f, Shin = 3.8f;
    public float HipW, ShoulderW;
    /// <summary>Radios del torso (adelante, costado) y multiplicador del grosor de brazos y piernas.</summary>
    public float ChestF, ChestS, WaistF, WaistS, Limb;
    /// <summary>El cráneo en el hueso de la cabeza (el mismo para las dos complexiones: pelo y ojos sirven para las dos).</summary>
    public Vector3 HeadC = new(0, 1.95f, 0), HeadR = new(1.0f, 0.98f, 0.8f);
    /// <summary>Enganche de la melena y de la coleta en la cabeza, y de la faja en la cadera.</summary>
    public Vector3 HairBackRoot = new(-0.82f, 2.05f, 0), TailRoot = new(-1.08f, 2.2f, 0), ScarfRoot = new(0.55f, 0.55f, -0.95f);
    public float HairBackSeg = 1.0f, TailSeg = 0.85f, ScarfSeg = 1.05f;
    /// <summary>Qué tan rígido vuelve a su forma cada resorte (lo pesado, como un manojo de llaves, se balancea más).</summary>
    public float HairStiff = 90, TailStiff = 55, ScarfStiff = 70;
    /// <summary>Límites del segundo resorte de la cabeza hacia adelante y atrás (un velo sobre la cara no puede meterse en ella).</summary>
    public float TailMinX = -1, TailMaxX = 0.3f;

    public float AnkleY => PelvisY - HipDown - Thigh - Shin;
    public float LegLen => Thigh + Shin;
    public float ArmLen => UpperArm + Forearm;
    /// <summary>Altura de los hombros sobre la cadera (el origen de la pelvis).</summary>
    public float ShoulderY => SpineUp + ShoulderUp;

    /// <summary>Largo de pierna del humano: la referencia para escalar la marcha de otros cuerpos.</summary>
    public const float HumanLeg = 3.85f + 3.8f;

    /// <summary>
    /// Qué tan grande es la gente (el grupo, el sepulturero) respecto de la medida de referencia
    /// de 16: el cuerpo se arma a 16 y se dibuja y se anima a esta escala. Al lado de ellos, el
    /// penitente y el confesor se ven altísimos.
    /// </summary>
    public const float People = 0.85f;

    /// <summary>Las mismas medidas a otra escala (largos, anchos, la cabeza y los enganches; no la rigidez de los resortes).</summary>
    public Dims Scaled(float s) => new()
    {
        PelvisY = PelvisY * s, SpineUp = SpineUp * s, NeckUp = NeckUp * s, ShoulderUp = ShoulderUp * s,
        UpperArm = UpperArm * s, Forearm = Forearm * s, HipDown = HipDown * s, Thigh = Thigh * s, Shin = Shin * s,
        HipW = HipW * s, ShoulderW = ShoulderW * s, ChestF = ChestF * s, ChestS = ChestS * s, WaistF = WaistF * s, WaistS = WaistS * s, Limb = Limb * s,
        HeadC = HeadC * s, HeadR = HeadR * s, HairBackRoot = HairBackRoot * s, TailRoot = TailRoot * s, ScarfRoot = ScarfRoot * s,
        HairBackSeg = HairBackSeg * s, TailSeg = TailSeg * s, ScarfSeg = ScarfSeg * s,
        HairStiff = HairStiff, TailStiff = TailStiff, ScarfStiff = ScarfStiff, TailMinX = TailMinX, TailMaxX = TailMaxX,
    };

    public static Dims Of(Build b) => b switch
    {
        Build.Robust => new Dims { HipW = 0.88f, ShoulderW = 1.85f, ChestF = 1.2f, ChestS = 1.55f, WaistF = 1.08f, WaistS = 1.42f, Limb = 1.17f },
        _ => new Dims { HipW = 0.78f, ShoulderW = 1.58f, ChestF = 0.98f, ChestS = 1.3f, WaistF = 0.84f, WaistS = 1.15f, Limb = 1f },
    };
}

/// <summary>Jerarquía y posición de reposo de cada articulación respecto de su padre.</summary>
public sealed class Skeleton
{
    public static readonly Bone[] Parent =
    {
        Bone.Root, Bone.Root, Bone.Pelvis, Bone.Spine,
        Bone.Spine, Bone.UpperArmR, Bone.ForearmR,
        Bone.Spine, Bone.UpperArmL, Bone.ForearmL,
        Bone.Pelvis, Bone.ThighR, Bone.ShinR,
        Bone.Pelvis, Bone.ThighL, Bone.ShinL,
        Bone.HandR,
        Bone.Head, Bone.HairBack1, Bone.Head, Bone.Tail1, Bone.Pelvis, Bone.Scarf1,
    };

    public static bool Secondary(Bone b) => b >= Bone.HairBack1 && b < Bone.Count;

    public readonly Build Build;
    public readonly Dims D;
    public readonly Vector3[] Offset = new Vector3[(int)Bone.Count];

    public Skeleton(Build build) : this(build, Dims.Of(build)) { }

    /// <summary>Con medidas propias (criaturas: goblins...). <paramref name="build"/> queda sólo como referencia.</summary>
    public Skeleton(Build build, Dims dims)
    {
        Build = build;
        D = dims;
        Set(Bone.Pelvis, 0, D.PelvisY, 0);
        Set(Bone.Spine, 0, D.SpineUp, 0);
        Set(Bone.Head, 0, D.NeckUp, 0);
        foreach (int side in new[] { 1, -1 })
        {
            Set(Arm(side, 0), 0, D.ShoulderUp, side * D.ShoulderW);
            Set(Arm(side, 1), 0, -D.UpperArm, 0);
            Set(Arm(side, 2), 0, -D.Forearm, 0);
            Set(Leg(side, 0), 0, -D.HipDown, side * D.HipW);
            Set(Leg(side, 1), 0, -D.Thigh, 0);
            Set(Leg(side, 2), 0, -D.Shin, 0);
        }
        Offset[(int)Bone.HairBack1] = D.HairBackRoot;
        Set(Bone.HairBack2, 0, -D.HairBackSeg, 0);
        Offset[(int)Bone.Tail1] = D.TailRoot;
        Set(Bone.Tail2, 0, -D.TailSeg, 0);
        Offset[(int)Bone.Scarf1] = D.ScarfRoot;
        Set(Bone.Scarf2, 0, -D.ScarfSeg, 0);
    }

    private void Set(Bone b, float x, float y, float z) => Offset[(int)b] = new Vector3(x, y, z);

    /// <summary>Brazo de un lado (+1 derecho, -1 izquierdo): 0 hombro, 1 codo, 2 mano.</summary>
    public static Bone Arm(int side, int seg) => (Bone)((side > 0 ? (int)Bone.UpperArmR : (int)Bone.UpperArmL) + seg);
    /// <summary>¿Es un hueso de un brazo (del hombro a la mano, de cualquier lado)?</summary>
    public static bool IsArm(Bone b) => (b >= Arm(1, 0) && b <= Arm(1, 2)) || (b >= Arm(-1, 0) && b <= Arm(-1, 2));

    /// <summary>Pierna de un lado: 0 muslo, 1 rodilla, 2 pie.</summary>
    public static Bone Leg(int side, int seg) => (Bone)((side > 0 ? (int)Bone.ThighR : (int)Bone.ThighL) + seg);

    // Atajos a las medidas.
    public float PelvisY => D.PelvisY;
    public float HipDown => D.HipDown;
    public float Thigh => D.Thigh;
    public float Shin => D.Shin;
    public float UpperArm => D.UpperArm;
    public float Forearm => D.Forearm;
    /// <summary>Altura del tobillo con la pierna estirada (para apoyar los pies con IK).</summary>
    public float AnkleY => D.AnkleY;
    public float LegLen => D.LegLen;
}
