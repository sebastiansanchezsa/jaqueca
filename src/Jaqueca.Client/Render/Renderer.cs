using Jaqueca.Figures.Render;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Jaqueca.Client.Render;

/// <summary>
/// Shadow map de la luz grande: cámara ortográfica que mira en la dirección de la luz, encajada a la
/// grilla de sus texels para que las sombras no titilen.
/// </summary>
public sealed class ShadowMap : IDisposable
{
    public const int Size = 2048;
    public readonly RenderTarget2D Rt;
    public Matrix ViewProj { get; private set; }
    /// <summary>Lado del cuadrado de mundo que cubre (unidades).</summary>
    public float Extent = 700;
    public const float Near = 1, Far = 2400, Back = 1000;

    public ShadowMap(GraphicsDevice gd)
    {
        Rt = new RenderTarget2D(gd, Size, Size, false, SurfaceFormat.Single, DepthFormat.Depth24);
    }

    public void Setup(Vector3 focus, Vector3 sunDir)
    {
        var fwd = -sunDir;
        var up = MathF.Abs(fwd.Y) > 0.99f ? -Vector3.UnitZ : Vector3.UnitY;
        var v0 = Matrix.CreateLookAt(Vector3.Zero, fwd, up);
        var f = Vector3.Transform(focus, v0);
        float texel = Extent / Size;
        float sx = MathF.Round(f.X / texel) * texel, sy = MathF.Round(f.Y / texel) * texel;
        var view = v0 * Matrix.CreateTranslation(-sx, -sy, -Back - f.Z);
        ViewProj = view * Matrix.CreateOrthographic(Extent, Extent, Near, Far);
    }

    public void Dispose() => Rt.Dispose();
}

/// <summary>Geometría que se rearma en cada cuadro (se llena, se dibuja y se vacía).</summary>
public sealed class GeometryBuffer
{
    private WorldVertex[] _v = new WorldVertex[2048];
    private short[] _i = new short[4096];
    private int _vn, _in;

    public int Vertices => _vn;

    public void Clear() { _vn = 0; _in = 0; }

    public int Vertex(in WorldVertex v)
    {
        if (_vn == _v.Length) Array.Resize(ref _v, _v.Length * 2);
        _v[_vn] = v;
        return _vn++;
    }

    public void Tri(int a, int b, int c)
    {
        if (_vn > short.MaxValue) return;
        if (_in + 3 > _i.Length) Array.Resize(ref _i, _i.Length * 2);
        _i[_in++] = (short)a;
        _i[_in++] = (short)b;
        _i[_in++] = (short)c;
    }

    /// <summary>Un vértice de color con alfa, mirando hacia <paramref name="n"/>.</summary>
    public int Vertex(Vector3 p, Vector3 n, Color c, float material = 0, float edge = 0) => Vertex(new WorldVertex(p, n, c, new Vector4(material, edge, 0, 1)));

    /// <summary>Un cuadrado que mira a la cámara (una gota, una chispa, una pizca de polvo).</summary>
    public void Billboard(Vector3 c, Vector3 right, Vector3 up, float size, Color col, Vector3 normal)
    {
        var r = right * size; var u = up * size;
        int a = Vertex(c - r + u, normal, col), b = Vertex(c + r + u, normal, col), d = Vertex(c + r - u, normal, col), e = Vertex(c - r - u, normal, col);
        Tri(a, b, d); Tri(a, d, e);
    }

    /// <summary>Un cuadrilátero cualquiera (a, b, c, d en orden).</summary>
    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color col, float material = 0)
    {
        int ia = Vertex(a, n, col, material), ib = Vertex(b, n, col, material), ic = Vertex(c, n, col, material), id = Vertex(d, n, col, material);
        Tri(ia, ib, ic); Tri(ia, ic, id);
    }

    public void Draw(GraphicsDevice gd)
    {
        if (_in == 0) return;
        gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _v, 0, _vn, _i, 0, _in / 3);
    }
}

/// <summary>Una figura para dibujar en este cuadro: su malla, sus huesos ya en el mundo, qué se ve y su destello.</summary>
public struct FigureDraw
{
    public FigureMesh Mesh;
    public Matrix[] Bones;
    public RenderMask Mask;
    public Vector4 Flash;
    public bool CastsShadow;
}

/// <summary>
/// Dibuja el mundo en "3D pixel art" desde los ojos de Ernesto: sombra de la luz grande → normal y
/// profundidad (para los contornos) → escena iluminada a 640×360 (el cielo de carne, las mallas, la
/// sangre pegada, las figuras, las partículas y lo que brilla) → el arma en la mano, encima de todo →
/// contornos, bloom y composición con escala entera.
/// </summary>
public sealed class Renderer : IDisposable
{
    public const int LowW = JaquecaGame.LowW, LowH = JaquecaGame.LowH;

    private readonly GraphicsDevice _gd;
    private readonly Effect _fx;
    public readonly FpsCamera Cam = new(LowW, LowH);
    public readonly Sky Sky = new();
    public readonly LightSet Lights = new();
    public readonly PostFx Post;
    public readonly ShadowMap Shadow;
    private readonly RenderTarget2D _scene, _nd;

    /// <summary>Mallas estáticas del mundo (en coordenadas de mundo).</summary>
    public readonly List<Mesh> Static = new();
    /// <summary>Lo que se arma cada cuadro y se ilumina (proyectiles, pedazos de mueble).</summary>
    public readonly GeometryBuffer Extra = new();
    /// <summary>Lo que no se ilumina (gotas de sangre en el aire, polvo de tiza).</summary>
    public readonly GeometryBuffer Flat = new();
    /// <summary>Lo que suma luz en este cuadro (fogonazos, chispas, el brillo de la sangre que cura).</summary>
    public readonly GeometryBuffer Glow = new();
    /// <summary>Lo que oscurece en este cuadro (las sombras de los pies).</summary>
    public readonly GeometryBuffer Shade = new();
    /// <summary>La sangre pegada en el mundo (la mantiene el juego: no se vacía cada cuadro).</summary>
    public DecalRing Decals;
    /// <summary>Las figuras de este cuadro, y las de la mano (el arma, la chancleta), que van encima de todo.</summary>
    public readonly List<FigureDraw> Figures = new(), ViewModel = new();
    /// <summary>Lo que brilla en la mano (el fogonazo del caño), encima de todo.</summary>
    public readonly GeometryBuffer ViewGlow = new();
    /// <summary>Campo de visión del arma en la mano (más cerrado que el del mundo: no se deforma en los bordes).</summary>
    public float ViewModelFov = 58;

    /// <summary>Dónde está el centro del lugar (lo que cubre la sombra).</summary>
    public Vector3 ShadowFocus;
    public Vector3 FigureAmbient = new(0.1f, 0.06f, 0.08f);
    public Vector3 FogColor = new(0.62f, 0.34f, 0.4f);
    public Vector2 FogRange = new(260, 900);
    public Vector3 FillDir = Vector3.Normalize(new Vector3(0.4f, 0.3f, 0.8f));
    public float Time;
    public float ShadowBias = 0.0004f;

    /// <summary>Prueba (--perfgpu): espera a la GPU al final de cada pasada para medirla.</summary>
    public static bool GpuProfile;

    private static readonly RasterizerState DecalState = new() { CullMode = CullMode.None, DepthBias = -0.00002f, SlopeScaleDepthBias = -1.5f };
    private static readonly BlendState Additive = new()
    {
        ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One, AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One,
        ColorWriteChannels = ColorWriteChannels.Red | ColorWriteChannels.Green | ColorWriteChannels.Blue,
    };
    private static readonly BlendState Darken = new()
    {
        ColorSourceBlend = Blend.SourceAlpha, ColorDestinationBlend = Blend.InverseSourceAlpha, AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One,
        ColorWriteChannels = ColorWriteChannels.Red | ColorWriteChannels.Green | ColorWriteChannels.Blue,
    };
    private static readonly DepthStencilState NoDepth = new() { DepthBufferEnable = false, DepthBufferWriteEnable = false };

    private readonly WorldVertex[] _skyV = new WorldVertex[4];
    private static readonly short[] SkyI = { 0, 1, 2, 0, 2, 3 };

    public Renderer(GraphicsDevice gd, ContentManager content)
    {
        _gd = gd;
        _fx = content.Load<Effect>("Effects/World");
        Post = new PostFx(gd, content.Load<Effect>("Effects/Post"));
        Shadow = new ShadowMap(gd);
        _scene = new RenderTarget2D(gd, LowW, LowH, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
        _nd = new RenderTarget2D(gd, LowW, LowH, false, SurfaceFormat.HalfVector4, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
    }

    public Effect Effect => _fx;

    /// <summary>Vacía lo del cuadro (lo que se ilumina, lo que brilla, lo que oscurece y las figuras).</summary>
    public void ClearFrame()
    {
        Extra.Clear();
        Flat.Clear();
        Glow.Clear();
        Shade.Clear();
        ViewGlow.Clear();
        Figures.Clear();
        ViewModel.Clear();
    }

    private void SetCommon(Matrix viewProj)
    {
        _fx.Set("World", Matrix.Identity);
        _fx.Set("ViewProj", viewProj);
        _fx.Set("LightViewProj", Shadow.ViewProj);
        _fx.Set("CamPos", Cam.Eye);
        _fx.Set("CamRight", Cam.Right);
        _fx.Set("CamUp", Cam.Up);
        _fx.Set("CamFwd", Cam.Forward);
        _fx.Set("CenterDepth", Vector3.Dot(Cam.Eye, Cam.Forward));
        _fx.Set("ScreenSize", new Vector2(LowW, LowH));
        _fx.Set("SunDir", Sky.SunDir);
        _fx.Set("SunColor", Sky.SunColor);
        _fx.Set("SkyColor", Sky.SkyColor);
        _fx.Set("GroundColor", Sky.GroundColor);
        _fx.Set("FillColor", Sky.FillColor);
        _fx.Set("FillDir", FillDir);
        _fx.Set("ShadowBias", ShadowBias);
        _fx.Set("Time", Time);
        float lum = Vector3.Dot(Sky.SunColor, new Vector3(0.3f, 0.5f, 0.2f));
        _fx.Set("SunVis", Math.Clamp(lum / 0.45f, 0, 1));
        _fx.Set("FigureAmbient", FigureAmbient);
        _fx.Set("FigureFlash", Vector4.Zero);
        _fx.Set("FogColor", FogColor);
        _fx.Set("FogRange", FogRange);
        _fx.Set("FigPalTex", FigurePalette.Texture(_gd));
        _fx.Set("FigRows", (float)Math.Max(1, FigurePalette.Count));
    }

    private void DrawStatic(string technique, RasterizerState rs)
    {
        _gd.RasterizerState = rs;
        _gd.DepthStencilState = DepthStencilState.Default;
        _gd.BlendState = BlendState.Opaque;
        _fx.CurrentTechnique = _fx.Techniques[technique];
        foreach (var m in Static)
        {
            if (m == null) continue;
            _fx.CurrentTechnique.Passes[0].Apply();
            m.Draw(_gd);
        }
    }

    private void DrawFigures(List<FigureDraw> list, string technique, bool shadowPass = false)
    {
        _gd.RasterizerState = RasterizerState.CullNone;
        _gd.DepthStencilState = DepthStencilState.Default;
        _gd.BlendState = BlendState.Opaque;
        _fx.CurrentTechnique = _fx.Techniques[technique];
        foreach (var f in list)
        {
            if (shadowPass && !f.CastsShadow) continue;
            _fx.Parameters["Bones"]?.SetValue(f.Bones);
            _fx.Set("FigureFlash", f.Flash);
            _fx.CurrentTechnique.Passes[0].Apply();
            f.Mesh.Draw(_gd, f.Mask);
        }
        _fx.Set("FigureFlash", Vector4.Zero);
    }

    private void DrawBuffer(GeometryBuffer g, string technique, BlendState blend, DepthStencilState depth, RasterizerState rs = null)
    {
        if (g == null || g.Vertices == 0) return;
        _gd.RasterizerState = rs ?? RasterizerState.CullNone;
        _gd.DepthStencilState = depth;
        _gd.BlendState = blend;
        _fx.CurrentTechnique = _fx.Techniques[technique];
        _fx.CurrentTechnique.Passes[0].Apply();
        g.Draw(_gd);
        _gd.BlendState = BlendState.Opaque;
        _gd.DepthStencilState = DepthStencilState.Default;
    }

    /// <summary>El cielo: un cuadrado en toda la pantalla, cada esquina con la dirección que mira.</summary>
    private void DrawSky()
    {
        float tanY = MathF.Tan(MathHelper.ToRadians(Cam.Fov) / 2), tanX = tanY * LowW / LowH;
        Vector3 D(float x, float y) => Cam.Forward + Cam.Right * (x * tanX) + Cam.Up * (y * tanY);
        _skyV[0] = new WorldVertex(new Vector3(-1, 1, 0), D(-1, 1), Color.White, Vector4.Zero);
        _skyV[1] = new WorldVertex(new Vector3(1, 1, 0), D(1, 1), Color.White, Vector4.Zero);
        _skyV[2] = new WorldVertex(new Vector3(1, -1, 0), D(1, -1), Color.White, Vector4.Zero);
        _skyV[3] = new WorldVertex(new Vector3(-1, -1, 0), D(-1, -1), Color.White, Vector4.Zero);
        _gd.RasterizerState = RasterizerState.CullNone;
        _gd.DepthStencilState = NoDepth;
        _gd.BlendState = BlendState.Opaque;
        _fx.CurrentTechnique = _fx.Techniques["Sky"];
        _fx.CurrentTechnique.Passes[0].Apply();
        _gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _skyV, 0, 4, SkyI, 0, 2);
        _gd.DepthStencilState = DepthStencilState.Default;
    }

    private Matrix ViewModelProj() => Cam.View * Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(ViewModelFov), LowW / (float)LowH, 0.05f, 200);

    public void Draw(SpriteBatch sb, RenderTarget2D target, Rectangle dest, int scale)
    {
        Sky.Update();
        Cam.Rebuild();
        Shadow.Setup(ShadowFocus, Sky.SunDir);
        for (int i = 0; i < 8; i++) _gd.Textures[i] = null;
        _fx.Set("ShadowTex", (Texture2D)null);
        SetCommon(Cam.ViewProj);

        // 1. Sombra de la luz grande (las dos caras: sin agujeros en mallas abiertas).
        _gd.SetRenderTarget(Shadow.Rt);
        _gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Vector4.One, 1, 0);
        _fx.Set("ViewProj", Shadow.ViewProj);
        DrawStatic("Shadow", RasterizerState.CullNone);
        DrawFigures(Figures, "FigureShadow", shadowPass: true);
        _fx.Set("ViewProj", Cam.ViewProj);

        // 2. Normal y profundidad (para los contornos); el arma en la mano, encima (con su propia profundidad).
        _gd.SetRenderTarget(_nd);
        _gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Vector4(0, 0, 9999, 0), 1, 0);
        DrawStatic("NormalDepth", RasterizerState.CullNone);
        DrawFigures(Figures, "FigureDepth");
        DrawBuffer(Extra, "NormalDepth", BlendState.Opaque, DepthStencilState.Default);
        if (ViewModel.Count > 0)
        {
            _gd.Clear(ClearOptions.DepthBuffer, Vector4.Zero, 1, 0);
            _fx.Set("ViewProj", ViewModelProj());
            DrawFigures(ViewModel, "FigureDepth");
            _fx.Set("ViewProj", Cam.ViewProj);
        }

        // 3. Escena iluminada. Alfa 1 = lleva contornos; el cielo queda en 0.
        _gd.SetRenderTarget(_scene);
        _gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Vector4(FogColor, 0), 1, 0);
        _fx.Set("ShadowTex", Shadow.Rt);
        Lights.Upload(_fx, Cam.Eye, Time);
        DrawSky();
        DrawStatic("Lit", RasterizerState.CullNone);
        if (Decals != null)
        {
            _gd.RasterizerState = DecalState;
            _gd.DepthStencilState = DepthStencilState.DepthRead;
            _gd.BlendState = BlendState.Opaque;
            _fx.CurrentTechnique = _fx.Techniques["Lit"];
            _fx.CurrentTechnique.Passes[0].Apply();
            Decals.Draw(_gd);
            _gd.DepthStencilState = DepthStencilState.Default;
        }
        DrawBuffer(Shade, "Shade", Darken, DepthStencilState.DepthRead, DecalState);
        DrawBuffer(Extra, "Lit", BlendState.Opaque, DepthStencilState.Default);
        DrawFigures(Figures, "Figure");
        DrawBuffer(Flat, "Flat", BlendState.Opaque, DepthStencilState.Default);
        DrawBuffer(Glow, "Glow", Additive, DepthStencilState.DepthRead);

        // 3b. El arma en la mano, encima de todo (se limpia la profundidad: nunca se mete en una pared).
        if (ViewModel.Count > 0)
        {
            _gd.Clear(ClearOptions.DepthBuffer, Vector4.Zero, 1, 0);
            _fx.Set("ViewProj", ViewModelProj());
            DrawFigures(ViewModel, "Figure");
            DrawBuffer(ViewGlow, "Glow", Additive, DepthStencilState.DepthRead);
            _fx.Set("ViewProj", Cam.ViewProj);
        }

        // 4. Contornos, bloom y composición.
        Post.Time = Time;
        for (int i = 0; i < 8; i++) _gd.Textures[i] = null;
        _fx.Set("ShadowTex", (Texture2D)null);
        Post.Render(sb, _scene, _nd, Cam, target, dest, scale);
    }

    public void Dispose()
    {
        Post.Dispose();
        Shadow.Dispose();
        _scene.Dispose();
        _nd.Dispose();
        foreach (var m in Static) m?.Dispose();
    }
}
