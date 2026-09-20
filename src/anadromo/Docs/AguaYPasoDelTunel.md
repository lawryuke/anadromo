# Agua y paso del túnel — TerrainTestCero

Se aplicó el agua en TerrainTestCero, la escena abierta con el túnel. No existe
una escena llamada oceanViz en este proyecto.

La referencia es `ihc/oceanVizForke/OceanViz3/Assets/Scenes/Main/Main.unity` y
su ubicación Mediterranean. La superficie, el material WaterMediterranean,
las cáusticas y los materiales de rayos de luz ya estaban importados y se
comprobó que coinciden con el fork. Se reutilizaron los objetos de agua de
Act1_OpenOcean: superficie animada a Y=10, dos proyectores de cáusticas,
reflejos, partículas suspendidas, rayos de luz y volumen submarino. Los
proyectores se adaptaron al tamaño del escenario y a su capa de renderizado.
La cámara limpia el fondo, usa profundidad y color opaco, activa posprocesado
y ve hasta 150 metros. La escena tiene niebla azul verdosa.

## Bloqueo identificado y corrección

Una prueba aislada con Unity 6000.3.10f1 detectó impactos contra:

- Rocks/Seabed Flat Rock A (5)/BoidObstacle (2).
- Rocks/Seabed Flat Rock A (1)/BoidObstacle (2).
- Los colliders LOD2 y LOD4 de Seabed Flat Rock A (5).

Los dos volúmenes auxiliares de boids ahora son triggers convexos en esas
instancias; mantienen sus límites para el sistema de peces. En la roca (5)
solo se conserva la colisión LOD0. Sus LOD visuales utilizan también la malla
LOD0 para que no aparezcan superficies sobre el corredor.

El acantilado del túnel tenía, además, caras atravesando la boca en los assets
LOD2/LOD4. Sus colliders ya estaban desactivados: no eran la causa física.
Los tres niveles ahora reconstruyen la superficie a partir del LOD0 abierto.
Esto aumenta la geometría de los niveles lejanos de este único prefab.
Los assets fuente no se modifican. También se corrigió un segmento omitido
en la triangulación de la costura de TunnelGapInfill.

## Comprobaciones

Unity ejecutó 18 barridos de cápsula sin impactos tras la corrección:
nueve posiciones, en ambos sentidos, de Z=19 a Z=30. Radio mundial 0.112 m
y altura total 0.7 m, correspondientes a Person1. La prueba aislada carga
la geometría estática; no sustituye una sesión de juego con los peces activos.
El resultado está en `ValidacionPasoTunel.txt`.

Se comprobó que las referencias locales de la escena resuelven. Los scripts
de generación se compilaron y ejecutaron en la prueba de Unity. Para repetir
la prueba en el proyecto completo: abrir TerrainTestCero fuera de Play Mode y
usar `Anadromo > Validate TerrainTestCero Tunnel`.

Pendiente: inspección visual del agua y efectos en el editor con GPU; la
conexión de control de ventanas no estuvo disponible y la prueba física se
ejecutó sin gráficos.
