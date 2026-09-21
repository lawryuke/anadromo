#!/usr/bin/env python3
"""
MediaPipe Pose Bridge para Anádromo
===================================
Captura video de una webcam, ejecuta estimación de pose corporal con MediaPipe Tasks API,
y envía los landmarks del tren superior a Unity vía UDP.

Uso:
    python mediapipe_bridge.py                     # Cámara 0, puerto 5555
    python mediapipe_bridge.py --camera 1          # Otra cámara
    python mediapipe_bridge.py --port 5556         # Otro puerto
    python mediapipe_bridge.py --no-show           # Sin preview visual
    python mediapipe_bridge.py --list-cameras      # Listar cámaras disponibles

Protocolo UDP (JSON por paquete):
    {
      "t": 1234567890.123,              // timestamp (epoch)
      "lw": [0.40, 0.65, 0.10],        // left wrist  [x, y, z]
      "rw": [0.60, 0.65, 0.10],        // right wrist [x, y, z]
      "le": [0.35, 0.50, 0.15],        // left elbow
      "re": [0.65, 0.50, 0.15],        // right elbow
      "ls": [0.30, 0.40, 0.20],        // left shoulder
      "rs": [0.70, 0.40, 0.20]         // right shoulder
    }

Coordenadas:
    X: 0.0 = izquierda de la imagen, 1.0 = derecha (espejado para intuición)
    Y: 0.0 = abajo, 1.0 = arriba (INVERTIDO desde MediaPipe nativo)
    Z: profundidad relativa a las caderas (MediaPipe nativo)
"""

import cv2
import mediapipe as mp
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision
import socket
import json
import time
import argparse
import sys
import os
import urllib.request


def list_cameras(max_check=10):
    """Detecta y lista las cámaras disponibles en el sistema."""
    print("\n📷 Cámaras detectadas:")
    found = False
    for i in range(max_check):
        cap = cv2.VideoCapture(i)
        if cap.isOpened():
            w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
            h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
            fps = int(cap.get(cv2.CAP_PROP_FPS))
            print(f"  [{i}] {w}x{h} @ {fps}fps")
            cap.release()
            found = True
    if not found:
        print("  (ninguna cámara encontrada)")
    print()


def create_status_bar(frame, data, all_visible, fps, host, port):
    """Dibuja información de debug en el frame de preview."""
    h, w = frame.shape[:2]
    overlay = frame.copy()
    cv2.rectangle(overlay, (0, 0), (w, 140), (0, 0, 0), -1)
    cv2.addWeighted(overlay, 0.6, frame, 0.4, 0, frame)

    color_ok = (0, 255, 0)
    color_warn = (0, 100, 255)
    color_info = (200, 200, 200)

    status = "TRACKING" if all_visible else "PARCIAL"
    status_color = color_ok if all_visible else color_warn

    if data:
        lw_y = data['lw'][1]
        rw_y = data['rw'][1]
        cv2.putText(frame, f"Muneca Izq Y: {lw_y:.3f}", (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 255), 2)
        cv2.putText(frame, f"Muneca Der Y: {rw_y:.3f}", (10, 50), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 0, 255), 2)

    cv2.putText(frame, f"Estado: {status}", (10, 80), cv2.FONT_HERSHEY_SIMPLEX, 0.6, status_color, 2)
    cv2.putText(frame, f"FPS: {fps}", (250, 80), cv2.FONT_HERSHEY_SIMPLEX, 0.6, color_ok, 2)
    cv2.putText(frame, f"UDP -> {host}:{port}", (10, 110), cv2.FONT_HERSHEY_SIMPLEX, 0.5, color_info, 1)
    cv2.putText(frame, "Presiona 'q' para salir", (10, 135), cv2.FONT_HERSHEY_SIMPLEX, 0.4, color_info, 1)


def download_model(model_path):
    if not os.path.exists(model_path):
        print(f"Descargando modelo {model_path}...")
        url = "https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/1/pose_landmarker_lite.task"
        urllib.request.urlretrieve(url, model_path)
        print("Descarga completada.")


def main():
    parser = argparse.ArgumentParser(
        description='MediaPipe Pose Bridge para Anadromo (Unity VR)',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__
    )
    parser.add_argument('--camera', type=int, default=0, help='Indice del dispositivo de camara (default: 0)')
    parser.add_argument('--port', type=int, default=5555, help='Puerto UDP de destino (default: 5555)')
    parser.add_argument('--host', type=str, default='127.0.0.1', help='Host UDP de destino (default: 127.0.0.1)')
    parser.add_argument('--show', action='store_true', default=True, help='Mostrar preview con esqueleto (default: activo)')
    parser.add_argument('--no-show', dest='show', action='store_false', help='Ocultar preview (solo enviar datos)')
    parser.add_argument('--confidence', type=float, default=0.5, help='Confianza minima de deteccion (0-1, default: 0.5)')
    parser.add_argument('--width', type=int, default=640, help='Ancho de captura (default: 640)')
    parser.add_argument('--height', type=int, default=480, help='Alto de captura (default: 480)')
    parser.add_argument('--list-cameras', action='store_true', help='Listar camaras disponibles y salir')
    args = parser.parse_args()

    if args.list_cameras:
        list_cameras()
        return

    model_path = 'pose_landmarker_lite.task'
    download_model(model_path)

    base_options = mp_python.BaseOptions(model_asset_path=model_path)
    options = vision.PoseLandmarkerOptions(
        base_options=base_options,
        output_segmentation_masks=False,
        min_pose_detection_confidence=args.confidence,
        min_pose_presence_confidence=args.confidence,
        min_tracking_confidence=args.confidence
    )
    detector = vision.PoseLandmarker.create_from_options(options)

    # Indices de landmarks
    LANDMARKS = {
        'ls': 11, 'rs': 12,
        'le': 13, 're': 14,
        'lw': 15, 'rw': 16,
    }

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    target = (args.host, args.port)

    print(f"\n🐟 Anadromo Pose Bridge")
    print(f"{'='*40}")

    cap = cv2.VideoCapture(args.camera)
    if not cap.isOpened():
        print(f"\n❌ ERROR: No se pudo abrir la camara {args.camera}")
        list_cameras()
        sys.exit(1)

    cap.set(cv2.CAP_PROP_FRAME_WIDTH, args.width)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, args.height)
    cap.set(cv2.CAP_PROP_FPS, 30)

    actual_w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    actual_h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))

    print(f"  Camara:     [{args.camera}] {actual_w}x{actual_h}")
    print(f"  UDP:        {args.host}:{args.port}")
    print(f"  Confianza:  {args.confidence}")
    print(f"  Preview:    {'SI' if args.show else 'NO'}")
    print(f"{'='*40}")
    print(f"  Esperando a Unity (ExternalCameraReceiver)...")
    print(f"  Presiona 'q' en la ventana de preview para salir\n")

    fps_counter = 0
    fps_timer = time.time()
    fps_display = 0
    frames_sent = 0

    try:
        while True:
            ret, frame = cap.read()
            if not ret:
                time.sleep(0.01)
                continue

            frame = cv2.flip(frame, 1)
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame)

            detection_result = detector.detect(mp_image)

            data = None
            all_visible = False

            if detection_result.pose_landmarks:
                landmarks = detection_result.pose_landmarks[0]
                data = {'t': round(time.time(), 4)}
                all_visible = True

                for key, idx in LANDMARKS.items():
                    lm = landmarks[idx]
                    if lm.visibility < args.confidence:
                        all_visible = False
                    data[key] = [round(lm.x, 4), round(1.0 - lm.y, 4), round(lm.z, 4)]

                packet = json.dumps(data, separators=(',', ':'))
                sock.sendto(packet.encode('utf-8'), target)
                frames_sent += 1

                if args.show:
                    h_frame, w_frame = frame.shape[:2]
                    # Draw basic connections
                    connections = [(11,13),(13,15),(12,14),(14,16),(11,12)]
                    for start_idx, end_idx in connections:
                        if start_idx < len(landmarks) and end_idx < len(landmarks):
                            p1 = landmarks[start_idx]
                            p2 = landmarks[end_idx]
                            if p1.visibility > args.confidence and p2.visibility > args.confidence:
                                cv2.line(frame, (int(p1.x*w_frame), int(p1.y*h_frame)), (int(p2.x*w_frame), int(p2.y*h_frame)), (0,255,0), 2)
                    
                    for key, color in [('lw', (0, 255, 255)), ('rw', (255, 0, 255))]:
                        lm = landmarks[LANDMARKS[key]]
                        cx, cy = int(lm.x * w_frame), int(lm.y * h_frame)
                        cv2.circle(frame, (cx, cy), 10, color, -1)
                        cv2.circle(frame, (cx, cy), 12, (255, 255, 255), 2)

            fps_counter += 1
            if time.time() - fps_timer >= 1.0:
                fps_display = fps_counter
                fps_counter = 0
                fps_timer = time.time()

            if args.show:
                create_status_bar(frame, data, all_visible, fps_display, args.host, args.port)
                cv2.imshow('Anadromo - Pose Bridge', frame)
                if cv2.waitKey(1) & 0xFF == ord('q'):
                    break

    except KeyboardInterrupt:
        print("\n\n⏹ Interrumpido")

    finally:
        cap.release()
        if args.show: cv2.destroyAllWindows()
        sock.close()
        detector.close()
        print(f"\n{'='*40}\n  Frames enviados: {frames_sent}\n  Bridge cerrado\n{'='*40}\n")


if __name__ == '__main__':
    main()
