using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Anadromo.CavernMVP; 

public class DamageVignetteController : MonoBehaviour
{
    public float maxIntensity = 0.5f;
    private Vignette vignette;
    private CavernPlayer player;

    void Start()
    {
        player = GetComponent<CavernPlayer>();
        if (player == null) return;

        // 1. Encontrar o crear un volumen global
        Volume globalVolume = FindObjectOfType<Volume>();
        if (globalVolume == null)
        {
            GameObject volObj = new GameObject("DamageVignetteVolume");
            globalVolume = volObj.AddComponent<Volume>();
            globalVolume.isGlobal = true;
        }

        // Asegurar que tiene un perfil
        if (globalVolume.profile == null)
        {
            globalVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        }

        // Buscar Vignette o añadirlo si la escena no lo tenía configurado
        if (!globalVolume.profile.TryGet(out vignette))
        {
            vignette = globalVolume.profile.Add<Vignette>();
        }
        
        // Forzar la configuración correcta para la viñeta de daño
        vignette.active = true;
        vignette.intensity.overrideState = true;
        vignette.color.overrideState = true;
        vignette.smoothness.overrideState = true;
        
        vignette.intensity.value = 0f;
        vignette.color.value = new Color(0.6f, 0f, 0f); // Rojo sangre
        vignette.smoothness.value = 0.8f; // Bordes suaves

        // 2. MUY IMPORTANTE: Asegurar que la cámara del jugador puede ver el Post-Processing
        if (player.view != null)
        {
            var camData = player.view.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null) camData = player.view.gameObject.AddComponent<UniversalAdditionalCameraData>();
            
            camData.renderPostProcessing = true;
        }
    }

    void Update()
    {
        if (vignette != null && player != null)
        {
            vignette.intensity.value = player.DamagePulse * maxIntensity;
        }
    }
}
