# Pez ciego en esc2

El objeto **PezCiegoEsc2** está en `(0, -6, 40)`, con su cuerpo y patrulla
cerca de `Z=42`. Está separado de lampreas, pez linterna y paso del tiburón.
Se usa una esfera alargada: el color del cuerpo sustituye provisionalmente
los sonidos 3D del GDD. No hay barra de inquietud en pantalla ni audio nuevo.

## Estados

| Estado | Color | Respuesta |
| --- | --- | --- |
| CHILL | Azul grisáceo | Patrulla; inquietud en cero. |
| PERTURBED | Amarillo a naranja | Aumenta inquietud al detectar nado rápido cerca. |
| CRITICAL | Rojo pulsante | Recuerda tu posición y se aproxima; tienes 2 s para detenerte. |
| INSPECTING | Violeta pulsante | Se sitúa delante y permanece 3 s. Mantén la quietud. |
| ATTACK | Rojo | Tras romper la gracia/inspección, persigue y agota la energía al contacto. |
| RETREAT | Azul grisáceo | Inquietud a cero, vuelve a su patrulla con un mínimo de 3 s sin detectar. |

Detección: 6 m con camino libre de paredes. El nado rápido añade 25 puntos/s:
cuatro segundos continuos para llegar a 100. La quietud o alejarse restan
12,5 puntos/s. Nadar despacio cerca mantiene la inquietud que ya tenga.

La inspección toma una sola vez la dirección de la cámara y busca un lugar
libre a 1,5 m del jugador. Si hay roca delante, busca otra posición próxima
segura; no atraviesa paredes ni orbita al girar la cabeza. Los tres segundos
empiezan al llegar a ese punto. Si queda bloqueado en la aproximación, se
retira tras 6 s para no bloquear el encuentro indefinidamente.

Al terminar la gracia en movimiento, o moverse durante la inspección, el
ataque queda confirmado: detenerse tarde no lo cancela. El contacto consume
toda la energía vital; R restaura jugador, pez e inquietud. Fuera del alcance
territorial de 12 m, o tras perder el camino al jugador durante 2 s, se retira.

## Teclado de depuración

- WASD/QE sin Shift: nado silencioso.
- Shift con movimiento y desplazamiento real: nado ruidoso, aunque energía
  baja o lampreas hayan reducido la velocidad.
- Shift estando inmóvil: no genera ruido.
- Durante alerta e inspección: soltar todas las teclas de movimiento.
  Soltar solo Shift no basta. Se permite mirar con el ratón.
- La quietud debe mantenerse 0,2 s antes de aceptarse; evita alternancias
  entre movimiento y quietud por un solo frame.

## VR

`BlindFishMotionSensor` mide la velocidad del Rigidbody para detectar nado
rápido (umbral inicial 1,5 m/s). Para quietud combina velocidad corporal y
poses locales del visor y ambos mandos, incluyendo rotación. Las poses locales
evitan contar dos veces el avance del rig.

Hay filtrado de 0,1 s y umbrales ajustables: cuerpo 0,06 m/s, visor 0,04 m/s,
manos 0,08 m/s; rotación de visor 8 grados/s y mandos 15 grados/s.
Si se pierde seguimiento, se pausa la evaluación del enemigo hasta recuperarlo.
La deriva real por corriente cuenta como movimiento. No se altera el tracking.
Estos valores requieren calibración y prueba con visor; la verificación local
automatizada cubre el control de escritorio.

## Referencias y duplicación

`Esc2BlindFish` tiene referencias explícitas a `Target = KM_Player`,
`Motion = BlindFishMotionSensor` del jugador, cuerpo, renderer y puntos de
patrulla. No depende de pertenecer al cardumen de pirañas.

Para duplicar: fuera de Play, Ctrl+D sobre **PezCiegoEsc2** y mueve el conjunto.
Comprueba el espacio para patrullar e inspeccionar y la distancia a las
lampreas. Si arrastras el prefab nuevo desde Project, asigna Target y Motion.
Selecciona el objeto para ver el alcance de detección con Gizmos.

## Verificación

**Anadromo → Esc2 → Pez ciego → Verificar encuentro en Play** prueba la escena
guardada con eventos reales de teclado: nado silencioso, Shift inmóvil, sprint,
alerta, inspección, giro de cámara, retirada, ataque por continuar moviéndose,
ataque por romper la inspección, energía y reinicio con R. Coloca al jugador
en el encuentro para cada caso y pausa solamente el desgaste pasivo de energía.
Resultado: `Logs/Esc2BlindFishCheck.txt`.
