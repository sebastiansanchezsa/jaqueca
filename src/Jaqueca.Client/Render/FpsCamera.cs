using Microsoft.Xna.Framework;

namespace Jaqueca.Client.Render;

/// <summary>
/// La cámara en primera persona: en perspectiva, desde los ojos de Ernesto. El giro (<see cref="Yaw"/>)
/// sigue la convención del motor: 0 mira al este (+X) y π/2 al sur (+Z); <see cref="Pitch"/> positivo
/// mira hacia arriba; <see cref="Roll"/> inclina la cabeza (al deslizarse, al hacer dash de costado).
/// El mundo se dibuja a 640×360: el pixelado es el de la resolución baja, no el de una textura.
/// </summary>
public sealed class FpsCamera
{
    public Vector3 Eye;
    public float Yaw, Pitch, Roll;
    /// <summary>Campo de visión vertical (grados). 72° a 16:9 son ~105° de costado a costado.</summary>
    public float Fov = 72;
    public float Near = 0.6f, Far = 2400;
    public int W, H;

    public Vector3 Forward { get; private set; }
    public Vector3 Right { get; private set; }
    public Vector3 Up { get; private set; }
    public Matrix View { get; private set; }
    public Matrix Projection { get; private set; }
    public Matrix ViewProj { get; private set; }
    /// <summary>Cuántas unidades del mundo mide un píxel a una unidad de distancia (el post lo usa para las aristas).</summary>
    public float PixelK { get; private set; }

    public FpsCamera(int w, int h) { W = w; H = h; Rebuild(); }

    public static Vector3 Dir(float yaw, float pitch) =>
        new(MathF.Cos(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Sin(yaw) * MathF.Cos(pitch));

    public void Rebuild()
    {
        Forward = Dir(Yaw, Pitch);
        var flatRight = new Vector3(-MathF.Sin(Yaw), 0, MathF.Cos(Yaw));
        var up0 = Vector3.Normalize(Vector3.Cross(flatRight, Forward));
        // El giro de la cabeza: el "arriba" rota alrededor de hacia dónde mira.
        var up = Vector3.Transform(up0, Quaternion.CreateFromAxisAngle(Forward, Roll));
        Up = up;
        Right = Vector3.Normalize(Vector3.Cross(Forward, Up));
        View = Matrix.CreateLookAt(Eye, Eye + Forward, Up);
        float fov = MathHelper.ToRadians(Fov);
        Projection = Matrix.CreatePerspectiveFieldOfView(fov, W / (float)H, Near, Far);
        ViewProj = View * Projection;
        PixelK = 2 * MathF.Tan(fov / 2) / H;
    }

    /// <summary>Dónde cae un punto del mundo en el render de baja resolución (y si está delante).</summary>
    public bool ToPixel(Vector3 p, out Vector2 px)
    {
        var v = Vector4.Transform(new Vector4(p, 1), ViewProj);
        px = default;
        if (v.W <= 0.01f) return false;
        px = new Vector2((v.X / v.W * 0.5f + 0.5f) * W, (0.5f - v.Y / v.W * 0.5f) * H);
        return true;
    }

    /// <summary>¿Una esfera se ve (aunque sea en parte)? Para no dibujar lo que queda atrás.</summary>
    public bool Sees(Vector3 c, float r)
    {
        var d = c - Eye;
        float z = Vector3.Dot(d, Forward);
        if (z < -r) return false;
        float fovY = MathHelper.ToRadians(Fov) * 0.5f;
        float tanY = MathF.Tan(fovY), tanX = tanY * W / H;
        float x = Vector3.Dot(d, Right), y = Vector3.Dot(d, Up);
        float kx = r * MathF.Sqrt(1 + tanX * tanX), ky = r * MathF.Sqrt(1 + tanY * tanY);
        return MathF.Abs(x) <= z * tanX + kx && MathF.Abs(y) <= z * tanY + ky;
    }
}
