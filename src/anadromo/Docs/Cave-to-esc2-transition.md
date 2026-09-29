# Transición de TerrainTestVisuales a esc2

La zona `Cueva_Limit` ya forma parte de la secuencia de huida del Bloop. Después de entrar en la cueva, el Bloop asciende, comienza el temblor y ocurre el derrumbe. Cuando el fundido final llega a negro y la fase pasa a `Complete`, `LevelManager` carga `esc2`. La escena siguiente inicia en la posición guardada de `VR_Player` y revela la imagen durante 1,5 segundos. La transición no muestra texto.

Las dos escenas están habilitadas en Build Settings. El fundido de llegada solo aparece al venir de `TerrainTestVisuales`; abrir `esc2` directamente o reiniciarla tras morir conserva su inicio habitual. No se cambió la geometría ni la posición del jugador en ninguna escena.

Verificación en Unity Play Mode: `Tools > Anadromo > VR > Check cave to esc2 transition`. Pasaron 28 comprobaciones de progresión y la carga de `esc2` con VR_Player activo, fundido negro sin texto y retirada automática del fundido. Resultado guardado en `Logs/CaveSceneTransitionCheck.txt` y copiado a `Docs/CaveSceneTransitionCheck.txt`. La prueba invoca el cierre de la cueva sobre la escena real; las reglas desde la entrada hasta `Complete` se comprueban por separado. Falta una pasada presencial con visor para valorar la continuidad visual y el ritmo.

El validador previo `Validate TerrainTestVisuales Level Logic` sigue reportando `Orca_Reference` activo en el archivo de escena. `LevelManager.Awake` desactiva esa referencia al comenzar Play Mode; la comprobación de transición confirmó que el nivel queda listo y carga la siguiente escena. Ese aviso no fue introducido por esta transición.
