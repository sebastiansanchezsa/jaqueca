using System.Globalization;
using Jaqueca.Client.Engine;
using Jaqueca.Client.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Jaqueca.Client;

/// <summary>Opciones de línea de comandos (para probar y para capturas automáticas).</summary>
public sealed class Options
{
    public int Seed = 7;
    /// <summary>Segundos hasta guardar una captura y salir (0 = no).</summary>
    public float ShotTime;
    public string ShotName;
    public bool Debug;
    /// <summary>Cuadros a guardar a 60 fps fijos (0 = no), desde qué segundo y uno de cada cuántos.</summary>
    public int Seq;
    public float SeqStart = 0.5f;
    public int SeqEvery = 1;
    /// <summary>Sin enemigos (para recorrer el lugar tranquilo).</summary>
    public bool NoEnemies;
    /// <summary>Prueba (--inmortal): la vida se llena sola en cada cuadro.</summary>
    public bool Immortal;
    /// <summary>
    /// Prueba (--autojuego): Ernesto juega solo (apunta al pensamiento más cercano, dispara, patea lo que
    /// le tiran, salta y hace dash de a ratos). Para capturas y para ver si algo se traba sin jugar.
    /// </summary>
    public bool AutoPlay;
    /// <summary>Dónde arranca (x, z) y hacia dónde mira (grados; 0 = este, 90 = sur), para capturas.</summary>
    public System.Numerics.Vector2? At;
    public float? Look;
    /// <summary>Cuánto mira hacia arriba (grados, negativo hacia abajo), para capturas.</summary>
    public float? LookUp;
    /// <summary>Con qué arma arranca (--arma revolver|escopeta).</summary>
    public int Weapon;
    /// <summary>Prueba (--vecinos N, --maestras N): esos pensamientos aparecen delante al empezar (sin oleadas).</summary>
    public int TestNeighbors = -1, TestTeachers = -1;
    /// <summary>Prueba (--quietos): los pensamientos no se mueven ni atacan (para mirarlos).</summary>
    public bool Frozen;
    /// <summary>Sin sonido (--mudo).</summary>
    public bool Mute;

    public static Options Parse(string[] args)
    {
        var o = new Options();
        float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        for (int i = 0; i < args.Length; i++)
        {
            string next = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--seed": o.Seed = int.Parse(next); i++; break;
                case "--shottime": o.ShotTime = F(next); i++; break;
                case "--shotname": o.ShotName = next; i++; break;
                case "--debug": o.Debug = true; break;
                case "--seq": o.Seq = int.Parse(next); i++; break;
                case "--seqstart": o.SeqStart = F(next); i++; break;
                case "--seqevery": o.SeqEvery = Math.Max(1, int.Parse(next)); i++; break;
                case "--sinenemigos": o.NoEnemies = true; break;
                case "--inmortal": o.Immortal = true; break;
                case "--autojuego": o.AutoPlay = true; break;
                case "--at": { var xz = next.Split(','); o.At = new System.Numerics.Vector2(F(xz[0]), F(xz[1])); i++; break; }
                case "--mira": o.Look = F(next); i++; break;
                case "--arriba": o.LookUp = F(next); i++; break;
                case "--arma": o.Weapon = next is "escopeta" or "2" ? 1 : 0; i++; break;
                case "--vecinos": o.TestNeighbors = int.Parse(next); i++; break;
                case "--vecino": o.TestNeighbors = 1; break;
                case "--maestras": o.TestTeachers = int.Parse(next); i++; break;
                case "--maestra": o.TestTeachers = 1; break;
                case "--quietos": o.Frozen = true; break;
                case "--mudo": o.Mute = true; break;
                case "--perfgpu": Render.Renderer.GpuProfile = true; o.Debug = true; break;
                // Prueba: dónde va el arma en la mano (adelante,arriba,derecha,giro,cabeceo,rolido,escala).
                case "--vm":
                {
                    var v = next.Split(',').Select(F).ToArray();
                    var at = new Microsoft.Xna.Framework.Vector3(v[0], v[1], v[2]);
                    var turn = new Microsoft.Xna.Framework.Vector3(v[3], v[4], v[5]);
                    if (o.Weapon == 0) { Game.ViewModel.RevolverAt = at; Game.ViewModel.RevolverTurn = turn; Game.ViewModel.RevolverScale = v[6]; }
                    else { Game.ViewModel.ShotgunAt = at; Game.ViewModel.ShotgunTurn = turn; Game.ViewModel.ShotgunScale = v[6]; }
                    i++;
                    break;
                }
            }
        }
        return o;
    }

    /// <summary>¿Se pidieron pensamientos de prueba (en vez de las oleadas)?</summary>
    public bool TestFoes => TestNeighbors >= 0 || TestTeachers >= 0;
}

public interface IScreen
{
    void Update(float dt);
    void Draw(SpriteBatch sb, RenderTarget2D target, Rectangle dest, int scale);
}

/// <summary>
/// Ventana y bucle del juego (el mismo esquema que Inquisition y Kill Kill Again): el mundo se dibuja
/// a 640×360 y se escala a un múltiplo entero de la ventana; F11 pantalla completa; --shottime
/// guarda una captura de 1920×1080 en screenshots/ y cierra.
/// </summary>
public sealed class JaquecaGame : Microsoft.Xna.Framework.Game
{
    public const int LowW = 640, LowH = 360;

    public readonly GraphicsDeviceManager Gdm;
    public readonly InputState Input = new();
    public readonly Options Options;
    public SpriteBatch Sb { get; private set; }
    public float Fps { get; private set; }

    private IScreen _screen;
    private Rectangle _dest;
    private float _clock, _fpsTimer;
    private int _fpsCount;
    private bool _shotTaken;
    private int _seqIndex, _seqFrame;

    public JaquecaGame(Options options)
    {
        Options = options;
        Gdm = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = LowW * 2,
            PreferredBackBufferHeight = LowH * 2,
            SynchronizeWithVerticalRetrace = true,
            GraphicsProfile = GraphicsProfile.HiDef,
            HardwareModeSwitch = false,
        };
        IsFixedTimeStep = false;
        IsMouseVisible = false;
        Window.AllowUserResizing = true;
        Window.Title = "Jaqueca";
        Content.RootDirectory = "Content";
    }

    /// <summary>Segundos desde que arrancó (el reloj del juego: con --seq va a 60 fps fijos).</summary>
    public float Clock => _clock;

    protected override void Initialize()
    {
        var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        int scale = Math.Max(1, Math.Min((int)(mode.Width * 0.85f) / LowW, (int)(mode.Height * 0.85f) / LowH));
        Gdm.PreferredBackBufferWidth = LowW * scale;
        Gdm.PreferredBackBufferHeight = LowH * scale;
        Gdm.ApplyChanges();
        base.Initialize();
    }

    /// <summary>El sonido (3D, mezclado por software) y los efectos.</summary>
    public Audio.SoundOut Sound { get; private set; }
    public Audio.Sfx Sfx { get; private set; }

    protected override void LoadContent()
    {
        Sb = new SpriteBatch(GraphicsDevice);
        Audio.Sounds.Warm();
        Sound = new Audio.SoundOut();
        Sound.Mixer.Master = Options.Mute ? 0 : 0.9f;
        Sfx = new Audio.Sfx(Sound.Mixer);
        _screen = new PlayScreen(this);
    }

    private readonly System.Diagnostics.Stopwatch _frameClock = System.Diagnostics.Stopwatch.StartNew();

    protected override void Update(GameTime gameTime)
    {
        // Si la sincronía vertical no frena, no pasar de ~240 cuadros por segundo (la cola del driver se
        // llena y cada tanto traba el juego y el sonido).
        if (Options.Seq <= 0 && _frameClock.Elapsed.TotalSeconds < 1 / 240.0) Thread.Sleep(1);
        _frameClock.Restart();
        float dt = MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1 / 20f);
        // Capturas: paso fijo de 60 fps, así guardar los PNG no altera lo que pasa.
        if (Options.Seq > 0 || Options.ShotTime > 0) dt = 1 / 60f;
        Input.Update(IsActive, Window);
        if (Input.Pressed(Keys.F11) || (Input.Pressed(Keys.Enter) && Input.Down(Keys.LeftAlt))) ToggleFullscreen();
        if (Input.Pressed(Keys.F5)) Options.Debug = !Options.Debug;

        _fpsTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _fpsCount++;
        if (_fpsTimer >= 0.5f) { Fps = _fpsCount / _fpsTimer; _fpsTimer = 0; _fpsCount = 0; }
        _clock += dt;
        using (Perf.Time("update")) _screen?.Update(dt);
        Sfx.Update(dt);
        Sound.Update(dt);
        base.Update(gameTime);
    }

    protected override void EndDraw()
    {
        using (Perf.Time("present")) base.EndDraw();
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        Sound?.Dispose();
        base.OnExiting(sender, args);
    }

    private void ToggleFullscreen()
    {
        if (!Gdm.IsFullScreen)
        {
            var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            Gdm.PreferredBackBufferWidth = mode.Width;
            Gdm.PreferredBackBufferHeight = mode.Height;
            Gdm.IsFullScreen = true;
        }
        else
        {
            Gdm.IsFullScreen = false;
            Gdm.PreferredBackBufferWidth = LowW * 2;
            Gdm.PreferredBackBufferHeight = LowH * 2;
        }
        Gdm.ApplyChanges();
    }

    protected override void Draw(GameTime gameTime)
    {
        var pp = GraphicsDevice.PresentationParameters;
        int scale = Math.Max(1, Math.Min(pp.BackBufferWidth / LowW, pp.BackBufferHeight / LowH));
        int w = LowW * scale, h = LowH * scale;
        _dest = new Rectangle((pp.BackBufferWidth - w) / 2, (pp.BackBufferHeight - h) / 2, w, h);
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        using (Perf.Time("draw")) _screen?.Draw(Sb, null, _dest, scale);

        if (Options.Seq > 0 && _clock >= Options.SeqStart)
        {
            string dir = Path.Combine(FindRoot(), "screenshots", "seq", Path.GetFileName(Options.ShotName ?? ""));
            if (_seqIndex == 0)
            {
                Directory.CreateDirectory(dir);
                foreach (var f in Directory.GetFiles(dir, "seq_*.png")) File.Delete(f);
            }
            if (_seqFrame++ % Options.SeqEvery == 0)
            {
                SaveShot(Path.Combine(dir, $"seq_{_seqIndex:000}.png"), 1);
                if (++_seqIndex >= Options.Seq) Exit();
            }
        }
        if (Options.ShotTime > 0 && _clock >= Options.ShotTime && !_shotTaken)
        {
            _shotTaken = true;
            SaveShot();
            Exit();
        }
        base.Draw(gameTime);
    }

    /// <summary>Dibuja la pantalla en 1920×1080 (escala ×3) y la guarda en screenshots/.</summary>
    public void SaveShot()
    {
        string dir = Path.Combine(FindRoot(), "screenshots");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, (Options.ShotName ?? $"shot_{DateTime.Now:HHmmss}") + ".png");
        SaveShot(path, 3);
        Console.WriteLine("Captura: " + path);
    }

    private void SaveShot(string path, int scale)
    {
        int w = LowW * scale, h = LowH * scale;
        using var shot = new RenderTarget2D(GraphicsDevice, w, h);
        _screen.Draw(Sb, shot, new Rectangle(0, 0, w, h), scale);
        GraphicsDevice.SetRenderTarget(null);
        using var fs = File.Create(path);
        shot.SaveAsPng(fs, w, h);
    }

    public static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Jaqueca.sln"))) dir = dir.Parent;
        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
