namespace Jaqueca.Look;

public enum Build : byte { Slim, Robust, Count }

/// <summary>
/// Lo que elige cada jugador en el creador más el equipo que lleva puesto (IDs visuales de
/// las capas). Es lo único que viaja por red para dibujar a otro jugador.
/// </summary>
public sealed class Appearance
{
    public Build Build;
    public int Skin;
    public int HairStyle;
    public uint HairColor = Palettes.HairColors[0];
    public int Eyes;
    public int Beard;
    public uint Accent = Palettes.Accents[0];

    /// <summary>La ropa (la de una clase, o la de peregrino).</summary>
    public Outfit Outfit;

    /// <summary>IDs visuales por slot de equipo (el arma: su ID en <see cref="Items.Weapons"/>, p. ej. "espada_comun"). null = nada.</summary>
    public string Torso, Head, Back, MainHand, OffHand;

    /// <summary>La armadura y lo demás que lleva puesto (IDs de <see cref="Items.Gear"/>; ver Items.Inventory). null = nada.</summary>
    public string Helm, Neck, Chest, Hands, Legs, Feet, Pack;

    /// <summary>Cuánta plata asoma de la mochila (0 nada, 3 repleta; ver Items.Denarios.Fill).</summary>
    public int Coins;

    /// <summary>Lo que tiene un momento en la mano izquierda (la calabaza del brebaje de Ossa, al tomarlo; ver Figures.Content.Props). null = nada.</summary>
    public string Prop;

    /// <summary>Lo que lleva puesto que no es el arma, en orden (para armar la figura).</summary>
    public IEnumerable<string> Worn => new[] { Chest, Legs, Feet, Hands, Neck, Pack, Helm }.Where(id => id != null);

    public Appearance Clone() => (Appearance)MemberwiseClone();

    /// <summary>Clave estable para cachear frames compuestos.</summary>
    public string Key => $"{Build}|{Skin}|{HairStyle}|{HairColor:X6}|{Eyes}|{Beard}|{Accent:X6}|{Outfit}|{Torso}|{Head}|{Back}|{MainHand}|{OffHand}|{Helm}|{Neck}|{Chest}|{Hands}|{Legs}|{Feet}|{Pack}|{Coins}|{Prop}";
}

public static class Palettes
{
    /// <summary>Tonos de piel del creador (claro → oscuro).</summary>
    public static readonly uint[] Skins = { 0xE8C4A4, 0xD6A884, 0xBC8A63, 0x9C6746, 0x744B32, 0x51331F };

    public static readonly uint[] HairColors = { 0x5A3A26, 0x2A1E1A, 0xB0703A, 0xD9B25C, 0xA3A7AE, 0x8E2F24, 0xE9E1CF, 0x3E4E7A };

    public static readonly uint[] EyeColors = { 0x1B1420 };

    /// <summary>Color de acento de cada jugador del cooperativo (capa, ribetes).</summary>
    public static readonly uint[] Accents = { 0xB8332E, 0x2F6FB5, 0x3E9A4C, 0xD39B2A, 0x7C4BB0, 0xE0E0D0 };
}

/// <summary>La ropa de un cuerpo (va al final lo nuevo: los IDs horneados son bytes).</summary>
public enum Outfit : byte { Pilgrim, Soldier, Hunter, Blasphemer, Healer, Rogue, Occultist, Peddler }
