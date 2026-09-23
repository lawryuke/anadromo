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
  "t": 1234567890.123,
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
| `t`   | Timestamp (epoch seconds) |
| `lw`  | Left Wrist (muñeca izquierda) |
| `rw`  | Right Wrist (muñeca derecha) |
| `le`  | Left Elbow (codo izquierdo) |
| `re`  | Right Elbow (codo derecho) |
| `ls`  | Left Shoulder (hombro izquierdo) |
| `rs`  | Right Shoulder (hombro derecho) |

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
