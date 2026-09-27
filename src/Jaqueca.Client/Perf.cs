using System.Diagnostics;

namespace Jaqueca.Client;

/// <summary>
/// Cuánto tarda cada parte del cuadro (la simulación, el rasterizado de los personajes, la subida,
/// armar lo dinámico, dibujar, presentar): se junta por segundo y se muestra con F5 o --debug, para
/// saber qué es lo que baja los cuadros por segundo.
/// </summary>
public static class Perf
{
    private static readonly Dictionary<string, (double sum, double max, int n)> Parts = new();
    /// <summary>Cuánta memoria pidió cada parte desde el último informe (bytes).</summary>
    private static readonly Dictionary<string, long> Alloc = new();
    private static readonly List<string> Order = new();

    /// <summary>Mide lo que pasa hasta que se suelta lo que devuelve (<c>using var _ = Perf.Time("sim");</c>).</summary>
    public static Span Time(string part) => new(part, Stopwatch.GetTimestamp(), GC.GetAllocatedBytesForCurrentThread());

    public static void AddAlloc(string part, long bytes) => Alloc[part] = Alloc.GetValueOrDefault(part) + bytes;

    public static void Add(string part, double ms)
    {
        if (!Parts.TryGetValue(part, out var p) && !Order.Contains(part)) Order.Add(part);
        Parts[part] = (p.sum + ms, Math.Max(p.max, ms), p.n + 1);
    }

    /// <summary>El promedio (y el peor) de cada parte desde el último informe, y vacía la cuenta.</summary>
    public static string Report()
    {
        var parts = Order.Where(Parts.ContainsKey).Select(k => $"{k} {Parts[k].sum / Math.Max(1, Parts[k].n):0.0}/{Parts[k].max:0.0}");
        // La basura: cuántas recolecciones hubo (las de nivel 1 y 2 frenan todo, también el sonido) y cuánto se pidió.
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        long bytes = GC.GetTotalAllocatedBytes();
        string gc = $"  basura: {g0 - _g0}/{g1 - _g1}/{g2 - _g2} recolecciones, {(bytes - _bytes) / (1024 * 1024.0):0.0} MB";
        (_g0, _g1, _g2, _bytes) = (g0, g1, g2, bytes);
        string alloc = Alloc.Count > 0 ? "  (" + string.Join(", ", Alloc.OrderByDescending(a => a.Value).Take(6).Select(a => $"{a.Key} {a.Value / (1024 * 1024.0):0.0} MB")) + ")" : "";
        string s = string.Join("  ", parts) + " ms (promedio/peor)" + gc + alloc;
        Parts.Clear();
        Alloc.Clear();
        return s;
    }

    private static int _g0, _g1, _g2;
    private static long _bytes;

    public readonly struct Span : IDisposable
    {
        private readonly string _part;
        private readonly long _t0, _b0;
        public Span(string part, long t0, long b0) { _part = part; _t0 = t0; _b0 = b0; }
        public void Dispose()
        {
            Add(_part, Stopwatch.GetElapsedTime(_t0).TotalMilliseconds);
            AddAlloc(_part, GC.GetAllocatedBytesForCurrentThread() - _b0);
        }
    }
}
