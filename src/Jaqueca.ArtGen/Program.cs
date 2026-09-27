using System.Diagnostics;
using Jaqueca.ArtGen;

// Generador de arte de Jaqueca: las hojas de revisión de los modelos (screenshots/vecino.png,
// maestra.png, armas.png), rasterizados en pixel art con el motor de las figuras.
// Uso: dotnet run --project src/Jaqueca.ArtGen -c Release   (o arte.bat)

var sw = Stopwatch.StartNew();
string root = FindRoot();
Sheets.Write(Path.Combine(root, "screenshots"));
Console.WriteLine($"Listo en {sw.Elapsed.TotalSeconds:0.0} s");

static string FindRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d != null && !File.Exists(Path.Combine(d.FullName, "Jaqueca.sln"))) d = d.Parent;
    return d?.FullName ?? Directory.GetCurrentDirectory();
}
