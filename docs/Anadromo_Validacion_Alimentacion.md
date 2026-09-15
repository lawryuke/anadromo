# Corrección del prototipo de alimentación — 15/09/2026

Escena: `Assets/_Project/Scenes/Blockout_Test.unity`.
Relacionada con T3.1, T3.4 y T4.1 de `Anadromo_Tareas_Acto1.md`.
Esta corrección permite continuar el prototipo; no completa el Acto I.

## Fallos corregidos

- El código había reemplazado `migrationZTarget` por `krillZoneTarget`, pero la escena conservaba el campo antiguo. Se conectó el destino al `KrillGroup` existente, con un radio de llegada de 3 unidades.
- `playerMovementScript` apuntaba a `XROrigin`. Para desarrollar sin VR, la escena deja esa referencia vacía y la lista de controles adicionales vacía. `DebugVuelo` está activo desde el inicio; J arranca únicamente el cardumen.
- Faltaban `EnergySystem` en el jugador y `PlayerFeeding` en la cámara. Se añadieron, junto con un trigger de boca de radio 1.
- Los peces podían volver a migración al salir del radio de llegada y dejar de evadir al jugador. Ahora recuerdan haber llegado y recuperan la velocidad normal cuando termina la huida.
- Una presa podía recibirse más de una vez antes de que `Destroy` terminara. Se desactiva su componente `Prey` al consumirla; sin un sistema de energía conectado no se consume.

Se conservaron las 15 presas bajo `KrillGroup`: están distribuidas en posiciones distintas. También se conservó `com.unity.pipeline`, necesario para controlar el Editor con Unity CLI. La revisión inicial no tenía evidencia suficiente para considerar accidentales estos elementos.

## Validación realizada

Unity 6000.5.8f1, mediante Unity CLI y el Editor conectado.

| Comprobación | Resultado |
| --- | --- |
| Espera: 40 peces y control de escritorio activo, nado VR deshabilitado | Pasó |
| `StartGame()` conserva la selección de controles de escritorio | Pasó |
| Migración de los 40 peces | Pasó; distancia media al destino de 35,84 a 2,41 unidades |
| Evasión tras cruzar otra vez el radio de llegada | Pasó |
| Recuperación de velocidad normal al perder al jugador | Pasó |
| Contacto físico con kril | Pasó; energía de 30 a 45 |
| Evento repetido para la misma presa, en el mismo frame | Pasó; energía permaneció en 45 |

El recorrido se verificó llamando a las actualizaciones reales de los peces durante una simulación acelerada en Play Mode. El contacto se comprobó con `Physics.Simulate`, seguido de un segundo evento para la misma presa. Esto comprueba comportamiento y conexiones; no mide rendimiento ni confort en el casco.

### Repetir la comprobación

Desde la raíz del repositorio, abrir `Blockout_Test`, entrar en Play Mode y esperar a que termine la recarga de scripts. No pulsar J antes de ejecutar:

```powershell
unity command eval_file "$PWD/tools/validation/feeding-regression.cs" 20000 --project-path "$PWD/src/anadromo" --format json
```

El script modifica objetos de la sesión de prueba. Salir de Play Mode al terminar, sin guardar el estado de ejecución. Se dejó el Editor fuera de Play Mode después de validar.

## Controles de escritorio

En `Blockout_Test`, `XR Device Simulator` está inactivo y `TrackedPoseDriver` y `SwimLocomotion` están deshabilitados. `DebugVuelo` controla al jugador de forma independiente al cardumen:

- WASD o flechas: desplazamiento respecto a la vista.
- Q/E: bajar/subir en el eje vertical del mundo.
- Mantener botón derecho y mover el mouse: girar la vista, sin desplazar al jugador.
- Mouse sin botón derecho: no altera la vista ni la posición.
- J: iniciar el cardumen, sin activar nado VR.

El movimiento de escritorio usa un Rigidbody cinemático para mantener los triggers de alimentación sin deriva física. Al deshabilitar `DebugVuelo` se restaura el estado cinemático anterior. Es un control de vuelo para desarrollar niveles, no una validación de colisiones o locomoción VR.

Se verificaron estas entradas con dispositivos de teclado y mouse sintéticos y simulación física. Para repetir, usar una sesión nueva de Play Mode y ejecutar:

```powershell
unity command eval_file "$PWD/tools/validation/desktop-controls-regression.cs" 20000 --project-path "$PWD/src/anadromo" --format json
```

Para volver a probar VR, deshabilitar `DebugVuelo` y habilitar `SwimLocomotion` y el `TrackedPoseDriver` de la cámara. El simulador XR solo se necesita para simular dispositivos; con un casco real debe permanecer inactivo.

## Pendiente para el Acto I

- Sustituir el arranque de prueba con J por el inicio diegético previsto en T3.5.
- Ajustar energía inicial, consumo y valor de presas para garantizar que 3–4 presas llenen la energía, y conectar el cierre del tutorial.
- Integrar feedback visual y latido, pez guía, medusa, entorno y audio.
- Realizar playtest y medición de rendimiento en casco. No se verificaron los 90 fps ni la sensación de las brazadas en esta sesión.
