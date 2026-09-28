using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;
using System.Collections.Generic;

namespace Anadromo.Locomotion
{
    /// <summary>
    /// Detecta aleteos de las manos rastreadas por el visor o del bridge legado.
    /// 
    /// Algoritmo:
    ///   1. Lee las muñecas rastreadas respecto al visor, o las muñecas de MediaPipe
    ///   2. Suaviza las alturas según la fuente de seguimiento
    ///   3. Calcula velocidad vertical (deltaY / deltaTime)
    ///   4. Detecta "flap" cuando la velocidad descendente supera el umbral
    ///   5. Emite eventos OnLeftFlap / OnRightFlap con la intensidad [0-1]
    ///   6. Aplica cooldown para evitar múltiples detecciones por aleteo
    /// 
    /// Velocidad negativa = brazo bajando = FLAP.
    /// </summary>
    public class FlapDetector : MonoBehaviour
    {
        public enum TrackingSource { ExternalCamera, QuestHands }

        [Header("Dependencias")]
        [SerializeField] private TrackingSource trackingSource = TrackingSource.ExternalCamera;
        [Tooltip("Referencia al receptor de datos de la cámara externa.")]
        [SerializeField] private Anadromo.Systems.ExternalCameraReceiver cameraReceiver;

        [Header("Seguimiento XR")]
        [SerializeField] private Transform headTransform;
        [Tooltip("Raíz XR que convierte las articulaciones del espacio de seguimiento al mundo.")]
        [SerializeField] private Transform xrOrigin;

        [Tooltip("Suavizado de las manos XR; menor que el de MediaPipe para conservar los gestos.")]
        [Range(0f, 0.99f)] [SerializeField] private float xrSmoothingFactor = 0.45f;
        [Tooltip("Velocidad descendente mínima de cada muñeca relativa a la cabeza (m/s).")]
        [Min(0f)] [SerializeField] private float xrFlapVelocityThreshold = 0.15f;
        [Tooltip("Descenso mínimo para contar un aleteo y descartar temblores (m).")]
        [Min(0f)] [SerializeField] private float xrMinimumStrokeDistance = 0.1f;
        [Tooltip("Subida necesaria para preparar el siguiente aleteo (m).")]
        [Min(0f)] [SerializeField] private float xrRecoveryDistance = 0.07f;
        [Tooltip("Velocidad descendente a la que el nado alcanza la máxima intensidad (m/s).")]
        [Min(0.01f)] [SerializeField] private float xrFullIntensitySpeed = 1.2f;

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
        [SerializeField] private bool leftHandTracked;
        [SerializeField] private float leftWristRawY;
        [SerializeField] private float leftWristSmoothedY;
        [SerializeField] private float leftVelocityY;
        [SerializeField] private bool leftInCooldown;

        [Header("Debug — Muñeca Derecha")]
        [SerializeField] private bool rightHandTracked;
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
        private bool leftInitialized;
        private bool rightInitialized;
        private bool warnedMissing;
        private XRHandSubsystem handSubsystem;
        private readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
        private XRStrokeState leftStroke;
        private XRStrokeState rightStroke;

        private struct XRStrokeState
        {
            public bool descending;
            public bool recovering;
            public float startY;
            public float lowestY;
            public float peakSpeed;
        }

        // ─── Lifecycle ───

        private void OnEnable()
        {
            leftInitialized = false;
            rightInitialized = false;
            leftHandTracked = false;
            rightHandTracked = false;
            warnedMissing = false;
            leftStroke = default;
            rightStroke = default;
        }

        private void Update()
        {
            // Validar dependencias
            if (settings == null || (trackingSource == TrackingSource.ExternalCamera && cameraReceiver == null) ||
                (trackingSource == TrackingSource.QuestHands && (headTransform == null || xrOrigin == null)))
            {
                if (!warnedMissing)
                {
                    Debug.LogWarning("[Anadromo] FlapDetector: revisa FlapSettings y las referencias de la fuente de seguimiento.");
                    warnedMissing = true;
                }
                return;
            }

            bool leftTracked = TryReadWrist(true, out float leftY);
            bool rightTracked = TryReadWrist(false, out float rightY);
            leftHandTracked = leftTracked;
            rightHandTracked = rightTracked;
            if (!leftTracked && !rightTracked)
            {
                leftInitialized = false;
                rightInitialized = false;
                leftStroke = default;
                rightStroke = default;
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateArm(leftTracked, leftY, dt, ref leftInitialized, ref leftWristRawY,
                ref leftWristSmoothedY, ref prevLeftSmoothedY, ref leftVelocityY,
                ref leftCooldownTimer, ref leftInCooldown, ref leftStroke,
                OnLeftFlap, ref totalLeftFlaps, "izquierdo");
            UpdateArm(rightTracked, rightY, dt, ref rightInitialized, ref rightWristRawY,
                ref rightWristSmoothedY, ref prevRightSmoothedY, ref rightVelocityY,
                ref rightCooldownTimer, ref rightInCooldown, ref rightStroke,
                OnRightFlap, ref totalRightFlaps, "derecho");
        }

        private bool TryReadWrist(bool left, out float height)
        {
            height = 0f;
            if (trackingSource == TrackingSource.ExternalCamera)
            {
                if (cameraReceiver == null || !cameraReceiver.IsReceiving) return false;
                height = left ? cameraReceiver.LeftWrist.y : cameraReceiver.RightWrist.y;
                return true;
            }

            if (headTransform == null || xrOrigin == null) return false;
            if (handSubsystem == null || !handSubsystem.running)
            {
                handSubsystem = null;
                SubsystemManager.GetSubsystems(handSubsystems);
                foreach (var subsystem in handSubsystems)
                    if (subsystem.running) { handSubsystem = subsystem; break; }
            }
            if (handSubsystem == null) return false;

            XRHand hand = left ? handSubsystem.leftHand : handSubsystem.rightHand;
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose wristPose))
                return false;

            // XR Hands entrega articulaciones en espacio de seguimiento.
            // Restar la cabeza evita contar el movimiento del cuerpo como un aleteo.
            height = xrOrigin.TransformPoint(wristPose.position).y - headTransform.position.y;
            return true;
        }

        private void UpdateArm(bool tracked, float height, float dt, ref bool initialized,
            ref float rawY, ref float smoothedY, ref float previousY, ref float velocityY,
            ref float cooldownTimer, ref bool inCooldown, ref XRStrokeState stroke,
            UnityEvent<float> flapEvent,
            ref int totalFlaps, string label)
        {
            if (!tracked)
            {
                initialized = false;
                velocityY = 0f;
                stroke = default;
                return;
            }

            rawY = height;
            if (!initialized)
            {
                smoothedY = previousY = rawY;
                initialized = true;
                return;
            }

            float alpha = trackingSource == TrackingSource.QuestHands ? xrSmoothingFactor : settings.smoothingFactor;
            // Conservar la respuesta de filtrado a distintas frecuencias del visor.
            if (trackingSource == TrackingSource.QuestHands) alpha = Mathf.Pow(alpha, dt * 72f);
            float priorY = smoothedY;
            smoothedY = Mathf.Lerp(rawY, smoothedY, alpha);
            velocityY = (smoothedY - previousY) / dt;
            previousY = smoothedY;
            UpdateCooldown(ref cooldownTimer, ref inCooldown, dt);
            if (trackingSource == TrackingSource.QuestHands)
                DetectXRStroke(priorY, smoothedY, velocityY, ref stroke, ref cooldownTimer,
                    ref inCooldown, flapEvent, ref totalFlaps, label);
            else
                TryDetectFlap(velocityY, ref cooldownTimer, ref inCooldown,
                    flapEvent, ref totalFlaps, label);
        }

        // ─── Detección ───

        private void DetectXRStroke(float priorY, float height, float velocityY, ref XRStrokeState stroke,
            ref float cooldownTimer, ref bool inCooldown, UnityEvent<float> flapEvent,
            ref int totalFlaps, string label)
        {
            if (stroke.recovering)
            {
                stroke.lowestY = Mathf.Min(stroke.lowestY, height);
                if (height - stroke.lowestY >= xrRecoveryDistance)
                {
                    stroke.recovering = false;
                    stroke.descending = false;
                }
                return;
            }

            if (velocityY < -xrFlapVelocityThreshold)
            {
                if (!stroke.descending)
                {
                    stroke.descending = true;
                    stroke.startY = priorY;
                    stroke.peakSpeed = 0f;
                }
                stroke.peakSpeed = Mathf.Max(stroke.peakSpeed, -velocityY);
                if (stroke.startY - height >= xrMinimumStrokeDistance && !inCooldown)
                {
                    float normalizedSpeed = Mathf.InverseLerp(
                        xrFlapVelocityThreshold, xrFullIntensitySpeed, stroke.peakSpeed);
                    float intensity = Mathf.Lerp(
                        Mathf.Max(0.3f, settings.minIntensity), settings.maxIntensity,
                        normalizedSpeed);
                    flapEvent.Invoke(intensity);
                    cooldownTimer = settings.flapCooldown;
                    inCooldown = true;
                    totalFlaps++;
                    stroke.recovering = true;
                    stroke.lowestY = height;
                    Debug.Log($"[Anadromo] FLAP XR {label} — intensidad: {intensity:F2} (vel: {stroke.peakSpeed:F2} m/s)");
                }
            }
            else if (velocityY >= 0f)
                stroke.descending = false;
        }

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
            leftInitialized = false;
            rightInitialized = false;
            leftStroke = default;
            rightStroke = default;
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
            settings != null && (leftInitialized || rightInitialized);
    }
}
