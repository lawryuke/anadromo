# Depredadores de esc2

Se reemplazaron únicamente los visuales de los siete enemigos existentes en `Assets/_Project/Scenes/esc2.unity`: dos pirañas, dos lampreas y tres peces linterna. Se conservan sus posiciones, territorios, velocidades, daño y controles. Los prefabs provisionales originales siguen disponibles y no se modificaron otras escenas.

## Modelos

- **Piraña:** `piranha2.zip`, elegida por su anatomía más natural y mandíbula articulada. `piranha1.rar` tiene una estética de monstruo. Malla suavizada, bordes de aletas conservados y transparencia recortada del atlas original.
- **Lamprea:** `sea-lamprey.zip`. Malla optimizada, texturas de piel, ojos y disco oral reparadas; esqueleto nuevo de 12 segmentos con pesos interpolados y forma de pulsación oral.
- **Pez linterna:** `pez_linterna1.zip`. Esqueleto original conservado, silueta suavizada, mapas de color, normales y rugosidad conectados. Señuelo luminoso unido al extremo del apéndice.

Los originales se obtuvieron de la carpeta local `ihc/Assets`. Los FBX, materiales y prefabs preparados están en `Assets/_Project/Art/Predators`. Los tres archivos `*_prepared.blend` editables están en `SourceAssets/Predators`.

## Movimiento

`PredatorNaturalMotion` anima los huesos del visual sin desplazar los controladores de los enemigos. La cadencia responde a la velocidad, con fases independientes, cola, aletas y respiración. La mandíbula de piraña y pez linterna se articula durante la amenaza y responde al instante de daño con una mordida. La lamprea ondula el cuerpo, orienta su visual durante la persecución y pulsa el disco oral al adherirse; al quedar aturdida reduce su movimiento.

Los cambios en los controladores originales se limitan a avisar a la animación al registrar una mordida. El cuerpo provisional queda oculto y conserva la referencia usada por el controlador, de modo que sus colores de depuración no tiñen las texturas nuevas.

## Reproducción y revisión

Ejecutar Blender en segundo plano desde la raíz del proyecto con `--python SourceAssets/Predators/Pipeline/build_predators.py` reconstruye los modelos. En Unity, `Anadromo > Esc2 > Depredadores > Instalar modelos naturales` importa materiales y prepara los visuales. El instalador conserva las posiciones y evita duplicados. Ejecutar con esc2 guardada y fuera de Play.

Las vistas de Unity están en `Docs/PredatorPreviews`: `_0` es la pose inicial, `_1` una fase de natación y `_2` la articulación de ataque. Los PNG terminados en `_Blender` son vistas de estudio.

Las verificaciones se registran en `Logs/PredatorArtValidation.txt`, `Logs/PredatorPlayValidation.txt` y `Logs/Esc2PiranhaValidation.txt`. Comprueban escala, deformación real de la malla, articulación, referencias, materiales, movimiento a 30/60/120 FPS y el comportamiento existente de los enemigos. Las vistas de estudio no sustituyen una evaluación de iluminación y rendimiento con el visor VR.
