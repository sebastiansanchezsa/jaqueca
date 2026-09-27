using System.Numerics;
using Jaqueca.Anim;
using Jaqueca.Figures.Model;
using Jaqueca.Figures.Rig;
using Jaqueca.Sprites;

namespace Jaqueca.Figures.Render;

/// <summary>
/// Cámara ortográfica del horneado: mira desde el sur y desde arriba con la elevación dada.
/// La luz está fija respecto de la pantalla (arriba a la izquierda y de frente), así todas las
/// direcciones quedan iluminadas igual y se lee el volumen.
/// </summary>
public sealed class Camera
{
    /// <summary>
    /// Píxeles por unidad de mundo (la misma escala del mundo en el juego): un humano de 16 u
    /// mide ~48 px, con la cabeza de ~7 px (se lee la cara). Casilla del sprite y pie dentro de ella.
    /// </summary>
    public const float PixelsPerUnit = 3.5f;
    public const int Size = 160, FootX = 80, FootY = 118;

    public float K = PixelsPerUnit, Elevation = 30f;
    public int W = Size, H = Size;
    public float PivotX = FootX, PivotY = FootY;
    public Vector3 Right, Up, ToCam, Light;

    public Camera() { Update(); }

    public void Update()
    {
        float e = Elevation * MathF.PI / 180;
        Right = Vector3.UnitX;
        Up = new Vector3(0, MathF.Cos(e), -MathF.Sin(e));
        ToCam = new Vector3(0, MathF.Sin(e), MathF.Cos(e));
        var l = Shading.BakeLight;
        Light = l.X * Right + l.Y * Up + l.Z * ToCam;
    }

    /// <summary>Giro del personaje para una dirección (el personaje mira a +X en su espacio).</summary>
    public static Matrix4x4 Yaw(Dir8 d) => Yaw(d.Angle());

    /// <summary>Giro para cualquier ángulo (0 = este, π/2 = sur, hacia la cámara).</summary>
    public static Matrix4x4 Yaw(float angle) => Matrix4x4.CreateRotationY(-angle);

    public Vector2 Project(Vector3 p) => new(PivotX + K * Vector3.Dot(p, Right), PivotY - K * Vector3.Dot(p, Up));
    public float Depth(Vector3 p) => Vector3.Dot(p, ToCam);
    public Vector3 ToCamSpace(Vector3 n) => new(Vector3.Dot(n, Right), Vector3.Dot(n, Up), Vector3.Dot(n, ToCam));
}

/// <summary>Resultado de rasterizar una capa: un píxel por celda (Mat = -1 vacío).</summary>
public sealed class Raster
{
    public readonly int W, H;
    public readonly int[] Mat, Tone, Part;
    public readonly float[] Depth;
    public readonly Vector3[] Normal;
    /// <summary>Caja (inclusive) de lo que se tocó en el último render; vacía si X1 &lt; X0.</summary>
    public int X0, Y0, X1 = -1, Y1 = -1;

    public Raster(int w, int h)
    {
        W = w; H = h;
        Mat = new int[w * h]; Tone = new int[w * h]; Part = new int[w * h];
        Depth = new float[w * h]; Normal = new Vector3[w * h];
        Array.Fill(Mat, -1);
        Array.Fill(Depth, float.NegativeInfinity);
    }

    public bool Filled(int x, int y) => (uint)x < (uint)W && (uint)y < (uint)H && Mat[y * W + x] >= 0;

    /// <summary>Vacía lo que se usó en el render anterior.</summary>
    public void Clear()
    {
        for (int y = Math.Max(0, Y0); y <= Math.Min(H - 1, Y1); y++)
        {
            int o = y * W;
            for (int x = Math.Max(0, X0); x <= Math.Min(W - 1, X1); x++)
            {
                Mat[o + x] = -1;
                Depth[o + x] = float.NegativeInfinity;
            }
        }
        X0 = Y0 = 0; X1 = Y1 = -1;
    }

    public LayerFrame Pack()
    {
        int minX = W, minY = H, maxX = -1, maxY = -1;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            if (Mat[y * W + x] < 0) continue;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
        }
        if (maxX < 0) return LayerFrame.Empty;
        int w = maxX - minX + 1, h = maxY - minY + 1;
        var px = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = (minY + y) * W + minX + x, o = (y * w + x) * 4;
            if (Mat[i] < 0) continue;
            px[o] = LayerFrame.Pack(Mat[i], Tone[i]);
            px[o + 1] = LayerLibrary.QuantDepth(Depth[i]);
            (px[o + 2], px[o + 3]) = Oct.Encode(Normal[i]);
        }
        return new LayerFrame { X = (short)minX, Y = (short)minY, W = (short)w, H = (short)h, Px = px };
    }
}

/// <summary>
/// Buffers de un rasterizado, para reusar frame a frame sin reservar memoria (uno por hilo).
/// </summary>
public sealed class RasterContext
{
    internal const int SS = 2;
    public readonly Camera Cam;
    public readonly Raster R;
    internal readonly int SW, SH;
    internal readonly float[] T;
    internal readonly short[] Shape;
    internal readonly int[] Mat;
    internal readonly Vector3[] N, P;
    internal readonly int[] Scratch;
    internal int Sx0, Sy0, Sx1 = -1, Sy1 = -1;
    /// <summary>
    /// Corrimiento subpíxel de la figura dentro del sprite (píxeles, x derecha, y abajo). El
    /// sprite se dibuja encajado al píxel; la figura se corre lo que faltó, así el cuerpo queda
    /// donde está de verdad y un pie apoyado no baila medio píxel mientras el cuerpo avanza.
    /// </summary>
    public float OffX, OffY;

    public RasterContext(Camera cam)
    {
        Cam = cam;
        R = new Raster(cam.W, cam.H);
        SW = cam.W * SS; SH = cam.H * SS;
        T = new float[SW * SH];
        Shape = new short[SW * SH];
        Mat = new int[SW * SH];
        N = new Vector3[SW * SH];
        P = new Vector3[SW * SH];
        Scratch = new int[cam.W * cam.H];
        Array.Fill(T, float.PositiveInfinity);
        Array.Fill(Shape, (short)-1);
    }

    internal void ClearSamples()
    {
        for (int y = Math.Max(0, Sy0); y <= Math.Min(SH - 1, Sy1); y++)
        {
            int o = y * SW;
            for (int x = Math.Max(0, Sx0); x <= Math.Min(SW - 1, Sx1); x++)
            {
                T[o + x] = float.PositiveInfinity;
                Shape[o + x] = -1;
            }
        }
        Sx0 = Sy0 = int.MaxValue; Sx1 = Sy1 = -1;
    }
}

/// <summary>
/// Rasteriza una figura posada: un rayo ortográfico por subpíxel (2×2), intersección
/// analítica con cada forma dentro de su caja proyectada, y después por píxel: cobertura y
/// material por mayoría (evita el "hervor" entre frames), luz cuantizada a 5 tonos, oclusión
/// en los pliegues, contorno selectivo entre partes y limpieza de píxeles huérfanos.
/// El mismo código hornea las capas (ArtGen) y dibuja los personajes en vivo cada frame.
/// </summary>
public static class Rasterizer
{
    private const int SS = RasterContext.SS;

    /// <summary>Horneado: una capa en una de las 8 direcciones (buffers nuevos).</summary>
    public static Raster Render(Figure fig, Matrix4x4[] bones, Dir8 dir, Camera cam)
    {
        var ctx = new RasterContext(cam);   // sin corrimiento subpíxel
        Draw(ctx, fig, bones, Camera.Yaw(dir));
        if (fig.Stamps.Count > 0) StampBaked(fig, bones, dir, cam, ctx.R);
        return ctx.R;
    }

    /// <summary>
    /// En vivo: la figura armada completa (cuerpo, pelo, equipo) con cualquier giro, sobre los
    /// buffers del contexto. Los estampados (ojos) van sobre la cabeza ya dibujada.
    /// <paramref name="smear"/>: posiciones pasadas del hueso del arma (en el espacio del
    /// personaje) donde se dibujan las formas de estela; juntas pintan el barrido del golpe.
    /// </summary>
    /// <remarks>
    /// <paramref name="scale"/>: la figura se armó a otra medida que su esqueleto (la gente, ver
    /// <see cref="Rig.Dims.People"/>): cada forma se achica sobre su hueso. Lo que se le clavó
    /// (flechas) no: ya está donde pegó.
    /// </remarks>
    public static Raster RenderLive(RasterContext ctx, Figure fig, Matrix4x4[] bones, float yaw, bool eyesClosed = false, float offX = 0, float offY = 0, IReadOnlyList<Matrix4x4> smear = null, RenderMask mask = null, IReadOnlyList<(Bone bone, Matrix4x4 local)> attached = null, float scale = 1, IReadOnlyList<Physics.ClothSim> cloth = null)
    {
        ctx.OffX = offX;
        ctx.OffY = offY;
        var m = Camera.Yaw(yaw);
        var shaped = bones;
        if (scale != 1)
        {
            // Una copia por hilo (se rasteriza en paralelo).
            if (_scaled == null || _scaled.Length < bones.Length) _scaled = new Matrix4x4[bones.Length];
            var sm = Matrix4x4.CreateScale(scale);
            for (int i = 0; i < bones.Length; i++) _scaled[i] = sm * bones[i];
            shaped = _scaled;
        }
        Draw(ctx, fig, shaped, m, smear, mask, attached, bones, scale, cloth);
        if (fig.Stamps.Count > 0 && mask is not ({ OnlyWeapon: true } or { OnlyGear: true })) StampLive(fig, shaped, m, ctx, eyesClosed, mask);
        return ctx.R;
    }

    [ThreadStatic] private static Matrix4x4[] _scaled;

    /// <summary>
    /// Desde qué alto (en subpíxeles) se reparte el trabajo en franjas que se hacen a la vez: lo enorme
    /// (el alcaide, el Jardinero) no queda en un solo hilo mientras los demás esperan.
    /// </summary>
    private const int BandRows = 600;
    /// <summary>Prueba: cuántas franjas usar siempre (null: las que tocan por el tamaño).</summary>
    public static int? ForceBands;

    private static void Draw(RasterContext ctx, Figure fig, Matrix4x4[] bones, Matrix4x4 yaw, IReadOnlyList<Matrix4x4> smear = null, RenderMask mask = null, IReadOnlyList<(Bone bone, Matrix4x4 local)> attached = null, Matrix4x4[] rawBones = null, float scale = 1, IReadOnlyList<Physics.ClothSim> cloth = null)
    {
        ctx.ClearSamples();
        ctx.R.Clear();
        if (fig.Shapes.Count == 0) return;
        var raw = rawBones ?? bones;
        var cloths = ClothPoints(fig, bones, yaw, cloth);
        int bands = ForceBands ?? (ctx.SH >= BandRows ? Math.Clamp(Environment.ProcessorCount, 2, 8) : 1);
        if (bands == 1)
        {
            var b = Box.Empty;
            Trace(ctx, fig, bones, yaw, smear, mask, attached, raw, scale, 0, ctx.SH - 1, ref b);
            TraceCloth(ctx, fig, cloths, mask, 0, ctx.SH - 1, ref b);
            b.Into(ctx);
        }
        else
        {
            var boxes = new Box[bands];
            Parallel.For(0, bands, k =>
            {
                var b = Box.Empty;
                Trace(ctx, fig, bones, yaw, smear, mask, attached, raw, scale, k * ctx.SH / bands, (k + 1) * ctx.SH / bands - 1, ref b);
                TraceCloth(ctx, fig, cloths, mask, k * ctx.SH / bands, (k + 1) * ctx.SH / bands - 1, ref b);
                boxes[k] = b;
            });
            var all = Box.Empty;
            foreach (var b in boxes) all.Add(b);
            all.Into(ctx);
        }
        Shade(fig, ctx, bands);
    }

    /// <summary>La caja de subpíxeles que se tocaron (para vaciar sólo eso la próxima vez).</summary>
    private struct Box
    {
        public int X0, Y0, X1, Y1;
        public static Box Empty => new() { X0 = int.MaxValue, Y0 = int.MaxValue, X1 = -1, Y1 = -1 };
        public void Add(int x0, int y0, int x1, int y1) { X0 = Math.Min(X0, x0); Y0 = Math.Min(Y0, y0); X1 = Math.Max(X1, x1); Y1 = Math.Max(Y1, y1); }
        public void Add(in Box b) { if (b.X1 >= 0) Add(b.X0, b.Y0, b.X1, b.Y1); }
        public readonly void Into(RasterContext s) { s.Sx0 = X0; s.Sy0 = Y0; s.Sx1 = X1; s.Sy1 = Y1; }
    }

    /// <summary>Recorre las filas <paramref name="y0"/>..<paramref name="y1"/> en <paramref name="bands"/> franjas a la vez (1: de corrido).</summary>
    private static void Rows(int y0, int y1, int bands, Action<int, int> body)
    {
        if (y1 < y0) return;
        if (bands <= 1 || y1 - y0 < 16) { body(y0, y1); return; }
        int n = y1 - y0 + 1;
        Parallel.For(0, bands, k => body(y0 + k * n / bands, y0 + (k + 1) * n / bands - 1));
    }

    // ------------------------------------------------------------------ trazado

    private static void Trace(RasterContext s, Figure fig, Matrix4x4[] bones, Matrix4x4 yaw, IReadOnlyList<Matrix4x4> smear, RenderMask mask, IReadOnlyList<(Bone bone, Matrix4x4 local)> attached, Matrix4x4[] rawBones, float scale, int rowMin, int rowMax, ref Box box)
    {
        for (int si = 0; si < fig.Shapes.Count; si++)
        {
            var shape = fig.Shapes[si];
            if (shape is ClothTri || shape.Smear || shape.Arrow || !(mask?.Shows(shape) ?? RenderMask.Default(shape))) continue;
            if (shape.Show != null && !shape.Show()) continue;
            if (shape.CordBone is { } cb)
            {
                if (mask != null && mask.Hidden[(int)cb]) continue;
                TraceShape(s, fig, si, CordMatrix(Vector3.Transform(shape.CordFrom, bones[(int)shape.Bone]), Vector3.Transform(shape.CordTo, bones[(int)cb])) * yaw, rowMin, rowMax, ref box);
                continue;
            }
            var local = shape.LocalNow;
            TraceShape(s, fig, si, local * bones[(int)shape.Bone] * yaw, rowMin, rowMax, ref box, local);
        }
        if (attached != null)
        {
            for (int si = 0; si < fig.Shapes.Count; si++)
            {
                var shape = fig.Shapes[si];
                if (!shape.Arrow) continue;
                foreach (var (bone, local) in attached)
                    if (mask == null || !mask.Hidden[(int)bone]) TraceShape(s, fig, si, shape.Local * local * rawBones[(int)bone] * yaw, rowMin, rowMax, ref box);
            }
        }
        if (smear == null || smear.Count == 0) return;
        var sm = Matrix4x4.CreateScale(scale);
        for (int si = 0; si < fig.Shapes.Count; si++)
        {
            var shape = fig.Shapes[si];
            if (!shape.Smear) continue;
            foreach (var g in smear) TraceShape(s, fig, si, shape.Local * sm * g * yaw, rowMin, rowMax, ref box);
        }
    }

    /// <summary>Lleva el tramo de largo 1 sobre Y de una cuerda a ir de <paramref name="a"/> a <paramref name="b"/>.</summary>
    private static Matrix4x4 CordMatrix(Vector3 a, Vector3 b)
    {
        var y = b - a;
        float len = MathF.Max(y.Length(), 1e-3f);
        var yn = y / len;
        var x = Vector3.Cross(yn, MathF.Abs(yn.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
        x = Vector3.Normalize(x);
        var z = Vector3.Cross(x, yn);
        return new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, a.X, a.Y, a.Z, 1);
    }

    /// <summary>Traza una forma con su matriz (forma → cámara): intersección por subpíxel dentro de su caja proyectada.</summary>
    private static void TraceShape(RasterContext s, Figure fig, int si, Matrix4x4 m, int rowMin, int rowMax, ref Box box, Matrix4x4? localNow = null)
    {
        var cam = s.Cam;
        int sw = s.SW, sh = s.SH;
        var dirW = -cam.ToCam;
        var shape = fig.Shapes[si];
        var toBone = localNow ?? shape.Local;
        if (!Matrix4x4.Invert(m, out var inv)) return;
        var invT = Matrix4x4.Transpose(inv);
        shape.Span(m, cam.Right, cam.Up, out float ex0, out float ex1, out float ey0, out float ey1);
        float ox = cam.PivotX + s.OffX, oy = cam.PivotY + s.OffY;
        int x0 = Math.Max(0, (int)MathF.Floor((ox + ex0 * cam.K - 1) * SS)), x1 = Math.Min(sw - 1, (int)MathF.Ceiling((ox + ex1 * cam.K + 1) * SS));
        // La primera fila de la forma (sin recortar a la franja): los rayos se cuentan desde ahí, así en
        // franjas dan exactamente lo mismo que de corrido.
        int top = Math.Max(0, (int)MathF.Floor((oy - ey1 * cam.K - 1) * SS));
        int y0 = Math.Max(rowMin, top), y1 = Math.Min(Math.Min(sh - 1, rowMax), (int)MathF.Ceiling((oy - ey0 * cam.K + 1) * SS));
        if (x1 < x0 || y1 < y0) return;
        box.Add(x0, y0, x1, y1);
        var dl = Vector3.TransformNormal(dirW, inv);
        // El origen del rayo es lineal en el subpíxel: se arma una vez y se suma por paso.
        var stepX = Vector3.TransformNormal(cam.Right / (SS * cam.K), inv);
        var stepY = Vector3.TransformNormal(-cam.Up / (SS * cam.K), inv);
        float px = cam.PivotX + s.OffX, py = cam.PivotY + s.OffY;
        var row0 = Vector3.Transform(cam.Right * (((x0 + 0.5f) / SS - px) / cam.K) + cam.Up * ((py - (top + 0.5f) / SS) / cam.K) + cam.ToCam * 100, inv);

        for (int sy = y0; sy <= y1; sy++)
        {
            var ol = row0 + stepY * (sy - top);
            for (int sx = x0; sx <= x1; sx++, ol += stepX)
            {
                if (!shape.HitFront(ol, dl, out float t0, out var n0)) continue;
                float t = t0;
                var nl = n0;
                var pb = Vector3.Transform(ol + dl * t0, toBone) + shape.Origin;
                if (shape.Mask != null && !shape.Mask(pb))
                {
                    // Por el hueco se ve el interior de la cáscara.
                    if (!shape.Hit(ol, dl, out _, out _, out float t1, out var n1)) continue;
                    t = t1; nl = -n1;
                    pb = Vector3.Transform(ol + dl * t1, toBone) + shape.Origin;
                    if (!shape.Mask(pb)) continue;
                }
                int i = sy * sw + sx;
                if (t >= s.T[i]) continue;
                int mat = shape.Paint != null ? shape.Paint(pb, shape.Mat) : shape.Mat;
                // Bajo un sombrero de ala, el pelo de arriba no existe (se ve lo de atrás).
                if (fig.HideHairTop && fig.Mats[mat].Def.HairTop) continue;
                s.T[i] = t;
                s.Shape[i] = (short)si;
                s.N[i] = Vector3.Normalize(Vector3.TransformNormal(nl, invT));
                s.P[i] = pb;
                s.Mat[i] = mat;
            }
        }
    }

    /// <summary>Los puntos de cada tela de este dibujo, ya en el espacio de la cámara (y sus normales).</summary>
    private readonly record struct ClothPts(ClothDef Def, Vector3[] P, Vector3[] N);

    /// <summary>
    /// Dónde está cada punto de cada tela: los de la simulación del personaje (<paramref name="sims"/>) o,
    /// sin simulación (el horneado, un retrato, un pedazo cortado), los de reposo sobre su hueso.
    /// </summary>
    private static ClothPts[] ClothPoints(Figure fig, Matrix4x4[] bones, Matrix4x4 yaw, IReadOnlyList<Physics.ClothSim> sims)
    {
        if (fig.Cloths.Count == 0) return Array.Empty<ClothPts>();
        var list = new ClothPts[fig.Cloths.Count];
        for (int c = 0; c < fig.Cloths.Count; c++)
        {
            var def = fig.Cloths[c];
            Physics.ClothSim sim = null;
            if (sims != null) foreach (var x in sims) if (x.Def == def && x.Ready) { sim = x; break; }
            int n = def.Count;
            var p = new Vector3[n];
            var nr = new Vector3[n];
            if (sim != null)
            {
                for (int i = 0; i < n; i++)
                {
                    p[i] = Vector3.Transform(sim.Local[i], yaw);
                    nr[i] = Vector3.TransformNormal(sim.Normal[i], yaw);
                }
            }
            else
            {
                var m = bones[(int)def.Anchor] * yaw;
                for (int i = 0; i < n; i++) p[i] = Vector3.Transform(def.Rest[i], m);
                Physics.ClothSim.Normals(p, nr, def);
            }
            list[c] = new ClothPts(def, p, nr);
        }
        return list;
    }

    /// <summary>
    /// Los triángulos de las telas (filas <paramref name="rowMin"/>..<paramref name="rowMax"/>): la cámara es
    /// ortográfica, así que cada uno se pinta en pantalla por coordenadas baricéntricas (la profundidad, la
    /// normal y el punto de reposo se interpolan). Se ven de los dos lados.
    /// </summary>
    private static void TraceCloth(RasterContext s, Figure fig, ClothPts[] cloths, RenderMask mask, int rowMin, int rowMax, ref Box box)
    {
        if (cloths.Length == 0) return;
        var cam = s.Cam;
        int sw = s.SW, sh = s.SH;
        float ox = cam.PivotX + s.OffX, oy = cam.PivotY + s.OffY;
        for (int si = 0; si < fig.Shapes.Count; si++)
        {
            if (fig.Shapes[si] is not ClothTri tri) continue;
            if (!(mask?.Shows(tri) ?? RenderMask.Default(tri))) continue;
            if (tri.Show != null && !tri.Show()) continue;
            ClothPts pts = default;
            foreach (var c in cloths) if (c.Def == tri.Cloth) { pts = c; break; }
            if (pts.P == null) continue;
            var pa = pts.P[tri.A]; var pb = pts.P[tri.B]; var pc = pts.P[tri.C];
            // En subpíxeles (el centro del subpíxel sx está en sx + 0,5).
            float ax = (ox + cam.K * Vector3.Dot(pa, cam.Right)) * SS, ay = (oy - cam.K * Vector3.Dot(pa, cam.Up)) * SS;
            float bx = (ox + cam.K * Vector3.Dot(pb, cam.Right)) * SS, by = (oy - cam.K * Vector3.Dot(pb, cam.Up)) * SS;
            float cx = (ox + cam.K * Vector3.Dot(pc, cam.Right)) * SS, cy = (oy - cam.K * Vector3.Dot(pc, cam.Up)) * SS;
            float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (MathF.Abs(area) < 1e-4f) continue;
            int x0 = Math.Max(0, (int)MathF.Floor(MathF.Min(ax, MathF.Min(bx, cx)) - 0.5f)), x1 = Math.Min(sw - 1, (int)MathF.Ceiling(MathF.Max(ax, MathF.Max(bx, cx)) - 0.5f));
            int y0 = Math.Max(rowMin, (int)MathF.Floor(MathF.Min(ay, MathF.Min(by, cy)) - 0.5f)), y1 = Math.Min(Math.Min(sh - 1, rowMax), (int)MathF.Ceiling(MathF.Max(ay, MathF.Max(by, cy)) - 0.5f));
            if (x1 < x0 || y1 < y0) continue;
            float ta = 100 - cam.Depth(pa), tb = 100 - cam.Depth(pb), tc = 100 - cam.Depth(pc);
            var ra = tri.Cloth.Rest[tri.A] + tri.Origin; var rb = tri.Cloth.Rest[tri.B] + tri.Origin; var rc = tri.Cloth.Rest[tri.C] + tri.Origin;
            var na = pts.N[tri.A]; var nb = pts.N[tri.B]; var nc = pts.N[tri.C];
            float inv = 1 / area;
            bool touched = false;
            for (int sy = y0; sy <= y1; sy++)
            {
                float py = sy + 0.5f;
                for (int sx = x0; sx <= x1; sx++)
                {
                    float px = sx + 0.5f;
                    // Baricéntricas (con un pelo de margen: los triángulos vecinos no dejan rendijas).
                    float wa = ((bx - px) * (cy - py) - (by - py) * (cx - px)) * inv;
                    float wb = ((cx - px) * (ay - py) - (cy - py) * (ax - px)) * inv;
                    float wc = 1 - wa - wb;
                    if (wa < -1e-3f || wb < -1e-3f || wc < -1e-3f) continue;
                    float t = wa * ta + wb * tb + wc * tc;
                    int i = sy * sw + sx;
                    if (t >= s.T[i]) continue;
                    var rest = ra * wa + rb * wb + rc * wc;
                    if (tri.Mask != null && !tri.Mask(rest)) continue;
                    var n = na * wa + nb * wb + nc * wc;
                    n = n.LengthSquared() > 1e-8f ? Vector3.Normalize(n) : cam.ToCam;
                    // Se ve de los dos lados: la cara que mira a la cámara.
                    if (Vector3.Dot(n, cam.ToCam) < 0) n = -n;
                    s.T[i] = t;
                    s.Shape[i] = (short)si;
                    s.N[i] = n;
                    s.P[i] = rest;
                    s.Mat[i] = tri.Paint != null ? tri.Paint(rest, tri.Mat) : tri.Mat;
                    touched = true;
                }
            }
            if (touched) box.Add(x0, y0, x1, y1);
        }
    }

    private static float MaxScale(Matrix4x4 m)
    {
        float a = new Vector3(m.M11, m.M12, m.M13).Length();
        float b = new Vector3(m.M21, m.M22, m.M23).Length();
        float c = new Vector3(m.M31, m.M32, m.M33).Length();
        return MathF.Max(a, MathF.Max(b, c));
    }

    // ------------------------------------------------------------------ sombreado

    private static void Shade(Figure fig, RasterContext s, int bands = 1)
    {
        var r = s.R;
        if (s.Sx1 < 0) return;
        int px0 = s.Sx0 / SS, py0 = s.Sy0 / SS, px1 = Math.Min(r.W - 1, s.Sx1 / SS), py1 = Math.Min(r.H - 1, s.Sy1 / SS);
        var box = Box.Empty;
        var gate = new object();
        Rows(py0, py1, bands, (y0, y1) =>
        {
            var b = ShadeRows(fig, s, px0, px1, y0, y1);
            lock (gate) box.Add(b);
        });
        if (box.X1 < 0) return;
        r.X0 = box.X0; r.Y0 = box.Y0; r.X1 = box.X1; r.Y1 = box.Y1;
        Folds(r, s.Scratch, bands);
        Orphans(r, s.Scratch, bands);
    }

    /// <summary>Cada píxel de las filas <paramref name="py0"/>..<paramref name="py1"/>: material y tono de lo que más lo cubre (ver <see cref="Shade"/>).</summary>
    private static Box ShadeRows(Figure fig, RasterContext s, int px0, int px1, int py0, int py1)
    {
        var cam = s.Cam;
        var r = s.R;
        Span<int> idx = stackalloc int[SS * SS];
        int bx0 = r.W, by0 = r.H, bx1 = -1, by1 = -1;
        for (int y = py0; y <= py1; y++)
        for (int x = px0; x <= px1; x++)
        {
            int n = 0;
            bool thin = false;
            for (int j = 0; j < SS; j++)
            for (int k = 0; k < SS; k++)
            {
                int i = (y * SS + j) * s.SW + x * SS + k;
                if (s.Shape[i] < 0) continue;
                idx[n++] = i;
                thin |= fig.Shapes[s.Shape[i]].Thin;
            }
            if (n < (thin ? 1 : 2)) continue;

            // Parte con más subpíxeles; a igual cantidad, la más cercana.
            int bestPart = -1, bestCount = 0;
            float bestT = float.PositiveInfinity;
            for (int a = 0; a < n; a++)
            {
                int part = fig.Shapes[s.Shape[idx[a]]].Part, count = 0;
                float tmin = float.PositiveInfinity;
                for (int b = 0; b < n; b++)
                {
                    if (fig.Shapes[s.Shape[idx[b]]].Part != part) continue;
                    count++;
                    tmin = MathF.Min(tmin, s.T[idx[b]]);
                }
                if (count > bestCount || (count == bestCount && tmin < bestT)) { bestPart = part; bestCount = count; bestT = tmin; }
            }
            int rep = -1;
            var nsum = Vector3.Zero;
            for (int a = 0; a < n; a++)
            {
                int i = idx[a];
                if (fig.Shapes[s.Shape[i]].Part != bestPart) continue;
                nsum += s.N[i];
                if (rep < 0 || s.T[i] < s.T[rep]) rep = i;
            }

            int o = y * r.W + x;
            var nw = Vector3.Normalize(nsum);
            var mat = fig.Mats[s.Mat[rep]];
            r.Mat[o] = s.Mat[rep];
            r.Part[o] = bestPart;
            r.Depth[o] = 100 - s.T[rep];
            r.Normal[o] = cam.ToCamSpace(nw);

            // La luz da tonos 1 a 4: el más oscuro queda para pliegues y contornos, así las formas no se ensucian.
            int t = Shading.Tone(Shading.Light(nw, cam.Light, mat.Def.Flat, mat.Def.Lift), mat.Def.Shiny);
            if (mat.Tex != null) t = mat.Tex(s.P[rep], t);
            r.Tone[o] = Math.Clamp(t, 0, 4);
            bx0 = Math.Min(bx0, x); bx1 = Math.Max(bx1, x);
            by0 = Math.Min(by0, y); by1 = Math.Max(by1, y);
        }
        return bx1 < 0 ? Box.Empty : new Box { X0 = bx0, Y0 = by0, X1 = bx1, Y1 = by1 };
    }

    /// <summary>Oclusión en los pliegues y contorno selectivo donde una parte más cercana pisa a otra.</summary>
    private static void Folds(Raster r, int[] tone, int bands = 1)
    {
        Rows(r.Y0, r.Y1, bands, (ya, yb) =>
        {
        for (int y = ya; y <= yb; y++)
        for (int x = r.X0; x <= r.X1; x++)
        {
            int i = y * r.W + x;
            if (r.Mat[i] < 0) continue;
            float occ = 0;
            bool line = false;
            // Un píxel de sombra junto a lo que tapa, y la línea donde una prenda pisa a otra
            // (el ruedo de la capelina sobre la camisa ya marca línea: a ~48 px la ropa se lee por sus bordes).
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (!r.Filled(x + ox, y + oy)) continue;
                int q = (y + oy) * r.W + x + ox;
                if (r.Part[q] == r.Part[i]) continue;
                float dz = r.Depth[q] - r.Depth[i];
                int man = Math.Abs(ox) + Math.Abs(oy);
                occ = MathF.Max(occ, dz * (man <= 1 ? 1f : 0.7f));
                if (man == 1 && dz > 0.3f) line = true;
            }
            int t = r.Tone[i];
            if (occ > 0.6f) t -= 1;
            if (line) t = Math.Min(t, 1) - 1;
            tone[i] = Math.Max(0, t);
        }
        });
        CopyBack(r, tone);
    }

    /// <summary>Un píxel suelto de otro tono, rodeado por un mismo tono del mismo material, se une a sus vecinos.</summary>
    private static void Orphans(Raster r, int[] tone, int bands = 1)
    {
        Rows(r.Y0, r.Y1, bands, (ya, yb) =>
        {
        for (int y = ya; y <= yb; y++)
        for (int x = r.X0; x <= r.X1; x++)
        {
            int i = y * r.W + x;
            if (r.Mat[i] < 0) continue;
            int same = 0, other = -1;
            bool agree = true;
            foreach (var (dx, dy) in Dirs4)
            {
                if (!r.Filled(x + dx, y + dy)) continue;
                int q = (y + dy) * r.W + x + dx;
                if (r.Mat[q] != r.Mat[i]) continue;
                same++;
                if (other < 0) other = r.Tone[q];
                else if (other != r.Tone[q]) agree = false;
            }
            tone[i] = same >= 3 && agree && other != r.Tone[i] ? other : r.Tone[i];
        }
        });
        CopyBack(r, tone);
    }

    private static void CopyBack(Raster r, int[] tone)
    {
        for (int y = r.Y0; y <= r.Y1; y++)
        for (int x = r.X0; x <= r.X1; x++)
        {
            int i = y * r.W + x;
            if (r.Mat[i] >= 0) r.Tone[i] = tone[i];
        }
    }

    private static readonly (int, int)[] Dirs4 = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    // ------------------------------------------------------------------ estampados

    /// <summary>Horneado: la capa de ojos no tiene la cabeza; la dibuja aparte como oclusor.</summary>
    private static void StampBaked(Figure fig, Matrix4x4[] bones, Dir8 dir, Camera cam, Raster r)
    {
        Raster occ = fig.Occluder != null ? Render(fig.Occluder, bones, dir, cam) : null;
        var yaw = Camera.Yaw(dir);
        foreach (var st in fig.Stamps)
        {
            if (!Place(st, bones, yaw, cam, out int ix, out int iy, out float d, out var nw, out int flip)) continue;
            if (occ != null && (!occ.Filled(ix, iy) || occ.Depth[iy * occ.W + ix] > d + 0.5f)) continue;
            foreach (var (dx, dy, mat, tone) in st.Pixels)
            {
                int x = ix + dx * flip, y = iy + dy;
                if ((uint)x >= (uint)r.W || (uint)y >= (uint)r.H) continue;
                int i = y * r.W + x;
                if (occ != null && !occ.Filled(x, y)) continue;
                float surf = occ != null ? MathF.Max(occ.Depth[i], d) : d;
                Put(r, i, x, y, mat, tone, surf + 0.25f, cam.ToCamSpace(nw));
            }
        }
    }

    /// <summary>En vivo: los ojos caen sobre la cabeza ya dibujada (sólo donde se ve la cara).</summary>
    private static void StampLive(Figure fig, Matrix4x4[] bones, Matrix4x4 yaw, RasterContext ctx, bool closed, RenderMask mask = null)
    {
        var cam = ctx.Cam;
        var r = ctx.R;
        foreach (var st in fig.Stamps)
        {
            if (mask != null && mask.Hidden[(int)st.Bone]) continue;
            if (!Place(st, bones, yaw, cam, out int ix, out int iy, out float d, out var nw, out int flip, ctx.OffX, ctx.OffY)) continue;
            if (!OnSurface(fig, r, ix, iy, d)) continue;
            var px = closed && st.Closed != null ? st.Closed : st.Pixels;
            foreach (var (dx, dy, mat, tone) in px)
            {
                int x = ix + dx * flip, y = iy + dy;
                if (!OnSurface(fig, r, x, y, d)) continue;
                int i = y * r.W + x;
                Put(r, i, x, y, mat, tone, MathF.Max(r.Depth[i], d) + 0.25f, cam.ToCamSpace(nw));
            }
        }
    }

    private static bool OnSurface(Figure fig, Raster r, int x, int y, float d)
    {
        if (!r.Filled(x, y)) return false;
        int i = y * r.W + x;
        return fig.StampParts.Contains(r.Part[i]) && r.Depth[i] <= d + 0.5f;
    }

    private static bool Place(Stamp st, Matrix4x4[] bones, Matrix4x4 yaw, Camera cam, out int ix, out int iy, out float d, out Vector3 nw, out int flip, float offX = 0, float offY = 0)
    {
        var m = bones[(int)st.Bone] * yaw;
        var p = Vector3.Transform(st.At, m);
        nw = Vector3.Normalize(Vector3.TransformNormal(st.Normal, m));
        ix = iy = 0; d = 0; flip = 1;
        if (Vector3.Dot(nw, cam.ToCam) < st.MinFacing) return false;
        var sp = cam.Project(p) + new Vector2(offX, offY);
        ix = (int)MathF.Floor(sp.X); iy = (int)MathF.Floor(sp.Y);
        d = cam.Depth(p);
        if (st.Outward != Vector3.Zero && Vector3.Dot(Vector3.TransformNormal(st.Outward, m), cam.Right) < 0) flip = -1;
        return true;
    }

    private static void Put(Raster r, int i, int x, int y, int mat, int tone, float depth, Vector3 n)
    {
        r.Mat[i] = mat;
        r.Tone[i] = tone;
        r.Part[i] = 0;
        r.Depth[i] = depth;
        r.Normal[i] = n;
        r.X0 = Math.Min(r.X0, x); r.X1 = Math.Max(r.X1, x);
        r.Y0 = Math.Min(r.Y0, y); r.Y1 = Math.Max(r.Y1, y);
    }
}
