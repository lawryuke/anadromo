# Escenario 4 — La garganta del Bloop

Abrir `Assets/_Project/Scenes/Escenario4.unity`, entrar en Play y pulsar Espacio o Comenzar. Menú: Anadromo > Escenario 4 > Abrir escena.

## Experiencia

- WASD para nadar, E/Q para subir y bajar, ratón para mirar. Seguir los minerales luminosos hacia la salida superior.
- El jugador no ataca. El Bloop sale lentamente de un agujero abierto en el fondo, nada con la animación corporal existente y abre la mandíbula gradualmente al atacar.
- Ascenso de 0,35–0,65 m/s; aviso de 3 segundos, acercamiento de 0,8 m/s durante 4 segundos y recuperación de 6 segundos. El ataque fija su objetivo al anunciarse, permitiendo esquivarlo.
- Rocas de 30–70 cm se desprenden de bóvedas físicas cercanas. Permanecen 1,8 segundos antes de soltarse, con partículas y sonido; descienden aproximadamente a 2,4 m/s. Se seleccionan sectores por delante del jugador y se comprueba espacio libre bajo el techo. No hay líneas artificiales de trayectoria.
- Un impacto ralentiza temporalmente al jugador y aplica una viñeta breve. No fuerza giros de cabeza. Espacio/R reinicia después de escapar o ser capturado.

## Arte y visibilidad

La caverna reutiliza los modelos escaneados Seabed Fractured Cliff B, Seabed Layered Cliff A y Seabed Flat Rock A, junto al material TerrainTestVisuales_RockMassif de la escena principal, con sus texturas y normales. El anillo rocoso inferior deja libre la apertura por donde emerge el Bloop. Bóvedas cercanas permiten ver los desprendimientos durante el ascenso.

El renderer dedicado conserva SSAO y las características del renderer principal. Scenario4VisibilityController controla la niebla submarina; se desactiva el pase de agua duplicado en este renderer. Alcance nominal de visibilidad entre 20 y 15 metros, iluminación de apoyo en la ruta y mayor claridad en refugios. La escena principal y sus materiales originales no se modifican.

## Autoría y comprobación

`Anadromo > Escenario 4 > Actualizar arte` ejecuta Scenario4ArtUpgrade.Build. Actualiza los grupos de entorno del escenario y conserva la escena abierta. Si Escenario4 ya estaba abierta, volver a abrirla para cargar el archivo actualizado antes de guardar cambios.

`Anadromo > Escenario 4 > Validar escena` comprueba referencias, shaders, geometría transitable, tamaño de las rocas y lógica de ataque/evasión. Escribe Temp/scenario4-validation.txt.

Scenario4PlayCheck.RunBatch ejecuta una prueba aislada en Play, observa desplazamiento real de rocas y emergencia del Bloop y guarda capturas. La prueba no sustituye una partida manual completa ni la evaluación de rendimiento y comodidad con visor XR.

Los scripts Python antiguos quedan deshabilitados: recreaban geometría simplificada y no representan los colliders de los modelos escaneados actuales.
