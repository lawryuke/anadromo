# Anádromo — Pose Bridge (MediaPipe → Unity)

## Nado con Oculus, sin cámara externa

La escena `Assets/_Project/Scenes/TerrainTestVisuales.unity` usa el seguimiento óptico de manos del visor Oculus para detectar aleteos. Activa el seguimiento de manos en el visor, deja los mandos y ejecuta la escena en Quest; no hace falta iniciar este bridge de Python. `FlapDetector` admite movimientos de las muñecas en cualquier dirección: arriba, abajo, lateral, adelante, atrás o circular. Una sola mano ya impulsa hacia delante y dos movimientos casi simultáneos forman un mismo impulso. La velocidad media del gesto regula la intensidad; al dejar de mover las manos cesan los impulsos y la resistencia del agua frena al jugador. Se exige un desplazamiento de 6 cm, velocidad mínima de 0.15 m/s y 80 ms de movimiento, con filtrado y rechazo de saltos de tracking. Los umbrales se ajustan en `PoseBridge > FlapDetector`.

`PoseBridge > SalmonFinHands` representa cada mano con una aleta pectoral de 28 cm, en forma de abanico con nervaduras. Sigue la posición de la muñeca y la orientación de la palma; un pulso de color confirma los gestos mientras la locomoción está habilitada. Al perder seguimiento, esa aleta se oculta y deja de impulsar. Son mallas procedurales iniciales, ajustables en longitud y orientación desde el Inspector. Las manos deben permanecer visibles para las cámaras del visor: no se infiere la posición de brazos ocultos. El movimiento se mide en espacio de tracking para que el giro del visor o el avance virtual no produzcan impulsos; mover físicamente todo el cuerpo puede también desplazar las manos y activar el detector.

El proyecto usa OpenXR y XR Hands; la función `Hand Tracking Subsystem` está activa para Android y para Standalone (Play en Unity con Quest Link). Si el visor pierde una mano, el detector espera a recuperarla sin generar un aleteo artificial. Al activarse el nado, la dirección de la cabeza establece el frente: mira al frente en ese momento. Hasta 15° a cada lado solo miras; entre 15° y 35° giras continuamente a 20°/s; desde 35° giras a 60°/s. Las velocidades cambian suavemente y el giro se detiene al regresar a la zona central. Para evitar oscilaciones, al regresar desde la zona rápida se cambia a lenta por debajo de 33°. Izquierda gira a la izquierda y derecha a la derecha. Perder el tracking del visor detiene el giro; recuperarlo calibra nuevamente el frente. Mirar arriba o abajo cambia la inclinación del nado. Los ajustes del giro y del impulso están en `SwimSettings.asset`.

El bridge descrito a continuación queda disponible para escenas que todavía usan MediaPipe.

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
