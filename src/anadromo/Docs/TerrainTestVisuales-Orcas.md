# Orcas de TerrainTestVisuales

La escena contiene 28 instancias de `Assets/_Project/Art/Models/Orca/Orca.prefab`, repartidas entre `Orca_Group_First` y `Orca_Group_Second`. Se conservaron las posiciones, rotaciones, escalas, destinos y la tecla 4 (tambien teclado numerico) del encuentro anterior.

El modelo y las texturas provienen del archivo facilitado `ihc/Assets/the-orca-or-killer-whale.zip`. El FBX original se conserva en `SourceAssets/Models/Orca/end.fbx`. La malla de la escena deriva de su geometria de reposo, con 10 416 triangulos, dos submallas, UV, normales y tangentes. Se normalizo a 6 unidades de largo, con el hocico en +Z y el lomo en +Y. El FBX original queda fuera de Assets para no importar tambien su extenso rig y animaciones que esta variante no utiliza.

`OrcaSwimAnimation` controla una onda vertical progresiva del pedunculo y las aletas caudales mediante GPU, con pequenos ajustes pectorales. Cada individuo tiene fase y cadencia propias. La intensidad responde a la velocidad del recorrido y alterna suavemente entre impulsos y deslizamiento. `OrcaGroupMovement` conserva el lanzamiento escalonado, con aceleracion 1.2 y balanceo limitado a 9 grados.

Los materiales conservan los mapas de manchas originales, usan normales y rugosidad, y anaden oclusion al cuerpo. El acabado es no metalico, con reflejo humedo y negros ligeramente levantados para mantener detalle bajo el agua. Las texturas tienen mipmaps, filtrado trilineal y anisotropia 4. La deformacion se comparte entre los pases de color, sombras y profundidad. Los limites de la malla incluyen margen para la cola animada.

## Comprobacion

- Comprobado: compilacion C# de los controladores y del validador con Roslyn y referencias de Unity 6000.3.10f1; buffers e indices de malla; referencias de prefab; posiciones, rotaciones y escalas originales conservadas; ausencia de referencias a tiburones en la escena.
- Pendiente: importacion y compilacion del shader en Unity, revision visual en Play y rendimiento en el dispositivo objetivo. El intento de Unity batch termino por falta de licencia activa (codigo 198).
- Con una licencia activa, abrir TerrainTestVisuales y ejecutar `Anadromo > Validate TerrainTestVisuales Orcas`. Comprueba modelos, materiales, shader y llegada a 30/60/120 FPS. El resultado se guarda en `Temp/orca-validation.txt` solo si pasa.
- En Play, pulsar 4 y observar ambos grupos: cola arriba/abajo, cabeza estable, transiciones suaves, manchas oculares y ventrales visibles y ausencia de recortes de las aletas durante el movimiento.
