using Jaqueca.Figures.Model;
using Jaqueca.Figures.Rig;

namespace Jaqueca.Figures.Render;

/// <summary>
/// Qué partes de una figura se dibujan: los huesos cortados no están (ni sus formas ni sus
/// estampados), los muñones sólo aparecen donde hubo un corte, y el arma se puede sacar (se
/// cayó) o dibujar sola (el arma tirada en el piso). Sirve igual para el cuerpo y para cada
/// pedazo: un pedazo es la misma figura con todo oculto salvo sus huesos.
/// </summary>
public sealed class RenderMask
{
    public readonly bool[] Hidden = new bool[(int)Bone.Count];
    /// <summary>Huesos cortados de su padre: muestran su muñón (del lado del cuerpo y del lado del pedazo).</summary>
    public readonly bool[] Cut = new bool[(int)Bone.Count];
    public bool NoWeapon, OnlyWeapon;
    /// <summary>Sólo el equipo puesto (el ícono de una pieza en el inventario); con <see cref="Piece"/>, sólo esa pieza.</summary>
    public bool OnlyGear;
    /// <summary>(Con <see cref="OnlyGear"/>) la pieza que se dibuja (su ID); null = todo el equipo.</summary>
    public string Piece;
    /// <summary>Una pieza que se dibuja siempre, aunque su hueso esté oculto (el ícono: la pieza entera sobre un pedazo de cuerpo).</summary>
    public string Always;

    public bool Shows(Shape s)
    {
        if (Always != null && s.Gear == Always) return true;
        if (OnlyGear) return s.Gear != null && (Piece == null || s.Gear == Piece) && !Hidden[(int)s.Bone];
        if (s.Weapon ? NoWeapon : OnlyWeapon) return false;
        if (Hidden[(int)s.Bone]) return false;
        return s.CapFor is not { } cap || Cut[(int)cap];
    }

    /// <summary>Sin máscara se dibuja todo menos los muñones.</summary>
    public static bool Default(Shape s) => s.CapFor == null;
}
