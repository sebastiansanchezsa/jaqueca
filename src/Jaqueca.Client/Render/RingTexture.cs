using Microsoft.Xna.Framework.Graphics;

namespace Jaqueca.Client.Render;

/// <summary>
/// Una textura que se reescribe en cada cuadro (la barra de vida, el retrato, el agua) hecha con tres
/// que se turnan: nunca se escribe sobre la que la GPU puede estar usando todavía. Escribir sobre una
/// textura en uso hace que la CPU espere a que la GPU termine el cuadro anterior, y eso baja los
/// cuadros por segundo aunque la GPU casi no tenga trabajo (ver también <see cref="Chars.LiveAtlas"/>).
/// </summary>
public sealed class RingTexture : IDisposable
{
    private const int Count = 3;
    private readonly Texture2D[] _tex = new Texture2D[Count];
    private int _current;

    public RingTexture(GraphicsDevice gd, int w, int h, SurfaceFormat format = SurfaceFormat.Color)
    {
        for (int i = 0; i < Count; i++) _tex[i] = new Texture2D(gd, w, h, false, format);
    }

    /// <summary>La que se dibuja (la última que se escribió).</summary>
    public Texture2D Current => _tex[_current];
    public int Width => _tex[0].Width;
    public int Height => _tex[0].Height;

    /// <summary>Pasa a la siguiente y le sube <paramref name="data"/>.</summary>
    public void SetData<T>(T[] data) where T : struct
    {
        _current = (_current + 1) % Count;
        _tex[_current].SetData(data);
    }

    public void Dispose()
    {
        foreach (var t in _tex) t.Dispose();
    }
}
