using Jaqueca.Look;

namespace Jaqueca.Anim;

public enum ClipId : byte
{
    // Base: todas las capas.
    Idle, Run, Dash, Hurt, DownedFall, Downed, Revive, ReviveUp, Death, Interact,
    // Sin arma.
    Punch1, Punch2, Kick,
    // Espada, hacha, maza.
    Slash1, Slash2, Slash3,
    // Mandoble, martillo.
    Heavy1, Heavy2,
    // Lanza.
    Thrust1, Thrust2, Sweep,
    // Dagas.
    Stab1, Stab2, Flurry,
    // Arco.
    BowDraw, BowHold, BowRelease,
    // Ballesta de mano.
    CrossbowShoot,
    // Báculo.
    StaffChannel, StaffRelease, StaffBash,
    // Escudo.
    Block, BlockHold, ShieldBash,
    // Tomo, orbe.
    Cast,
    // Enemigos: el tajo del goblin (carga larga, para verlo venir).
    GoblinChop,
    // Sin arma: el gancho que cierra el combo de puños (va al final: los ids horneados no se corren).
    Hook,
    // El familiar: un tajo bajo y rápido, y el salto con estocada.
    FamiliarSlash, FamiliarLunge,
    // El carcelero: el mazazo con el brazo encadenado y el tiro de la cadena con el garfio.
    JailerSmash, JailerThrow,
    // El penitente suelto: zarpazos con una mano y con la otra, y el salto encima.
    PenitentSwipe1, PenitentSwipe2, PenitentPounce,
    // El flagelante: el latigazo a su presa y el que se da en la espalda.
    FlagellantLash, FlagellantScourge,
    // El alcaide: el mazazo con los dos puños, el barrido de la cadena, el tiro del garfio, el tirón
    // de las cadenas del techo (mueve la cárcel: celdas, rejas, jaulas) y, suelto, la embestida.
    WardenSlam, WardenSweep, WardenThrow, WardenHaul, WardenCharge,
    // El cayado de la curandera: dos barridos con las dos manos (el de arriba es StaffBash).
    StaffSweep1, StaffSweep2,
    // El cetro de la ocultista: la descarga, con una mano y con la otra de revés.
    ScepterCast1, ScepterCast2,
    // Las cloacas (Nivel 2). El ahogado: el golpe con los dos brazos y el vómito de agua negra.
    DrownedSlam, DrownedVomit,
    // La plañidera: el zarpazo y el lamento.
    MournerClaw, MournerWail,
    // El cenagoso: el mazazo de barro y tragarse a alguien.
    MireSlam, MireEngulf,
    // Los Jardines Flotantes: yacente, caballero, polilla y estatua.
    MossSwipe, MossGrasp,
    KnightSlash, KnightThrust, KnightRoot,
    MothSting, StatueSmash,
    // El Jardinero Ciego: barrido, golpe al suelo, salto corto y tijeretazo.
    GardenerSweep, GardenerSlam, GardenerLeap, GardenerShear,
    // El Jardinero clava las tijeras como una pala (revientan raíces) e invoca enredaderas con las tijeras al cielo.
    GardenerStab, GardenerCall,
    // El caballero del jardín: el golpe de escudo; tomado por las raíces, el latigazo de raíz y el salto en cuatro patas.
    KnightBash, KnightWhip, KnightPounce,
    // La enredadera hambrienta: el latigazo del zarcillo, el lazo (agarra y arrastra) y la mordida del pimpollo.
    VineLash, VineSnare, VineBite,
    // El Jardinero unido al jardín (la tercera fase), de pie con los brazos de raíz: el latigazo del brazo, el
    // agarre (hunde la mano y la raíz va por debajo de la tierra) y el golpe con los dos puños (el anillo de púas).
    GardenerWhip, GardenerGrasp, GardenerQuake,
    // El Reino Vegetal (Nivel 4). El yuyo: el mordisco y el salto para treparse.
    YuyoBite, YuyoLeap,
    // El uro de corteza: la cornada, el pisotón (se para en dos patas) y escarbar antes de embestir.
    UroGore, UroStomp, UroPaw,
    // El acechador: el zarpazo y el salto desde el pasto.
    StalkerSwipe, StalkerPounce,
    // El espantajo: el tajo de la hoz, la siega (gira) y el llamado a los cuervos.
    ScarecrowSlash, ScarecrowReap, ScarecrowCall,
    // El retoño del Hambre Verde: el mordisco de la quijada de cuatro y la embestida.
    SproutBite, SproutCharge,
    // El Hambre Verde: el mordisco (agarra y mastica), escarbar antes de la embestida, el coletazo, el bramido
    // (parado en dos patas), la andanada de semillas y el golpe con las dos patas de adelante; el terrón (mete los
    // cuernos en la tierra y la tira por el aire) y la corona de espinas (se agacha temblando y se estira de golpe).
    HungerBite, HungerPaw, HungerTail, HungerRoar, HungerSpit, HungerSlam, HungerThrow, HungerRing,
    Count
}

/// <summary>
/// Tiempos y eventos de un clip. La simulación autoritativa usa estos datos (cuándo pega un
/// golpe, desde cuándo se puede cancelar) sin cargar nada de arte; el generador hornea
/// exactamente <see cref="Frames"/> frames por dirección.
/// </summary>
public sealed class ClipDef
{
    public ClipId Id;
    public WeaponFamily Family;
    /// <summary>Duración de cada frame en milisegundos: los frames clave se sostienen más.</summary>
    public int[] FrameMs;
    public bool Loop;
    /// <summary>Frames (inclusive) en los que el golpe está activo. -1 = no pega.</summary>
    public int HitFrom = -1, HitTo = -1;
    /// <summary>Desde este frame se puede encadenar otra acción. -1 = al terminar.</summary>
    public int CancelFrom = -1;
    /// <summary>Avance del personaje durante el clip (unidades de mundo); lo aplica la simulación.</summary>
    public float Advance;
    /// <summary>Desde qué frame avanza (-1 = desde la mitad de la carga hasta que empieza el golpe).</summary>
    public int AdvanceFrom = -1;

    public int Frames => FrameMs.Length;
    public int TotalMs => FrameMs.Sum();

    /// <summary>Segundos desde el inicio del clip hasta que empieza un frame.</summary>
    public float SecondsAt(int frame)
    {
        int ms = 0;
        for (int i = 0; i < Math.Min(frame, FrameMs.Length); i++) ms += FrameMs[i];
        return ms / 1000f;
    }

    /// <summary>Tiempos en segundos (para la animación en vivo y la simulación): duración, golpe activo y desde cuándo se encadena.</summary>
    public float Duration => TotalMs / 1000f;
    public float HitStart => HitFrom < 0 ? -1 : SecondsAt(HitFrom);
    public float HitEnd => HitTo < 0 ? -1 : SecondsAt(HitTo + 1);
    public float CancelAt => CancelFrom < 0 ? Duration : SecondsAt(CancelFrom);
    /// <summary>Segundos desde el inicio hasta que el cuerpo empieza a avanzar (ver <see cref="Advance"/>).</summary>
    public float AdvanceStart => AdvanceFrom >= 0 ? SecondsAt(AdvanceFrom) : HitStart * 0.5f;
    public string Name => ClipDefs.SnakeName(Id);

    /// <summary>Frame que corresponde a un tiempo (ms) desde el inicio del clip.</summary>
    public int FrameAt(float ms)
    {
        int total = TotalMs;
        if (Loop) ms = ((ms % total) + total) % total;
        else if (ms >= total) return Frames - 1;
        for (int i = 0; i < FrameMs.Length; i++)
        {
            if (ms < FrameMs[i]) return i;
            ms -= FrameMs[i];
        }
        return Frames - 1;
    }
}

public static class ClipDefs
{
    public static readonly ClipDef[] All = Build();

    public static ClipDef Get(ClipId id) => All[(int)id];

    /// <summary>El combo del ataque básico de una familia de arma (vacío = no se ataca así: el arco tensa y suelta).</summary>
    public static IReadOnlyList<ClipId> Combo(WeaponFamily f) => f switch
    {
        WeaponFamily.Blade => BladeCombo,
        WeaponFamily.None or WeaponFamily.Unarmed => FistCombo,
        WeaponFamily.Dagger => DaggerCombo,
        WeaponFamily.Staff => StaffCombo,
        WeaponFamily.Scepter => ScepterCombo,
        _ => Array.Empty<ClipId>(),
    };

    /// <summary>Armas que pegan a distancia (lo que lastima es lo que sale disparado, no el golpe).</summary>
    public static bool Ranged(WeaponFamily f) => f is WeaponFamily.Bow or WeaponFamily.Crossbow or WeaponFamily.Scepter;

    private static readonly ClipId[] BladeCombo = { ClipId.Slash1, ClipId.Slash2, ClipId.Slash3 };
    /// <summary>Las dos dagas: estocada de derecha, de izquierda y el cierre en cruz con las dos.</summary>
    private static readonly ClipId[] DaggerCombo = { ClipId.Stab1, ClipId.Stab2, ClipId.Flurry };
    /// <summary>El cayado: barrido, barrido de revés y el golpe de arriba contra el piso.</summary>
    private static readonly ClipId[] StaffCombo = { ClipId.StaffSweep1, ClipId.StaffSweep2, ClipId.StaffBash };
    /// <summary>El cetro: una descarga tras otra.</summary>
    private static readonly ClipId[] ScepterCombo = { ClipId.ScepterCast1, ClipId.ScepterCast2 };
    /// <summary>Con las manos vacías: jab de izquierda, cross de derecha y gancho de izquierda.</summary>
    private static readonly ClipId[] FistCombo = { ClipId.Punch1, ClipId.Punch2, ClipId.Hook };

    private static ClipDef[] Build()
    {
        var list = new ClipDef[(int)ClipId.Count];
        void Add(ClipId id, WeaponFamily fam, int[] ms, bool loop = false, int hitFrom = -1, int hitTo = -1, int cancel = -1, float advance = 0, int advanceFrom = -1)
            => list[(int)id] = new ClipDef { Id = id, Family = fam, FrameMs = ms, Loop = loop, HitFrom = hitFrom, HitTo = hitTo, CancelFrom = cancel, Advance = advance, AdvanceFrom = advanceFrom };

        const WeaponFamily B = WeaponFamily.None;
        Add(ClipId.Idle, B, new[] { 160, 140, 140, 160, 140, 140 }, loop: true);
        Add(ClipId.Run, B, new[] { 75, 75, 75, 75, 75, 75, 75, 75 }, loop: true);
        Add(ClipId.Dash, B, new[] { 45, 50, 60, 70, 90 }, cancel: 3, advance: 28);
        Add(ClipId.Hurt, B, new[] { 60, 90, 130 });
        Add(ClipId.DownedFall, B, new[] { 70, 70, 90, 110, 160 });
        Add(ClipId.Downed, B, new[] { 220, 180, 220, 180 }, loop: true);
        Add(ClipId.Revive, B, new[] { 150, 150, 150, 150 }, loop: true);
        Add(ClipId.ReviveUp, B, new[] { 90, 90, 100, 110, 150 });
        Add(ClipId.Death, B, new[] { 70, 70, 80, 90, 100, 120, 160, 400 });
        Add(ClipId.Interact, B, new[] { 70, 90, 110, 130 });

        const WeaponFamily U = WeaponFamily.Unarmed;
        // Los puños salen rápido y se encadenan apenas llegan; el gancho carga un poco más y empuja.
        Add(ClipId.Punch1, U, new[] { 70, 60, 90, 120 }, hitFrom: 1, hitTo: 1, cancel: 2, advance: 2);
        Add(ClipId.Punch2, U, new[] { 80, 60, 100, 130 }, hitFrom: 1, hitTo: 1, cancel: 2, advance: 3);
        Add(ClipId.Kick, U, new[] { 70, 60, 40, 50, 80, 110 }, hitFrom: 2, hitTo: 3, cancel: 4, advance: 5);
        Add(ClipId.Hook, U, new[] { 120, 70, 60, 120, 160 }, hitFrom: 1, hitTo: 2, cancel: 3, advance: 4);

        const WeaponFamily S = WeaponFamily.Blade;
        Add(ClipId.Slash1, S, new[] { 80, 45, 40, 60, 80, 100 }, hitFrom: 1, hitTo: 2, cancel: 3, advance: 6);
        Add(ClipId.Slash2, S, new[] { 80, 45, 40, 60, 80, 100 }, hitFrom: 1, hitTo: 2, cancel: 3, advance: 6);
        Add(ClipId.Slash3, S, new[] { 110, 90, 45, 40, 70, 90, 110, 150 }, hitFrom: 2, hitTo: 3, cancel: 6, advance: 10);

        const WeaponFamily H = WeaponFamily.Heavy;
        Add(ClipId.Heavy1, H, new[] { 90, 100, 120, 45, 40, 80, 120, 150 }, hitFrom: 3, hitTo: 4, cancel: 6, advance: 5);
        Add(ClipId.Heavy2, H, new[] { 100, 100, 120, 130, 45, 40, 60, 100, 130, 170 }, hitFrom: 4, hitTo: 6, cancel: 8, advance: 7);

        const WeaponFamily P = WeaponFamily.Polearm;
        Add(ClipId.Thrust1, P, new[] { 80, 60, 40, 60, 80, 100 }, hitFrom: 2, hitTo: 3, cancel: 4, advance: 5);
        Add(ClipId.Thrust2, P, new[] { 80, 60, 40, 60, 80, 100 }, hitFrom: 2, hitTo: 3, cancel: 4, advance: 5);
        Add(ClipId.Sweep, P, new[] { 90, 80, 45, 40, 45, 80, 110, 140 }, hitFrom: 2, hitTo: 4, cancel: 6, advance: 3);

        const WeaponFamily D = WeaponFamily.Dagger;
        Add(ClipId.Stab1, D, new[] { 45, 35, 55, 70 }, hitFrom: 1, hitTo: 1, cancel: 2, advance: 3);
        Add(ClipId.Stab2, D, new[] { 45, 35, 55, 70 }, hitFrom: 1, hitTo: 1, cancel: 2, advance: 3);
        Add(ClipId.Flurry, D, new[] { 40, 40, 40, 40, 40, 90 }, hitFrom: 1, hitTo: 4, cancel: 5, advance: 4);

        const WeaponFamily W = WeaponFamily.Bow;
        Add(ClipId.BowDraw, W, new[] { 70, 80, 90, 110 });
        Add(ClipId.BowHold, W, new[] { 220, 220 }, loop: true);
        Add(ClipId.BowRelease, W, new[] { 50, 90, 120 }, hitFrom: 0, hitTo: 0, cancel: 1);

        Add(ClipId.CrossbowShoot, WeaponFamily.Crossbow, new[] { 50, 70, 100, 130 }, hitFrom: 0, hitTo: 0, cancel: 2);

        const WeaponFamily T = WeaponFamily.Staff;
        Add(ClipId.StaffChannel, T, new[] { 120, 120, 120, 120 }, loop: true);
        Add(ClipId.StaffRelease, T, new[] { 60, 50, 80, 110, 140 }, hitFrom: 1, hitTo: 1, cancel: 3);
        Add(ClipId.StaffBash, T, new[] { 110, 90, 45, 45, 100, 130 }, hitFrom: 2, hitTo: 3, cancel: 4, advance: 4);
        // Los barridos cargan un poco (el cayado es largo) y barren ancho.
        Add(ClipId.StaffSweep1, T, new[] { 90, 60, 45, 45, 80, 110 }, hitFrom: 2, hitTo: 3, cancel: 4, advance: 4);
        Add(ClipId.StaffSweep2, T, new[] { 90, 60, 45, 45, 80, 110 }, hitFrom: 2, hitTo: 3, cancel: 4, advance: 4);

        // La descarga del cetro sale en el golpe (hitFrom) y se puede encadenar enseguida.
        Add(ClipId.ScepterCast1, WeaponFamily.Scepter, new[] { 80, 50, 90, 120 }, hitFrom: 1, hitTo: 1, cancel: 2);
        Add(ClipId.ScepterCast2, WeaponFamily.Scepter, new[] { 80, 50, 90, 120 }, hitFrom: 1, hitTo: 1, cancel: 2);

        const WeaponFamily Sh = WeaponFamily.Shield;
        Add(ClipId.Block, Sh, new[] { 45, 60, 80 });
        Add(ClipId.BlockHold, Sh, new[] { 220, 220 }, loop: true);
        Add(ClipId.ShieldBash, Sh, new[] { 70, 50, 60, 90, 120 }, hitFrom: 1, hitTo: 2, cancel: 3, advance: 6);

        Add(ClipId.Cast, WeaponFamily.Focus, new[] { 70, 60, 70, 100, 140 }, hitFrom: 2, hitTo: 2, cancel: 3);

        Add(ClipId.GoblinChop, S, new[] { 200, 170, 50, 40, 90, 120, 150 }, hitFrom: 2, hitTo: 3, cancel: 6, advance: 7);

        // El familiar es rápido, pero avisa: se encoge y abre la boca antes de cada golpe.
        // Tajo: se enrosca un cuarto de segundo y barre bajo de derecha a izquierda.
        Add(ClipId.FamiliarSlash, S, new[] { 140, 120, 45, 45, 80, 110 }, hitFrom: 2, hitTo: 3, cancel: 5, advance: 6);
        // Salto: se agacha, se enrosca, salta (el cuerpo vuela desde el frame 2) y clava el cuchillo al caer.
        Add(ClipId.FamiliarLunge, S, new[] { 170, 90, 60, 50, 60, 60, 110, 170 }, hitFrom: 3, hitTo: 5, cancel: 7, advance: 18, advanceFrom: 2);
        // El carcelero es lento y pesado: levanta el brazo de la cadena bien alto y lo deja caer.
        Add(ClipId.JailerSmash, B, new[] { 220, 220, 180, 60, 60, 150, 200, 200 }, hitFrom: 3, hitTo: 4, cancel: 7, advance: 4, advanceFrom: 2);
        // El tiro: revolea la cadena casi un segundo y la suelta al empezar el frame 3 (no pega: lo que pega es el garfio).
        Add(ClipId.JailerThrow, B, new[] { 300, 300, 300, 120, 200, 250 });

        // El penitente suelto no avisa casi nada: zarpazos rapidísimos que se encadenan, y el salto con los dos brazos.
        Add(ClipId.PenitentSwipe1, B, new[] { 90, 50, 40, 60, 90 }, hitFrom: 2, hitTo: 2, cancel: 3, advance: 5);
        Add(ClipId.PenitentSwipe2, B, new[] { 80, 50, 40, 60, 100 }, hitFrom: 2, hitTo: 2, cancel: 3, advance: 5);
        Add(ClipId.PenitentPounce, B, new[] { 140, 80, 60, 60, 60, 120, 180 }, hitFrom: 3, hitTo: 5, cancel: 6, advance: 20, advanceFrom: 2);
        // El flagelante: levanta la disciplina por encima del hombro y la descarga adelante.
        Add(ClipId.FlagellantLash, B, new[] { 160, 150, 50, 50, 110, 150 }, hitFrom: 2, hitTo: 3, cancel: 5, advance: 4);
        // Se azota: la disciplina va por encima del hombro y las colas le pegan en la espalda al empezar el frame 2 (no le pega a nadie más).
        Add(ClipId.FlagellantScourge, B, new[] { 200, 150, 60, 60, 150, 200 });

        // El alcaide es enorme y lento: todo lo avisa. El mazazo: sube los dos puños juntos por encima
        // de la cabeza casi un segundo y los deja caer contra el piso con todo el cuerpo.
        Add(ClipId.WardenSlam, B, new[] { 280, 280, 260, 140, 70, 90, 260, 260, 260 }, hitFrom: 4, hitTo: 5, cancel: 8);
        // El barrido: se enrosca con la cadena atrás y la pasa alrededor suyo, a ras del piso.
        Add(ClipId.WardenSweep, B, new[] { 240, 240, 220, 80, 80, 80, 80, 220, 260 }, hitFrom: 3, hitTo: 6, cancel: 8);
        // El garfio: revolea la cadena un segundo sobre la cabeza y la suelta al empezar el frame 3.
        Add(ClipId.WardenThrow, B, new[] { 340, 340, 320, 120, 260, 300 });
        // Las cadenas del techo: levanta los brazos, las agarra y tira hacia abajo con todo el peso (al empezar el frame 3).
        Add(ClipId.WardenHaul, B, new[] { 260, 260, 200, 160, 360, 280 });
        // Suelto, en cuatro patas: se agacha y embiste (corre 72 unidades en línea recta).
        Add(ClipId.WardenCharge, B, new[] { 260, 260, 140, 140, 140, 140, 140, 140, 280, 300 }, hitFrom: 2, hitTo: 7, cancel: 9, advance: 72, advanceFrom: 2);

        // El ahogado es lento y pesado: levanta los dos brazos hinchados y los deja caer.
        Add(ClipId.DrownedSlam, B, new[] { 220, 200, 170, 60, 60, 170, 220 }, hitFrom: 3, hitTo: 4, cancel: 6);
        // El vómito: se hincha, echa la cabeza atrás y lo larga hacia adelante (del frame 2 al 6: no pega el
        // golpe, pega el chorro; ver GameScene.Sewers).
        Add(ClipId.DrownedVomit, B, new[] { 260, 240, 130, 130, 130, 130, 130, 220, 220 });
        // La plañidera: un zarpazo rápido con las uñas (para que la dejen en paz) y el lamento: se yergue,
        // echa la cabeza atrás, abre los brazos y la boca, y grita al empezar el frame 3.
        Add(ClipId.MournerClaw, B, new[] { 120, 80, 45, 50, 130 }, hitFrom: 2, hitTo: 2, cancel: 3, advance: 4);
        Add(ClipId.MournerWail, B, new[] { 320, 320, 280, 160, 420, 420, 320 });
        // El cenagoso: levanta la maza de barro bien alto y la aplasta; tragar: se abre el pecho, se tira
        // con los dos brazos y agarra (en el golpe).
        Add(ClipId.MireSlam, B, new[] { 260, 240, 210, 70, 70, 200, 250, 250 }, hitFrom: 3, hitTo: 4, cancel: 7);
        Add(ClipId.MireEngulf, B, new[] { 240, 220, 140, 80, 80, 320, 260 }, hitFrom: 3, hitTo: 4, cancel: 6);

        // Bajo el musgo parecen tumbas: el movimiento arranca despacio, pero el agarre dura.
        Add(ClipId.MossSwipe, B, new[] { 180, 150, 70, 75, 140, 170 }, hitFrom: 2, hitTo: 3, cancel: 5, advance: 3);
        Add(ClipId.MossGrasp, B, new[] { 220, 180, 100, 110, 180, 210 }, hitFrom: 2, hitTo: 3, cancel: 5, advance: 2);
        // El hierro pesado se anuncia; tras la pérdida de vida las raíces pueden atacar desde el piso.
        Add(ClipId.KnightSlash, B, new[] { 170, 140, 65, 70, 120, 170 }, hitFrom: 2, hitTo: 3, cancel: 5, advance: 5);
        Add(ClipId.KnightThrust, B, new[] { 190, 140, 70, 75, 120, 170 }, hitFrom: 2, hitTo: 3, cancel: 5, advance: 7);
        Add(ClipId.KnightRoot, B, new[] { 220, 190, 150, 140, 180, 190 }, hitFrom: 3, hitTo: 3, cancel: 5);
        Add(ClipId.MothSting, B, new[] { 90, 70, 45, 75, 120 }, hitFrom: 2, hitTo: 2, cancel: 4, advance: 4);
        Add(ClipId.StatueSmash, B, new[] { 260, 220, 160, 75, 95, 240, 280 }, hitFrom: 3, hitTo: 4, cancel: 6, advance: 2);
        // El Jardinero es viejo y enorme: todo lo avisa. El corte horizontal: abre las tijeras y las lleva atrás
        // casi un segundo, y barre por delante. El vertical: las sube por encima de la cabeza y las clava.
        Add(ClipId.GardenerSweep, B, new[] { 300, 300, 240, 80, 90, 90, 260, 300, 300 }, hitFrom: 3, hitTo: 5, cancel: 8, advance: 5, advanceFrom: 2);
        Add(ClipId.GardenerSlam, B, new[] { 340, 330, 260, 80, 100, 380, 360, 320 }, hitFrom: 3, hitTo: 4, cancel: 7, advance: 4, advanceFrom: 2);
        // El salto: se agacha, sale (la cadera vuela desde el frame 2) y cae de punta.
        Add(ClipId.GardenerLeap, B, new[] { 320, 260, 170, 170, 110, 110, 320, 340 }, hitFrom: 4, hitTo: 5, cancel: 7, advance: 34, advanceFrom: 2);
        // El tijeretazo: las abre de par en par y espera (el aviso), y se tira adelante cerrándolas de golpe.
        Add(ClipId.GardenerShear, B, new[] { 320, 320, 300, 280, 70, 90, 300, 340 }, hitFrom: 4, hitTo: 5, cancel: 7, advance: 9, advanceFrom: 3);
        // Clavarlas como una pala: sube las manos y las hunde en el suelo (revientan raíces al empezar el frame 3).
        Add(ClipId.GardenerStab, B, new[] { 320, 300, 240, 90, 140, 420, 360 }, hitFrom: 3, hitTo: 4, cancel: 6);
        // Invocar: se endereza con las tijeras al cielo; las enredaderas salen al empezar el frame 3.
        Add(ClipId.GardenerCall, B, new[] { 360, 360, 300, 420, 420, 380 }, hitFrom: 3, hitTo: 4, cancel: 5);
        // El caballero: el golpe de escudo (corto, empuja) y, tomado por las raíces, el latigazo (lejos) y el salto.
        Add(ClipId.KnightBash, B, new[] { 160, 120, 60, 60, 140, 170 }, hitFrom: 2, hitTo: 3, cancel: 5, advance: 9, advanceFrom: 1);
        Add(ClipId.KnightWhip, B, new[] { 200, 170, 60, 60, 60, 160, 200 }, hitFrom: 2, hitTo: 4, cancel: 6, advance: 2);
        Add(ClipId.KnightPounce, B, new[] { 200, 120, 80, 80, 80, 90, 160, 200 }, hitFrom: 3, hitTo: 5, cancel: 7, advance: 26, advanceFrom: 2);
        // La enredadera: el latigazo avisa echándose atrás; el lazo se enrosca y sale disparado; la mordida se yergue y cae.
        Add(ClipId.VineLash, B, new[] { 240, 220, 70, 70, 70, 200, 240 }, hitFrom: 2, hitTo: 4, cancel: 6);
        Add(ClipId.VineSnare, B, new[] { 280, 240, 90, 180, 220, 240 }, hitFrom: 2, hitTo: 3, cancel: 5);
        Add(ClipId.VineBite, B, new[] { 320, 300, 90, 120, 280, 320 }, hitFrom: 2, hitTo: 3, cancel: 5);
        // El Jardinero de raíz. El latigazo: echa el brazo atrás (la raíz se enrosca) y lo descarga (la raíz sale
        // al empezar el frame 3; ver GameScene.Gardener). El agarre: sube la mano y la hunde en la tierra (la raíz
        // sale por debajo al empezar el frame 3). Los dos puños: los sube juntos y los clava (al empezar el frame 3).
        Add(ClipId.GardenerWhip, B, new[] { 240, 230, 200, 70, 90, 220, 240 }, hitFrom: 3, hitTo: 4, cancel: 5);
        Add(ClipId.GardenerGrasp, B, new[] { 230, 210, 150, 380, 260, 260 }, hitFrom: 3, hitTo: 3, cancel: 5);
        Add(ClipId.GardenerQuake, B, new[] { 280, 280, 240, 80, 120, 320, 300 }, hitFrom: 3, hitTo: 4, cancel: 5);
        // El yuyo: chico y rápido. El mordisco: se echa atrás y se tira con la semilla abierta. El salto:
        // se agacha y salta encima (en el golpe se prende: ver GameScene.WildFoes).
        Add(ClipId.YuyoBite, B, new[] { 110, 90, 45, 60, 110 }, hitFrom: 2, hitTo: 2, cancel: 4, advance: 5);
        Add(ClipId.YuyoLeap, B, new[] { 170, 130, 80, 80, 150 }, hitFrom: 2, hitTo: 3, cancel: 4, advance: 13, advanceFrom: 1);
        // El uro: la cornada baja la cabeza y la levanta de golpe; el pisotón se para en dos patas y cae con
        // todo el peso (en el golpe tumba a los de alrededor); escarbar avisa que va a embestir.
        Add(ClipId.UroGore, B, new[] { 230, 190, 80, 80, 210, 250 }, hitFrom: 2, hitTo: 3, cancel: 5, advance: 6);
        Add(ClipId.UroStomp, B, new[] { 300, 280, 230, 70, 90, 300, 320 }, hitFrom: 3, hitTo: 4, cancel: 6);
        Add(ClipId.UroPaw, B, new[] { 240, 240, 240, 240, 240, 240 });
        // El acechador: el zarpazo rápido y el salto largo desde el pasto (la cadera vuela desde el frame 2).
        Add(ClipId.StalkerSwipe, B, new[] { 120, 90, 45, 60, 130 }, hitFrom: 2, hitTo: 2, cancel: 4, advance: 4);
        Add(ClipId.StalkerPounce, B, new[] { 220, 110, 70, 70, 70, 120, 170 }, hitFrom: 3, hitTo: 5, cancel: 6, advance: 30, advanceFrom: 2);
        // El espantajo: el tajo sale de un salto; la siega gira dos vueltas con la hoz afuera; el llamado:
        // abre los brazos y grazna (los cuervos llegan al empezar el frame 2).
        Add(ClipId.ScarecrowSlash, B, new[] { 200, 160, 60, 70, 160, 200 }, hitFrom: 2, hitTo: 3, cancel: 5, advance: 7);
        Add(ClipId.ScarecrowReap, B, new[] { 300, 260, 120, 120, 120, 120, 220, 280 }, hitFrom: 2, hitTo: 5, cancel: 7);
        Add(ClipId.ScarecrowCall, B, new[] { 300, 300, 420, 420, 300 });
        // El retoño: el mordisco abre la quijada en cuatro y la cierra; la embestida baja la cabeza y arranca.
        Add(ClipId.SproutBite, B, new[] { 250, 210, 80, 90, 230, 260 }, hitFrom: 2, hitTo: 3, cancel: 5, advance: 8);
        Add(ClipId.SproutCharge, B, new[] { 300, 170, 90, 90, 90, 90, 210, 250 }, hitFrom: 2, hitTo: 5, cancel: 7, advance: 44, advanceFrom: 1);
        // El Hambre Verde. El mordisco: echa la cabeza atrás con la flor abierta y la tira adelante (agarra al
        // cerrarse: ver GameScene.Hunger). Escarbar: el aviso de la embestida (la embestida la hace la escena).
        // El coletazo: se tuerce y barre con la cola alrededor. El bramido: se para en dos patas y brama. La
        // andanada: levanta la cabeza, se le hincha el pecho y escupe semillas (al empezar el frame 3). El golpe:
        // se para en dos patas y cae con las de adelante (después abre el pecho, cansado).
        Add(ClipId.HungerBite, B, new[] { 320, 300, 140, 110, 300, 320 }, hitFrom: 2, hitTo: 3, cancel: 5, advance: 10, advanceFrom: 1);
        Add(ClipId.HungerPaw, B, new[] { 260, 260, 260, 260, 260 });
        Add(ClipId.HungerTail, B, new[] { 360, 300, 110, 110, 110, 300, 360 }, hitFrom: 2, hitTo: 4, cancel: 6);
        Add(ClipId.HungerRoar, B, new[] { 380, 420, 520, 520, 520, 420 });
        Add(ClipId.HungerSpit, B, new[] { 340, 340, 200, 200, 200, 300, 300 }, hitFrom: 3, hitTo: 4, cancel: 6);
        Add(ClipId.HungerSlam, B, new[] { 380, 360, 320, 90, 120, 420, 400 }, hitFrom: 3, hitTo: 4, cancel: 6);
        // El terrón: escarba con los cuernos (la tierra salta) y levanta la cabeza de un tirón (sale al empezar el
        // frame 3). La corona: se agacha, tiembla entero y se estira (la corona sale al empezar el frame 3).
        Add(ClipId.HungerThrow, B, new[] { 300, 300, 280, 90, 140, 320, 300 }, hitFrom: 3, hitTo: 4, cancel: 6);
        Add(ClipId.HungerRing, B, new[] { 320, 320, 320, 80, 140, 380, 320 }, hitFrom: 3, hitTo: 4, cancel: 6);

        foreach (var c in list)
            if (c == null) throw new InvalidOperationException("Falta definir un clip");
        return list;
    }

    /// <summary>
    /// Clips que hay que hornear para una capa. Las capas del cuerpo (pelo, ropa, tocado,
    /// espalda) se ven con cualquier arma: llevan todos. Un arma lleva los base, los de su
    /// familia y, si es de una mano, los de la mano secundaria (se ve mientras bloqueás o
    /// lanzás). Lo de la mano secundaria lleva todo lo que se puede usar a una mano.
    /// </summary>
    public static IEnumerable<ClipDef> ClipsFor(GearSlot slot, WeaponFamily family)
    {
        foreach (var c in All)
        {
            if (c.Family == WeaponFamily.None) { yield return c; continue; }
            if (slot == GearSlot.MainHand)
            {
                if (c.Family == family || (!family.TwoHanded() && c.Family.IsOffHand())) yield return c;
            }
            else if (slot == GearSlot.OffHand)
            {
                if (c.Family == family || (c.Family.IsMainHand() && !c.Family.TwoHanded())) yield return c;
            }
            else yield return c;
        }
    }

    public static string SnakeName(ClipId id)
    {
        var s = id.ToString();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i]) && !char.IsDigit(s[i - 1])) sb.Append('_');
            sb.Append(char.ToLowerInvariant(s[i]));
        }
        return sb.ToString();
    }
}
