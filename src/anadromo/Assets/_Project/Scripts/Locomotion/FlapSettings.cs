using UnityEngine;

namespace Anadromo.Locomotion
{
    /// <summary>
    /// Parámetros configurables para la detección de aleteo.
    /// Editable desde el Inspector sin recompilar.
    /// 
    /// Crear asset: click derecho en Project > Create > Anadromo > Flap Settings
    /// </summary>
    [CreateAssetMenu(fileName = "FlapSettings", menuName = "Anadromo/Flap Settings")]
    public class FlapSettings : ScriptableObject
    {
        [Header("Detección de Aleteo")]
        [Tooltip("Velocidad vertical mínima (descendente) para registrar un aleteo. " +
                 "Valores más altos = requiere aleteos más enérgicos, ignora vibraciones de cámara.")]
        [Range(0.05f, 2.0f)]
        public float flapVelocityThreshold = 0.4f; // Aumentado de 0.15 a 0.4 para ignorar ruido

        [Tooltip("Tiempo mínimo entre aleteos consecutivos del mismo brazo (segundos).")]
        [Range(0.1f, 0.6f)]
        public float flapCooldown = 0.3f; // Aumentado levemente

        [Header("Intensidad")]
        [Tooltip("Intensidad mínima de un aleteo registrado (0-1).")]
        [Range(0f, 0.5f)]
        public float minIntensity = 0.1f;

        [Tooltip("Intensidad máxima de un aleteo (0-1). Cap de intensidad.")]
        [Range(0.5f, 1f)]
        public float maxIntensity = 1.0f;

        [Tooltip("Multiplicador de intensidad. Escala la velocidad del aleteo antes de normalizarla a [min, max].")]
        [Range(0.5f, 10.0f)]
        public float intensityMultiplier = 1.2f; // Reducido para que la intensidad no llegue a 1.0 tan fácilmente

        [Header("Filtrado de Ruido")]
        [Tooltip("Factor de suavizado (filtro pasa-bajos). " +
                 "0 = sin suavizado, 0.95 = casi congelado. " +
                 "Con MediaPipe es necesario un valor ALTO (0.8-0.9) para eliminar el temblor (jitter).")]
        [Range(0f, 0.99f)]
        public float smoothingFactor = 0.85f; // Aumentado drásticamente de 0.3 a 0.85 para absorber el temblor

        [Header("Simultaneidad (Ambos Brazos = Avance)")]
        [Tooltip("Ventana de tiempo (segundos) para considerar que dos aleteos son simultáneos.")]
        [Range(0.05f, 0.4f)]
        public float simultaneityWindow = 0.2f; // Un poco más indulgente para el jugador
    }
}
