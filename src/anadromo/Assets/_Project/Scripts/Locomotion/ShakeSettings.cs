using UnityEngine;

namespace Anadromo.Locomotion
{
    [CreateAssetMenu(fileName = "ShakeSettings", menuName = "Anadromo/Shake Settings")]
    public class ShakeSettings : ScriptableObject
    {
        [Header("Postura de Descanso (Resting Pose)")]
        [Tooltip("Tolerancia en el eje Y para considerar que las muñecas, codos y hombros están alineados (brazos extendidos).")]
        [Range(0.01f, 0.3f)]
        public float restYAlignmentTolerance = 0.1f;

        [Tooltip("Distancia máxima al centro (X=0.5) para considerar que las muñecas están juntas en el cuerpo.")]
        [Range(0.01f, 0.3f)]
        public float restCenterXTolerance = 0.15f;

        [Header("Detección de Sacudida (Shake)")]
        [Tooltip("Umbral mínimo de velocidad de la muñeca para considerar movimiento válido de sacudida.")]
        [Range(0.05f, 2.0f)]
        public float shakeVelocityThreshold = 0.5f;

        [Tooltip("Cantidad de 'energía' de sacudida añadida por cada movimiento válido detectado.")]
        public float shakeEnergyAddRate = 40f;

        [Tooltip("Cantidad de 'energía' que se pierde por segundo si no hay movimiento.")]
        public float shakeEnergyDecayRate = 20f;

        [Tooltip("Energía máxima de sacudida acumulable.")]
        public float maxShakeEnergy = 100f;

        [Header("Ataque de Lamprea")]
        [Tooltip("Energía requerida mantenida para considerar que el jugador se está sacudiendo exitosamente.")]
        public float requiredShakeEnergyLevel = 75f;

        [Tooltip("Tiempo en segundos que el jugador debe mantener la energía sobre el nivel requerido para liberarse.")]
        public float timeRequiredToBreakFree = 3.0f;

        [Tooltip("Tiempo máximo en segundos que el jugador tiene para liberarse antes de morir.")]
        public float maxAttackDuration = 8.0f;

        [Header("Filtrado de Ruido")]
        [Tooltip("Factor de suavizado para calcular la velocidad de movimiento.")]
        [Range(0f, 0.99f)]
        public float smoothingFactor = 0.8f;
    }
}
