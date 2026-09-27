using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Jaqueca.Client.Render;

/// <summary>
/// Vértice del mundo. Color = albedo (alfa = emisión: 1 normal, menos brilla solo).
/// Data: x = material (ver <see cref="Materials"/>), y = contorno (1 completo, 0.5 sólo brillo
/// de arista, 0 nada), z = cuánto lo mueve el viento, w = oclusión horneada (1 = nada).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct WorldVertex : IVertexType
{
    public Vector3 Position;
    public Vector3 Normal;
    public Color Color;
    public Vector4 Data;

    public static readonly VertexDeclaration Declaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0),
        new VertexElement(28, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0));

    public WorldVertex(Vector3 p, Vector3 n, Color c, Vector4 d) { Position = p; Normal = n; Color = c; Data = d; }
    VertexDeclaration IVertexType.VertexDeclaration => Declaration;
}

/// <summary>
/// Los materiales del mundo (los dibuja World.fx con ruido en el espacio del mundo, cuantizado). Los de
/// Inquisition siguen (piedra, madera...) y los de la casa van después: lo nuevo, al final.
/// </summary>
public static class Materials
{
    public const float Plain = 0, Stone = 1, Sand = 2, Grass = 3, Bark = 4, Seabed = 5, Moss = 6, Tile = 7, Cobble = 8, Hay = 9, Wood = 10, Leaf = 11, Flagstone = 12, Ashlar = 13, Brick = 14, Sludge = 15;
    /// <summary>El parquet del living: tablitas en espiga, alguna más oscura, juntas finas.</summary>
    public const float Parquet = 16;
    /// <summary>El empapelado de la abuela: franjas verticales con florcitas repetidas.</summary>
    public const float Wallpaper = 17;
    /// <summary>La alfombra: trama gruesa con una guarda en el borde (el color de la guarda, en el vértice).</summary>
    public const float Carpet = 18;
    /// <summary>La tela del sillón: un tapizado con trama de rombos.</summary>
    public const float Upholstery = 19;
    /// <summary>Los azulejos de la cocina: cuadrados blancos con junta gris.</summary>
    public const float Azulejo = 20;
    /// <summary>La carne del cielo de adentro de la cabeza (las paredes que se terminan): venas que laten.</summary>
    public const float Flesh = 21;
    /// <summary>El mantel de hule: cuadros rojos y blancos.</summary>
    public const float Oilcloth = 22;
    /// <summary>La pantalla del televisor sin señal: la lluvia que se mueve y las líneas.</summary>
    public const float Static = 23;
}

/// <summary>Malla estática en GPU (vértices + índices).</summary>
public sealed class Mesh : IDisposable
{
    public VertexBuffer Vb;
    public IndexBuffer Ib;
    public int Triangles;

    public static Mesh Create<T>(GraphicsDevice gd, T[] verts, int[] idx) where T : struct, IVertexType
    {
        if (verts.Length == 0) return null;
        var m = new Mesh();
        m.Vb = new VertexBuffer(gd, typeof(T), verts.Length, BufferUsage.WriteOnly);
        m.Vb.SetData(verts);
        if (verts.Length < 65536)
        {
            var s = new short[idx.Length];
            for (int i = 0; i < idx.Length; i++) s[i] = (short)idx[i];
            m.Ib = new IndexBuffer(gd, IndexElementSize.SixteenBits, s.Length, BufferUsage.WriteOnly);
            m.Ib.SetData(s);
        }
        else
        {
            m.Ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, idx.Length, BufferUsage.WriteOnly);
            m.Ib.SetData(idx);
        }
        m.Triangles = idx.Length / 3;
        return m;
    }

    public void Draw(GraphicsDevice gd)
    {
        gd.SetVertexBuffer(Vb);
        gd.Indices = Ib;
        gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, Triangles);
    }

    public void Dispose()
    {
        Vb?.Dispose();
        Ib?.Dispose();
    }
}

/// <summary>
/// Arma mallas facetadas en CPU: cada triángulo lleva su normal de cara (el look low-poly de
/// las rocas y las ruinas) salvo que se pasen normales suaves.
/// </summary>
public sealed class MeshBuilder
{
    public readonly List<WorldVertex> V = new();
    public readonly List<int> I = new();
    /// <summary>
    /// Por cada triángulo (en el orden de <see cref="I"/>), si frena a los cuerpos (ver
    /// <see cref="Solid"/>): lo arma la grilla de colisiones.
    /// </summary>
    public readonly List<bool> Hard = new();

    public float Material = Materials.Plain, Edge = 1, Wind = 0;
    /// <summary>
    /// Lo que se agrega desde acá frena a los cuerpos (muros, columnas, muebles). Los adornos del
    /// piso (huesos, hongos, velas, basura) se arman con esto en false: se ven, pero se pasa por encima.
    /// </summary>
    public bool Solid = true;

    private Vector4 Data(float ao = 1) => new(Material, Edge, Wind, ao);

    public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
    {
        var n = Vector3.Cross(b - a, c - a);
        if (n.LengthSquared() < 1e-10f) return;
        n.Normalize();
        int o = V.Count;
        V.Add(new WorldVertex(a, n, col, Data()));
        V.Add(new WorldVertex(b, n, col, Data()));
        V.Add(new WorldVertex(c, n, col, Data()));
        I.Add(o); I.Add(o + 1); I.Add(o + 2);
        Hard.Add(Solid);
    }

    /// <summary>Cuadrilátero plano a, b, c, d en sentido antihorario visto desde afuera.</summary>
    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
    {
        Tri(a, b, c, col);
        Tri(a, c, d, col);
    }

    /// <summary>Vértice con normal propia (superficies suaves: terreno).</summary>
    public int Vertex(Vector3 p, Vector3 n, Color col, float ao = 1)
    {
        V.Add(new WorldVertex(p, n, col, Data(ao)));
        return V.Count - 1;
    }

    public void Index(int a, int b, int c) { I.Add(a); I.Add(b); I.Add(c); Hard.Add(Solid); }

    /// <summary>¿El triángulo <paramref name="t"/> (su primer índice en <see cref="I"/>) frena a los cuerpos?</summary>
    public bool IsHard(int t) => t / 3 >= Hard.Count || Hard[t / 3];

    /// <summary>
    /// Desde acá hasta que se suelta (<c>using var _ = b.Decor();</c>), lo que se arma es adorno: se
    /// ve pero no frena (ver <see cref="Solid"/>). Al soltarse vuelve a como estaba.
    /// </summary>
    public DecorScope Decor()
    {
        var scope = new DecorScope(this, Solid);
        Solid = false;
        return scope;
    }

    public readonly struct DecorScope : IDisposable
    {
        private readonly MeshBuilder _b;
        private readonly bool _was;
        public DecorScope(MeshBuilder b, bool was) { _b = b; _was = was; }
        public void Dispose() { if (_b != null) _b.Solid = _was; }
    }

    /// <summary>Caja con giro en Y; <paramref name="top"/> pinta la tapa de otro color.</summary>
    public void Box(Vector3 center, Vector3 half, Color col, float yaw = 0, Color? top = null)
    {
        var rot = Matrix.CreateRotationY(yaw);
        Vector3 P(float x, float y, float z) => center + Vector3.Transform(new Vector3(x * half.X, y * half.Y, z * half.Z), rot);
        var t = top ?? col;
        Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), t);      // arriba
        Quad(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), col);  // abajo
        Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), col);      // sur (+Z)
        Quad(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), col);  // norte
        Quad(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), col);      // este
        Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), col);  // oeste
    }

    /// <summary>Caja con cualquier giro: <paramref name="transform"/> lleva su centro y su orientación (las caras, en su espacio).</summary>
    public void Box(Matrix transform, Vector3 half, Color col, Color? top = null)
    {
        Vector3 P(float x, float y, float z) => Vector3.Transform(new Vector3(x * half.X, y * half.Y, z * half.Z), transform);
        var t = top ?? col;
        Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), t);
        Quad(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), col);
        Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), col);
        Quad(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), col);
        Quad(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), col);
        Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), col);
    }

    public Mesh Build(GraphicsDevice gd)
    {
        Captured?.Add(this);
        return Mesh.Create(gd, V.ToArray(), I.ToArray());
    }

    /// <summary>Si está, cada malla que se sube se anota acá (la auditoría de colisiones mira lo que se armó).</summary>
    public static List<MeshBuilder> Captured;
}
