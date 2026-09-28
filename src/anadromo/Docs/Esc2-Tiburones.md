# Tiburones de esc2

Se sustituyeron los cuerpos provisionales de `Tiburon1` y `Tiburon2` mediante su prefab compartido `Assets/_Project/Prefabs/Enemies/PasoTiburonEsc2.prefab`, usado únicamente en `esc2`. El archivo de escena y los cambios pendientes del editor no se guardaron ni se modificaron durante esta integración.

El modelo procede de `ihc/Assets/shark.zip`, que contiene un modelo de Otodus/Megalodon con 28 huesos. En Blender se normalizó a 1,9 m para aproximar el tamaño del cuerpo provisional, se refinó la silueta y se horneó una textura con lomo gris azulado y vientre más claro, conservando ojos, dientes y branquias originales. El archivo editable está en `SourceAssets/Shark/Shark_prepared.blend`; `build_shark.py` reproduce la preparación desde la raíz del proyecto.

`Esc2SharkNaturalMotion` produce natación lateral con amplitud creciente hacia la cola, pequeños ajustes de las aletas pectorales, respiración mandibular sutil y una inclinación suave al girar. La frecuencia responde a la velocidad real del recorrido. La animación no desplaza el controlador ni modifica colisiones, daño, tiempos, velocidad, puntos de ruta o activación.

Los tiburones conservan el comportamiento existente: permanecen ocultos mientras esperan, aparecen tras el aviso para recorrer el paso y se ocultan al terminar. El nuevo modelo está dentro de `Tiburon_Naranja > Shark` en cada instancia; el nombre del contenedor antiguo se conserva para no alterar referencias.

Verificación: escala y materiales en Unity; deformación real de la malla a 30/60/120 FPS; visibilidad inicial y reactivación del prefab en la comprobación de editor; inspección de las vistas `Docs/SharkPreviews/Shark_0.png` y `Shark_1.png`. Informe técnico en `Logs/SharkArtValidation.txt`. Se verificó que el controlador y todos los transform originales del prefab permanecen iguales salvo la incorporación de un hijo visual al contenedor del tiburón. Los cinco renderizadores de primitivas quedan ocultos.

No se realizó una prueba de rendimiento en visor VR.
