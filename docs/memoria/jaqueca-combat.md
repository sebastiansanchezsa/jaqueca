---
name: jaqueca-combat
description: "Jaqueca: movimiento de Ernesto, armas, pensamientos (IA), gore, sangre que cura, estilo, oleadas y el piloto automático de prueba"
---

**Unidades:** las del motor (un hombre mide 16 ≈ 1,80 m). Ernesto: cilindro de radio 3,2 y alto 14,5 (7 deslizándose),
ojos a 13.

**Movimiento (`Game/Player.cs`)**: correr 100, salto 118 (llega a ~21 de alto: alcanza sillas a 16 y, desde una
silla, la mesa a 30), gravedad 330 (y más si suelta el salto), dash 290 durante 0,16 s (3 cargas, 1,1 s cada una,
invulnerable), deslizarse a 175 sin perder velocidad (saltar deslizándose sale largo), golpe al piso a 640 (onda que
empuja y lastima; saltar justo al caer suma hasta 90), salto en pared (3 antes de tocar el piso), coyote y buffer de
salto de 0,12 s. Las pruebas de `tests/MovementTests.cs` lo cubren.

**Armas (`Screens/PlayScreen.Combat.cs`)**: revólver 1 de daño (×3 a la cabeza), cada 0,36 s; el cargado (0,75 s)
hace 3,5 y atraviesa; escopeta 12 perdigones de 0,55 (más de cerca), cada 0,95 s, empuja; patada 1,2 de daño, empuja
lejos, devuelve tizas (hitstop de 0,1 s, cura 15). Los tiros pegan en cápsulas por hueso; los miembros se cortan con
`Animator.Sever` (el pedazo es un `Piece`), a la cabeza con el revólver la cabeza revienta. Los cuerpos quedan y se
pueden seguir despedazando.

**Pensamientos (`Game/Foe*.cs`)**: el Animator del motor mueve el cuerpo; la simulación es propia (cilindro, gravedad,
separación). Vecino: 5 de vida, persigue a 60, se prepara 0,45 s (clip Stab1 en cámara lenta, el taladro acelera),
embiste a 185 durante 0,3 s (22 de daño), salta a los muebles si Ernesto está parado arriba. Maestra: 3 de vida, guarda
distancia girando, chista y tira una tiza cada ~2-3 s apuntando adelante de Ernesto (12 de daño). Oleadas: 3 vecinos;
2+2; 4+3; y crece.

**Sangre (`PlayScreen.Blood.cs`)**: gotas con física (rayitas estiradas en el aire) que dejan manchas irregulares
(`DecalRing`, tope 1800); los muñones chorrean. Cura: la sangre de un golpe cerca de Ernesto (a menos de 30) y las gotas
que le llegan a la cara.

**Estilo**: puntos por acción, baja solo más rápido cuanto más alto; recibir un golpe baja 60.

**Pruebas sin jugar:** `--autojuego --inmortal` (apunta, cambia de arma por distancia, patea tizas, salta, hace dash)
con `--seq`/`--shottime` para capturas; `--debug` imprime el estado de cada pensamiento dos veces por segundo.
