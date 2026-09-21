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

        [Tooltip("Si es > 0, un aleteo individual también empuja levemente hacia adelante.")]
        [Range(0f, 1f)]
        public float forwardOnSingleFlapRatio = 0.1f;

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
        [Tooltip("Ventana de tiempo (seg.) en que dos aleteos consecutivos de distintos brazos cuentan como simultáneos (Avance).")]
        [Range(0.05f, 0.4f)]
        public float simultaneityWindow = 0.15f;
    }
}
