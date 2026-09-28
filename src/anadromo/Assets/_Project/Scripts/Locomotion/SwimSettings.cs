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
        [Tooltip("Multiplicador de fuerza para avanzar cuando ambos brazos aletean. (Impulso frontal)")]
        [Range(1f, 300f)]
        public float forwardForceMultiplier = 6f; // Reducido muchísimo de 80 a 6 para evitar "teletransporte"

        [Tooltip("Multiplicador de torque para rotar cuando un solo brazo aletea.")]
        [Range(1f, 50f)]
        public float rotationTorqueMultiplier = 4f; // Reducido de 15 a 4

        [Tooltip("Escala del impulso de un aleteo individual; 1 permite avanzar con una sola mano.")]
        [Range(0f, 1f)]
        public float forwardOnSingleFlapRatio = 1f;

        [Header("Giro con el visor")]
        [Tooltip("Ángulo horizontal de la cabeza desde el que empieza a girar el XR Origin.")]
        [Range(0f, 45f)] public float headTurnDeadzone = 15f;

        [Tooltip("Ángulo desde el frente calibrado que activa el giro rápido.")]
        [Range(16f, 90f)] public float headTurnFastAngle = 35f;

        [Tooltip("Velocidad continua de la zona de giro lento, en grados por segundo.")]
        [Range(1f, 90f)] public float headTurnSlowSpeed = 20f;

        [Tooltip("Suavizado de cambios de velocidad de giro, en grados por segundo al cuadrado.")]
        [Min(1f)] public float headTurnAcceleration = 120f;

        [Tooltip("Velocidad máxima del giro por cabeza, en grados por segundo.")]
        [Range(1f, 180f)] public float headTurnMaxSpeed = 60f;

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

        [Tooltip("Velocidad a la que el cuerpo orienta el avance hacia el giro horizontal del visor.")]
        [Range(0.5f, 15f)]
        public float headYawLerpSpeed = 6f;

        [Tooltip("Restricción del pitch (inclinación) máximo hacia arriba/abajo en grados.")]
        [Range(30f, 90f)]
        public float maxPitchAngle = 80f;

        [Header("Simultaneidad")]
        [Tooltip("Ventana de tiempo (seg.) en que dos aleteos consecutivos de distintos brazos cuentan como simultáneos (Avance).")]
        [Range(0.05f, 0.4f)]
        public float simultaneityWindow = 0.15f;
    }
}
