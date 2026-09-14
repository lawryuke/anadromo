**DOCUMENTO DE DISEÑO DE JUEGO (GDD)**

**ANÁDROMO**

*El último viaje del salmón*

Títulos alternativos: Río de Origen · Salvelinus · Nacer, Volver, Morir

v0.2 — Documento vivo de preproducción

**Logline:**&nbsp;

*Un salmón de cuatro años debe cruzar el océano, sobrevivir al colapso de su hogar de roca y remontar una cascada mortal para llegar al río donde nació — solo para reproducirse y morir. Una experiencia narrativa en VR de un único recorrido sobre el ciclo de la vida.*

# **1\. Visión y pilares de diseño**

Anádromo es una experiencia narrativa en primera persona (encarnada) en VR, de una sola partida ("single-run"), sin progresión meta ni rejugabilidad basada en desbloqueos. Su objetivo es que el jugador sienta, en su propio cuerpo, el instinto migratorio y la fragilidad de un salmón: comer o morir, avanzar o ser arrastrado, esconderse o ser detectado.

## **Pilares de diseño**

* **1\. El cuerpo es el mando.** Todas las mecánicas clave (nadar, quedarse quieto, saltar, esquivar) se resuelven con movimiento físico real del jugador, no con botones abstractos.

* **2\. La tensión es sensorial, no numérica.** En vez de barras de vida tradicionales, el peligro se comunica con háptica progresiva, audio espacial y oscuridad — el jugador "siente" el peligro antes de "verlo".

* **3\. Vulnerabilidad constante.** El salmón nunca es poderoso. Cada escenario reemplaza la mecánica de amenaza (ambiente → depredador oculto → depredador ciego → depredadores aéreos/terrestres) para que el jugador nunca se sienta "a salvo" del mismo modo dos veces.

* **4\. Un solo viaje, un final con sentido.** No hay checkpoints duros ni reintentos invisibles: morir es parte del tema. El juego reencuadra el fracaso como parte natural del ciclo de vida (ver sección 10).

# **2\. Ficha técnica**

| Categoría | Definición |
| :---- | :---- |
| Género | Aventura narrativa de supervivencia / sigilo encarnado en VR |
| Plataforma | VR de sala (room-scale) con seguimiento de los brazos/manos — Quest 2 |
| Duración | Una partida: 35–50 minutos |
| Perspectiva | Primera persona encarnada (el jugador ES el salmón; ve sus propias aletas) |
| Público objetivo | Jugadores VR de experiencia media |
| Rejugabilidad | Baja intencional (experiencia narrativa); alta solo por curiosidad de exploración/secretos |
| Tono | Contemplativo con picos de terror de supervivencia — no gore, sí tensión |

# **3\. Fantasía central y tema**

El jugador no controla a un salmón: es un salmón. El diseño evita HUD tradicional; toda la información (hambre, peligro, dirección) se comunica de forma diegética (a través del propio cuerpo, la vibración del agua, el sonido, la luz). El tema central es la anadromía como metáfora: volver al origen cuesta todo, y el sentido del viaje no es sobrevivir, sino cerrar el ciclo.

# **4\. Estructura narrativa — arco de 4 actos \+ epílogo**

| Acto | Escenario | Función narrativa | Amenaza central |
| :---- | :---- | :---- | :---- |
| I | Océano abierto | Tutorial diegético de movimiento, alimentación y por último buscar a un lugar seguro con los movimientos aprendidos | Ninguna al inicio (zona segura, luz plena), después de completar el tutorial vienen los depredadores a alterar el cardumen y el personaje debe buscar refugio |
| II | Colapso y túnel de roca | Pérdida del grupo; introducción del sigilo por vibración | Peces ciegos de profundidad, pirañas, lampreas marinas (se adhieren a tu cuerpo y te chupan vida). Medusa brillosa (npc que guia la ruta final por si se pierde el personaje)&nbsp; |
| III | Zona oscura profunda | Escalada de tensión; el jugador domina el sigilo | Peces ciegos letales (con barra de alterado) |
| IV | Zona del vacío | saliendo de la zona de sigilo oscuro, debe huir a toda velocidad de nado. | huir de la boca gigante del Bloop Fish |
| V | La cascada | debe esquivar rocas, garras, picos y aguilas del rio y además de saltar en cada cascada, al final debe esquivar bocas de oso | rocas, garra de oso, boca de oso, águila, ave picadora |
| Epílogo | El desove | videoclip. Resolución temática del ciclo de vida. | Ninguna — es catártico, no peligroso |

# **5\. Sistemas transversales (presentes en todo el juego)**

## **5.1 Locomoción: nado por remada física**

El movimiento principal se realiza "remando" con los brazos frente al cuerpo. Estilo pecho simple. (los mandos detectan velocidad y dirección de la brazada).&nbsp;

Girar el cuerpo/cabeza determina la dirección. Se evita el stick de desplazamiento para reducir el mareo (motion sickness) y para reforzar la fantasía de "nadar de verdad".

* Velocidad de crucero: brazadas lentas y amplias.

* Sprint/impulso de escape: brazadas rápidas y cortas – consume barra de energía más rápido.

* Frenado/quietud: soltar los brazos a los costados – crítico para el sigilo (Actos II y III).

## **5.2 Barra de energía (hambre), sin HUD**

La energía (es el hambre también) se representa diegéticamente por el color y contraste de la visión del jugador (más viva y saturada con energía alta; grisácea y con visión periférica reducida cuando el hambre es crítica) y por el ritmo cardíaco audible (un latido grave que se acelera al bajar la energía). Se recupera comiendo presas pequeñas (kril, alevines) que aparecen un poco más brillantes para guiar la atención del jugador sin romper la inmersión.

## **5.3 Háptica progresiva (regla de oro del juego)**

La vibración de los mandos escala en tres niveles en cualquier escenario de amenaza. Esta tabla es la referencia maestra para todo el equipo de diseño de sonido/haptics:

| Nivel | Disparador | Patrón háptico | Audio asociado |
| :---- | :---- | :---- | :---- |
| Calma | Sin amenaza cercana / amenaza no alertada | ninguno | Ambiente submarino grave, clics lejanos |
| Alerta | Amenaza detecta movimiento / se aproxima | Vibración irregular creciente | Clics rápidos, zumbido direccional (audio 3D hacia la fuente) |
| Crítico | Amenaza sobre el jugador / ventana de gracia activa | Vibración sostenida fuerte \+ doble pulso | Chillido agudo, silencio súbito del resto del ambiente (focaliza atención) |

## **5.4 Audio espacial 3D como "visión"**

Dado que buena parte del juego ocurre con poca luz, el audio 3D binaural es un sistema de juego, no solo ambientación: la posición e intensidad de cada sonido comunica dirección y distancia real de amenazas y comida. Se recomienda uso de audífonos obligatorio y calibración de audio espacial en el arranque.

## **5.5 Comodidad y accesibilidad VR**

* Viñeta dinámica opcional durante movimientos rápidos (corriente, cascada).

* nada sentado el usuario??&nbsp;

* Subtítulos direccionales para sonidos críticos (posición del icono indica ángulo de la fuente).

# **6\. Acto I — Océano abierto (tutorial diegético)**

Zona de luz plena, aguas abiertas, cardumen del salmón protagonista aún completo. No hay HUD ni texto: cada mecánica se enseña con un pez guía del cardumen que modela la acción (nadar en formación, bucear a comer, esquivar una medusa inofensiva).

El menú del juego inicialmente es “diegetic main menu”. Empieza nadando el personaje principal con su cardumen hacia adelante en el azul nublado, el peresonaje está en medio de este cardumen. Cuando da play inmediatamente empieza el juego ya iniciado.

Aún no controla el salmon, segundos despues todo el cardumen paran en una zona rocosa que parece estar dividido y que por dentro de la division es la profundidad del mar (donde vive el bloop fish) que no se ve por la distancia (sensación de vertigo?). Esta zona hay corales, kril, alevines (comida basicamente) algo brillante a comparación del ambiente donde está.&nbsp;

Una vez que el usuario empieza a controlar al salmon, su vision está un poco distorisonada (aún puede ver el ambiente) y con sonidos de hambre, pero la comida tiene un brillo y que lo ve de forma semi-clara. El jugador debe aprender a mover el pez con el nado sin indicaciones, obviamente va comer el krill porque vió a los demás salmones comiendo krills, (aprendizaje colectivo).

De comer 3 o 4 comidas para llenar su saciar su hambre y estar con energias.&nbsp;

&nbsp;

**7\. Acto II — El colapso y el túnel de roca**

## **7.1 El evento de ruptura**

Se escuchan orcas y tiburones viniendo de las profundidaes, 3.5 segundos hasta llegar donde está tu cardumen. El cardumen se desespera y se dispersan. pasan entre ellos una bandada de estos depredadores pero sin objetivo de comer, obviamente que habrá algunos comidos. Tú tambien quieres esquivarlos por temor, pero no lo sentirá el personaje que hay una boca debajo por la concentracion de nadar, asi que nunca será comido, solo será empujado por el cuerpo de los depredadores.&nbsp;

&nbsp;

El usuario no sabrá a donde ir, asi que se insertará una “guia”: 3 peces pasar en frente de donde esté el usuario viendo, de una esquina superior en direccion a la cueva o la zona segura. tendrá alrededor de 20 segundos para escapar, si no llega entonces será devorado. Mientras huye, suena el sonido gutural del bloop fish.

&nbsp;

Cuando llegue a la cueva, no será profundo y puede ver el final de este. Además que ahi están los peces que seguiste “a pocos pasos”.

&nbsp;

Una vez dentro de la cueva, cuando volteas se ve que los últimos depredadores huyendo hacia arriba.

&nbsp;

Después el usuario verá todo se calmó sin sonido alguno, unos segundos de calma, pero empieza a temblar y vez la boca inmensa del bloop fish yendo de abajo hacia arriba, arrasando con todo tu cardumen. El lugar empieza a temblar y desprenderse algunas rocas mientras se escucha su sonido. Segundos después se observa su ojo realista gigante y cuando te ve, se dilata la pupila en un momento. Sigue subiendo sin parar y derumba la cueva y te cae una piedra y te desmayas.

&nbsp;

## **7.2 El túnel — bioluminiscencia tenue**

El jugador encuentra un túnel/pasadizo de rocas iluminado tenuemente por anguilas o criaturas bioluminiscentes como medusas que reaccionan al movimiento del jugador (se encienden más cerca de él, guiando el camino de forma orgánica, no con un waypoint artificial).

* Debe comer para mantener energía mientras esquiva pirañas y lamprea marina (se adhiere a tu cuerpo haciendo vibrar el mando y bajandote vida. El usuario debe sacudirse para sacarse de encima a estos) atrapados en el derrumbe.

## **7.3 Peces ciegos — sigilo por vibración (mecánica núcleo)**

Al llegar a la zona de oscuridad total, el jugador encuentra peces que vivían en las profundidades y subieron por el sismo. No ven: detectan vibración por movimiento.

* **Barra de inquietud (por cada pez ciego):** sube al moverse cerca de un pez ciego; baja lentamente si el jugador permanece quieto o se aleja.

* **Retroalimentación de estado — el pez emite sonido constante:** clics lentos y espaciados \= tranquilo (no sabe dónde estás); clics rápidos y erráticos \= alterado (te está buscando); chillido agudo continuo \= ubicó tu posición y se dirige hacia ti.

* **Consecuencia:** si la barra llega al máximo y el jugador sigue en movimiento, el pez lo alcanza y lo mata. Si el jugador se congela a tiempo, el pez pierde el rastro y la barra desciende.

Si el usuario se pierde y no sabe dónde está la salida. habrá una medusa brillante que irá rápidamente sin hacer sonido hacia la salida. Vienen cada cierto tiempo o que el usuario está muy alejado de la zona de salida.

La cueva no es recta, tiene curvas o huecos que lleva a otra parte de rocas, es posible que se pierda. Por eso se introducen las medusas guías.

**8\. Acto III — Zona oscura profunda (versión avanzada)**

Reutiliza la mecánica de inquietud del Acto II pero eleva el riesgo. Aquí formalizo tu duda de diseño con una propuesta concreta:

## **8.1 Mecánica mejorada: "Ventana de gracia"**

También hay mesas guía.

&nbsp;

En vez de que la barra de inquietud al máximo mate instantáneamente, se activa una ventana de gracia de 1.5–2 segundos: háptica crítica \+ chillido agudo \+ oscurecimiento de visión periférica. El jugador debe quedarse absolutamente inmóvil de verdad (el juego mide micro-movimientos reales del headset y los mandos, no solo un botón). Si logra la quietud total durante la ventana, el pez pierde el rastro por completo (reset de barra). Si se detecta el más mínimo movimiento, ocurre la muerte instantánea.

## **8.2 Escalada adicional de dificultad**

* Más de un pez ciego activo a la vez, con zonas de detección que se solapan — obliga a planear rutas, no solo reaccionar.

* Corrientes de agua que empujan levemente al jugador, dificultando la quietud perfecta durante la ventana de gracia — refuerza tensión sin agregar un enemigo nuevo.

&nbsp;

# **8.1 Acto IV — Persecución del Bloop Fish**

## **8.1 Cierre del acto — La Zona del Vacío y el regreso del Bloop Fish**

Al salir de la última cámara de peces ciegos, el jugador entra en una zona amplia con luz tenue y una calma total y extraña: sin corrientes, sin sonido de fondo, sin criaturas visibles — la "Zona del Vacío". Es una falsa calma deliberada: tras dos actos de tensión constante por sigilo, este silencio se siente como un respiro, y por eso mismo resulta inquietante. No hay barra de inquietud ni indicador alguno en esta sección: el peligro llega sin previo aviso mecánico.

De pronto, un temblor grave recorre el agua y un sonido gutural comienza muy débil y crece de forma continua en intensidad (no es un pulso de alerta como el de los peces ciegos, sino un crescendo ininterrumpido). El jugador nota que el fondo bajo sus pies deja de verse: es la boca del Bloop Fish, completamente negra, abriéndose y ascendiendo directamente por debajo de él — la misma criatura, ahora revelada en parte, que causó el derrumbe del Acto II.

* **Mecánica: persecución forzada sin sigilo.** Se rompe intencionalmente la regla de "quedarse quieto" aprendida en los Actos II y III — aquí quedarse quieto es la muerte. El jugador debe remar a máxima intensidad (sprint sostenido) en línea recta hacia la salida de la zona, donde comienza la corriente ascendente del Acto IV.

* **Retroalimentación: intensidad de audio como distancia.** No hay barra ni número visible: la cercanía del Bloop Fish se comunica únicamente por el crescendo continuo del sonido grave y por la háptica, que pasa de un temblor casi imperceptible a una vibración máxima sostenida — cuanto más fuerte y más rápido crece, más cerca está.

* **Micro-decisiones durante la huida.** Bloques de roca sueltos por el temblor caen en el trayecto en 2–3 líneas posibles (mismo lenguaje visual de esquive que se reutilizará en la cascada del Acto IV), obligando al jugador a esquivar mientras nada a máxima velocidad —  sin detenerse nunca.

* **Resolución.** El jugador llega a la grieta de salida justo cuando la boca se cierra a centímetros de él (obligatorio en el juego) (breve cámara lenta de un segundo, sin input, para dar peso al escape) y es arrastrado por la corriente que va de derecha a izquierda del mar (simulando que es una corriente horizontal que lo llevará cientos de metros) hacia el Acto IV, va tener unos breves desmayor distorsionando su visión y se apagará todo por un momento para que sienta que fue arrastrado por varios minutos. Si no logra mantener la velocidad o es alcanzado por un bloque de roca que lo detiene, el Bloop Fish lo alcanza: fin del intento.

Función narrativa: esta escena cierra el misterio abierto en el Acto II (qué causó el derrumbe) y marca el quiebre de ritmo entre la tensión "lenta" del sigilo (Actos II–III) y la tensión "veloz" del clímax físico (Acto V), sirviendo de puente entre ambos registros de juego.

# **9\. Acto V — La cascada**

## **9.1 Corriente ascendente y recolección**

Al salir del túnel, una corriente empuja al jugador hacia la superficie. Recolecta comida en una zona de transición antes de llegar a la base de la cascada, donde conviven salmones muertos, exhaustos y luchando — refuerzo visual del costo real de la migración antes del clímax.

## **9.2 Nado a contracorriente**

Objetivo: avanzar contra la corriente con recursos limitados (comida escasa en esta zona). La remada debe ser más intensa y sostenida; el jugador siente físicamente el desgaste, reforzando el tema de sacrificio.

## **9.3 Subida por carriles (nado y saltos)**

Sección estilo endless-runner encarnado: el jugador realiza un gesto físico real de "aleteo". Se mueve entre carriles (3 o más, variable por tramo) en una seccion de horizontal.&nbsp;

Habrá n cascadas, al llegar a una cascada, deberá saltar con los brazos con una forma de clavado pero viendo 45 grados arriba (usuario), en estos saltos no habrá bocas de oso, excepto en la última cascada. Si no llega a poder saltar lo debido por temas de fuerza inicial en su clavado (usuario), deberá de nadar con más velocidad en el aire, simulando que sigue ganando altura, o sino cae en una roca y le baja la vida.

en cada salto, en el aire tendrá pocos segundos de "cámara lenta" perceptual.

| Amenaza | Comportamiento | Aviso al jugador | Forma de evitarla |
| :---- | :---- | :---- | :---- |
| Rocas / troncos | Caen por un carril específico según el tramo | Aviso visual (sombra/salpicadura) \+ audio direccional creciente | Cambiar de carril a tiempo |
| Oso | Mete la garra dentro de la cascada en un carril | Rugido \+ vibración grave localizada en ese lado | Cambiar de carril antes del zarpazo |
| Águila | Avisa con graznido, se lanza en picada a un carril específico y se sumerge para atrapar | Sonido de aviso claro antes de la zambullida (ventana de reacción justa) | Cambiar de carril / agacharse según el tramo |
| Ave picadora | Ataca desde arriba en la posición inmediatamente delante del jugador | Aleteo cercano \+ sombra que cruza rápido | No avanzar al carril delantero anunciado / esquivar con giro de cuerpo |

## **9.4 El salto final — decisión aérea**

en la última cascada un salto real del jugador. El jugador extiende los brazos hacia los lados (gesto físico real) para desplazarse entre carriles y evitar las fauces de osos que esperan en algunos de ellos. Es el clímax de tensión física y de decisión del juego.

* Sugerencia: variar el número de carriles disponibles en cada intento del salto (no fijo en 3\) para que el momento no se sienta memorizable, sino leído en el instante.

# **10\. Epílogo — El desove y el cierre del ciclo**

Tras superar la cascada, transición a una cinemática semi-interactiva: el jugador realiza un gesto físico real (agitar los mandos, como el temblor del desove) que dispara la animación de reproducción. Inmediatamente después, el cuerpo del salmón se debilita y la visión se desvanece — la muerte se presenta sin violencia, como agotamiento natural.

Cierre sugerido: la cámara se separa del cuerpo del salmón (ya inerte, arrastrado suavemente por la corriente) y asciende sobre el lecho del río lleno de huevos. Salto temporal breve. Último plano: un alevín recién nacido abre los ojos en ese mismo lugar — mismo punto de partida del jugador al inicio del juego, cerrando el ciclo sin necesidad de texto explicativo.

Nota de diseño: si el jugador muere antes en cualquier acto (Acto II, III o IV), no se trata como un "game over" tradicional con reintento invisible — se sugiere una breve viñeta contemplativa (el cuerpo se hunde, otros salmones continúan, la vida sigue) antes de reiniciar el acto, reforzando el tema incluso en el fracaso.

# **11\. Balance y curva de dificultad**

| Acto | Presión temporal | Riesgo de muerte | Agencia del jugador |
| :---- | :---- | :---- | :---- |
| I | Ninguna | Nula | Exploración libre |
| II | Baja | Media (evitable con sigilo básico) | Alta (control total del ritmo avance/pausa) |
| III | Media, luego súbitamente alta (huida del Bloop Fish) | Alta (ventana de gracia exige precisión | Media (herramientas de distracción limitadas; en la huida final la única agencia es esquivar y remar) |
| IV | Alta | devorado por el bloop fish o por una roca que te desmaya; la huida final es de reacción pura). |  |
| V | Alta | Alta (reacción rápida \+ resistencia física) | Media (rutas leídas en tiempo real) |

# **12\. Dirección de arte y sonido**

* Paleta Acto I: azules y turquesas saturados — seguridad, la visión será limitada por el agua azulina (igual que el ambiente de neblina de silent hill).

* Paleta Acto II/III: verdes y ocres apagados, bioluminiscencia puntual fría — aislamiento.

* Paleta Acto IV/V: blancos de espuma, grises de piedra mojada, contraluces cálidos del sol filtrándose — urgencia y esperanza.

* Diseño de sonido: prioridad absoluta al audio 3D binaural; la música es mínima y solo aparece en el Acto I y el Epílogo (silencio tenso en II–IV).

* Sin música orquestal genérica de "tensión": preferir texturas orgánicas (percusión de piedra, cuerdas graves, respiración submarina) para no romper la inmersión diegética.

# **13\. Riesgos de producción y mitigaciones**

| Riesgo | Mitigación |
| :---- | :---- |
| Mareo por movimiento (locomoción por remada) | Prototipar temprano con playtesting extenso; ofrecer modo de comodidad con viñeta y velocidad reducida |
| Falsos negativos en detección de quietud (Acto III) | Umbral de tolerancia calibrable; filtrar micro-jitter del sensor antes de contarlo como movimiento |
| Fatiga física en la sección de skipping (Acto IV) | Modo sentado alternativo con gestos de brazos; sesiones de prueba con duración real medida |
| Frustración por muerte instantánea (Acto III) | La "ventana de gracia" da un segundo de agencia; comunicar el estado del pez con claridad sonora constante |

*Fin del documento — v0.2. Documento vivo: iterar tras primeros playtests de los Actos II y III (mecánica núcleo del juego).*