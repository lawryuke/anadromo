using UnityEngine;

namespace Anadromo.Config
{
    /// <summary>
    /// Configuración global del juego. Un solo asset compartido por todos los scripts.
    /// Crear asset: click derecho en Project → Create → Anadromo → Game Settings
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Anadromo/Game Settings")]
    public class GameSettings : ScriptableObject
    {
        // ───────────────────── Singleton de conveniencia ─────────────────────
        private static GameSettings _instance;

        /// <summary>
        /// Acceso global: GameSettings.I.playerSpeed
        /// Se carga automáticamente desde Resources/ si existe, o se puede asignar manualmente.
        /// </summary>
        public static GameSettings I
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<GameSettings>("GameSettings");
                return _instance;
            }
            set => _instance = value;
        }

        // ───────────────────── Movimiento del Jugador (Teclado) ─────────────
        [Header("Movimiento del Jugador (Teclado/Ratón)")]
        [Tooltip("Velocidad normal de movimiento con WASD (m/s).")]
        [Range(0.1f, 10f)]
        public float playerSpeed = 0.7f;

        [Tooltip("Velocidad rápida al mantener Shift (m/s).")]
        [Range(0.5f, 20f)]
        public float playerSprintSpeed = 1.2f;

        [Tooltip("Sensibilidad del ratón para la vista.")]
        [Range(0.01f, 1f)]
        public float mouseSensitivity = 0.05f;

        // ───────────────────── Alimentación ─────────────────────────────────
        [Header("Alimentación")]
        [Tooltip("Radio de la boca del jugador para detectar presas.")]
        [Range(0.2f, 5f)]
        public float mouthRadius = 1f;

        [Tooltip("Comidas necesarias en el abismo para avanzar de fase.")]
        [Min(1)]
        public int requiredAbysmMeals = 5;

        // ───────────────────── Medusa Guía ──────────────────────────────────
        [Header("Medusa Guía")]
        [Tooltip("Velocidad de la medusa bioluminiscente (m/s).")]
        [Range(0.5f, 10f)]
        public float jellyfishSpeed = 2f;

        [Tooltip("¿La medusa espera a que el jugador entre en Abysm_Mid para aparecer?")]
        public bool jellyfishWaitsForAbysm = true;

        // ───────────────────── Cámara y Experiencia ─────────────────────────
        [Header("Cámara y Experiencia")]
        [Tooltip("Límite de ángulo vertical de la cámara (grados).")]
        [Range(30f, 90f)]
        public float cameraPitchLimit = 85f;

        [Tooltip("Tiempo de espera antes de activar el Bloop como alternativa (0 = desactivado).")]
        [Min(0)]
        public float bloopTimeout = 30f;
    }
}
