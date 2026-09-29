# Integración selectiva de shake_Oculus en dev

## Decisión

Se compararon `dev` (`1935e2f`) y `shake_Oculus` (`2113cc1`) mediante `git merge-tree --write-tree dev shake_Oculus`, sin cambiar de rama ni iniciar un merge real. Se encontraron conflictos de contenido en:

- `Assets/_Project/Scenes/esc2.unity`.
- `Assets/_Project/Scripts/AI/Esc2Lamprey.cs`.
- `Assets/_Project/Scripts/AI/PiranhaPlayerTarget.cs`.

No se recomienda un merge automático tal cual. Aunque solo son tres archivos, afectan el rig, sus identificadores y el ciclo de vida del jugador. La rama convierte el jugador anterior en otro XR Origin; dev ya tiene VR_Player completo, con alimentación, energía, enemigos y zonas conectadas. Además, ambas versiones incluían instrucciones de texto y detectores de sacudida que no deben ejecutarse simultáneamente.

Se incorporaron manualmente el detector de inversiones de dirección, el controlador de captura y las pruebas de `shake_Oculus`. Se conservaron la escena, el rig y las referencias de dev. No se creó un merge commit ni se publicó ningún cambio.

## Mecánica y principios de interacción

| Principio | Implementación |
|---|---|
| Restricciones | Primera lamprea: rigidbody cinemático, nado y giro virtual desactivados, aleteos suprimidos. Una segunda lamprea no reinicia el plazo. |
| Mapeamiento | Sacudidas de manos en espacio de tracking; el balanceo de cámara y el giro del rig no suman progreso. |
| Visibilidad | No se presenta interfaz ni señal gráfica durante la captura; la escena y la lamprea siguen visibles. |
| Feedback | Se detienen el avance y el giro virtual; `heartbeat.wav` suena de fondo y la vista oscila lentamente de izquierda a derecha. |
| Affordance | La oscilación sugiere el gesto lateral sin representarlo con iconos o instrucciones. |
| Recuperación | La última lamprea restaura los estados de movimiento y de tracking previos. La derrota permanece bloqueada hasta reiniciar. |

Parámetros simplificados: 6 segundos para escapar; dos inversiones válidas con cualquiera de las manos liberan una lamprea. Cada inversión requiere al menos 6 cm de recorrido, 0,25 m/s y un cambio de dirección en un máximo de 0,8 segundos. Se rechazan temblores pequeños, movimiento lento/unidireccional, saltos de tracking, muestras inválidas y recuperación inicial de tracking.

Se detiene el giro **virtual** producido por cabeza o joystick. Se mantiene el seguimiento físico del visor: no se congela su pose. La oscilación lateral se aplica al Camera Offset alrededor de los ojos (±2,5° a 0,25 Hz), restaurándose después; puede calibrarse en el inspector.

## Pantalla de muerte

Se revisó también `origin/shake` (`7041990`): su `TestSceneBuilder` crea un canvas con el texto «MORISTE», y su antiguo `LampreyAttackManager` usa otro sistema de energía y altera directamente la cámara. No se importaron esos componentes incompatibles.

La versión integrada utiliza oscurecimiento y el único texto permitido, «MORISTE», en rojo como en la rama `shake`. Después de tres segundos se recarga automáticamente toda la escena, restableciendo comida y trampas. Durante la captura no aparece ningún texto ni interfaz. También se retiraron los mensajes de depuración en pantalla de PiranhaPlayerTarget, LuzViajera y la advertencia textual de Esc2SharkPassage en este flujo. El aviso sonoro del paso de tiburón se conserva.

## Validación

- Menú `Anadromo > Esc2 > Lampreas > Verificar sacudida y rig Oculus`: detector, bloqueo, varias lampreas, liberación, timeout, muerte, restitución, pose del visor y ausencia de componentes de texto.
- Menú `Tools > Anadromo > VR > Run esc2 Play Mode checks`: regresión del resto de interacciones de esc2.
- Resultados: `Logs/LampreyShakeCheck.txt`, `Logs/VRPlayerPlayCheck.txt` y captura `Logs/LampreyDeath-MORISTE.png`.

La validación incluye sacudida, bloqueo, audio, ausencia de interfaz durante la captura, pantalla «MORISTE» y reinicio automático. Los resultados se conservan en `Docs/Lamprey-Shake-PlayMode-results.txt` y `Docs/VR-Player-PlayMode-results.txt`.

Las pruebas automatizadas no sustituyen una sesión con personas usando Quest para comprobar que infieren el gesto, ni la calibración de esfuerzo y comodidad. No se generó ni instaló un APK.
