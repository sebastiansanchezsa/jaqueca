using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Jaqueca.Client.Render;

/// <summary>
/// Post-proceso sobre el render de baja resolución (portado de Kill Kill Again): contornos de
/// pixel art, bloom en dos niveles y composición a la pantalla con escala entera por punto
/// más el corrimiento subpíxel de la cámara.
/// </summary>
public sealed class PostFx : IDisposable
{
    private readonly GraphicsDevice _gd;
    private readonly Effect _fx;
    private RenderTarget2D _edged, _half, _halfB, _quarter, _quarterB, _eighth, _eighthB;
    private int _w, _h;

    public float Exposure = 1f;
    public float BloomStrength = 0.55f;
    public float Threshold = 0.95f;
    public float Vignette = 0.35f;
    public float Grain = 0f;
    public float Saturation = 1.06f;
    public bool Tonemap;
    /// <summary>Las luces se comprimen por el canal más alto (conservan el tono) en vez de canal por canal.</summary>
    public bool KeepHue;
    public Vector3 Lift = new(0.0f, 0.0f, 0.015f);
    public Vector3 Gain = Vector3.One;
    public Vector4 Flash;
    /// <summary>0..1: la imagen ondula (mareado).</summary>
    public float Wobble;
    public float Time;

    /// <summary>Salto de profundidad (unidades) que cuenta como silueta, y cuánto más por unidad de distancia.</summary>
    public float EdgeDepth = 1.6f, EdgeSlope = 0.06f;
    /// <summary>El borde rojo del golpe (rgb, cuánto) y cuánto se separan los canales (la puntada).</summary>
    public Vector4 Hurt;
    public float Aberration;
    public float OutlineDark = 0.55f;
    public float HighlightGain = 0.35f;

    private static readonly RasterizerState Scissor = new() { ScissorTestEnable = true, CullMode = CullMode.None };

    public PostFx(GraphicsDevice gd, Effect fx)
    {
        _gd = gd;
        _fx = fx;
    }

    /// <summary>Vuelve al color de siempre (al salir de un lugar que lo cambió).</summary>
    public void Defaults()
    {
        Exposure = 1f;
        BloomStrength = 0.55f;
        Threshold = 0.95f;
        Vignette = 0.35f;
        Grain = 0f;
        Saturation = 1.06f;
        Lift = new Vector3(0.0f, 0.0f, 0.015f);
        Gain = Vector3.One;
    }

    private void Ensure(int w, int h)
    {
        if (w == _w && h == _h && _half != null) return;
        Dispose();
        _w = w; _h = h;
        RenderTarget2D Rt(int d) => new(_gd, Math.Max(1, w / d), Math.Max(1, h / d), false, SurfaceFormat.HalfVector4, DepthFormat.None);
        _edged = Rt(1);
        _half = Rt(2); _halfB = Rt(2);
        _quarter = Rt(4); _quarterB = Rt(4);
        _eighth = Rt(8); _eighthB = Rt(8);
    }

    private void Pass(SpriteBatch sb, Texture2D src, RenderTarget2D dst, string technique, SamplerState sampler)
    {
        _gd.SetRenderTarget(dst);
        _fx.CurrentTechnique = _fx.Techniques[technique];
        _fx.Set("SceneTex", src);
        _fx.Set("PixelTex", src);
        _fx.Set("TexelSize", new Vector2(1f / src.Width, 1f / src.Height));
        sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque, sampler, DepthStencilState.None, RasterizerState.CullNone, _fx);
        sb.Draw(src, new Rectangle(0, 0, dst.Width, dst.Height), Color.White);
        sb.End();
    }

    private void Blur(SpriteBatch sb, RenderTarget2D a, RenderTarget2D b, int iterations = 1)
    {
        for (int i = 0; i < iterations; i++)
        {
            _fx.Set("Direction", new Vector2(1, 0));
            Pass(sb, a, b, "Blur", SamplerState.LinearClamp);
            _fx.Set("Direction", new Vector2(0, 1));
            Pass(sb, b, a, "Blur", SamplerState.LinearClamp);
        }
    }

    /// <summary>
    /// Procesa la escena (con su normal/profundidad) y la dibuja en <paramref name="target"/>
    /// dentro de <paramref name="dest"/>, corrida el subpíxel de la cámara.
    /// </summary>
    public void Render(SpriteBatch sb, RenderTarget2D scene, RenderTarget2D normalDepth, FpsCamera cam, RenderTarget2D target, Rectangle dest, int scale)
    {
        Ensure(scene.Width, scene.Height);
        _fx.Set("NormalDepthTex", normalDepth);
        _fx.Set("EdgeDepth", EdgeDepth);
        _fx.Set("EdgeSlope", EdgeSlope);
        _fx.Set("OutlineDark", OutlineDark);
        _fx.Set("HighlightGain", HighlightGain);
        _fx.Set("PixelSize", cam.PixelK);
        Pass(sb, scene, _edged, "Edges", SamplerState.PointClamp);
        Bloom(sb, _edged);
        Composite(sb, _edged, target, dest, new Rectangle(dest.X, dest.Y, scene.Width * scale, scene.Height * scale));
    }

    /// <summary>
    /// Lo mismo para una imagen plana, sin mundo detrás (la pantalla de título): sin contornos
    /// ni margen de cámara; bloom, gradación y escala entera.
    /// </summary>
    public void RenderFlat(SpriteBatch sb, RenderTarget2D scene, RenderTarget2D target, Rectangle dest, int scale)
    {
        Ensure(scene.Width, scene.Height);
        Bloom(sb, scene);
        Composite(sb, scene, target, dest, new Rectangle(dest.X, dest.Y, scene.Width * scale, scene.Height * scale));
    }

    /// <summary>El bloom en baja resolución: lo que pasa el umbral, borroneado a 1/4 y a 1/8.</summary>
    private void Bloom(SpriteBatch sb, Texture2D src)
    {
        _fx.Set("Threshold", Threshold);
        Pass(sb, src, _half, "Threshold", SamplerState.LinearClamp);
        Blur(sb, _half, _halfB);
        Pass(sb, _half, _quarter, "Down", SamplerState.LinearClamp);
        Blur(sb, _quarter, _quarterB, 2);
        Pass(sb, _quarter, _eighth, "Down", SamplerState.LinearClamp);
        Blur(sb, _eighth, _eighthB, 2);
    }

    /// <summary>La imagen nítida (por punto) + el bloom, exposición y gradación, en <paramref name="at"/> (recortada a <paramref name="dest"/>).</summary>
    private void Composite(SpriteBatch sb, Texture2D pixels, RenderTarget2D target, Rectangle dest, Rectangle at)
    {
        _gd.SetRenderTarget(target);
        _gd.Clear(Color.Black);
        _fx.CurrentTechnique = _fx.Techniques["Composite"];
        _fx.Set("PixelTex", pixels);
        _fx.Set("BloomTex", _quarter);
        _fx.Set("BloomTex2", _eighth);
        _fx.Set("Time", Time);
        _fx.Set("Exposure", Exposure);
        _fx.Set("BloomStrength", BloomStrength);
        _fx.Set("Vignette", Vignette);
        _fx.Set("Grain", Grain);
        _fx.Set("OutSize", new Vector2(dest.Width, dest.Height));
        _fx.Set("Lift", Lift);
        _fx.Set("Gain", Gain);
        _fx.Set("Saturation", Saturation);
        _fx.Set("FlashColor", Flash);
        _fx.Set("Tonemap", Tonemap ? 1f : 0f);
        _fx.Set("KeepHue", KeepHue ? 1f : 0f);
        _fx.Set("Wobble", Wobble);
        _fx.Set("HurtColor", Hurt);
        _fx.Set("Aberration", Aberration);
        _gd.ScissorRectangle = dest;
        sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, Scissor, _fx);
        sb.Draw(pixels, at, Color.White);
        sb.End();
    }

    public void Dispose()
    {
        foreach (var rt in new[] { _edged, _half, _halfB, _quarter, _quarterB, _eighth, _eighthB }) rt?.Dispose();
        _half = null;
    }
}
