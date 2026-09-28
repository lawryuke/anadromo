using UnityEngine;

namespace Anadromo.Mechanics
{
    /// <summary>Emissive mesh only: no real light and no opening in the surrounding fog.</summary>
    [DisallowMultipleComponent]
    public sealed class DistantLureGlow : MonoBehaviour
    {
        public Material waterSettings;
        [Tooltip("Aplicar también a los renderers hijos del modelo, incluidos los animados.")]
        public bool includeChildRenderers;
        [ColorUsage(false, true)] public Color emission = new Color(.75f, 3, 2.1f);
        Renderer[] visuals;
        MaterialPropertyBlock properties;
        void OnEnable()
        {
            visuals = includeChildRenderers ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();
            properties = new MaterialPropertyBlock();
            LateUpdate();
        }
        void LateUpdate()
        {
            float start = waterSettings ? waterSettings.GetFloat("_LightVisibilityStart") : 12;
            float end = waterSettings ? waterSettings.GetFloat("_LightVisibilityEnd") : 18;
            foreach (var visual in visuals)
            {
                if (!visual) continue;
                visual.GetPropertyBlock(properties);
                properties.SetVector("_Visibility", new Vector4(start, end, 0, 0));
                properties.SetColor("_Color", emission);
                visual.SetPropertyBlock(properties);
            }
        }
    }
}
