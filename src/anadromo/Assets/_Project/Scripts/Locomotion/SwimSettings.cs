using UnityEngine;

namespace Anadromo.Locomotion
{
    /// <summary>
    /// Parámetros de física y movimiento para la locomoción VR por aleteo.
    /// Crear asset en: Project > Create > Anadromo > Swim Settings
    /// </summary>
    [CreateAssetMenu(fileName = "SwimSettings", menuName = "Anadromo/Swim Settings")]
    public class SwimSettings : ScriptableObject
    {
        [Header("Fuerzas de Movimiento")]
        [Tooltip("Multiplicador del impulso frontal de un ciclo de aleteo de cualquier brazo.")]
        [Range(1f, 300f)]
        public float forwardForceMultiplier = 6f; // Reducido muchísimo de 80 a 6 para evitar "teletransporte"

        [Header("Joysticks VR")]
        [Tooltip("Aceleración al mover el joystick izquierdo, en m/s².")]
        [Min(0f)] public float joystickAcceleration = 12f;

        [Tooltip("Velocidad de giro con el joystick derecho, en grados por segundo.")]
        [Min(0f)] public float joystickTurnSpeed = 90f;

        [Tooltip("Zona muerta de ambos joysticks.")]
        [Range(0f, 0.5f)] public float joystickDeadzone = 0.15f;

        [Header("Giro con el visor")]
        [Tooltip("Ángulo horizontal del visor desde el que empieza a girar el XR Origin.")]
        [Range(0f, 45f)]
        public float headTurnDeadzone = 15f;

        [Tooltip("Grados adicionales desde la zona muerta para alcanzar la velocidad máxima de giro.")]
        [Range(1f, 90f)]
        public float headTurnFullSpeedAngle = 30f;

        [Tooltip("Velocidad máxima de giro del XR Origin, en grados por segundo.")]
        [Range(1f, 180f)]
        public float headTurnMaxSpeed = 60f;

        [Tooltip("Velocidad a la que el cuerpo sigue el yaw del visor para orientar el avance.")]
        [Range(1f, 20f)]
        public float yawLerpSpeed = 8f;

        [Header("Velocidades Máximas")]
        [Tooltip("Velocidad lineal máxima permitida (m/s).")]
        [Range(1f, 20f)]
        public float maxLinearVelocity = 6f; // Bajado de 8 a 6

        [Tooltip("Velocidad angular máxima permitida (rad/s).")]
        [Range(1f, 10f)]
        public float maxAngularVelocity = 3f;

        [Header("Comportamiento de Cabeza y Cuerpo")]
        [Tooltip("Velocidad a la que el cuerpo del salmón (pitch) iguala la inclinación de la cabeza del jugador.")]
        [Range(0.5f, 10f)]
        public float pitchLerpSpeed = 3f;

        [Tooltip("Restricción del pitch (inclinación) máximo hacia arriba/abajo en grados.")]
        [Range(30f, 90f)]
        public float maxPitchAngle = 80f;

        [Header("Simultaneidad")]
        [Tooltip("Ventana para combinar impulsos próximos sin duplicar la fuerza. Un brazo ya permite avanzar.")]
        [Range(0.05f, 0.4f)]
        public float simultaneityWindow = 0.12f;
    }
}
