# Jaqueca

Shooter en primera persona, rápido y sangriento (como ULTRAKILL), con el delirio de Postal Brain
Damage: Ernesto Bazán, 43 años, se durmió con una jaqueca de once días y se despertó adentro de su
propia cabeza, en pijama y con el revólver de cebita de cuando tenía siete, que ahora tira de verdad.
Los recuerdos están llenos de gente que le arruinó la vida.

Todo el arte y los sonidos se generan por código, con el motor de *Inquisition* (las figuras hechas de
primitivas colgadas de huesos, la animación procedural con los pies clavados al piso, los muñecos de
trapo y los cortes, la luz en bandas y los contornos de pixel art, el sonido sintetizado). Es un proyecto
aparte: tiene su propia copia del motor.

## Jugar

- En Windows: `jugar.bat` (compila en Release y abre el juego). Hace falta el SDK de .NET 8.
- `arte.bat`: las hojas de revisión de los modelos (`screenshots/vecino.png`, `maestra.png`, `armas.png`).

### Controles

| | |
|---|---|
| WASD | caminar |
| Espacio | saltar (también contra las paredes: hasta tres veces antes de tocar el piso) |
| Shift | dash (tres cargas que vuelven solas; mientras dura no te tocan) |
| Ctrl (o C) | deslizarse por el piso; en el aire, tirarse de cabeza contra el piso (saltá justo al caer y rebotás más alto) |
| Clic | tirar |
| Clic derecho | revólver: cargar el tiro que atraviesa a todos los de la línea (sale al soltar) |
| F (o V) | patada con la pantufla: empuja lejos y **devuelve lo que te tiran** (las tizas de la maestra) |
| 1, 2, rueda | revólver de cebita, escopeta del abuelo |
| [ y ] | sensibilidad del mouse |
| Esc | pausa (Q para salir) · F11 pantalla completa · F5 medición de cuadros |

La sangre de cerca cura: la pelea se gana metiéndose adentro. El estilo sube variando (a la cabeza, en
el aire, devolviendo tizas, de a varios) y baja solo; sus rangos van de MOLESTIA a MUERTE CEREBRAL.

### Opciones de prueba

`--sinintro`, `--sinenemigos`, `--inmortal`, `--autojuego` (juega solo), `--vecinos N`, `--maestras N`
(aparecen delante, sin oleadas), `--quietos`, `--arma escopeta`, `--at x,z`, `--mira grados`,
`--arriba grados`, `--shottime S --shotname nombre` (captura de 1920×1080 en `screenshots/`),
`--seq N --seqstart S --seqevery K` (cuadros sueltos), `--debug`, `--mudo`, `--patear`,
`--vm ...` y `--pierna ...` (para acomodar lo que se ve en la mano). La lista está en `Options.Parse`
(src/Jaqueca.Client/JaquecaGame.cs).

## Dónde está cada cosa

- `src/Jaqueca.Core`, `Jaqueca.Sprites`, `Jaqueca.Figures`, `Jaqueca.Audio`: el motor (copia del de
  Inquisition, sin lo propio de aquel juego). Los modelos de Jaqueca están en
  `Jaqueca.Figures/Content/Thoughts.cs` (los pensamientos) y `Ernesto.cs` (manos, armas, pierna).
  Los sonidos, en `Jaqueca.Audio/Sfx/Recipes*.cs`.
- `src/Jaqueca.Client`: el juego. `Render/` (cámara en primera persona, figuras como mallas animadas en
  la GPU, post), `World/` (colisiones y el living), `Game/` (Ernesto, los pensamientos, lo que tiene en la
  mano), `Screens/PlayScreen.*.cs` (la partida, repartida por tema).
- `src/Jaqueca.ArtGen`: las hojas de revisión.
- `tests/Jaqueca.Tests`: pruebas (`dotnet test tests/Jaqueca.Tests`).
- `tools/nube/`: para compilar con los shaders y sacar capturas en una máquina Linux sin pantalla.
- `docs/memoria/`: las notas de diseño y de cómo está hecho cada sistema.
