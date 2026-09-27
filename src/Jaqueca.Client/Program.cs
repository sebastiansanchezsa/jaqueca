using Jaqueca.Client;

try
{
    using var game = new JaquecaGame(Options.Parse(args));
    game.Run();
}
catch (Exception e)
{
    // Si el juego se cae, deja el detalle en crash.log (en la carpeta del proyecto si se corre desde el código).
    string path = Path.Combine(JaquecaGame.FindRoot(), "crash.log");
    try { File.WriteAllText(path, $"{DateTime.Now}\n{e}\n"); }
    catch (Exception) { /* sin dónde escribir: el error igual sale por la consola */ }
    Console.Error.WriteLine($"El juego se cerró por un error. Detalle en: {path}");
    throw;
}
