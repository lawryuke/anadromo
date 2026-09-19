# Generar objetos dentro de una caja 3D

1. Crea un GameObject vacío en Hierarchy y añade **Anadromo > Box Object Spawner**.
2. Arrastra el objeto que quieres copiar a **Source Object**. También admite prefabs.
3. Ajusta **Count**, **Center** y **Size**. Con el generador seleccionado puedes modificar las caras de la caja directamente en Scene. El Transform permite moverla, rotarla y escalarla; no necesita un BoxCollider.
4. Pulsa **Generar / regenerar** para crear copias en edición, o activa **Generate On Start** para generarlas al entrar en Play. Si ya existen copias generadas en edición, Start las conserva sin duplicarlas.
5. **Borrar copias generadas** elimina solo las copias registradas por este componente. Las operaciones del Inspector admiten Undo.

Las copias son hijas del generador y conservan los componentes del objeto original. El original no se modifica ni se desactiva: puedes desactivarlo manualmente si solo debe servir de plantilla; sus copias se activarán. No uses como fuente el propio generador, un antecesor suyo, otra copia generada ni un objeto que contenga un generador.

**Avoid Overlaps** rechaza posiciones que se solapen con colliders habilitados de la escena en **Obstacle Layers**, incluido el original si está activo, y con las copias anteriores independientemente de sus capas. Admite colliders en hijos. **Include Triggers** decide si los triggers participan; un objeto que solo tenga triggers necesita esta opción. Sin colliders utilizables, la generación con esta opción avisa y se detiene. Desactívala para distribuir objetos sin comprobación de colisiones.

CapsuleCollider, BoxCollider, SphereCollider y MeshCollider convexo se comprueban con `Physics.ComputePenetration`. Para colliders no compatibles, incluidas mallas no convexas, se usan sus bounds conservadoramente: puede rechazarse espacio que visualmente parece libre. No se cambian ni deshabilitan colliders de las copias.

**Contain Whole Object** exige que los bounds mundiales de renderers y colliders queden dentro de la caja. Es conservador en objetos o cajas rotadas. Desactivado, solo se limita el pivote. **Random Yaw** permite variar la orientación y **Seed** reproduce la secuencia de candidatos con las mismas opciones; las posiciones aceptadas también dependen de los obstáculos de la escena.

**Attempts Per Object** limita los intentos. Si no hay espacio suficiente se generan menos objetos y aparece una advertencia, sin colocar copias superpuestas como alternativa.

La caja limita la colocación inicial: los scripts de movimiento, animaciones y la física del objeto pueden mover las copias después. No es un sistema ECS ni un volumen de confinamiento durante el juego.

## Apuntar a un objeto al generar

Arrastra un objeto de Hierarchy al campo **Target Object** antes de pulsar **Generar / regenerar** o de generar las copias al iniciar Play. Cada copia orienta su eje local +Z hacia la posición del objetivo una sola vez, al generarse. Esta orientación tiene prioridad sobre **Random Yaw** y se aplica antes de comprobar los límites de la caja y las colisiones.

El generador no mueve las copias hacia el objetivo ni actualiza su orientación después. Mover o cambiar el objetivo no afecta a las copias existentes; regenera para aplicar una nueva orientación. Sin objetivo (o si su posición coincide con la de la copia), se usa la orientación habitual del generador.

## Comprobación en Unity

- Genera varias cápsulas dentro de una caja y comprueba la separación, incluyendo un obstáculo en la escena.
- Prueba un original desactivado, colliders en hijos y un MeshCollider convexo/no convexo.
- Mueve, rota y escala la caja; comprueba los límites con Contain Whole Object activado.
- Regenera, borra y usa Undo; comprueba que el original se conserva y no reaparecen candidatos rechazados.
- Reduce la caja hasta que no quepa el objeto: debe avisar y terminar sin colocar objetos fuera.
- Entra en Play tanto con copias previas como sin ellas; comprueba que no se duplican automáticamente.
- Asigna un objetivo y genera copias: sus ejes +Z deben apuntar a él. Mueve el objetivo durante Play y verifica que el generador no cambia las posiciones ni las rotaciones de las copias. Regenera para orientarlas hacia la nueva posición. Prueba también un objetivo directamente encima/debajo y sin objetivo.
