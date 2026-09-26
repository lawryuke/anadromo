# ANÁDROMO · El laberinto sumergido · PC

Nueva propuesta del escenario de cueva. Ocho cavernas principales, trece conexiones, tres circuitos y tres callejones sin salida. La suma de los ejes de las galerías es de unos 334 m; esa cifra incluye rutas alternativas y no representa la distancia de entrada a salida.

## Lógica espacial

El umbral conduce a una primera bifurcación. Ambas ramas pueden llevar a la catedral sumergida, pero la cámara de derrumbe ofrece un desvío profundo hacia la fosa del eco. Desde la catedral, el jugador puede cruzar a la bóveda de las raíces o explorar la fosa. Ambas rutas convergen en el sifón de salida.

Los circuitos permiten volver a una sala por otra entrada, rodear amenazas y perder la orientación. Los tres ramales cerrados sirven para encuentros, escondites o descubrimientos. Los recodos cortan vistas entre sectores y las salas permiten reconocer varias salidas y maniobrar.

| Sala | Tamaño nominal en planta | Cota central |
|---|---|---|
| A · Umbral | 9 × 12 m | 0 m |
| B · Sala de las mareas | 16 × 17 m | −2 m |
| C · Cámara del derrumbe | 13 × 14 m | −4 m |
| D · Jardín ciego | 15 × 13 m | −5 m |
| E · Catedral sumergida | 20 × 24 m | −7 m |
| F · Fosa del eco | 12 × 15 m | −10 m |
| G · Bóveda de las raíces | 14 × 18 m | −12 m |
| H · Sifón de salida | 11 × 14 m | −14 m |

Las galerías tienen diámetros nominales de 2,5–4 m, con variación de sección y salientes que producen estrechamientos locales. Las dimensiones son de diseño; las paredes erosionadas y los objetos modifican el espacio libre real. En la catedral, la bóveda llega aproximadamente a 15 m sobre el suelo central.

## Construcción y dirección visual

Las cámaras y las galerías se unen mediante reconstrucción volumétrica en una única superficie continua. Las aberturas de las conexiones no conservan paredes superpuestas. La entrada y la salida están abiertas para unir este conjunto a otros escenarios.

El acabado utiliza relieve irregular a dos escalas, sombreado suave, roca gris verdosa, superficies húmedas, columnas erosionadas, estalactitas y acumulaciones de cantos. La guía visual procede de colonias pequeñas de pólipos bioluminiscentes, alternando grupos turquesa y azul.

La escena contiene unas 710.000 caras trianguladas equivalentes, sin objetivo de compatibilidad con Quest 2. Se ha priorizado la nueva distribución y el detalle para PC.

## Archivos

- `Anadromo_Laberinto_PC.blend`: escena editable, organizada por colecciones, con materiales procedurales, luces, cámaras y curvas de referencia.
- `Anadromo_Laberinto_PC.fbx`: geometría y referencias de las salas para importar a Unity, a escala métrica.
- `Mapa_Laberinto.png`: planta esquemática que muestra las salas y los recorridos alternativos.
- `01_Catedral.png`, `02_Bifurcacion.png`, `03_Galeria.png`: renders reales de la escena.
- `04_Vista_Seccionada.png`: vista técnica del conjunto, con el techo retirado temporalmente y luz de maqueta. El modelo entregado conserva el techo y su iluminación interior.
- `distribucion.json`: centros, dimensiones y ejes de las galerías en coordenadas de Blender, con Z vertical.
- `validacion.json`: resultados de las comprobaciones geométricas.
- `Source/generar_laberinto.py`: generador completo. Editar `rooms` y `edges` permite rediseñar la red y reconstruir la cueva.

Abrir el `.blend` con Blender 5.2 o posterior. El generador escribe en la carpeta del paquete por defecto y sobrescribe sus archivos; la variable de entorno `ANADROMO_OUTPUT` permite elegir otro destino. La geometría volumétrica y su erosión están aplicadas en la malla final. Las curvas son referencias: moverlas a mano no actualiza la cueva.

## Unity

Importar el FBX con escala 1. Las normales de la cueva miran hacia su interior. Añadir MeshColliders estáticos no convexos a la cueva y las formaciones; separar las colisiones decorativas según las necesidades del controlador de nado.

El FBX no traslada los nodos procedurales de Blender, sus texturas ni las luces de presentación. Los materiales y la iluminación deberán prepararse para el renderizador del proyecto de Unity; el `.blend` conserva el aspecto completo de las imágenes. Las colonias utilizan materiales separados `AN_Flora_Turquesa` y `AN_Flora_Azul`, para facilitar la emisión.

No se incluyen lógica de enemigos, agua animada ni eventos de derrumbe. Los renders muestran el entorno estático. La navegación, las colisiones y el rendimiento deben probarse en el proyecto de Unity.

## Verificación

Se han muestreado los ejes de las trece conexiones y comprobado los segmentos consecutivos contra las paredes y las formaciones sólidas. No se encontraron segmentos que atraviesen paredes, columnas o derrumbes. Los resultados detallados de espacio libre están en `validacion.json`. Esto valida los ejes previstos, no todos los movimientos posibles del jugador ni un tamaño arbitrario de collider.
