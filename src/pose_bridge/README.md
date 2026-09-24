# Anádromo — Pose Bridge (MediaPipe → Unity)

Bridge de visión por computadora que captura los movimientos de los brazos del jugador
mediante una webcam y envía los datos de pose a Unity en tiempo real vía UDP.

## Arquitectura

```
Webcam → Python (MediaPipe Pose) → UDP (JSON) → Unity (ExternalCameraReceiver) → FlapDetector
```

## Requisitos

- **Python** 3.8 o superior
- **Webcam** posicionada frente al jugador (debe ver el torso y los brazos)
- **Unity** ejecutándose con `ExternalCameraReceiver` en la escena

## Instalación

```bash
cd src/pose_bridge
pip install -r requirements.txt
```

## Uso

### Escena TerrainTestVisuales (Oculus + cámara externa)

La escena `Assets/_Project/Scenes/TerrainTestVisuales.unity` ya incluye el `XR Origin`,
`PoseBridge`, `FlapDetector` y sus referencias. Conecta el visor Oculus al proyecto,
abre la escena en Unity y ejecuta `python mediapipe_bridge.py --port 5555` desde
`src/pose_bridge` antes de entrar en Play.

Un ciclo de cualquiera de los brazos impulsa al jugador hacia la dirección del visor.
Se admiten recorridos verticales, laterales, diagonales y circulares visibles en la cámara.
Los brazos pueden trabajar juntos o alternarse. Al girar el visor más de 15° a un lado, el `XR Origin` gira
continuamente hacia ese lado; la velocidad aumenta con el ángulo hasta 60°/s.
Los valores se ajustan en `Assets/_Project/ScriptableObjects/SwimSettings.asset`.
La cámara externa debe ver ambos hombros y la muñeca y el codo de al menos un brazo.
Un brazo oculto se suspende sin bloquear el otro. Si se pierde la referencia de los
hombros, se suspenden ambos. Reinicia el bridge tras actualizar para enviar el protocolo nuevo.

### Cómo se reconoce un ciclo

El detector mide un punto formado por muñeca (80 %) y codo (20 %) respecto a su hombro,
usando el ancho de hombros como escala y corrigiendo la relación de aspecto de la imagen.
Un recorrido de 0.3 anchos de hombros genera un impulso. Para volver a impulsarse,
el brazo debe regresar cerca de su punto inicial (45 % del umbral). El regreso no
genera otro impulso. Permanecer quieto durante 0.7 segundos adopta esa postura como
nuevo reposo; mantener el brazo extendido no genera impulsos repetidos.

Los parámetros están en `Assets/_Project/ScriptableObjects/FlapSettings.asset`:

| Parámetro | Valor inicial | Ajuste |
|---|---:|---|
| `cycle.amplitude` | 0.3 | Bajar para recorridos más pequeños; subir si hay activaciones involuntarias. |
| `cycle.recoveryRatio` | 0.45 | Subir para permitir un regreso menos completo. |
| `cycle.restHoldTime` | 0.7 s | Tiempo quieto para adoptar otra postura cómoda. |
| `cycle.smoothingTime` | 0.08 s | Subir reduce ruido, pero añade retraso. |
| `minimumVisibility` | 0.5 | Confianza mínima por punto del brazo y hombros. |
| `trackingTimeout` | 0.3 s | Un intervalo mayor reinicia los ciclos sin impulso. |

Estos son valores iniciales para probar con usuarios. La detección usa la proyección
2D: un gesto dirigido exclusivamente hacia la cámara puede ser difícil de reconocer.
Mantén el torso orientado hacia ella y utiliza el visor para dirigir el giro virtual.
La física conserva inercia y corrientes cuando cesan los impulsos.

En el panel de depuración (F3) aparecen los contadores y los estados `LISTO`,
`RECUPERANDO` y `SIN TRACKING` de cada brazo. Para una prueba manual, realiza aleteos
verticales, laterales y alternados; después mantén los brazos quietos y oculta uno.
Comprueba que los contadores no aumenten en reposo ni al recuperar el tracking.

### 1. Listar cámaras disponibles

```bash
python mediapipe_bridge.py --list-cameras
```

### 2. Ejecutar el bridge

```bash
# Cámara por defecto (índice 0), puerto 5555
python mediapipe_bridge.py

# Especificar otra cámara
python mediapipe_bridge.py --camera 1

# Puerto diferente (debe coincidir con ExternalCameraReceiver en Unity)
python mediapipe_bridge.py --port 5556

# Sin ventana de preview (modo headless)
python mediapipe_bridge.py --no-show
```

### 3. En Unity

1. Abre la escena `TerrainTestCero` (o la que tenga el XR Origin)
2. Agrega un GameObject vacío llamado `PoseBridge`
3. Añade el componente `ExternalCameraReceiver` (puerto: 5555)
4. Añade el componente `FlapDetector`
5. Crea un asset `FlapSettings`: click derecho en Project > Create > Anadromo > Flap Settings
6. Asigna las referencias en el Inspector:
   - `FlapDetector.cameraReceiver` → el `ExternalCameraReceiver`
   - `FlapDetector.settings` → el `FlapSettings` asset
7. Dale **Play** en Unity
8. Ejecuta el bridge Python

## Protocolo UDP

Cada frame, Python envía un paquete UDP con JSON:

```json
{
  "version": 2,
  "t": 12345.123,
  "tracked": true,
  "aspect": 1.3333333,
  "v": [0.99, 0.99, 0.98, 0.98, 0.99, 0.99],
  "lw": [0.40, 0.65, 0.10],
  "rw": [0.60, 0.65, 0.10],
  "le": [0.35, 0.50, 0.15],
  "re": [0.65, 0.50, 0.15],
  "ls": [0.30, 0.40, 0.20],
  "rs": [0.70, 0.40, 0.20]
}
```

| Campo | Significado |
|:------|:------------|
| `t`   | Tiempo monotónico de captura en segundos, con precisión double en Unity |
| `tracked` | Si se detectó una pose; false reinicia ambos brazos sin impulso |
| `aspect` | Ancho/alto de la imagen |
| `v` | Visibilidad en orden lw, rw, le, re, ls, rs |
| `lw`  | Left Wrist (muñeca izquierda) |
| `rw`  | Right Wrist (muñeca derecha) |
| `le`  | Left Elbow (codo izquierdo) |
| `re`  | Right Elbow (codo derecho) |
| `ls`  | Left Shoulder (hombro izquierdo) |
| `rs`  | Right Shoulder (hombro derecho) |

Se envían también poses parciales y paquetes con `tracked: false` cuando no hay persona.
Unity procesa cada timestamp una sola vez. Los paquetes antiguos con seis landmarks
siguen siendo aceptados, pero no permiten evaluar la visibilidad por brazo.

### Pruebas del detector

Desde la raíz del repositorio, ejecuta `src/pose_bridge/tests/run-cycle-checks.ps1`
con PowerShell y Unity 6000.3.10f1 instalado (la ruta se puede pasar con `-UnityEditor`).
El runner compila el detector C# real y reproduce trayectorias a 15, 30 y 60 muestras/s,
además de ruido, regreso, posturas mantenidas y saltos de tracking. Los resultados
temporales se guardan en `src/anadromo/Temp/LocomotionValidation`.

### Coordenadas

- **X**: 0.0 = izquierda, 1.0 = derecha (imagen espejada para intuición natural)
- **Y**: 0.0 = abajo, 1.0 = arriba (**invertido** desde MediaPipe nativo)
- **Z**: profundidad relativa a las caderas

## Posición de la cámara

La cámara debe estar posicionada para ver **el torso y los brazos del jugador**:

```
        [Cámara]
           |
           v
     ┌───────────┐
     │  Jugador   │
     │  con Quest │
     │   ╔═══╗   │
     │   ║   ║   │  ← La cámara debe ver
     │  /║   ║\  │     los brazos completos
     │ / ╚═══╝ \ │
     │/         \│
     └───────────┘
```

- **Distancia recomendada**: 1.5 - 2.5 metros
- **Altura**: a nivel del pecho del jugador
- **Iluminación**: buena iluminación frontal, evitar contraluz
- El jugador puede estar sentado o de pie

## Troubleshooting

| Problema | Solución |
|:---------|:---------|
| "No se pudo abrir la cámara" | Usa `--list-cameras` para ver las disponibles. Si el Quest 2 está conectado, su cámara puede aparecer como otro índice |
| Tracking inestable | Mejora la iluminación. Usa `--confidence 0.7` para filtrar detecciones débiles |
| Latencia alta | Reduce resolución: `--width 320 --height 240`. Cierra otras aplicaciones |
| Unity no recibe datos | Verifica que el puerto coincida. Revisa firewall. Ambos deben estar en la misma máquina (localhost) |
| "No pose detected" | Asegúrate de que la cámara ve tu torso y brazos. Aléjate un poco si estás muy cerca |
