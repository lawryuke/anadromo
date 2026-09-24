using UnityEngine;
using UnityEngine.Events;

namespace Anadromo.Locomotion
{
    /// <summary>
    /// Detecta sacudidas del jugador a partir de los datos
    /// de pose recibidos por ExternalCameraReceiver.
    /// </summary>
    public class ShakeDetector : MonoBehaviour
    {
        [Header("Dependencias")]
        [SerializeField] private Anadromo.Systems.ExternalCameraReceiver cameraReceiver;
        [SerializeField] private ShakeSettings settings;

        [Header("Estado de Sacudida")]
        [Tooltip("Nivel actual de energía de sacudida (0 a maxShakeEnergy)")]
        [SerializeField] private float currentShakeEnergy = 0f;
        
        [Tooltip("¿El jugador está en postura de descanso?")]
        [SerializeField] private bool isRestingPose = true;

        public UnityEvent<float> OnShakeEnergyUpdated = new UnityEvent<float>();

        // Estado interno
        private Vector3 prevLeftWristSmoothed;
        private Vector3 prevRightWristSmoothed;
        private bool initialized;

        private void OnEnable()
        {
            initialized = false;
            currentShakeEnergy = 0f;
        }

        private void Update()
        {
            if (cameraReceiver == null || settings == null || !cameraReceiver.IsReceiving)
                return;

            Vector3 lw = cameraReceiver.LeftWrist;
            Vector3 rw = cameraReceiver.RightWrist;
            Vector3 le = cameraReceiver.LeftElbow;
            Vector3 re = cameraReceiver.RightElbow;
            Vector3 ls = cameraReceiver.LeftShoulder;
            Vector3 rs = cameraReceiver.RightShoulder;

            if (!initialized)
            {
                prevLeftWristSmoothed = lw;
                prevRightWristSmoothed = rw;
                initialized = true;
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 1. Evaluar postura de descanso
            isRestingPose = CheckRestingPose(lw, rw, le, re, ls, rs);

            // 2. Calcular velocidad de las muñecas
            Vector3 currentLeftSmoothed = Vector3.Lerp(prevLeftWristSmoothed, lw, 1f - settings.smoothingFactor);
            Vector3 currentRightSmoothed = Vector3.Lerp(prevRightWristSmoothed, rw, 1f - settings.smoothingFactor);

            float leftVelocity = (currentLeftSmoothed - prevLeftWristSmoothed).magnitude / dt;
            float rightVelocity = (currentRightSmoothed - prevRightWristSmoothed).magnitude / dt;

            prevLeftWristSmoothed = currentLeftSmoothed;
            prevRightWristSmoothed = currentRightSmoothed;

            // 3. Modificar energía de sacudida
            if (!isRestingPose && (leftVelocity > settings.shakeVelocityThreshold || rightVelocity > settings.shakeVelocityThreshold))
            {
                // Jugador se está moviendo fuera de la pose de descanso
                float maxVel = Mathf.Max(leftVelocity, rightVelocity);
                float energyToAdd = settings.shakeEnergyAddRate * (maxVel / settings.shakeVelocityThreshold) * dt;
                currentShakeEnergy = Mathf.Min(currentShakeEnergy + energyToAdd, settings.maxShakeEnergy);
            }
            else
            {
                // Jugador no se mueve lo suficiente o está en pose de descanso
                currentShakeEnergy = Mathf.Max(currentShakeEnergy - settings.shakeEnergyDecayRate * dt, 0f);
            }

            OnShakeEnergyUpdated?.Invoke(currentShakeEnergy);
        }

        private bool CheckRestingPose(Vector3 lw, Vector3 rw, Vector3 le, Vector3 re, Vector3 ls, Vector3 rs)
        {
            // Condición 1: Brazos extendidos horizontalmente (Y alineado)
            bool leftAlignedY = Mathf.Abs(lw.y - le.y) < settings.restYAlignmentTolerance && Mathf.Abs(le.y - ls.y) < settings.restYAlignmentTolerance;
            bool rightAlignedY = Mathf.Abs(rw.y - re.y) < settings.restYAlignmentTolerance && Mathf.Abs(re.y - rs.y) < settings.restYAlignmentTolerance;
            if (leftAlignedY && rightAlignedY) return true;

            // Condición 2: Muñecas cerca del centro del cuerpo (X cerca de 0.5 o muy juntas)
            bool leftNearCenter = Mathf.Abs(lw.x - 0.5f) < settings.restCenterXTolerance;
            bool rightNearCenter = Mathf.Abs(rw.x - 0.5f) < settings.restCenterXTolerance;
            bool wristsTogether = Mathf.Abs(lw.x - rw.x) < settings.restCenterXTolerance;

            if ((leftNearCenter && rightNearCenter) || wristsTogether) return true;

            return false;
        }

        public float GetCurrentEnergy() => currentShakeEnergy;
        public float GetNormalizedEnergy() => currentShakeEnergy / (settings != null ? settings.maxShakeEnergy : 100f);
        public bool HasReachedRequiredEnergy() => currentShakeEnergy >= (settings != null ? settings.requiredShakeEnergyLevel : 75f);
    }
}
