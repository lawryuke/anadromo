using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Anadromo.Locomotion
{
    /// <summary>
    /// Viñeta de confort que oscurece los bordes de la visión cuando el jugador
    /// se mueve rápido o rota, para reducir el motion sickness en VR.
    /// 
    /// Fase 5 — F5.2: Viñeta dinámica anti-mareo.
    /// 
    /// Requisitos:
    ///   - Un GameObject con Volume + VolumeProfile que incluya Vignette override.
    ///   - Referencia al Rigidbody del jugador (XR Origin).
    /// 
    /// Uso:
    ///   El script de editor SetupPhase5 crea automáticamente el Volume y el Profile.
    ///   También se puede configurar manualmente:
    ///   1. Crear un Volume (Global) en la escena.
    ///   2. Añadir Vignette override al profile con intensity = 0.
    ///   3. Agregar este script y asignar las referencias.
    /// </summary>
    public class ComfortVignette : MonoBehaviour
    {
        [Header("Dependencias")]
        [Tooltip("Rigidbody del jugador (XR Origin) para leer velocidad.")]
        [SerializeField] private Rigidbody targetRigidbody;

        [Tooltip("Componente Volume con override de Vignette.")]
        [SerializeField] private Volume volume;

        [Header("Configuración")]
        [Tooltip("Velocidad lineal a la que la viñeta alcanza intensidad máxima.")]
        [Range(1f, 20f)]
        [SerializeField] private float velocityForMaxVignette = 5f;

        [Tooltip("Velocidad angular (rad/s) a la que la viñeta alcanza intensidad máxima.")]
        [Range(0.5f, 10f)]
        [SerializeField] private float angularVelForMaxVignette = 3f;

        [Tooltip("Intensidad máxima de la viñeta (0-1). Recomendado: 0.3-0.5 para VR.")]
        [Range(0f, 1f)]
        [SerializeField] private float maxVignetteIntensity = 0.4f;

        [Tooltip("Intensidad mínima (en reposo). 0 = sin viñeta cuando está quieto.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float minVignetteIntensity = 0f;

        [Tooltip("Velocidad de transición. Más alto = responde más rápido al movimiento.")]
        [Range(1f, 20f)]
        [SerializeField] private float smoothSpeed = 8f;

        [Tooltip("Peso relativo de la velocidad angular vs lineal. " +
                 "Valores altos hacen que la rotación oscurezca más que el avance.")]
        [Range(0f, 3f)]
        [SerializeField] private float angularWeight = 1.5f;

        private Vignette vignette;
        private float currentIntensity;

        private void Start()
        {
            if (volume != null && volume.profile != null)
            {
                if (!volume.profile.TryGet(out vignette))
                {
                    Debug.LogWarning("[Anadromo] ComfortVignette: El Volume no tiene un override de Vignette.");
                }
            }
        }

        private void Update()
        {
            if (vignette == null || targetRigidbody == null) return;

            // Calcular contribución de velocidad lineal y angular
            float linearFactor = targetRigidbody.linearVelocity.magnitude / velocityForMaxVignette;
            float angularFactor = (targetRigidbody.angularVelocity.magnitude / angularVelForMaxVignette) * angularWeight;

            // Combinar ambas (el máximo domina, no se suman)
            float combinedFactor = Mathf.Max(linearFactor, angularFactor);
            combinedFactor = Mathf.Clamp01(combinedFactor);

            // Mapear a rango de intensidad
            float targetIntensity = Mathf.Lerp(minVignetteIntensity, maxVignetteIntensity, combinedFactor);

            // Suavizar transición
            currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, Time.deltaTime * smoothSpeed);

            // Aplicar al Volume
            vignette.intensity.Override(currentIntensity);
        }

        private void OnDisable()
        {
            // Restaurar viñeta a 0 al desactivarse
            if (vignette != null)
            {
                vignette.intensity.Override(0f);
            }
        }
    }
}
