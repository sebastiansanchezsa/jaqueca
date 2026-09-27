# Jaqueca

Shooter en primera persona (ULTRAKILL + el delirio de Postal Brain Damage) adentro de la cabeza de Ernesto
Bazán. Proyecto aparte de *Inquisitio* (el otro juego del usuario): tiene su propia copia del motor de
Inquisition y **no se toca Inquisitio** desde acá. Todo el arte y los sonidos se generan por código con
ese motor (figuras de primitivas sobre huesos, animador procedural, muñecos de trapo, cortes, sonido
sintetizado). C# .NET 8 + MonoGame 3.8 DesktopGL, render a 640×360 escalado entero.

## Lo más importante al trabajar acá

- **El usuario escribe en castellano rioplatense** y el código está comentado igual (voseo, comentarios que
  explican el porqué con el vocabulario del juego). Los identificadores, en inglés.
- **Revisa por partes**: trabajá hasta un punto que se pueda mirar y pará para que lo vea. Lo visual se
  mira con capturas (en la nube también: ver abajo).
- **Rendimiento**: nada de basura por cuadro (LINQ, listas nuevas, lambdas que capturan) porque las pausas del
  GC cortan el sonido. Medí con `--debug` (`[cuadro]`: tiempos y MB de basura por segundo).
- **Reglas del juego**: los pensamientos (enemigos) no hablan (gruñen, chistan, hacen ruido); los cuerpos
  y pedazos quedan en el lugar y nunca atraviesan paredes ni vuelan al vacío.
- **Música**: la compone el usuario. No hay música todavía; no inventar temas ni usar los de Inquisitio.
- En los `enum` que se guardan por número (Sound, FoeKind, Materials) lo nuevo va **al final**.

## Compilar, probar, jugar

- `dotnet build src/Jaqueca.Client` · `dotnet test tests/Jaqueca.Tests`.
- `jugar.bat [opciones]` en Windows. Opciones de prueba en `README.md` y en `Options.Parse`.
- `arte.bat`: hojas de los modelos en `screenshots/`.
- **En la nube se puede compilar con shaders y sacar capturas**: `bash tools/nube/preparar.sh` una vez
  (instala .NET, Wine con el compilador de shaders de Microsoft y Xvfb), después
  `export PATH=/opt/mgfxwine/bin:$PATH MGFXC_WINE_PATH=/opt/mgfxwine/prefix` y
  `xvfb-run -a -s "-screen 0 1920x1080x24" dotnet run --project src/Jaqueca.Client -c Debug --no-build -- --shottime 2 --shotname algo`.
  La GPU ahí es por software (el "render" mide ~80 ms): los tiempos de la lógica sí valen.

## La memoria del proyecto (`docs/memoria/`)

Antes de tocar un tema, leé su nota; cuando aprendas algo que valga para otras sesiones, actualizala.

- [jaqueca-design](docs/memoria/jaqueca-design.md): la idea, la historia, el tono, lo que ya hay y lo que falta.
- [jaqueca-engine](docs/memoria/jaqueca-engine.md): cómo se pasó el motor a primera persona (render, figuras en la GPU, colisiones).
- [jaqueca-combat](docs/memoria/jaqueca-combat.md): movimiento, armas, pensamientos, sangre, estilo.
