---
name: jaqueca-engine
description: "Cómo se pasó el motor de Inquisition a primera persona: qué se copió, render en perspectiva, figuras como mallas skinneadas, colisiones 3D, lo que se ve en la mano, y cómo compilar shaders en la nube"
---

**Qué se copió de Inquisitio (2026-09-27):** Core (Anim, Look, Mathx), Sprites, Figures (modelo, rig, Animator,
física de muñecos y pedazos, Body/Face/Outfits/Gore/WeaponModels base), Audio (Mixer, Synth, Dsp, Vocal y las
recetas del cuerpo), ArtGen (Canvas, Png). Se sacó lo propio de Inquisitio (ítems, códice, cerebros, jefes,
personajes, recetas de sonido de cada nivel). Namespaces `Jaqueca.*`. Cambios al motor copiado:
- `VerletBody.Gravity` 170 → 300 (en primera persona los muñecos flotaban).
- `Mixer`: paneo según `ListenerRight/ListenerForward` (los oídos de la cámara), no la pantalla.
- `Look/Appearance.cs` trae el enum `Outfit` (estaba en Classes.cs, que se sacó).

**Render (src/Jaqueca.Client/Render):**
- `FpsCamera`: perspectiva, yaw 0 = este y π/2 = sur (la convención del motor), FOV vertical 72.
- `Renderer`: sombra de la "luz grande" (2048, ortográfica, encajada a texels) → normal/profundidad (para
  contornos) → escena a 640×360 (cielo de carne, estático, calcomanías, figuras, partículas, brillos) → lo de la
  mano encima (profundidad limpia, proyección de 58°) → `PostFx` (contornos en perspectiva: el umbral de
  silueta crece con la distancia, `EdgeSlope`; bloom; borde rojo del golpe y aberración cromática).
- `World.fx`: la de Inquisition más materiales de la casa (Parquet 16, Wallpaper 17, Carpet 18, Upholstery 19,
  Azulejo 20, Flesh 21, Oilcloth 22, Static 23), niebla en escalones, técnica Sky y técnicas Figure*.
- **Figuras en la GPU (`FigureMesh`)**: cada Figure del motor se tesela una vez (elipsoides, conos redondeados
  con normales exactas, cajas, triángulos de tela quietos); cada vértice queda en el espacio de su hueso con el
  índice del hueso en `Color.R`. El vertex shader usa `Bones[24]`. Máscara y pintura por triángulo, textura del
  material (tono que corre) por vértice, estampados (ojos) como cuadraditos de tono fijo. Lo cortado por la
  `RenderMask` se achica a un punto; muñones y arma son tramos aparte de índices. `FigurePalette`: una fila de
  rampa (5 tonos con `Ramp.From`) por material, como los personajes de Inquisition.
- Aparecer: la matriz del mundo se escala (crece desde el piso).

**Colisiones (`World/Solids.cs`):** cajas y rampas en una grilla de 24. Los cuerpos son cilindros parados:
`Move` desliza de a pasos, sube escalones, pisa tapas, choca la cabeza. `Raycast` para tiros. Para los muñecos
del motor: `FloorBelow(x, z, y)` (el piso debajo de una altura de referencia: sin eso un cuerpo bajo la mesa
aparecía arriba) y `Walls` (saca de costado lo que se metió en un mueble).

**Lo que se ve en la mano (`Game/ViewModel.cs`):** las piezas de `Content/Ernesto.cs` cuelgan de huesos que son
lugares (HandR el arma, HandL la corredera, FootR la pierna). Se acomodaron mirando capturas (`--vm` y
`--pierna` sirven para probar): el signo del giro importa (con el arma apuntando para afuera se la ve de atrás).
La patada: la punta del pie para arriba, si no el tobillo tapa la pantufla.

**Shaders en la nube:** el MGFXC de MonoGame en Linux pide Wine + .NET de Windows adentro. `tools/nube/`
lo evita: un `wine64` falso compila con `fxc2.exe` (mingw) que llama a D3DCompile de `D3DCompiler_47_cor3.dll`
(del paquete NuGet Microsoft.WindowsDesktop.App.Runtime.win-x64). builds.dotnet.microsoft.com está bloqueado
en la nube; el SDK sale de apt (dotnet-sdk-8.0). Con Xvfb el juego corre y saca capturas.
