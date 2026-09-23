# PezNPC en TerrainTestVisuales

El objeto conservaba una malla de lubina con un material de caballa. Ahora utiliza `OceanViz_SeaBass` con sus texturas correspondientes de color y normales, en un material URP Lit independiente (`PezNPC_RealisticSeaBass`). El relieve de escamas, acabado humedo y sombreado utilizan la iluminacion real de la escena.

`RealisticFishNPC` refina la geometria conservando las UV y sin modificar la malla compartida. Dos subdivisiones producen 7.200 triangulos. El refinamiento se ve tambien en el editor. Durante Play solo anima la malla independiente: onda progresiva de cabeza a cola y aleteo pectoral leve. Mide el desplazamiento real en LateUpdate para ajustar la intensidad y frecuencia de la cola, con una fase distinta por pez. No modifica la posicion ni la rotacion: un pez de referencia sin controlador permanece en su lugar y conserva una animacion suave en reposo.

Las copias generadas se desplazan mediante `SwimGroupController`, que conserva sus esperas, objetivos, caza y colisiones. En el perfil Fish avanzan siguiendo su orientacion incluso durante la caza, reducen la velocidad al girar y modulan suavemente el avance. Se elimina asi el recorrido autonomo de `RealisticFishNPC` que comenzaba hacia +Z. Conservar el componente en los modelos que necesitan esta deformacion de malla; los peces que ya se animan por shader no necesitan agregarlo.

Seleccionar **PezNPC > Realistic Fish NPC** para ajustar:

- **Cruise Speed**: velocidad de referencia para la intensidad maxima de la cola; inicial 0.35 m/s. No controla el desplazamiento. La velocidad del grupo se ajusta en **Swim Speed** del controlador.
- **Tail Amplitude**: amplitud de cola en coordenadas locales; 0.035.
- **Tail Frequency**: frecuencia maxima de la cola; 1.3 Hz.
- **Subdivisions**: 0 a 2; bajar a 1 si hace falta reducir coste.

La malla refinada es transitoria y se reconstruye desde **Source Mesh** al abrir la escena. Al desactivar el componente se restaura la malla original. El material y la conexion al componente si quedan guardados en la escena. No cambiar materiales de los peces de cardumen: este NPC tiene uno propio.

## Comprobaciones

Runtime y validador compilan con el compilador y referencias del proyecto Unity 6000.3.10f1. La revision visual en Unity y la medicion de rendimiento VR estan pendientes.

El menu **Anadromo > Validate and Preview PezNPC** verifica refinamiento, UV, tangentes, limites, animacion de cola, mapa normal, shader y restauracion de la malla. Genera `Temp/PezNPC-preview.png` y `Temp/realistic-fish-validation.txt`. La vista previa es un estudio del pez; probar tambien Play en TerrainTestVisuales para evaluar niebla, escala, trayectoria y obstaculos con la iluminacion final.
