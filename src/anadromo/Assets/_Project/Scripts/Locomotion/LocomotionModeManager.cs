using UnityEngine;

namespace Anadromo.Locomotion
{
    /// <summary>
    /// Permite alternar entre FlapSwimController (locomoción VR por aleteo)
    /// y DebugVuelo (locomoción de debug por teclado/ratón en Editor).
    /// 
    /// Fase 5 — F5.6: Compatibilidad con DebugVuelo.
    /// 
    /// Uso: Presiona la tecla configurable (default: F1) para alternar.
    /// En modo Debug, se desactiva el FlapSwimController y se activa DebugVuelo.
    /// En modo VR, se hace lo inverso.
    /// </summary>
    public class LocomotionModeManager : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Controlador de locomoción por aleteo VR.")]
        [SerializeField] private FlapSwimController flapController;

        [Tooltip("Script de vuelo libre para debug en Editor.")]
        [SerializeField] private MonoBehaviour debugVuelo;

        [Header("Configuración")]
        [Tooltip("Tecla para alternar entre modos.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        [Tooltip("Modo inicial al arrancar. True = VR (aleteo), False = Debug.")]
        [SerializeField] private bool startInVRMode = true;

        private bool isVRMode;

        private void Start()
        {
            isVRMode = startInVRMode;
            ApplyMode();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                isVRMode = !isVRMode;
                ApplyMode();
                string modeName = isVRMode ? "VR (Aleteo)" : "Debug (Teclado)";
                Debug.Log($"[Anadromo] 🔄 Modo de locomoción: {modeName} — Presiona [{toggleKey}] para cambiar");
            }
        }

        private void ApplyMode()
        {
            if (flapController != null)
                flapController.enabled = isVRMode;

            if (debugVuelo != null)
                debugVuelo.enabled = !isVRMode;
        }

        /// <summary>
        /// Cambiar modo programáticamente.
        /// </summary>
        public void SetVRMode(bool vrMode)
        {
            isVRMode = vrMode;
            ApplyMode();
        }
    }
}
