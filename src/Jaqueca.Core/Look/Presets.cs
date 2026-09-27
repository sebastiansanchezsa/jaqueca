namespace Jaqueca.Look;

/// <summary>Personajes de muestra (hojas de revisión, lab y jugadores de prueba).</summary>
public static class Presets
{
    public static readonly IReadOnlyList<(string name, Appearance look)> All = new[]
    {
        ("peregrina", new Appearance { Build = Build.Slim, Skin = 1, HairStyle = 1, HairColor = Palettes.HairColors[2], Eyes = 3, Accent = Palettes.Accents[0] }),
        ("herrero", new Appearance { Build = Build.Robust, Skin = 3, HairStyle = 2, HairColor = Palettes.HairColors[1], Eyes = 2, Beard = 2, Accent = Palettes.Accents[1] }),
        ("novicio", new Appearance { Build = Build.Slim, Skin = 4, HairStyle = 0, HairColor = Palettes.HairColors[0], Eyes = 0, Accent = Palettes.Accents[2] }),
        ("pastora", new Appearance { Build = Build.Robust, Skin = 0, HairStyle = 3, HairColor = Palettes.HairColors[5], Eyes = 1, Accent = Palettes.Accents[3] }),
        ("erudita", new Appearance { Build = Build.Slim, Skin = 2, HairStyle = 4, HairColor = Palettes.HairColors[3], Eyes = 0, Accent = Palettes.Accents[4] }),
        ("anciano", new Appearance { Build = Build.Robust, Skin = 5, HairStyle = 5, HairColor = Palettes.HairColors[4], Eyes = 1, Beard = 3, Accent = Palettes.Accents[5] }),
    };
}
