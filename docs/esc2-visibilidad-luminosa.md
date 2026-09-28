# Visibilidad luminosa en esc2

En `Assets/_Project/Art/Materials/Esc2-WaterDepthY.mat` hay dos pares de distancias:

| Inspector | Valor inicial | Afecta a |
| --- | --- | --- |
| Visibility fade start / end | 4 / 7 m, conservados | Terreno y cuerpos opacos. |
| Luminous objects fade start / end | 12 / 18 m | Cuerpo/halo de medusas, señuelos y alcance visual de las zonas reveladas. |

Start es donde empieza a desaparecer; End es donde ya no se ve. Configura End
mayor que Start. El segundo par no aumenta la visibilidad general del escenario.
La cámara debe tener Far Clip mayor que la distancia luminosa máxima.

## Medusa y algas

La medusa de esc2 referencia ahora el material de esc2. Conserva su recorrido,
color, brillo y alcance de luz existentes. Tiene `WaterRevealLight` con `Radius = 3`
y `Strength = 1`. El radio se mide desde la luz, en metros mundiales, y nunca
supera `Light > Range`. Se desvanece suavemente desde el centro hasta el borde.

La luz real ilumina y el shader permite ver el terreno dentro de ese radio,
incluso más allá del End normal. Entre el jugador y esa zona sigue habiendo
oscuridad. Las sombras impiden revelar al otro lado de una pared; la profundidad
de la cámara oculta la medusa detrás de geometría opaca. Desactivar la luz elimina
también su revelado. La activación por `LuzViajera` sigue funcionando.

Para un alga luminosa, añade una Point Light hija y `WaterRevealLight` en ese
mismo objeto; activa sombras, ajusta Range, Radius y Strength. Para representar
el punto luminoso con las formas del MVP puedes añadir `MedusaGlow`, asignar
el shader `Anadromo/Medusa Glow` y el material de agua de esc2. Para un mesh
luminoso existente usa el material `Esc2-LureGlow` y `DistantLureGlow`, con su
referencia al agua y su color; el revelado procede de la Point Light separada.

## Pez linterna

El señuelo utiliza `Esc2-LureGlow` y `DistantLureGlow`: emisión visible desde
lejos, sin iluminar terreno ni revelar el cuerpo. Su Light permanece apagada,
incluso al reiniciar el encuentro. No lleva revelado local activo.

El controlador del pez sigue encendiendo el renderer solamente en CHILL;
DISTURBED, ATTACK y RECOVER ocultan la bola. Se actualizó tanto el encuentro
en esc2 como `PezLinternaEsc2.prefab`, por lo que duplicarlo conserva el comportamiento.

## Render y límites

`Esc2-Renderer` usa `WaterVisibilityFeature` antes de transparencias. El pase
anterior del agua queda desactivado para evitar niebla doble. Cada cámara recibe
sus propias luces y datos; no se modifica el material de TerrainTestVisuales.
La emisión transparente se dibuja después del agua, con prueba de profundidad.

Se procesan hasta 16 luces de revelado visibles por cámara. Requieren sombras
adicionales URP y espacio en el atlas de sombras. Si una luz no tiene sombras
disponibles, no abre la niebla: evita filtraciones por paredes. Muchas luces con
sombras necesitan evaluación de rendimiento en el visor; reutiliza puntos
luminosos sin revelado para decoración. Far Clip y Shadow Distance deben cubrir
las distancias elegidas. El revelado está diseñado para terreno/cuerpos opacos.
Se habilitaron las sombras adicionales en el perfil activo `URP-HighFidelity`;
esta opción del perfil también permite sombras en otras escenas que lo usan.

## Verificación

`Anadromo > Esc2 > Visibilidad > Verificar render en Play` crea una prueba temporal
fuera de la cueva y compara imágenes: terreno oculto, revelado local, sombras,
apagado, bola a 10/15/19 metros y bola detrás de una pared. No mueve los objetos
de la escena guardada. Informe: `Logs/Esc2WaterVisibilityCheck.txt`; imágenes:
`Logs/WaterVisibility`. La apariencia y rendimiento VR requieren prueba con visor.
