**DOCUMENTO TÉCNICO DE ARQUITECTURA Y PLANIFICACIÓN (TDD)**

**ANÁDROMO — VR / Unity / Quest 2 vía Link**

v0.1 — Complementa al GDD v0.3

---

## 0. Supuestos de partida

Antes de planificar, dejo explícitos los supuestos que tomé porque no estaban definidos en tu mensaje. Si alguno no aplica, se ajusta fácil:

- **Alcance de los 10 días:** el GDD completo (5 actos + epílogo, ~35-50 min de contenido final) **no es viable en 10 días con 3 personas**, ni siquiera con apoyo de IA. Por eso planifico los 10 días como un **vertical slice / prototipo jugable**: mecánica núcleo completa (nado, hambre, háptica, sigilo por peces ciegos) + Acto I completo + primera mitad del Acto II (estampida + entrada al túnel). Esto es lo estándar para validar el core loop antes de escalar al resto del contenido.
- **Roles del equipo (no especificaste):** asumo 1 perfil de **programación/sistemas**, 1 perfil de **arte 3D/entorno**, y 1 perfil de **audio + diseño de niveles/producción**. Los tres usan agentes de IA como "cuarto y quinto par de manos" para tareas mecánicas (boilerplate de código, wiring de escenas, ajuste de datos).
- **Motor:** Unity 6 LTS (o la LTS más reciente disponible al iniciar), con **URP** (Universal Render Pipeline) — es el pipeline recomendado para VR por rendimiento.
- **Modelo de renderizado:** al ser Quest 2 conectado por cable USB-C (Oculus/Meta Quest Link), **todo el renderizado ocurre en la PC** (RTX 5060) y el casco solo actúa como pantalla + tracking + input. Es decir, técnicamente es un **build PCVR (.exe para Windows)**, no un APK standalone de Quest. Esto es una gran ventaja de presupuesto gráfico frente a un build standalone.

---

## 1. Stack tecnológico

| Componente | Elección | Motivo |
| :---- | :---- | :---- |
| Motor | Unity 6 LTS | URP maduro, soporte OpenXR de primera clase, mejor tooling de profiling para VR |
| Render pipeline | URP (Universal Render Pipeline), Single Pass Instanced (stereo) | Reduce a la mitad las draw calls de cámara vs. multi-pass; imprescindible para 90-120 fps |
| Runtime VR | OpenXR (plugin oficial de Unity) + Meta XR SDK (antes "Oculus Integration") como capa de features específicas de Meta | OpenXR es el estándar; Meta XR SDK aporta extras (hápticos avanzados, passthrough si se necesitara, debug tools) |
| Input | Unity Input System (nuevo) + XR Interaction Toolkit para poses/velocidad de controladores | Necesario para leer velocidad/aceleración de la brazada de remada |
| Audio 3D | Meta XR Audio SDK o Steam Audio (gratuito, ambos con HRTF binaural + oclusión geométrica) | Oclusión y reverberación geométrica en cuevas es un requisito de diseño explícito (sección 5.4/5.3 del GDD) |
| Control de versiones | Git + Git LFS (assets binarios: modelos, texturas, audio) | Equipo de 3 + IA trabajando en paralelo sobre el mismo repo |
| Perfilado | Unity Profiler, Meta Quest Developer Hub + OVR Metrics Tool, RenderDoc puntual | Medir fps/ms real en el casco vía Link, no solo en el editor |
| Iteración sin casco | Meta XR Simulator / XR Device Simulator de Unity | Permite a los 3 miembros iterar sin turnarse el único casco físico — crítico en 10 días |

### 1.1 Presupuesto de rendimiento (frame budget)

| Objetivo | Tiempo de frame disponible | Nota |
| :---- | :---- | :---- |
| 90 fps (mínimo aceptable) | 11.1 ms/frame | Quest 2 soporta 90 Hz de forma oficial y estable |
| 120 fps (objetivo estirado) | 8.3 ms/frame | 120 Hz en Quest 2 es un modo **experimental** (requiere habilitarlo vía Meta Quest Link / Oculus Debug Tool); no darlo por garantizado en planificación, tratarlo como *stretch goal*, no como requisito duro |

Recomendación práctica: diseñar y perfilar contra el presupuesto de **90 fps** como línea base innegociable (es la que garantiza confort en VR — por debajo de 72-80 fps sostenidos aumenta fuerte el riesgo de mareo), y usar cualquier margen sobrante para acercarse a 120 fps.

---

## 2. Arquitectura del proyecto

### 2.1 Estructura de carpetas (Assets/)

```
Assets/
  _Project/
    Scripts/
      Locomotion/        (nado por remada, dirección, comfort/viñeta)
      Systems/           (EnergySystem, HapticThreatSystem, ActStateMachine)
      AI/                (StealthFishAI, PredatorBehaviours, SchoolingFlock)
      Audio/             (AudioLayerManager, OcclusionBridge, MixerSnapshots)
      UI/                (mínimo, solo diegético: subtítulos direccionales)
      Editor/            (herramientas propias + scripts que usen los agentes de IA para wiring de escenas)
    Prefabs/
      Characters/
      Creatures/
      Environment/
      VFX/
    Art/
      Models/
      Materials/
      Textures/
    Audio/
      Ambient/
      Creatures/
      Music/
      SFX/
    ScriptableObjects/
      CreatureConfigs/   (umbrales de detección, velocidades, daño — ajustables sin tocar código)
      ActConfigs/        (parámetros de cada acto: duración, spawns, dificultad)
    Scenes/
      Act1_OpenOcean.unity
      Act2_Collapse.unity
      Act3_DarkZone.unity
      Act4_Chase.unity
      Act5_Waterfall.unity
      Epilogue.unity
      _Bootstrap.unity   (carga aditiva del acto correspondiente + persistente)
```

### 2.2 Patrones de arquitectura

- **Máquina de estados de actos (Act State Machine):** un estado por acto (`TutorialState`, `CollapseState`, `StealthState`, `ChaseState`, `WaterfallState`, `EpilogueState`), cada uno controla qué sistemas están activos. Facilita que la IA agente trabaje "por acto" sin tocar el resto.
- **Comunicación desacoplada por eventos (ScriptableObject Events / `UnityEvent` channels):** por ejemplo, `OnThreatLevelChanged`, `OnEnergyChanged`, `OnGraceWindowStart`. Así el sistema de háptica, el de audio y el de shading de visión reaccionan al mismo evento sin conocerse entre sí — importante porque distintas personas (y agentes) tocan cada sistema en paralelo.
- **Datos en ScriptableObjects, no hardcodeados:** velocidades, radios de detección, duración de la ventana de gracia, etc. viven en assets de datos editables desde el Inspector. Esto es clave para que el balance (sección 12 del GDD) se ajuste sin recompilar y sin que un agente de IA tenga que tocar lógica de código para cambiar un número.
- **Carga aditiva de escenas por acto:** cada acto es una escena separada cargada de forma aditiva sobre una escena `_Bootstrap` persistente (jugador, sistemas globales). Permite streaming sin pantallas de carga y que cada persona/agente trabaje en su acto sin conflictos de merge en una escena gigante compartida.
- **FSM simple de criatura amenazante** (reutilizable para pez ciego, oso, águila): estados `Calma → Alerta → Crítico → (Perdido | Contacto)`, parametrizada por `CreatureConfig` (ScriptableObject). Un solo sistema cubre varios enemigos del GDD.

---

## 3. Requisitos funcionales (RF)

| ID | Requisito |
| :---- | :---- |
| RF-01 | El sistema debe traducir la velocidad y dirección de la brazada de ambos controladores en desplazamiento de nado (sin stick). |
| RF-02 | La dirección de nado se determina por la orientación de cabeza/torso del jugador, no por los controladores. |
| RF-03 | El hambre/energía se representa solo con saturación/contraste de visión y un latido cardíaco audible — sin HUD. |
| RF-04 | El jugador recupera energía al entrar en contacto con presas "brillantes" (kril, alevines) cercanas. |
| RF-05 | La háptica debe escalar en 3 niveles (Calma / Alerta / Crítico) en función del estado de detección de la amenaza activa. |
| RF-06 | Cada pez ciego mantiene una "barra de inquietud" individual que sube con movimiento cercano y baja con quietud/distancia. |
| RF-07 | El sistema debe detectar quietud *real* (micro-movimiento de headset + controladores bajo un umbral configurable) para resolver la "ventana de gracia". |
| RF-08 | La persecución del Bloop Fish debe comunicar cercanía solo vía crescendo de audio + háptica continua, sin barra visible. |
| RF-09 | El sistema de carriles de la cascada debe detectar el gesto de salto/clavado y el cambio de carril por movimiento de brazos. |
| RF-10 | Al morir, el sistema reproduce una viñeta contemplativa (sin "game over" tradicional) y reinicia el acto, no la partida completa. |
| RF-11 | El menú principal es diegético: la partida inicia ya "dentro" del Acto I, sin pantallas de menú tradicionales. |
| RF-12 | Las fuentes de audio de criaturas y ambiente deben aplicar oclusión geométrica y atenuación 3D binaural. |
| RF-13 | Deben existir opciones de confort accesibles (viñeta dinámica, modo sentado, subtítulos direccionales). |
| RF-14 | La transición entre actos se resuelve por carga aditiva de escena, sin pantallas de carga visibles al jugador. |

## 4. Requisitos no funcionales (RNF)

| ID | Requisito |
| :---- | :---- |
| RNF-01 | Rendimiento mínimo de 90 fps (≤11.1 ms/frame) sostenidos en Quest 2 vía Link con RTX 5060; 120 fps como objetivo estirado, no garantizado. |
| RNF-02 | Latencia motion-to-photon objetivo <20 ms (cable USB-C mínimo 3.0/3.1, evitar hubs USB intermedios). |
| RNF-03 | Cero locomoción artificial por stick; toda la locomoción es física, con opciones de viñeta y modo sentado para minimizar cinetosis. |
| RNF-04 | Estabilidad: sesión continua de 35-50 min sin fugas de memoria ni caídas (validar con Memory Profiler). |
| RNF-05 | Compatibilidad: Windows 10/11 de 64 bits, GPU RTX 5060, Quest 2 con firmware actualizado, cable Link certificado. |
| RNF-06 | Presupuesto de escena: definir techos de draw calls y triángulos visibles por frame desde el prototipo (a calibrar con profiling real; no asumir cifras sin medir en el hardware objetivo). |
| RNF-07 | Arquitectura modular (eventos + ScriptableObjects) que permita a 3 personas + agentes de IA trabajar en paralelo sin bloquearse. |
| RNF-08 | Accesibilidad: subtítulos direccionales, modo sentado, redundancia audio-háptica (ningún aviso depende de un solo canal sensorial). |
| RNF-09 | Trazabilidad: Git + Git LFS, commits por sistema/feature, sin binarios pesados fuera de LFS. |
| RNF-10 | Portabilidad futura: aislar la capa de input tras OpenXR para no acoplar la lógica de juego a APIs específicas de Meta, por si más adelante se evalúa un build standalone. |

## 5. Historias de usuario (backlog priorizado)

Prioridad: **P0** = necesario para el vertical slice de 10 días · **P1** = resto del Acto II/III · **P2** = Actos IV/V + epílogo (siguientes sprints).

| ID | Prioridad | Historia |
| :---- | :---- | :---- |
| US-01 | P0 | Como jugador, quiero nadar remando con mis propios brazos, para sentir que soy físicamente el salmón. |
| US-02 | P0 | Como jugador, quiero notar mi hambre por cambios visuales/sonoros, para mantenerme inmerso sin HUD. |
| US-03 | P0 | Como jugador, quiero comer presas brillantes cercanas, para recuperar energía de forma clara. |
| US-04 | P0 | Como jugador, quiero sentir háptica progresiva cuando una amenaza se acerca, para reaccionar antes de verla. |
| US-05 | P0 | Como jugador, quiero que quedarme quieto reduzca mi detección frente a un pez ciego, para que el sigilo se sienta justo. |
| US-06 | P0 | Como jugador, quiero una "ventana de gracia" antes de morir por detección, para tener una última oportunidad de agencia. |
| US-07 | P0 | Como jugador, quiero que el juego inicie ya nadando con mi cardumen, sin menús que rompan la inmersión. |
| US-08 | P1 | Como jugador, quiero que una estampida de depredadores disperse a mi cardumen, para sentir el quiebre narrativo del Acto II. |
| US-09 | P1 | Como jugador, quiero que una medusa me guíe si me pierdo en el túnel, para no sentirme frustrado por la navegación. |
| US-10 | P1 | Como jugador, quiero poder sacudirme físicamente una lamprea adherida, para resolver esa amenaza con mi cuerpo. |
| US-11 | P2 | Como jugador, quiero huir del Bloop Fish sin sigilo, guiado solo por audio y háptica crecientes, para vivir el clímax de persecución. |
| US-12 | P2 | Como jugador, quiero saltar cascadas y cambiar de carril con mis brazos para esquivar osos, águilas y rocas. |
| US-13 | P2 | Como jugador, quiero vivir el desove y la muerte de mi salmón como un cierre catártico, no como un fracaso. |
| US-14 | P0/P1/P2 (transversal) | Como jugador con posible fatiga física o movilidad reducida, quiero un modo sentado, para poder completar la experiencia cómodamente. |

---

## 6. Personajes

### 6.1 Personaje principal

| Elemento | Detalle |
| :---- | :---- |
| Salmón protagonista | Malla completa (necesaria para el plano final del epílogo y reflejos en el agua) + rig de aletas pectorales visibles en primera persona ("manos" del jugador) |
| Animaciones necesarias | Nado crucero, nado sprint, quietud/flotar, comer, sacudida (lamprea), impulso de salto/clavado, temblor de desove, desvanecimiento/muerte |

### 6.2 Personajes secundarios (no amenazantes)

| Personaje | Función | Acto |
| :---- | :---- | :---- |
| Cardumen (NPC banco de peces) | Compañía visual, aprendizaje colectivo, se dispersan en el colapso | I, II |
| 3 peces guía | Señalan la ruta de escape hacia la cueva | II |
| Medusa bioluminiscente guía | Guía silenciosa si el jugador se pierde | II, III |
| Salmones moribundos/exhaustos | Set dressing narrativo en la base de la cascada | V |
| Alevín recién nacido | Plano de cierre del ciclo | Epílogo |

### 6.3 Enemigos / NPCs de amenaza

| Enemigo | Comportamiento clave | Acto | Complejidad de IA |
| :---- | :---- | :---- | :---- |
| Orcas / tiburones | Paso scripteado, no letal para el jugador, empuje físico | II | Baja (trayectoria + colisión de empuje) |
| Pez ciego | Detección por vibración/movimiento, 3 estados (tranquilo/alterado/persiguiendo), "ventana de gracia" | II, III | Media-alta (mecánica núcleo) |
| Pirañas | Amenaza menor, esquivable | II | Baja |
| Lamprea marina | Se adhiere, requiere gesto físico de sacudida | II | Baja-media (detección de adhesión + input de sacudida) |
| Bloop Fish | Antagonista recurrente: revelación (Acto II) + persecución (Acto IV) | II, IV | Media (más dirección de cámara/audio que IA compleja; rara vez se ve completo) |
| Oso | Zarpazo por carril en la cascada | V | Baja (patrón por carril) |
| Águila | Picada con ventana de aviso | V | Media (timing + trayectoria de picada) |
| Ave picadora | Ataque rápido frontal | V | Baja |

---

## 7. Listado de objetos 3D (assets)

| Categoría | Objeto | Acto(s) | Prioridad |
| :---- | :---- | :---- | :---- |
| Personaje | Salmón (cuerpo completo + aletas 1ª persona) | Todos | P0 |
| Personaje | Alevín recién nacido | Epílogo | P2 |
| Criatura | Pez del cardumen (variante baja poli, instanciable) | I, II | P0 |
| Criatura | Pez guía | II | P1 |
| Criatura | Medusa bioluminiscente | II, III | P1 |
| Criatura | Pez ciego | II, III | P0 |
| Criatura | Piraña | II | P1 |
| Criatura | Lamprea marina | II | P1 |
| Criatura | Orca / tiburón | II | P1 |
| Criatura | Bloop Fish (boca, ojo, silueta de cuerpo — geometría parcial) | II, IV | P1 |
| Criatura | Oso (garra + boca, geometría parcial) | V | P2 |
| Criatura | Águila | V | P2 |
| Criatura | Ave picadora | V | P2 |
| Entorno | Kril / plancton (partícula o mesh instanciado) | I | P0 |
| Entorno | Coral (variantes) | I | P0 |
| Entorno | Rocas (variantes, incl. rocas de derrumbe) | I-V | P0 |
| Entorno | Anguilas/plantas bioluminiscentes (fuente de luz ambiental) | II, III | P1 |
| Entorno | Troncos flotantes | V | P2 |
| Entorno | Formaciones de roca de cascada | V | P2 |
| Entorno | Lecho de grava / nido de desove | Epílogo | P2 |
| Entorno | Cadáveres de salmón (set dressing) | V | P2 |
| VFX | Burbujas / partículas de sedimento | Todos | P0 |
| VFX | Salpicaduras / ondas de impacto | II, V | P1 |
| VFX | Huevos de desove | Epílogo | P2 |

---

## 8. Terreno y entornos

### 8.1 Tipos de terreno (a nivel motor)

| Tipo | Uso | Herramienta recomendada |
| :---- | :---- | :---- |
| Terreno abierto | Suelo oceánico arenoso del Acto I | Unity Terrain + Terrain Layers (arena, roca dispersa) |
| Mallas esculpidas a mano | Túneles/cuevas de los Actos II-III | Modelado externo (Blender) importado como mesh — mejor control para occlusion culling por celdas que el componente Terrain |
| Terreno mixto río/cascada | Lecho del río y caídas de agua del Acto V | Unity Terrain (lecho) + mallas de roca a medida (cascada, carriles) |
| Mallas de baja complejidad | Lecho de grava del epílogo | Mesh simple, bajo presupuesto |

### 8.2 Zonas de terreno por acto

- **Acto I:** llanura oceánica abierta, grieta rocosa dividida (umbral hacia la profundidad), parche de coral/kril.
- **Acto II:** zona de colapso (rocas desprendiéndose), túnel de entrada a la cueva, túnel bioluminiscente sinuoso con curvas y bifurcaciones.
- **Acto III:** cámaras de oscuridad total con múltiples peces ciegos y zonas de detección solapadas.
- **Acto IV:** "Zona del Vacío" (amplia, vacía, sin corrientes) y corredor de huida con bloques de roca desprendibles en 2-3 líneas.
- **Acto V:** zona de transición/recolección, tramo de nado a contracorriente, sucesión de "n" cascadas con carriles (3+), base de cascada con set dressing de salmones caídos.
- **Epílogo:** lecho de grava del desove.

---

## 9. Oclusión

### 9.1 Oclusión visual (rendimiento)

| Técnica | Aplicación |
| :---- | :---- |
| Occlusion Culling (bakeado, Unity) | Especialmente en el sistema de cuevas/túneles (Actos II-III), definido por celdas/`Occlusion Area` |
| Frustum culling | Automático, todo el juego |
| Culling por niebla/distancia | La niebla submarina (pilar de diseño) se aprovecha como "LOD gratis": reducir o descartar detalle más allá de la distancia de visibilidad |
| LOD Groups | Criaturas y props repetidos (3-4 niveles) |
| GPU Instancing | Kril, coral, rocas repetidas |
| Fixed Foveated Rendering | Evaluar disponibilidad vía Meta XR SDK en modo Link; usarla si está disponible para liberar presupuesto de GPU en la periferia |
| Application SpaceWarp / motion smoothing del runtime | Tratarla como red de seguridad para sostener el frame rate objetivo, no como solución de diseño primaria — validar disponibilidad real en modo Link antes de depender de ella |

### 9.2 Oclusión de audio

| Técnica | Aplicación |
| :---- | :---- |
| Oclusión geométrica por raycast (Steam Audio / Meta XR Audio) | Sonidos de criaturas detrás de rocas se oyen amortiguados (filtro pasa-bajos + atenuación) — crítico para que el sigilo del pez ciego se pueda "leer" por oído |
| Reverb zones / room modeling | Reverberación distinta por espacio: océano abierto (difusa, amplia), cueva (cerrada, "slap-back"), cascada (densa, ruido blanco) |

---

## 10. Sonido 3D ambiental (capas/planos)

| Capa | Contenido | Comportamiento |
| :---- | :---- | :---- |
| Fondo profundo | Drone grave, clics lejanos de orcas | Loop, filtrado adaptable según acto |
| Corriente/agua | Burbujas, flujo de corriente | Reacciona a la velocidad del jugador |
| Criatura próxima | Vocalizaciones posicionales (pez ciego, lamprea, Bloop Fish) | Fuentes puntuales 3D con oclusión geométrica |
| Jugador | Latido cardíaco, respiración/branquias | Ligada al estado de energía (sección 5.2 del GDD) |
| Eventos | Stingers: derrumbe, rugido del Bloop Fish, chillido de ventana de gracia | Disparados por evento, prioridad alta en el mixer |
| Música | Mínima, solo Acto I y Epílogo | Silencio tenso en Actos II-IV por diseño |

Cada capa se enruta a un bus propio del Audio Mixer (Ambiente, Criaturas, Jugador, Eventos, Música), con **snapshots por acto** para transiciones suaves al cambiar de escena.

---

## 11. Uso de IA/agentes en el desarrollo

Con 3 personas y 10 días, los agentes de IA rinden más como **aceleradores de tareas mecánicas y repetitivas** que como reemplazo de las decisiones de diseño/arte. Sugerencia de reparto:

- **Código de sistemas (C#):** un agente de codificación (p. ej. Claude Code) trabajando sobre el repo para: boilerplate de los sistemas descritos en la sección 2 (locomoción, energía, háptica, FSM de amenaza), scripts de Editor para automatizar tareas repetitivas, y ajustes guiados por los ScriptableObjects de configuración — el programador humano revisa y dirige, no escribe cada línea desde cero.
- **Wiring/ensamblado de escenas:** scripts de Editor (generados con ayuda del agente) que colocan prefabs, configuran triggers y conectan referencias automáticamente a partir de una lista de datos (por ejemplo, un CSV/ScriptableObject de "qué criatura va en qué punto del túnel"), en vez de arrastrar objetos a mano uno por uno.
- **Música y SFX:** herramientas de generación asistida por IA son útiles para **placeholders rápidos** que permiten probar el ritmo y la mezcla de las capas de audio sin esperar assets finales — pero conviene revisar las condiciones de licencia de cada herramienta antes de usar cualquier resultado en una versión que se vaya a publicar o mostrar públicamente.
- **Objetos y estados:** los agentes ayudan a generar variaciones de configuración de datos (por ejemplo, distintos `CreatureConfig` para calibrar dificultad) y a mantener consistente la máquina de estados de actos a medida que se agregan actos nuevos.
- **Punto de control humano:** decisiones de sensación de juego (cuánta háptica es "demasiada", si un salto se siente justo) se validan siempre con playtesting humano en el casco — ningún agente puede evaluar eso por su cuenta.

---

## 12. Otros requerimientos de preproducción

- **Control de versiones:** Git + Git LFS, rama `main` protegida, ramas por sistema/feature, convención de commits (p. ej. `feat(locomotion): ...`).
- **Gestión de tareas:** tablero simple (Trello/Notion/GitHub Projects) con el backlog de la sección 5, revisado a diario dado lo corto del sprint.
- **Convenciones de assets:** nomenclatura y estructura de carpetas fijadas el día 1 (sección 2.1), ajustes de importación estándar (compresión de texturas, mallas optimizadas) para no perder tiempo re-normalizando assets a mitad del sprint.
- **Hardware de prueba:** al menos 1 Quest 2 + cable Link certificado (USB-C 3.0 o superior) + la PC con RTX 5060 disponibles para pruebas diarias en casco real, no solo en editor.
- **QA/Playtesting:** al menos 2 sesiones de playtesting internas dentro de los 10 días (ver plan, sección 13), con foco en confort VR (cinetosis) y legibilidad de las señales de peligro sin HUD.
- **Documentación viva:** este documento y el GDD se actualizan tras cada sesión de playtesting, igual que indica la nota de cierre del GDD.

---

## 13. Planificación de 10 días (vertical slice)

Cronograma de 10 días hábiles, ~8h/día. Alcance: mecánica núcleo + Acto I completo + primera mitad del Acto II (estampida + entrada al túnel).

| Día | Foco principal | Entregable del día |
| :---- | :---- | :---- |
| 1 | Setup del proyecto: Unity + URP, paquetes XR/OpenXR/Meta XR SDK, repo Git+LFS, escena de prueba, validar 90 fps en escena vacía sobre RTX 5060 vía Link | Proyecto base ejecutándose en el casco con fps objetivo confirmado |
| 2 | Prototipo de locomoción: detección de remada por velocidad/dirección de controladores, dirección por cabeza/torso, viñeta de confort | Nado funcional probado en casco |
| 3 | Sistema de energía/hambre (shader de saturación + latido cardíaco) y arranque diegético (el jugador aparece ya nadando en el cardumen) | Ciclo de hambre + inicio diegético jugables |
| 4 | Blockout de entorno del Acto I (llanura abierta, grieta rocosa, coral/kril) + cardumen NPC con comportamiento de banco (flocking) | Acto I recorrible con cardumen |
| 5 | Secuencia de estampida (Acto II): orcas/tiburones scripteados, dispersión del cardumen, peces guía, temporizador de escape hacia la cueva | Estampida jugable de principio a fin |
| 6 | Túnel + mecánica núcleo de sigilo: pez ciego, barra de inquietud, estados de detección, occlusion culling del sistema de cuevas | Primer encuentro con pez ciego jugable |
| 7 | Háptica de 3 niveles integrada a todos los sistemas anteriores + audio 3D con oclusión geométrica (Steam Audio/Meta XR Audio) | Retroalimentación sensorial completa (háptica + audio) en la mecánica de sigilo |
| 8 | Pase de arte y optimización: materiales, bioluminiscencia, partículas, profiling real en casco (draw calls, batching, LODs) para sostener 90 fps | Build optimizado, fps medido y documentado |
| 9 | Playtesting interno #1: sesiones completas del vertical slice, registro de bugs y de sensaciones de confort/legibilidad | Lista priorizada de ajustes |
| 10 | Corrección de bugs críticos, ajustes de balance según playtesting, build final del vertical slice + backlog priorizado para el siguiente sprint | Vertical slice presentable + plan de siguiente sprint |

**Nota:** los días 6-7 (mecánica núcleo de sigilo) son el punto de mayor riesgo del cronograma — si se atrasan, es preferible recortar alcance de arte (día 8) antes que recortar tiempo de esta mecánica, porque es el pilar central del juego (pilar de diseño #2 y #3 del GDD).

---

## 14. Riesgos técnicos adicionales (complementa la sección 14 del GDD)

| Riesgo | Mitigación |
| :---- | :---- |
| No alcanzar 90 fps estables en el hardware real | Medir en casco desde el día 1 (no solo en editor); presupuestar draw calls/triángulos temprano en vez de optimizar al final |
| 120 fps experimental no disponible o inestable | Tratarlo siempre como stretch goal; el compromiso de diseño y de RNF es 90 fps |
| Cuello de botella en el único perfil de programación para 3 sistemas complejos en paralelo (locomoción, sigilo, háptica) | Usar el agente de codificación para boilerplate y dejar al programador humano el diseño de cada sistema y la revisión final |
| Fricción de merge en escenas compartidas | Carga aditiva por acto (sección 2.2) para que cada persona/agente trabaje en su propia escena |
| Dependencia de un solo casco físico para validar sensaciones VR | Usar el simulador de XR (Meta XR Simulator / XR Device Simulator) para iteración diaria, reservando el casco físico para validación de confort y sensación real |

---

*Fin del documento — v0.1. Complementa al GDD v0.3 de Anádromo.*
