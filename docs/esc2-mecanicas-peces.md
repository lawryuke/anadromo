# Esc2 — Mecánicas de lamprea, tiburón, piraña y pez linterna

Documento de comportamiento y diseño del MVP `Esc2_Enemigos_MVP`.
Los valores descritos corresponden a los scripts y a la configuración del
generador de la escena; son parámetros de prueba, no un balance definitivo.

Referencias: [pseudocódigo original](esc2-enemigos-pseudocode.txt),
[GDD](Anadromo_GDD.md) y [guía de controles del MVP](esc2-enemigos-mvp.md).

## 1. Identidad de cada amenaza

| Enemigo | Amenaza | Respuesta que busca provocar |
| --- | --- | --- |
| Lamprea | Se adhiere, ralentiza y drena salud | Sacudirse para desprenderla y alejarse mientras está aturdida |
| Tiburón | Cruza una ruta y mata al contacto | Salir de su trayectoria antes de que pase |
| Piraña | Reacciona al movimiento rápido y alerta al cardumen | Pasar despacio sin permanecer cerca; esquivar si se activa |
| Pez linterna | Atrae con un señuelo y embiste desde la penumbra | Reconocer el apagado del señuelo y apartarse |

**Pez linterna — opción A implementada:** el jugador no tiene iluminación
propia. El apagado del señuelo avisa de la embestida; la defensa consiste
en escapar de su trayectoria. Mirarlo o quedarse quieto no lo detiene.

## 2. Lamprea — adhesión y desgaste

### Comportamiento implementado

1. **CHILL:** permanece en espera. Detecta al jugador a menos de 7 m si no
   hay una pared entre ambos.
2. **CHASING:** nada hacia el jugador a 4 m/s. Abandona la persecución si
   pierde la línea de detección o el jugador sale de un alcance de 14 m.
3. **ATTACHED:** al alcanzarlo se fija a un punto junto a la cámara. Añade
   una penalización de velocidad y comienza a drenar salud.
4. **STUNNED:** cuando el agarre llega a cero, se separa, deja de drenar y
   queda inmóvil durante 3 s. Después recupera el agarre y puede detectar
   al jugador otra vez.

### Adhesión y desprendimiento

Cada lamprea resta **15 puntos porcentuales** de velocidad. Dos dejan al
jugador al 70% de su velocidad base; la velocidad nunca baja del 25%.
Cada una drena **5 de salud cada 2 s**, con su propio temporizador. No hay
una mordida de daño inmediato al adherirse.

El agarre inicial es **100**. En el prototipo de escritorio:

- Alternar pulsaciones A/D en un intervalo máximo de 0,6 s resta 35 de agarre.
- Un desplazamiento horizontal del ratón superior a 50 unidades de entrada
  en un frame resta 25 de agarre.
- Hay un intervalo mínimo de 0,12 s entre sacudidas aceptadas.
- Cada sacudida afecta a todas las lampreas adheridas: tres alternancias
  válidas o cuatro sacudidas de ratón bastan desde agarre completo.

Al desprenderse se elimina únicamente la penalización de esa lamprea y se
intenta separarla hasta 2 m hacia delante, respetando obstáculos.

### Ejemplo de encuentro

Entras en D y dos lampreas te alcanzan. Notas la pérdida de velocidad y las
formas junto a la cámara. Te sacudes, ambas se sueltan y aprovechas sus
3 s de aturdimiento para abandonar su alcance.

**Ubicación actual:** dos en D y dos en F.
**Pendiente para VR:** sustituir la entrada de escritorio por sacudidas
corporales y añadir háptica y audio de succión. Actualmente hay movimiento
visual de cámara al recibir daño y un pulso rojo en los bordes de pantalla.

## 3. Tiburón — peligro de paso

### Comportamiento implementado

El tiburón no caza al jugador ni cambia su recorrido para seguirlo. Es un
peligro móvil asociado a una ruta de puntos.

1. Cuando el jugador se aproxima a menos de 5 m del segmento de recorrido,
   se activa un aviso de **3 s**, si el encuentro está disponible.
2. El tiburón aparece en un extremo y recorre la ruta a **15 m/s**.
3. Cualquier contacto detectado inflige **100 de daño**, letal para la salud
   máxima actual del jugador.
4. Al terminar el recorrido, desaparece.
5. El encuentro puede volver a activarse una vez transcurridos **18 s desde
   la aparición**, siempre que no haya otro tiburón de ese paso activo y el
   jugador siga o vuelva a estar cerca.

El daño comprueba cada tramo recorrido entre frames, para evitar que la
velocidad permita atravesar al jugador sin registrar el contacto. El
tiburón sigue los puntos configurados; la ruta debe mantenerse dentro del
túnel porque no utiliza la evitación de paredes de los otros enemigos.

### Respuesta del jugador

Apartarse del eje, cambiar de altura o esperar en una zona lateral. Quedarse
quieto dentro de su trayectoria no lo detiene. Mirarlo tampoco influye.

### Ejemplo de encuentro

Te acercas al paso B–F. Aparece el aviso. Te apartas hacia un lateral y el
tiburón cruza sin desviarse hacia ti; después puedes continuar.

**Rutas actuales:** F → B y G → E.
**Señal actual:** texto de aviso en el HUD. El audio espacial y la háptica
que podrían sustituir ese aviso en la experiencia final están pendientes.

## 4. Piraña — ruido, proximidad y ataque de cardumen

### Comportamiento implementado

1. **CHILL:** nada alrededor de su posición inicial a **1 m/s**.
2. **DISTURBED:** detecta al jugador dentro de **7 m**, sin una pared entre
   ambos. Acumula tiempo de proximidad mientras mantiene la detección.
3. **ATTACK:** se activa inmediatamente si la velocidad real del jugador
   alcanza **3 m/s**, o si permanece cerca durante **2,5 s** aunque se mueva
   despacio o se quede quieto.

Al atacar, alerta a las pirañas de su lista de cardumen situadas a menos de
**20 m**. Esa propagación usa distancia; no comprueba paredes entre aliadas.
Fuera de la detección se reinicia el contador de proximidad. Si ya estaba
atacando, necesita **3 s sin detectar al jugador** para volver a CHILL.

### Movimiento y daño

En ataque nada a **7 m/s** y gira lentamente hacia el jugador. Su dirección
se actualiza durante la persecución, pero la inercia dificulta corregir una
esquiva brusca. El contacto causa **8 de daño por piraña**, con un intervalo
mínimo de **1 s** entre daños de la misma piraña. Varias pueden dañar a la vez.

El MVP calcula el ruido a partir del desplazamiento real del jugador. Nadar
normalmente supone 2,4 m/s; acelerar con Shift llega a 6 m/s antes de aplicar
penalizaciones. Empujar una pared sin desplazarse no cuenta como nadar rápido.

### Respuesta del jugador

Pasar despacio y sin detenerse junto al grupo. Si comienza el ataque,
cambiar de dirección, usar cobertura y salir del alcance de detección.
**Quedarse quieto no garantiza seguridad:** el temporizador de proximidad
continúa si la piraña sigue detectando al jugador.

### Ejemplo de encuentro

Cruzas cerca de B sin acelerar. Si sigues avanzando puedes evitar completar
el tiempo de proximidad; si permaneces junto al grupo, se activa igualmente.
Si aceleras dentro de su radio, una piraña puede alertar de inmediato a sus
compañeras cercanas.

**Ubicación actual:** cuatro en B y cuatro en E.

## 5. Pez linterna — señuelo y emboscada

### Intención del encuentro

En una zona semiiluminada, el pez permanece oculto y ofrece una luz que
recuerda a la comida. El jugador se aproxima creyendo encontrar un kril.
La luz se apaga de golpe: descubre el engaño justo antes de la embestida.

La animación actual del señuelo es una oscilación sencilla. El MVP no tiene
todavía una reproducción completa del aspecto y comportamiento del kril
que permita validar visualmente ese engaño.

### Comportamiento implementado

1. **CHILL:** permanece quieto con el señuelo encendido, salvo durante
   recuperación.
2. **DISTURBED:** al detectar al jugador a menos de **8 m**, sin paredes de
   por medio, apaga el señuelo y permanece quieto preparando la emboscada.
3. Tras **1,8 s**, entra en **ATTACK**. Si durante la preparación pierde la
   detección dentro de un alcance ampliado de 12 m, cancela la preparación.
4. **ATTACK:** fija la dirección hacia la posición del jugador en ese
   instante y embiste a **9 m/s**. No corrige la dirección para seguirlo.
5. El contacto inflige **25 de daño** y termina el ataque. Si falla, termina
   después de aproximadamente **1,3 s**. En ambos casos se recupera durante
   **2 s**, inmóvil y con el señuelo apagado.

Después de recuperarse puede preparar otro ataque desde su nueva posición;
actualmente no regresa a su punto original de emboscada.

### Defensa implementada: escapar sin luz propia

- El señuelo se apaga y deja 1,8 s de preparación para reaccionar.
- El jugador se aparta lateralmente, cambia de altura o busca cobertura.
- La embestida mantiene una dirección fija y deja 2 s de recuperación.
- Mirar al pez o quedarse quieto no cancela el ataque.
- El jugador no tiene una luz propia ni un control F para iluminar.
- No se necesita un alga luminosa ni una habilidad de iluminación.

Se eliminó la cancelación por deslumbramiento del pseudocódigo original.
La iluminación ambiental y el señuelo del enemigo siguen presentes, pero
no reinician su contador de ataque. La duración del aviso debe probarse
con jugadores; 1,8 s no es un ajuste definitivo para VR. También falta una
señal sonora para que el apagado no sea la única advertencia perceptible.

### Relación con el GDD

Las secciones **7.3 y 8.1** del GDD describen a los **peces ciegos**, que
detectan vibraciones y pierden el rastro cuando el jugador se queda quieto
a tiempo. No describen una mecánica de detener al pez linterna mirándolo.

Para conservar ambas respuestas sin confundir al jugador:

| Amenaza | Señal | Defensa |
| --- | --- | --- |
| Pez ciego del GDD | Clics que aumentan y señal crítica | Dejar de moverse a tiempo |
| Pez linterna del MVP | Se apaga el falso alimento | Apartarse antes de la embestida |

Conviene enseñar los encuentros por separado antes de combinarlos. El pez
ciego no forma parte de los cuatro enemigos implementados en este MVP.

**Ubicación actual del pez linterna:** uno en C y uno en G.

## 6. Presentación y límites del prototipo

Los cuerpos son primitivas 3D. Pirañas, peces linterna y lampreas cambian
de color según el estado común: color base en CHILL, amarillo en DISTURBED
y rojo en ATTACK. La lamprea tiene además su máquina interna de adhesión;
durante el aturdimiento utiliza el color de DISTURBED. El tiburón mantiene
su estado base durante todo el recorrido.

El jugador tiene 100 de salud, HUD de depuración y reinicio manual. Son
herramientas del MVP, no una sustitución del planteamiento sensorial del GDD.
Las animaciones definitivas, modelos, audio espacial, háptica y controles
corporales VR quedan pendientes.

## 7. Dónde ajustar las mecánicas

Código en `src/anadromo/Assets/_Project/Scripts/CavernMVP/`:

| Archivo | Responsabilidad |
| --- | --- |
| `CavernLamprey.cs` | Persecución, adhesión, drenaje, agarre y aturdimiento |
| `CavernShark.cs` | Recorrido por puntos y daño letal por barrido |
| `CavernPiranha.cs` | Detección, alerta de cardumen y movimiento con inercia |
| `CavernAngler.cs` | Señuelo, preparación, embestida fija y recuperación |
| `CavernEnemy.cs` | Estados comunes, detección, movimiento con obstáculos y contacto |
| `CavernPlayer.cs` | Salud, velocidad y entrada de sacudidas |
| `CavernMvpWorld.cs` | Distribución de enemigos, valores por escena y activación de tiburones |

Los campos públicos pueden ajustarse en el Inspector. El reinicio reconstruye
el escenario desde `CavernMvpWorld.Build()`: para conservar ajustes al pulsar
R, también deben reflejarse en los valores del generador o de los scripts.
