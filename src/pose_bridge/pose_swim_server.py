import time
import json
import socket

import cv2
import numpy as np
import mediapipe as mp

# ---------------- CONFIGURACION ----------------
UNITY_IP = "127.0.0.1"
UNITY_PORT = 5065

# Umbrales de angulo para la maquina de estados (en grados)
ANGLE_UP = 80       # Brazo levantado (horizontal o mas)
ANGLE_DOWN = 30     # Brazo bajado (cerca del torso)

SYNC_WINDOW = 0.2   # Segundos de ventana para considerar aleteo de ambos brazos
FLAP_DURATION = 0.2 # Cuanto tiempo (segundos) se mantiene la accion activa para Unity

mp_pose = mp.solutions.pose

LM = {
    "hip_l": mp_pose.PoseLandmark.LEFT_HIP,
    "hip_r": mp_pose.PoseLandmark.RIGHT_HIP,
    "shoulder_l": mp_pose.PoseLandmark.LEFT_SHOULDER,
    "shoulder_r": mp_pose.PoseLandmark.RIGHT_SHOULDER,
    "elbow_l": mp_pose.PoseLandmark.LEFT_ELBOW,
    "elbow_r": mp_pose.PoseLandmark.RIGHT_ELBOW,
}


def calculate_angle_3d(a, b, c):
    """Calcula el angulo 3D entre 3 puntos (a-b-c, con vertice en b).
    Usa coordenadas x, y, z para ser robusto a la rotacion del cuerpo."""
    ba = np.array([a.x - b.x, a.y - b.y, a.z - b.z])
    bc = np.array([c.x - b.x, c.y - b.y, c.z - b.z])
    
    # Prevenir division por cero
    norm_ba = np.linalg.norm(ba)
    norm_bc = np.linalg.norm(bc)
    if norm_ba == 0 or norm_bc == 0:
        return 0.0
        
    cosine_angle = np.dot(ba, bc) / (norm_ba * norm_bc)
    angle = np.arccos(np.clip(cosine_angle, -1.0, 1.0))
    return np.degrees(angle)


class ArmStateMachine:
    """Maquina de estados para detectar aleteos basados en el angulo del hombro."""
    def __init__(self):
        self.stage = "up"
        self.last_flap_time = 0

    def update(self, angle, current_time):
        flapped = False
        if angle < ANGLE_DOWN and self.stage == "half-down":
            self.stage = "down"
            flapped = True
            self.last_flap_time = current_time
        elif ANGLE_DOWN <= angle <= ANGLE_UP and self.stage == "down":
            self.stage = "half-up"
        elif ANGLE_DOWN <= angle <= ANGLE_UP and self.stage == "up":
            self.stage = "half-down"
        elif angle > ANGLE_UP and self.stage == "half-up":
            self.stage = "up"
        return flapped


def main():
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    cap = cv2.VideoCapture(0)

    left_arm = ArmStateMachine()
    right_arm = ArmStateMachine()
    
    current_action = "idle"
    action_expire_time = 0

    with mp_pose.Pose(min_detection_confidence=0.6, min_tracking_confidence=0.6) as pose:
        while cap.isOpened():
            ok, frame = cap.read()
            if not ok:
                break

            current_time = time.time()
            frame_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            results = pose.process(frame_rgb)

            if results.pose_landmarks:
                lm = results.pose_landmarks.landmark

                # Calcular angulo para brazo izquierdo (cadera -> hombro -> codo) en 3D
                angle_l = calculate_angle_3d(lm[LM["hip_l"]], lm[LM["shoulder_l"]], lm[LM["elbow_l"]])
                flapped_l = left_arm.update(angle_l, current_time)

                # Calcular angulo para brazo derecho en 3D
                angle_r = calculate_angle_3d(lm[LM["hip_r"]], lm[LM["shoulder_r"]], lm[LM["elbow_r"]])
                flapped_r = right_arm.update(angle_r, current_time)

                # Logica de sincronia: solo ambos brazos = avance
                # (El giro lo controla el headset VR en Unity)
                if flapped_l or flapped_r:
                    # Chequear si el otro brazo tambien aleteo recientemente
                    time_diff = abs(left_arm.last_flap_time - right_arm.last_flap_time)
                    if time_diff <= SYNC_WINDOW and current_time - left_arm.last_flap_time <= SYNC_WINDOW and current_time - right_arm.last_flap_time <= SYNC_WINDOW:
                        current_action = "forward"
                        action_expire_time = current_time + FLAP_DURATION
                    # Aleteo individual: se ignora (no genera accion)

                # Expirar la accion si ha pasado el tiempo
                if current_time > action_expire_time:
                    current_action = "idle"

                # Enviar accion a Unity
                msg = {"action": current_action, "speed": 1.0 if current_action != "idle" else 0.0}
                sock.sendto(json.dumps(msg).encode("utf-8"), (UNITY_IP, UNITY_PORT))

                # Visualizacion en la ventana
                cv2.putText(frame, f"L: {int(angle_l)} ({left_arm.stage})", (20, 40), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0), 2)
                cv2.putText(frame, f"R: {int(angle_r)} ({right_arm.stage})", (20, 70), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0), 2)
                cv2.putText(frame, f"ACTION: {current_action}", (20, 110), cv2.FONT_HERSHEY_SIMPLEX, 1, (255, 0, 0), 2)

                # Dibujar esqueleto basico
                mp.solutions.drawing_utils.draw_landmarks(frame, results.pose_landmarks, mp_pose.POSE_CONNECTIONS)

            cv2.imshow("Pose Swim Tracker - Angle Based", frame)
            if cv2.waitKey(1) & 0xFF == 27:  # ESC
                break

    cap.release()
    cv2.destroyAllWindows()
    sock.close()


if __name__ == "__main__":
    main()
