namespace Jaqueca.Audio;

/// <summary>
/// Todos los sonidos del juego (cada uno con varias variantes: nunca suenan dos veces igual). Lo
/// nuevo va al final: hay sonidos que se guardan por número.
/// </summary>
public enum Sound
{
    // ---- Ernesto (el que juega)
    /// <summary>Un paso en el parquet del living: el taco, la madera que resuena abajo.</summary>
    StepWood,
    /// <summary>Cae de un salto.</summary>
    Land,
    /// <summary>Salta: el envión (aire y ropa).</summary>
    Jump,
    /// <summary>El dash: una ráfaga de aire que pasa.</summary>
    Dash,
    /// <summary>Se desliza por el piso (en bucle mientras dura).</summary>
    Slide,
    /// <summary>Se tira de cabeza contra el piso desde el aire: el golpe que hace saltar todo.</summary>
    Slam,
    /// <summary>Se impulsa en una pared.</summary>
    WallJump,
    /// <summary>Le pegan.</summary>
    Hurt,
    /// <summary>Se cura con la sangre de otro (un sorbo, un brillo).</summary>
    Heal,

    // ---- armas
    /// <summary>El revólver de cebita: el martillo, el estallido seco y el golpe del aire.</summary>
    Revolver,
    /// <summary>El revólver cargando el tiro que atraviesa (un zumbido que sube).</summary>
    RevolverCharge,
    /// <summary>El tiro que atraviesa: más grande, con un chispazo.</summary>
    RevolverPierce,
    /// <summary>La escopeta: el estampido grave y largo.</summary>
    Shotgun,
    /// <summary>La corredera de la escopeta: dos golpes de metal.</summary>
    ShotgunPump,
    /// <summary>La patada: el aire que mueve la pierna.</summary>
    Kick,
    /// <summary>La patada que pega: la suela contra el cuerpo.</summary>
    KickHit,
    /// <summary>Devolvió algo de una patada (un tiro, una tiza): un tañido brillante.</summary>
    Parry,
    /// <summary>Cambia de arma.</summary>
    Switch,
    /// <summary>La bala contra algo duro (pared, mueble).</summary>
    Ricochet,

    // ---- la carne
    HitFlesh, HitBlunt, HitHeavy,
    /// <summary>Un miembro que se va: el hueso que se quiebra y lo mojado.</summary>
    Dismember,
    /// <summary>La cabeza que revienta.</summary>
    Headshot,
    BloodSpray, BloodDrip,
    BodyFall, GibLand,

    // ---- los pensamientos
    /// <summary>Aparece un pensamiento: el aire que se chupa hacia un punto y revienta.</summary>
    Spawn,
    /// <summary>El vecino: un gruñido grave (no habla).</summary>
    NeighborGrunt,
    /// <summary>El taladro del vecino (en bucle): el motor que zumba.</summary>
    Drill,
    /// <summary>El taladro que entra en la carne.</summary>
    DrillHit,
    /// <summary>La maestra que chista (un "shh" largo, no habla): va a tirar.</summary>
    Shush,
    /// <summary>La tiza que sale volando.</summary>
    ChalkThrow,
    /// <summary>La tiza que se rompe contra algo.</summary>
    ChalkBreak,
    /// <summary>Un pensamiento que muere: se le va el aire.</summary>
    Groan,

    // ---- el lugar
    /// <summary>El reloj de la abuela: el tic y el tac.</summary>
    Clock,
    /// <summary>La heladera y el televisor prendido (en bucle).</summary>
    RoomTone,
    /// <summary>Sube el estilo: dos notas.</summary>
    StyleUp,
    /// <summary>Termina la oleada: una campana grave.</summary>
    WaveClear,
}
