# Plan de Trabajo — Locomoción por Aleteo VR + Cámara Externa

**ANÁDROMO — Sistema de nado por aleteo con los brazos**

v0.1 — Complementa al GDD v0.3 y al TDD v0.1

---

## 0. Contexto y objetivo

El jugador **es** un salmón en primera persona. El movimiento se controla mediante **aleteos verticales de los brazos** (detectados por los controladores del Oculus Quest 2), y la vista se controla completamente con el headset VR. Adicionalmente, se integrará una **cámara externa** (webcam u otra) para funcionalidades futuras (posiblemente tracking de cuerpo completo o supervisión).

### Mecánica de aleteo (resumen)

| Acción del jugador | Resultado en el juego |
|:---|:---|
| Aleteo brazo **izquierdo** solamente | El salmón **rota hacia la derecha** (yaw +) |
| Aleteo brazo **derecho** solamente | El salmón **rota hacia la izquierda** (yaw -) |
| Aleteo de **ambos brazos** simultáneamente | El salmón **avanza hacia adelante** (en la dirección frontal del cuerpo) |
| Girar la cabeza (headset) | La **vista/cámara** gira (mirar alrededor), pero NO cambia la dirección de avance del cuerpo |
| Quedarse quieto | El salmón se detiene gradualmente (frenado/drag) |

### Principios clave de diseño

1. **La vista la controla el headset**: mirar a los lados, arriba, abajo — todo con la cabeza. Es la cámara VR estándar.
2. **El avance es siempre frontal al cuerpo del salmón**, no a la cámara. Si miro a la derecha pero aleteo con ambos brazos, avanzo hacia donde apunta el cuerpo del salmón, no hacia donde miro.
3. **Para avanzar en diagonal arriba/abajo**: se determina por el pitch del cuerpo del salmón. Esto se puede vincular al pitch de la cabeza con suavizado, o dejarse como resultado de aleteo asimétrico vertical.
4. **El jugador se queda en el mismo lugar físico**: no necesita moverse en el mundo real, todo es locomotion artificial guiada por gestos.

---

## 1. Análisis del estado actual

### 1.1 Lo que ya existe

| Componente | Archivo | Estado |
|:---|:---|:---|
| Script de nado por brazada | [`SwimLocomotion.cs`](../src/anadromo/Assets/_Project/Scripts/Locomotion/SwimLocomotion.cs) | Funcional pero con paradigma diferente (remada eje Z, no aleteo vertical) |
| Cámara de debug libre | [`SimpleFlyCamera.cs`](../src/anadromo/Assets/_Project/Scripts/Mechanics/SimpleFlyCamera.cs) | Funcional (teclado+ratón, para testing sin casco) |
| Debug de vuelo | [`DebugVuelo.cs`](../src/anadromo/Assets/_Project/Scripts/Mechanics/DebugVuelo.cs) | Funcional (WASD + ratón con botón derecho) |
| Sistema de energía | [`EnergySystem.cs`](../src/anadromo/Assets/_Project/Scripts/Systems/EnergySystem.cs) | Funcional, desacoplado por eventos |
| Debug de velocidad | [`PlayerSpeedDebug.cs`](../src/anadromo/Assets/_Project/Scripts/Mechanics/PlayerSpeedDebug.cs) | Funcional |
| XR Origin en escena | `Act1_OpenOcean.unity` | Configurado con XR Origin (VR), Main Camera, Camera Offset |
| Escena de test | `TerrainTestCero.unity` | Sin XR Origin, solo Main Camera con SimpleFlyCamera |
| Paquetes XR | `manifest.json` | XR Interaction Toolkit 3.6.0, Oculus 4.5.5, OpenXR 1.18.0 ✅ |

### 1.2 Lo que falta

- [ ] **Detección de aleteo vertical**: el sistema actual detecta movimiento en eje Z, necesitamos detectar movimiento **vertical (eje Y)** de cada mano
- [ ] **Rotación diferencial**: aleteo de un solo brazo → rotación del cuerpo del salmón
- [ ] **Separación cuerpo/cabeza**: el cuerpo del salmón (dirección de avance) debe ser independiente de la cabeza (dirección de vista)
- [ ] **XR Origin en TerrainTestCero**: configurar escena de prototipado
- [ ] **Cámara externa**: integración de webcam/cámara para tracking o supervisión
- [ ] **Tuning de parámetros**: umbrales de aleteo, velocidad de rotación, fuerza de avance, drag

---

## 2. Arquitectura propuesta

### 2.1 Jerarquía de GameObjects

```
XR Origin (VR)               ← XR Origin component (Room Scale)
├── Camera Offset             ← offset estándar de XR
│   ├── Main Camera           ← controlada por headset (TrackedPoseDriver)
│   ├── Left Controller       ← TrackedPoseDriver (mano izquierda)
│   └── Right Controller      ← TrackedPoseDriver (mano derecha)
└── SalmonBody (Empty)        ← Transform independiente que representa la orientación del "cuerpo"
                                 NO sigue la cabeza en yaw, solo se rota por aleteo diferencial
```

### 2.2 Diagrama de flujo del sistema

```
┌─────────────────────────────────────────────────────────────┐
│                 PYTHON SERVER (pose_swim_server.py)         │
│  1. Captura de Webcam (OpenCV)                              │
│  2. MediaPipe Pose: Extrae keypoints 2D (Cadera, Hombro, Codo)│
│  3. Cálculo de Ángulos: Calcula el ángulo del brazo/hombro  │
│  4. Máquina de Estados: Transiciones up->half-down->down    │
│     (El "flap" se emite al llegar a 'down')                 │
│  5. Sincronía: Si ambos bajan en ventana < 0.2s = 'forward' │
│  6. UDP Socket: Envía JSON a Unity {"action":"...", "speed"}│
└─────────┬───────────────────────────────────────────────────┘
          │ (Red UDP 127.0.0.1:5065)
          ▼
┌─────────────────────────────────────────────────────────────┐
│           LÓGICA DE MOVIMIENTO (FlapSwimController.cs)      │
│                                                             │
│  SI recibe "forward":                                       │
│    → Aplicar fuerza de AVANCE en salmonBody.forward         │
│                                                             │
│  SI recibe "turn_left":                                     │
│    → Aplicar torque de ROTACIÓN hacia la IZQUIERDA (-yaw)   │
│                                                             │
│  SI recibe "turn_right":                                    │
│    → Aplicar torque de ROTACIÓN hacia la DERECHA (+yaw)     │
│                                                             │
│  PITCH del cuerpo:                                          │
│    → Interpolar suavemente hacia el pitch de la cabeza VR   │
└─────────┬───────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────┐
│              APLICACIÓN FÍSICA (Rigidbody)                  │
│  - AddForce(salmonBody.forward * force, ForceMode.Force)    │
│  - AddTorque(Vector3.up * torque, ForceMode.Force)          │
│  - Drag lineal y angular para frenado natural               │
└─────────────────────────────────────────────────────────────┘
```

### 2.3 Scripts nuevos/modificados

| Script | Ubicación | Descripción |
|:---|:---|:---|
| `FlapDetector.cs` | `Scripts/Locomotion/` | **NUEVO** — Detecta aleteos verticales de cada mano. Emite eventos `OnFlapDetected(hand, intensity)` |
| `FlapSwimController.cs` | `Scripts/Locomotion/` | **NUEVO** — Convierte flaps en movimiento: avance + rotación diferencial. Maneja el `SalmonBody` transform |
| `SwimLocomotion.cs` | `Scripts/Locomotion/` | **CONSERVAR** como respaldo. El nuevo sistema lo reemplaza, pero no se borra |
| `ExternalCameraManager.cs` | `Scripts/Systems/` | **NUEVO** — Gestiona la cámara externa (webcam). Feed de video en textura, configuración |

---

## 3. Plan de implementación por fases

### Fase 1 — Setup de escena de prototipado (Est: 1-2h)

> **Objetivo:** Tener `TerrainTestCero` lista con XR Origin para poder probar locomoción VR.

| ID | Tarea | Detalle |
|:---|:---|:---|
| F1.1 | Agregar XR Origin (VR) a TerrainTestCero | Añadir el prefab `XR Origin (VR)` del XR Interaction Toolkit. Configurar Tracking Origin Mode = Floor. Incluir Main Camera, Camera Offset, Left/Right Controllers con TrackedPoseDriver |
| F1.2 | Desactivar/remover SimpleFlyCamera | La Main Camera existente se reemplaza por la del XR Origin. Desactivar `SimpleFlyCamera` (no borrar, puede servir de debug en Editor) |
| F1.3 | Crear GameObject `SalmonBody` | Empty child del XR Origin. Este transform representa la orientación del cuerpo del salmón independiente de la cabeza |
| F1.4 | Verificar Input Actions | Confirmar que las acciones de posición de controladores (`XRI LeftHand/Position`, `XRI RightHand/Position`) funcionan correctamente |
| F1.5 | Probar en casco | Build rápido → verificar que la vista funciona correctamente con el headset en la escena TerrainTestCero |

### Fase 2 — Detección de aleteo en Python (`pose_swim_server.py`) (Est: 3-4h)

> **Objetivo:** Detectar de forma confiable cuándo el jugador realiza un movimiento de "aleteo" con cada brazo mediante Computer Vision.

| ID | Tarea | Detalle |
|:---|:---|:---|
| F2.1 | Actualizar `pose_swim_server.py` | Configurar el script de Python para leer keypoints 2D (Cadera, Hombro, Codo) usando MediaPipe Pose |
| F2.2 | Cálculo de ángulos trigonométricos | Reemplazar lógica de distancias absolutas por cálculo de ángulos (hombro) garantizando invarianza a escala del jugador |
| F2.3 | Máquina de estados (State Machine) | Implementar transiciones de aleteo (`up` -> `half-down` -> `down` -> `half-up`). El aleteo se registra al entrar en estado `down` |
| F2.4 | Ventana de Sincronía | Evaluar si ambos brazos completaron un aleteo (transición a `down`) en una ventana de ~0.2s |
| F2.5 | Emisión por UDP | Enviar mensaje JSON continuo a Unity: `{"action": "forward"|"turn_left"|"turn_right"|"idle", "speed": 1.0}` |
| F2.6 | Debug visual en Python | Dibujar los ángulos y el estado actual de cada brazo en la ventana de OpenCV para calibración |
| F2.7 | Tuning de variables | Ajustar constantes `ANGLE_UP`, `ANGLE_DOWN` y `SYNC_WINDOW` para que se sientan naturales en VR |

### Fase 3 — Locomoción en Unity (`FlapSwimController`) (Est: 3-4h)

> **Objetivo:** Convertir los eventos JSON recibidos por UDP en movimiento físico del salmón en VR.

| ID | Tarea | Detalle |
|:---|:---|:---|
| F3.1 | Crear `FlapSwimController.cs` | Script principal de locomoción que lee el puerto UDP 5065 o se conecta al manager de UDP existente |
| F3.2 | Avance frontal | Si la acción recibida es `forward`: `rb.AddForce(salmonBody.forward * forceMultiplier, ForceMode.Force)` |
| F3.3 | Rotación diferencial | Si la acción es `turn_left`: rotar `salmonBody` a la izquierda. Si es `turn_right`: rotar a la derecha |
| F3.4 | Pitch del cuerpo por cabeza | Interpolar suavemente el pitch del `SalmonBody` hacia el pitch de la cabeza VR (`Mathf.LerpAngle`) |
| F3.5 | Configuración de Rigidbody | `useGravity = false`, `linearDamping` y `angularDamping` apropiados para sensación submarina |
| F3.6 | Compatibilidad con sistemas | Mantener compatibilidad con corrientes marinas (`OceanEnvironment`) y `EnergySystem` |
| F3.7 | Crear `SwimSettings.asset` | ScriptableObject con: fuerza de avance, fuerza de rotación, drag, velocidad de pitch para tuning |

### Fase 4 — Cámara externa (Est: 2-3h)

> **Objetivo:** Integrar una cámara externa (webcam) al proyecto. Inicialmente solo captura y visualización; funcionalidades avanzadas (pose estimation, body tracking) se dejan para iteraciones futuras.

| ID | Tarea | Detalle |
|:---|:---|:---|
| F4.1 | Crear `ExternalCameraManager.cs` | Script que inicializa `WebCamTexture`, selecciona el dispositivo de cámara, y expone la textura |
| F4.2 | Renderizar feed en UI/debug | Mostrar el feed de la cámara en un `RawImage` del Canvas (solo debug/admin, no visible para el jugador VR). O en un quad en la escena visible en Editor |
| F4.3 | Configuración de resolución y FPS | Parámetros configurables para la cámara externa |
| F4.4 | Lifecycle management | Start/Stop de la cámara, manejo de errores si no hay cámara conectada |

> [!NOTE]
> La cámara externa se deja como módulo independiente. No afecta la locomoción en esta fase. Futuras iteraciones podrían usar MediaPipe/OpenCV para body tracking que complemente o sustituya el tracking de controllers.

### Fase 5 — Pulido y tuning (Est: 2-3h)

> **Objetivo:** Ajustar la sensación del movimiento hasta que sea intuitiva y cómoda.

| ID | Tarea | Detalle |
|:---|:---|:---|
| F5.1 | Playtest en casco | Sesión de prueba real con el Quest 2. Iterar sobre umbrales, fuerzas, drag |
| F5.2 | Viñeta de confort | Reutilizar o crear viñeta dinámica que se active con movimientos rápidos (anti motion sickness) |
| F5.3 | Límites de velocidad | Velocidad máxima lineal y angular para evitar que el jugador se desoriente |
| F5.4 | Feedback háptico por flap | Vibración breve en el controller al detectar un flap válido — feedback de "sí, tu aleteo fue registrado" |
| F5.5 | Suavizado general | Lerps y curvas de animación en rotación y avance para que el movimiento se sienta orgánico, no robótico |
| F5.6 | Compatibilidad con DebugVuelo | Permitir alternar entre `FlapSwimController` y `DebugVuelo` (para iterar en editor sin casco) |

### Fase 6 — Migración a Act1_OpenOcean (Est: 1-2h)

> **Objetivo:** Llevar el sistema validado a la escena principal del Acto I.

| ID | Tarea | Detalle |
|:---|:---|:---|
| F6.1 | Reemplazar `SwimLocomotion` | En el XR Origin de Act1_OpenOcean, desactivar `SwimLocomotion` y agregar `FlapDetector` + `FlapSwimController` |
| F6.2 | Configurar `SalmonBody` | Agregar el GameObject SalmonBody al XR Origin existente |
| F6.3 | Verificar integración | Confirmar que `EnergySystem`, `PlayerFeeding`, corrientes oceánicas y el cardumen funcionan con el nuevo sistema |
| F6.4 | Playtest final del Acto I | Recorrido completo con la nueva locomoción |

---

## 4. Decisiones de diseño abiertas

Las siguientes decisiones necesitan confirmación antes o durante la implementación:

| # | Decisión | Resolución | Nota |
|:---|:---|:---|:---|
| D1 | ¿Cómo se determina el **pitch** (inclinación arriba/abajo) del cuerpo? | ✅ **A) Sigue el pitch de la cabeza suavizado** — mirar arriba con el headset = nadar diagonal arriba | Confirmado por el usuario |
| D2 | ¿El **avance** debe ser por impulso o fuerza continua? | ✅ **A) Impulso por flap** — cada aleteo da un "golpe" de avance, más fiel a la natación real | Confirmado |
| D3 | ¿La **rotación** por aleteo individual también genera avance? | ✅ **B) Rota + pequeño avance** — más natural, un pez que gira siempre se mueve un poco | Confirmado por el usuario |
| D4 | ¿Qué hace la **cámara externa**? | ✅ **Input principal de aleteo** — La cámara externa (webcam + pose estimation) detecta el movimiento de brazos/aleteo del jugador. Es el dispositivo de input para la locomoción. Los Oculus solo controlan la vista/rotación de la cámara del jugador | **Cambio de arquitectura**: el flap detection se basa en computer vision (cámara), no en controllers VR |
| D5 | ¿Se permite **mirar completamente hacia atrás** (180° de yaw de cabeza)? | ✅ **A) Sin restricción** — el headset maneja esto naturalmente | Confirmado |

---

## 5. Estimaciones de tiempo total

| Fase | Estimación | Acumulado |
|:---|:---|:---|
| Fase 1 — Setup escena | 1-2h | 1-2h |
| Fase 2 — FlapDetector | 3-4h | 4-6h |
| Fase 3 — FlapSwimController | 4-5h | 8-11h |
| Fase 4 — Cámara externa | 2-3h | 10-14h |
| Fase 5 — Pulido y tuning | 2-3h | 12-17h |
| Fase 6 — Migración | 1-2h | 13-19h |
| **Total estimado** | | **13-19 horas de trabajo** |

> [!IMPORTANT]
> Las fases 2 y 3 son el **núcleo crítico** del plan. Si hay que recortar alcance, la Fase 4 (cámara externa) se puede posponer sin afectar la locomoción.

---

## 6. Archivos que se crearán

```
Assets/_Project/
├── Scripts/
│   └── Locomotion/
│       ├── FlapDetector.cs              ← NUEVO
│       ├── FlapSwimController.cs        ← NUEVO
│       └── SwimLocomotion.cs            ← EXISTENTE (se conserva como respaldo)
│   └── Systems/
│       └── ExternalCameraManager.cs     ← NUEVO
├── ScriptableObjects/
│   ├── FlapSettings.asset              ← NUEVO
│   └── SwimSettings.asset             ← NUEVO
```

---

## 7. Relación con documentación existente

- **GDD v0.3 sección 5.1**: Este plan implementa una variante de "nado por remada física" adaptada a **aleteo vertical**, manteniendo el principio de que "girar el cuerpo/cabeza determina la dirección".
- **TDD v0.1 sección RF-01, RF-02**: Se mantienen los requisitos funcionales. RF-01 cambia de "brazada" a "aleteo" como gesto de input, pero el principio es el mismo.
- **Tareas del Acto I (T2.1-T2.3)**: Las tareas de detección de brazada y traducción a impulso se reemplazan conceptualmente por las Fases 2 y 3 de este plan.

---

## 8. Riesgos específicos de este plan

| Riesgo | Probabilidad | Impacto | Mitigación |
|:---|:---|:---|:---|
| El gesto de aleteo se siente poco natural o cansado | Media | Alto | Umbrales bajos de detección, ScriptableObjects para tuning rápido sin recompilar. Sesión de playtest temprana (Fase 2, antes de construir Fase 3) |
| Confusión entre "ambos brazos" y "uno solo" por timing | Media | Medio | Ventana de simultaneidad configurable. Feedback háptico diferente para rotación vs. avance |
| Motion sickness por rotación artificial (yaw) | Alta | Alto | Rotación por pasos discretos (snap turn) como opción, drag angular alto, viñeta de confort activa durante rotación |
| La cámara externa no se detecta o tiene latencia | Baja | Bajo | La cámara es módulo independiente; fallo no bloquea la locomoción |
| Fatiga de brazos en sesiones largas (35-50 min del GDD) | Media | Alto | Permitir umbral de aleteo bajo (gestos suaves), considerar modo "crucero" donde el salmón mantiene velocidad mínima |

---

*Fin del documento — v0.1. Complementa al GDD v0.3 y al TDD v0.1 de Anádromo.*
