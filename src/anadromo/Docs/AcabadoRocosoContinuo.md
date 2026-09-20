# Acabado rocoso continuo del paso existente

La escena TerrainTestCero conserva las posiciones, dimensiones y superficies
del paso existente. El techo, suelo y los dos laterales TerrainCube2 (2), (3),
(4) y (5) ahora utilizan el mismo acabado rocoso que el relleno y el interior.
La fachada de roca de la entrada conserva su geometría y material originales.

Se detectaron 24 componentes de borde abierto en el submesh interior original
del LOD0, después de soldar posiciones para el diagnóstico: no era solamente
una costura de textura. ContinuousRockTunnelInterior reconstruye ese submesh
como una pared continua, con anillos conectados y costura radial cerrada.
Se aplica a los tres LOD. Conserva los triángulos exteriores del acantilado,
los contornos de los extremos y la profundidad del paso; los assets de malla
fuente no se escriben ni se eliminan.

La pared tiene relieve geométrico irregular de hasta 3.5 cm hacia la roca,
desvanecido en ambos extremos. No estrecha el paso. Se recalculan normales
suaves y tangentes. El collider del acantilado utiliza la reconstrucción;
los BoxCollider originales permanecen sin cambios. El collider del relleno
conserva el contorno previo, por lo que la zona accesible no se amplía con
los pequeños huecos visuales del relieve.

TunnelContinuousRock añade grano, estratos y fisuras mediante textura procedural
en coordenadas mundiales, manteniendo continuidad entre superficies de distinta
escala. El material usa tono (0.38, 0.36, 0.33), suavidad 0.20 y relieve 1.15.
No se cambiaron la iluminación global ni la cámara. La textura es procedural;
no reutiliza el atlas fotográfico de la fachada como textura repetible.

Validación realizada: análisis de bordes de la malla original, compilación C#
contra las bibliotecas locales de Unity y comprobación de referencias de los
tres LOD. Pendiente: revisión visual en Unity y compilación GPU del shader.
