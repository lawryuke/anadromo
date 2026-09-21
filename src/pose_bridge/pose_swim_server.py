"""
Servidor de deteccion de "nado" (brazada) usando MediaPipe Pose.
Generaliza la logica que describiste para brazo izquierdo, espejada al derecho.
Envia comandos de movimiento a Unity via UDP en formato JSON.

Requisitos:
    pip install mediapipe opencv-python numpy

Uso:
    python pose_swim_server.py
    (ESC para salir de la ventana de preview)
"""

import time
import json
import socket
from collections import deque

import cv2
import numpy as np
import mediapipe as mp

# ---------------- CONFIGURACION (calibrar con tu setup real) ----------------
UNITY_IP = "127.0.0.1"
UNITY_PORT = 5065

D_EXTEND = 0.12       # umbral de extension muñeca-codo en x (coords normalizadas 0-1 de MediaPipe)
V_Z_THRESHOLD = 0.05  # "error" de tu formula: |v_muñeca.z| <= este umbral
DM_SYNC = 0.05        # diferencia maxima en z entre ambas muñecas para considerar brazada sincronizada
TURN_SPEED = 30.0     # grados/seg de giro que se reporta a Unity
FORWARD_SPEED = 1.0   # velocidad de avance normalizada (Unity la escala con su propio multiplicador)
SMOOTH_WINDOW = 5     # frames para promediar la velocidad y reducir ruido

mp_pose = mp.solutions.pose

LM = {
    "elbow_l": mp_pose.PoseLandmark.LEFT_ELBOW,
    "elbow_r": mp_pose.PoseLandmark.RIGHT_ELBOW,
    "wrist_l": mp_pose.PoseLandmark.LEFT_WRIST,
    "wrist_r": mp_pose.PoseLandmark.RIGHT_WRIST,
}


class VelocityTracker:
    """Calcula velocidad por diferencias finitas, suavizada con ventana movil."""

    def __init__(self, window=SMOOTH_WINDOW):
        self.prev_pos = {}
        self.prev_t = {}
        self.history = {}
        self.window = window

    def update(self, name, pos, t):
        vel = np.zeros(3)
        if name in self.prev_pos:
            dt = max(t - self.prev_t[name], 1e-3)
            vel = (pos - self.prev_pos[name]) / dt
        self.prev_pos[name] = pos
        self.prev_t[name] = t

        buf = self.history.setdefault(name, deque(maxlen=self.window))
        buf.append(vel)
        return np.mean(buf, axis=0)


def get_point(landmarks, idx):
    lm = landmarks[idx]
    return np.array([lm.x, lm.y, lm.z])


def evaluate_arm(wrist, elbow, v_wrist):
    """
    Condicion 'externa' (tu E.i / E.d):
    brazo extendido hacia adelante y muñeca estable en z.
    """
    extended = abs(wrist[0] - elbow[0]) >= D_EXTEND
    stable_z = abs(v_wrist[2]) <= V_Z_THRESHOLD
    return extended and stable_z


def decide_action(wrist_l, wrist_r, elbow_l, elbow_r, v_wrist_l, v_wrist_r):
    active_l = evaluate_arm(wrist_l, elbow_l, v_wrist_l)
    active_r = evaluate_arm(wrist_r, elbow_r, v_wrist_r)

    if active_l and active_r:
        synced = abs(wrist_l[2] - wrist_r[2]) <= DM_SYNC
        if synced:
            return {"action": "forward", "speed": FORWARD_SPEED}
        # ambos activos pero no sincronizados: gira hacia el lado con mayor extension
        if abs(wrist_l[0] - elbow_l[0]) > abs(wrist_r[0] - elbow_r[0]):
            return {"action": "turn_left", "speed": TURN_SPEED}
        return {"action": "turn_right", "speed": TURN_SPEED}
    elif active_l:
        return {"action": "turn_left", "speed": TURN_SPEED}
    elif active_r:
        return {"action": "turn_right", "speed": TURN_SPEED}
    else:
        return {"action": "idle", "speed": 0.0}


def main():
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    cap = cv2.VideoCapture(0)
    tracker = VelocityTracker()

    with mp_pose.Pose(min_detection_confidence=0.6, min_tracking_confidence=0.6) as pose:
        while cap.isOpened():
            ok, frame = cap.read()
            if not ok:
                break

            t = time.time()
            frame_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            results = pose.process(frame_rgb)

            if results.pose_landmarks:
                lm = results.pose_landmarks.landmark

                wrist_l = get_point(lm, LM["wrist_l"])
                wrist_r = get_point(lm, LM["wrist_r"])
                elbow_l = get_point(lm, LM["elbow_l"])
                elbow_r = get_point(lm, LM["elbow_r"])

                v_wrist_l = tracker.update("wrist_l", wrist_l, t)
                v_wrist_r = tracker.update("wrist_r", wrist_r, t)

                action = decide_action(wrist_l, wrist_r, elbow_l, elbow_r, v_wrist_l, v_wrist_r)
                sock.sendto(json.dumps(action).encode("utf-8"), (UNITY_IP, UNITY_PORT))

                cv2.putText(frame, action["action"], (20, 40),
                            cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)

            cv2.imshow("Pose Swim Tracker", frame)
            if cv2.waitKey(1) & 0xFF == 27:  # ESC
                break

    cap.release()
    cv2.destroyAllWindows()
    sock.close()


if __name__ == "__main__":
    main()
