using Microsoft.Xna.Framework;

namespace Jaqueca.Client.Render;

/// <summary>
/// Luz del día según la hora: dirección y color del sol (o la luna), luz de cielo y de rebote.
/// El sol siempre entra desde arriba a la izquierda de la pantalla (igual que la luz horneada
/// de los personajes) y cambia de altura y de color: las sombras caen abajo a la derecha como
/// en la referencia, más largas al amanecer y al atardecer.
/// </summary>
public sealed class Sky
{
    /// <summary>Hora del día, 0..24.</summary>
    public float Hour = 16;

    public Vector3 SunDir { get; private set; }
    public Vector3 SunColor { get; private set; }
    public Vector3 SkyColor { get; private set; }
    public Vector3 GroundColor { get; private set; }
    /// <summary>Relleno desde el lado de la cámara (las caras que miran al sur).</summary>
    public Vector3 FillColor { get; private set; }
    /// <summary>0 de día, 1 de noche (para encender runas y bajar el reflejo).</summary>
    public float Night { get; private set; }

    /// <summary>
    /// La luz de un lugar cerrado (bajo tierra): una luz fría y débil que baja desde arriba (la
    /// que se cuela por rejas y grietas) y el ambiente; no depende de la hora, y las antorchas y
    /// las velas se ven como de noche.
    /// </summary>
    public readonly record struct Ambience(Vector3 Sun, Vector3 Sky, Vector3 Ground, Vector3 Fill, float Elevation = 68);

    /// <summary>Luz fija de un lugar cerrado (null = la del día según <see cref="Hour"/>).</summary>
    public Ambience? Fixed;

    private readonly record struct Key(float Hour, float Elev, Vector3 Sun, Vector3 Sky, Vector3 Ground, Vector3 Fill);

    // Luna de noche; de día el cielo aporta bastante (sombras claras y azuladas, como en la referencia).
    private static readonly Key[] Keys =
    {
        new(0, 42, new(0.18f, 0.25f, 0.46f), new(0.16f, 0.2f, 0.36f), new(0.08f, 0.08f, 0.14f), new(0.05f, 0.07f, 0.13f)),
        new(5, 30, new(0.18f, 0.24f, 0.44f), new(0.17f, 0.2f, 0.36f), new(0.08f, 0.08f, 0.14f), new(0.05f, 0.07f, 0.13f)),
        new(6.5f, 8, new(0.62f, 0.4f, 0.32f), new(0.38f, 0.38f, 0.52f), new(0.22f, 0.18f, 0.2f), new(0.2f, 0.14f, 0.14f)),
        new(9, 34, new(0.54f, 0.5f, 0.43f), new(0.44f, 0.48f, 0.58f), new(0.28f, 0.26f, 0.27f), new(0.24f, 0.23f, 0.22f)),
        new(13, 58, new(0.56f, 0.54f, 0.47f), new(0.42f, 0.47f, 0.56f), new(0.28f, 0.27f, 0.28f), new(0.24f, 0.24f, 0.23f)),
        new(17, 30, new(0.6f, 0.48f, 0.34f), new(0.42f, 0.42f, 0.54f), new(0.3f, 0.24f, 0.24f), new(0.27f, 0.21f, 0.18f)),
        new(19.3f, 6, new(0.55f, 0.3f, 0.25f), new(0.33f, 0.3f, 0.47f), new(0.18f, 0.14f, 0.17f), new(0.18f, 0.12f, 0.12f)),
        new(20.5f, 30, new(0.18f, 0.24f, 0.44f), new(0.18f, 0.2f, 0.37f), new(0.08f, 0.08f, 0.14f), new(0.05f, 0.07f, 0.13f)),
        new(24, 42, new(0.18f, 0.25f, 0.46f), new(0.16f, 0.2f, 0.36f), new(0.08f, 0.08f, 0.14f), new(0.05f, 0.07f, 0.13f)),
    };

    public void Update()
    {
        var horiz = Vector3.Normalize(new Vector3(-0.78f, 0, -0.62f));
        if (Fixed is { } f)
        {
            SunColor = f.Sun;
            SkyColor = f.Sky;
            GroundColor = f.Ground;
            FillColor = f.Fill;
            Night = 1;
            float e = MathHelper.ToRadians(f.Elevation);
            SunDir = Vector3.Normalize(horiz * MathF.Cos(e) + Vector3.UnitY * MathF.Sin(e));
            return;
        }
        float h = ((Hour % 24) + 24) % 24;
        int i = 0;
        while (i < Keys.Length - 2 && Keys[i + 1].Hour <= h) i++;
        var a = Keys[i];
        var b = Keys[i + 1];
        float t = Math.Clamp((h - a.Hour) / (b.Hour - a.Hour), 0, 1);
        t = t * t * (3 - 2 * t);
        float elev = MathHelper.ToRadians(MathHelper.Lerp(a.Elev, b.Elev, t));
        SunColor = Vector3.Lerp(a.Sun, b.Sun, t);
        SkyColor = Vector3.Lerp(a.Sky, b.Sky, t);
        GroundColor = Vector3.Lerp(a.Ground, b.Ground, t);
        FillColor = Vector3.Lerp(a.Fill, b.Fill, t);
        Night = h < 5.5f || h > 20 ? 1 : h < 7 ? (7 - h) / 1.5f : h > 18.5f ? (h - 18.5f) / 1.5f : 0;
        Night = Math.Clamp(Night, 0, 1);
        // Hacia el sol: arriba a la izquierda y un poco hacia el fondo.
        SunDir = Vector3.Normalize(horiz * MathF.Cos(elev) + Vector3.UnitY * MathF.Sin(elev));
    }
}
