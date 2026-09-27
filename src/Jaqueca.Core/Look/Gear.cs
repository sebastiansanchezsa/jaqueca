namespace Jaqueca.Look;

/// <summary>
/// Capas del paper-doll, en orden de prioridad: cuando dos capas quedan a la misma
/// profundidad gana la que está más abajo en la lista.
/// </summary>
public enum GearSlot : byte { Body, Eyes, Beard, Hair, Torso, Head, Back, OffHand, MainHand, Count }

/// <summary>
/// Familia de arma: decide qué clips de ataque usa el personaje. Las de la mano secundaria
/// (escudo, foco) suman sus propios clips a los de la principal.
/// </summary>
public enum WeaponFamily : byte
{
    /// <summary>Clips base: los tienen todas las capas.</summary>
    None,
    Unarmed, Blade, Heavy, Polearm, Dagger, Bow, Crossbow, Staff,
    Shield, Focus,
    /// <summary>Un cetro: se lleva en una mano, de punta arriba (va al final: los IDs horneados son bytes).</summary>
    Scepter,
    Count
}

/// <summary>Qué parte del pelo tapa un tocado.</summary>
public enum HairHide : byte { None, Top, All }

public static class Families
{
    /// <summary>Armas que ocupan las dos manos: no dejan usar la mano secundaria.</summary>
    public static bool TwoHanded(this WeaponFamily f) => f is WeaponFamily.Heavy or WeaponFamily.Polearm or WeaponFamily.Bow or WeaponFamily.Staff;

    public static bool IsOffHand(this WeaponFamily f) => f is WeaponFamily.Shield or WeaponFamily.Focus;

    public static bool IsMainHand(this WeaponFamily f) => f is > WeaponFamily.None and < WeaponFamily.Shield or WeaponFamily.Scepter;
}
