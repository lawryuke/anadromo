using UnityEngine;
using UnityEngine.Rendering;

namespace Anadromo.Environment
{
    [ExecuteAlways]
    public class UnderwaterEnvironmentPreset : MonoBehaviour
    {
        [Header("Configuración de Niebla Submarina (URP RenderSettings)")]
        public bool habilitarNiebla = true;
        public Color colorNieblaAbisal = new Color(0.015f, 0.045f, 0.08f, 1f); // Azul Cyan Profundo
        public FogMode modoNiebla = FogMode.ExponentialSquared;
        [Range(0.005f, 0.05f)] public float densidadNiebla = 0.018f;

        [Header("Iluminación Ambiental")]
        public Color luzCieloAbisal = new Color(0.04f, 0.12f, 0.22f, 1f);
        public Color luzEcuadorAbisal = new Color(0.02f, 0.06f, 0.12f, 1f);
        public Color luzSueloAbisal = new Color(0.01f, 0.02f, 0.04f, 1f);

        [Header("Cámara Principal")]
        public float planoLejanoCamara = 300f;

        [ContextMenu("🌊 Aplicar Iluminación y Atmósfera Cinematic")]
        public void AplicarConfiguracionSubmarina()
        {
            // 1. Configuración de RenderSettings en la escena
            RenderSettings.fog = habilitarNiebla;
            RenderSettings.fogColor = colorNieblaAbisal;
            RenderSettings.fogMode = modoNiebla;
            RenderSettings.fogDensity = densidadNiebla;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = luzCieloAbisal;
            RenderSettings.ambientEquatorColor = luzEcuadorAbisal;
            RenderSettings.ambientGroundColor = luzSueloAbisal;

            // 2. Ajuste de Cámara Principal
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.farClipPlane = planoLejanoCamara;
                mainCam.backgroundColor = colorNieblaAbisal;
                mainCam.clearFlags = CameraClearFlags.SolidColor;
            }

            Debug.Log("✅ Entorno submarino cinematográfico aplicado con éxito.");
        }

        private void OnEnable()
        {
            AplicarConfiguracionSubmarina();
        }
    }
}
