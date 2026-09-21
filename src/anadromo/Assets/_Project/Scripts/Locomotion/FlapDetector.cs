using UnityEngine;
using UnityEngine.Events;

namespace Anadromo.Locomotion
{
    /// <summary>
    /// Detecta aleteos verticales de los brazos del jugador a partir de los datos
    /// de pose recibidos por ExternalCameraReceiver (cámara + MediaPipe).
    /// 
    /// Algoritmo:
    ///   1. Lee la posición Y de cada muñeca desde ExternalCameraReceiver
    ///   2. Aplica filtro pasa-bajos para suavizar ruido de cámara
    ///   3. Calcula velocidad vertical (deltaY / deltaTime)
    ///   4. Detecta "flap" cuando la velocidad descendente supera el umbral
    ///   5. Emite eventos OnLeftFlap / OnRightFlap con la intensidad [0-1]
    ///   6. Aplica cooldown para evitar múltiples detecciones por aleteo
    /// 
    /// Coordenadas: Y=1 es arriba, Y=0 es abajo. Velocidad negativa = brazo bajando = FLAP.
    /// </summary>
    public class FlapDetector : MonoBehaviour
    {
        [Header("Dependencias")]
        [Tooltip("Referencia al receptor de datos de la cámara externa.")]
        [SerializeField] private Anadromo.Systems.ExternalCameraReceiver cameraReceiver;

        [Tooltip("Configuración de parámetros de detección. " +
                 "Crear con: click derecho > Create > Anadromo > Flap Settings")]
        [SerializeField] private FlapSettings settings;

        [Header("Eventos de Aleteo")]
        [Tooltip("Se dispara al detectar un aleteo del brazo IZQUIERDO. Parámetro: intensidad [0-1].")]
        public UnityEvent<float> OnLeftFlap = new UnityEvent<float>();

        [Tooltip("Se dispara al detectar un aleteo del brazo DERECHO. Parámetro: intensidad [0-1].")]
        public UnityEvent<float> OnRightFlap = new UnityEvent<float>();

        // ─── Debug (visibles en Inspector) ───
        [Header("Debug — Muñeca Izquierda")]
        [SerializeField] private float leftWristRawY;
        [SerializeField] private float leftWristSmoothedY;
        [SerializeField] private float leftVelocityY;
        [SerializeField] private bool leftInCooldown;

        [Header("Debug — Muñeca Derecha")]
        [SerializeField] private float rightWristRawY;
        [SerializeField] private float rightWristSmoothedY;
        [SerializeField] private float rightVelocityY;
        [SerializeField] private bool rightInCooldown;

        [Header("Debug — Contadores")]
        [SerializeField] private int totalLeftFlaps;
        [SerializeField] private int totalRightFlaps;

        // ─── Estado interno ───
        private float prevLeftSmoothedY;
        private float prevRightSmoothedY;
        private float leftCooldownTimer;
        private float rightCooldownTimer;
        private bool initialized;
        private bool warnedMissing;

        // ─── Lifecycle ───

        private void OnEnable()
        {
            initialized = false;
            warnedMissing = false;
        }

        private void Update()
        {
            // Validar dependencias
            if (cameraReceiver == null || settings == null)
            {
                if (!warnedMissing)
                {
                    Debug.LogWarning("[Anadromo] FlapDetector: Asigna ExternalCameraReceiver y FlapSettings en el Inspector.");
                    warnedMissing = true;
                }
                return;
            }

            // No procesar si no hay datos de la cámara
            if (!cameraReceiver.IsReceiving)
            {
                return;
            }

            // Leer posiciones Y de las muñecas (crudas)
            leftWristRawY = cameraReceiver.LeftWrist.y;
            rightWristRawY = cameraReceiver.RightWrist.y;

            // Primer frame con datos: inicializar
            if (!initialized)
            {
                leftWristSmoothedY = leftWristRawY;
                rightWristSmoothedY = rightWristRawY;
                prevLeftSmoothedY = leftWristSmoothedY;
                prevRightSmoothedY = rightWristSmoothedY;
                initialized = true;
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // ─── Suavizado (low-pass filter) ───
            // Lerp entre el valor nuevo y el anterior. Alpha alto = más suavizado.
            float alpha = settings.smoothingFactor;
            leftWristSmoothedY = Mathf.Lerp(leftWristRawY, leftWristSmoothedY, alpha);
            rightWristSmoothedY = Mathf.Lerp(rightWristRawY, rightWristSmoothedY, alpha);

            // ─── Velocidad vertical ───
            leftVelocityY = (leftWristSmoothedY - prevLeftSmoothedY) / dt;
            rightVelocityY = (rightWristSmoothedY - prevRightSmoothedY) / dt;

            // Guardar para siguiente frame
            prevLeftSmoothedY = leftWristSmoothedY;
            prevRightSmoothedY = rightWristSmoothedY;

            // ─── Cooldowns ───
            UpdateCooldown(ref leftCooldownTimer, ref leftInCooldown, dt);
            UpdateCooldown(ref rightCooldownTimer, ref rightInCooldown, dt);

            // ─── Detección de aleteos ───
            // Velocidad negativa = brazo bajando = aleteo (power stroke)
            TryDetectFlap(
                leftVelocityY, ref leftCooldownTimer, ref leftInCooldown,
                OnLeftFlap, ref totalLeftFlaps, "izquierdo"
            );

            TryDetectFlap(
                rightVelocityY, ref rightCooldownTimer, ref rightInCooldown,
                OnRightFlap, ref totalRightFlaps, "derecho"
            );
        }

        // ─── Detección ───

        private void TryDetectFlap(float velocityY, ref float cooldownTimer,
                                    ref bool inCooldown, UnityEvent<float> flapEvent,
                                    ref int totalFlaps, string label)
        {
            // Aleteo = velocidad Y negativa (brazo bajando) que supera el umbral
            if (velocityY < -settings.flapVelocityThreshold && !inCooldown)
            {
                // Calcular intensidad normalizada [minIntensity, maxIntensity]
                float rawIntensity = Mathf.Abs(velocityY) * settings.intensityMultiplier;
                float intensity = Mathf.Clamp(rawIntensity, settings.minIntensity, settings.maxIntensity);

                // Disparar evento
                flapEvent?.Invoke(intensity);

                // Activar cooldown
                cooldownTimer = settings.flapCooldown;
                inCooldown = true;
                totalFlaps++;

                Debug.Log($"[Anadromo] 🐟 FLAP {label} — intensidad: {intensity:F2} (vel: {velocityY:F3})");
            }
        }

        private void UpdateCooldown(ref float timer, ref bool flag, float dt)
        {
            if (timer > 0f)
            {
                timer -= dt;
                flag = true;
            }
            else
            {
                flag = false;
            }
        }

        // ─── API pública ───

        /// <summary>
        /// Resetea contadores y estado interno del detector.
        /// Útil al reiniciar un nivel o al cambiar de escena.
        /// </summary>
        public void ResetState()
        {
            initialized = false;
            leftCooldownTimer = 0f;
            rightCooldownTimer = 0f;
            leftInCooldown = false;
            rightInCooldown = false;
            totalLeftFlaps = 0;
            totalRightFlaps = 0;
        }

        /// <summary>
        /// Devuelve true si el detector está recibiendo datos y operativo.
        /// </summary>
        public bool IsOperational =>
            cameraReceiver != null && settings != null && cameraReceiver.IsReceiving && initialized;
    }
}
