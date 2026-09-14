**PLANIFICACIÓN DE TAREAS — ACTO I**

**ANÁDROMO — Océano abierto (tutorial diegético)**

v0.1 — Complementa al GDD v0.3 y al TDD v0.1 (corresponde a los Días 1-4 del plan de 10 días)

---

## 0. Alcance de este sprint

Cubre todo lo necesario para que el **Acto I** sea jugable de punta a punta: arranque diegético → aprendizaje por imitación del cardumen → alimentación hasta llenar energía → cierre del tutorial. No incluye la estampida de depredadores (eso es el inicio del Acto II, Día 5 del plan general).

**Roles** (mismos que el TDD): **Prog** = Programación/Sistemas · **Arte** = Arte 3D/Entorno · **Audio/Diseño** = Audio + diseño de nivel/producción. "Todos" = tarea conjunta y sincrónica, no se triplica el esfuerzo.

**Tablero sugerido:** columnas `Backlog → En progreso → Revisión → Hecho`, un ticket por ID de esta tabla. Etiquetar cada ticket con el sistema al que pertenece (Locomoción / Energía / Entorno / Cardumen / Audio) para que sea fácil filtrar.

---

## 1. Día 1 — Setup compartido (prerrequisito del Acto I)

| ID | Tarea | Rol | Est. | Depende de | Criterio de aceptación |
| :---- | :---- | :---- | :---- | :---- | :---- |
| T1.1 | Crear proyecto Unity 6 LTS + URP, Single Pass Instanced, XR Plugin Management, OpenXR + Meta XR SDK | Prog | 2h | — | Build corre en Quest 2 vía Link a ≥90 fps en escena vacía |
| T1.2 | Repo Git + Git LFS, `.gitignore` de Unity, estructura de carpetas (sección 2.1 del TDD) | Prog | 1h | — | Estructura pusheada; LFS trackea `.fbx/.png/.wav` |
| T1.3 | Setup de Input System + XR Interaction Toolkit; verificar lectura de posición/velocidad de ambos controladores | Prog | 2h | T1.1 | Velocidad de cada mano visible en debug dentro del casco |
| T1.4 | Moodboard y bloqueo de escala del Acto I (paleta azul/turquesa saturada, niebla tipo Silent Hill) | Arte | 2h | — | Moodboard + escena de referencia de escala compartida |
| T1.5 | Boceto de layout del Acto I: grieta rocosa, zona de coral/kril, punto de inicio del cardumen, recorrido esperado (top-down 2D) | Audio/Diseño | 2h | — | Boceto aprobado por el equipo, usado como referencia en T2.5 |
| T1.6 | Setup de Meta XR Simulator / XR Device Simulator para iterar sin casco físico | Todos | 1h | T1.1 | Los 3 pueden probar cambios básicos sin ponerse el casco |

## 2. Día 2 — Locomoción y blockout inicial

| ID | Tarea | Rol | Est. | Depende de | Criterio de aceptación |
| :---- | :---- | :---- | :---- | :---- | :---- |
| T2.1 | Detección de velocidad/dirección de brazada por controlador (datos crudos) | Prog | 3h | T1.3 | Script expone velocidad de brazada por mano en tiempo real |
| T2.2 | Traducir brazada → impulso de nado (crucero/sprint) + dirección por cabeza/torso (RF-01, RF-02) | Prog | 4h | T2.1 | El jugador se desplaza nadando de forma reconocible en casco |
| T2.3 | Frenado/quietud: soltar brazos detiene el impulso gradualmente (base del sigilo del Acto II) | Prog | 2h | T2.2 | El jugador puede quedarse quieto de forma natural |
| T2.4 | Viñeta de confort dinámica ligada a la aceleración del jugador (RF-13 / RNF-03) | Prog | 2h | T2.2 | Viñeta aparece/desaparece según velocidad; toggle en config |
| T2.5 | Blockout gris de la grieta rocosa dividida y la zona de coral/kril (volúmenes simples) | Arte | 4h | T1.5 | Blockout importado a `Act1_OpenOcean`, escalado respecto al salmón |
| T2.6 | Niebla submarina + iluminación base (paleta azul/turquesa) en URP | Arte | 3h | T2.5 | La escena blockout transmite la profundidad limitada del GDD |
| T2.7 | Colocar marcadores/placeholder de spawn: cardumen, coral, kril, alevines, medusa inofensiva, según T1.5 | Audio/Diseño | 3h | T2.5 | Escena con todos los puntos clave marcados |
| T2.8 | Playtest interno rápido de la locomoción (15 min en casco) | Todos | 1h | T2.2 | Lista corta de ajustes de sensación para el Día 3 |

## 3. Día 3 — Energía, alimentación e inicio diegético

| ID | Tarea | Rol | Est. | Depende de | Criterio de aceptación |
| :---- | :---- | :---- | :---- | :---- | :---- |
| T3.1 | Sistema de energía/hambre (0-100) con evento `OnEnergyChanged` (arquitectura por eventos) | Prog | 3h | — | Energía decae con tiempo/esfuerzo y es modificable desde código |
| T3.2 | Shader/post-proceso de saturación y contraste ligado a la energía + visión periférica reducida en hambre crítica (RF-03) | Prog | 3h | T3.1 | Cambio visual perceptible en casco a distintos niveles de energía |
| T3.3 | Latido cardíaco ligado a energía, en bus "Jugador" del mixer | Prog | 2h | T3.1 | El latido se acelera al bajar la energía |
| T3.4 | Mecánica de alimentación: colisión/proximidad con kril/alevines, incremento de energía (RF-04) | Prog | 3h | T3.1, T2.7 | Comer 3-4 presas llena la energía al 100% |
| T3.5 | Flujo de inicio diegético: el jugador arranca ya nadando junto al cardumen, sin menú (RF-11) | Prog | 2h | T2.2 | Al lanzar el build, el jugador aparece nadando sin pantallas intermedias |
| T3.6 | Modelar/importar pez del cardumen (baja poli, apto para instancing) y pez guía | Arte | 4h | — | Al menos 1 variante de cardumen + 1 pez guía con material básico |
| T3.7 | Modelar/importar kril, alevines y coral con shader de brillo (glow) para guiar la atención sin HUD | Arte | 4h | — | Presas visualmente distinguibles del entorno ya iluminado |
| T3.8 | Diseñar y colocar la capa de audio ambiental del Acto I: música mínima + ambiente submarino calmo | Audio/Diseño | 3h | T2.6 | Ambiente sonoro reproduciéndose en la escena, música sutil integrada |

## 4. Día 4 — Cardumen, medusa, pulido e integración

| ID | Tarea | Rol | Est. | Depende de | Criterio de aceptación |
| :---- | :---- | :---- | :---- | :---- | :---- |
| T4.1 | Comportamiento de banco/flocking simple del cardumen (formación + reacción al jugador) | Prog | 4h | T3.6 | El cardumen nada en formación de forma creíble alrededor del jugador |
| T4.2 | Comportamiento del pez guía: modela nadar en formación, bucear a comer y esquivar medusa (enseñanza sin texto) | Prog | 3h | T4.1 | Las 3 acciones se ejecutan de forma visible y con timing legible |
| T4.3 | Comportamiento de medusa inofensiva (trayectoria simple, sin daño, enseña el gesto de esquivar) | Prog | 2h | — | Medusa presente y esquivable, sin penalización si se toca |
| T4.4 | Pulido final de materiales/iluminación del Acto I (coherente con dirección de arte del GDD) | Arte | 3h | T2.6, T3.7 | Escena con look final aprobado, no placeholder gris |
| T4.5 | Integración final del audio del Acto I en un snapshot de mixer propio | Audio/Diseño | 2h | T3.8 | Snapshot "Acto I" mezclado y listo para conectar con el Acto II |
| T4.6 | Profiling y optimización del Acto I completo en casco real (draw calls, batching, LODs) | Prog | 3h | T4.1, T4.4 | fps medido y documentado; ajustes aplicados si está por debajo de 90 fps |
| T4.7 | Playtesting interno #0 del Acto I completo (informal) | Todos | 2h | T4.6 | Lista de bugs/ajustes menores registrada en el tablero |
| T4.8 | Cierre de sprint: demo interna de punta a punta y decisión go/no-go para el Acto II | Todos | 1h | T4.7 | Acto I jugable de inicio a fin sin bugs bloqueantes |

---

## 5. Balance de carga por rol

| Rol | Día 1 | Día 2 | Día 3 | Día 4 | Total (4 días) | Capacidad (32h) |
| :---- | :---- | :---- | :---- | :---- | :---- | :---- |
| Prog | 5h | 11h | 13h | 12h | **41h** | Sobrecargado (+9h, ~28%) |
| Arte | 2h | 7h | 8h | 3h | **20h** | Con holgura (-12h) |
| Audio/Diseño | 2h | 3h | 3h | 2h | **10h** | Con holgura (-22h) |

Esta tabla confirma el riesgo que ya señalaba el TDD (sección 14): **Prog es el cuello de botella real del Acto I**, mientras Arte y Audio/Diseño tienen margen. Mitigaciones concretas para este sprint:

- **Repartir con apoyo de IA:** tareas mecánicas de Prog con bajo riesgo de diseño (T2.4 viñeta, T3.3 latido cardíaco, T4.3 medusa) pueden ejecutarse por Arte o Audio/Diseño con un agente de codificación guiando el script, bajo revisión final de Prog. Esto reduce el total real de Prog en ~6-7h sin tocar las tareas núcleo (locomoción, energía, cardumen).
- **Simplificar V1 del flocking (T4.1):** usar un comportamiento de banco básico (offsets de formación + suavizado), no un sistema de boids completo; el pulido de comportamiento se deja para un sprint posterior, ya que el Acto I no depende de que el cardumen sea perfecto, solo creíble.
- **Colchón:** si el Día 3 se atrasa, el margen de Arte/Audio en el Día 4 permite absorber parte del trabajo de integración sin mover la fecha de cierre del Acto I.

## 6. Definición de "hecho" del Acto I (checklist)

- [ ] El jugador arranca la partida ya nadando junto a su cardumen, sin ningún menú tradicional.
- [ ] La locomoción por remada física funciona en crucero, sprint y quietud, con dirección por cabeza/torso.
- [ ] El hambre/energía se comunica solo por saturación de visión + latido cardíaco, sin HUD.
- [ ] Comer 3-4 presas brillantes llena la energía y cierra el tutorial.
- [ ] El pez guía enseña, por imitación, a nadar en formación, comer y esquivar la medusa.
- [ ] La escena sostiene ≥90 fps medidos en el casco vía Link con la RTX 5060.
- [ ] No hay bugs bloqueantes registrados en el playtest del Día 4.

## 7. Riesgos específicos de esta fase

| Riesgo | Mitigación |
| :---- | :---- |
| La detección de brazada se siente "rara" o imprecisa en el casco (solo se valida bien en playtest, no en editor) | Playtest corto ya el Día 2 (T2.8), no esperar al Día 9 del plan general para detectar el problema |
| El comportamiento de banco (T4.1) toma más de lo estimado por ser IA de movimiento en grupo | Empezar simple (ver mitigación de la sección 5) y solo sofisticar si sobra tiempo el Día 4 |
| La detección de "comer" por colisión se siente poco confiable a corta distancia en VR | Usar un radio de detección generoso (esfera de trigger) en vez de colisión exacta de malla, y dar feedback inmediato (partícula/sonido) aunque el contacto no sea perfecto |
| Prog sigue sobrecargado pese a las mitigaciones | Recortar alcance de pulido visual (T4.4) antes que recortar cualquier tarea núcleo de Prog — el Acto I puede lanzarse con arte "suficiente", no con locomoción a medias |

---

*Fin del documento — v0.1. Complementa al GDD v0.3 y al TDD v0.1 de Anádromo.*
