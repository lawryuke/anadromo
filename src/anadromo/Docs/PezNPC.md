# PezNPC en TerrainTestCero

El objeto conservaba una malla de lubina con un material de caballa. Ahora utiliza `OceanViz_SeaBass` con sus texturas correspondientes de color y normales, en un material URP Lit independiente (`PezNPC_RealisticSeaBass`). El relieve de escamas, acabado humedo y sombreado utilizan la iluminacion real de la escena.

`RealisticFishNPC` refina la geometria conservando las UV y sin modificar la malla compartida. Dos subdivisiones producen 7.200 triangulos. El refinamiento se ve tambien en el editor. Durante Play se anima la malla independiente: onda progresiva de cabeza a cola, aleteo pectoral leve, frecuencia ligada a la velocidad, aceleracion gradual y giro con inclinacion. Un barrido fisico frena ante obstaculos solidos. El collider del pez es trigger para no interferir con ese barrido ni empujar al jugador.

Seleccionar **PezNPC > Realistic Fish NPC** para ajustar:

- **Cruise Speed**: velocidad en metros por segundo; inicial 0.35.
- **Roam Radius**: radio del recorrido alrededor del punto inicial; 2.5 m.
- **Turn Speed**: velocidad de giro; 45 grados/s.
- **Tail Amplitude**: amplitud de cola en coordenadas locales; 0.035.
- **Tail Frequency**: frecuencia maxima de la cola; 1.3 Hz.
- **Vertical Wander**: variacion vertical del objetivo; 0.15 m.
- **Subdivisions**: 0 a 2; bajar a 1 si hace falta reducir coste.

La malla refinada es transitoria y se reconstruye desde **Source Mesh** al abrir la escena. Al desactivar el componente se restaura la malla original. El material y la conexion al componente si quedan guardados en la escena. No cambiar materiales de los peces de cardumen: este NPC tiene uno propio.

## Comprobaciones

Runtime y validador compilan con el compilador y referencias del proyecto Unity 6000.3.10f1. La revision visual en Unity y la medicion de rendimiento VR estan pendientes.

El menu **Anadromo > Validate and Preview PezNPC** verifica refinamiento, UV, tangentes, limites, animacion de cola, mapa normal, shader y restauracion de la malla. Genera `Temp/PezNPC-preview.png` y `Temp/realistic-fish-validation.txt`. La vista previa es un estudio del pez; probar tambien Play en TerrainTestCero para evaluar niebla, escala, trayectoria y obstaculos con la iluminacion final.
