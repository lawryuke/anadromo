using UnityEngine;

namespace Anadromo.Mechanics
{
    /// <summary>Emissive mesh only: no real light and no opening in the surrounding fog.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Renderer))]
    public sealed class DistantLureGlow : MonoBehaviour
    {
        public Material waterSettings;
        [ColorUsage(false, true)] public Color emission = new Color(.75f, 3, 2.1f);
        Renderer visual;
        MaterialPropertyBlock properties;
        void OnEnable() { visual = GetComponent<Renderer>(); properties = new MaterialPropertyBlock(); LateUpdate(); }
        void LateUpdate()
        {
            if (!visual) return;
            visual.GetPropertyBlock(properties);
            float start = waterSettings ? waterSettings.GetFloat("_LightVisibilityStart") : 12;
            float end = waterSettings ? waterSettings.GetFloat("_LightVisibilityEnd") : 18;
            properties.SetVector("_Visibility", new Vector4(start, end, 0, 0));
            properties.SetColor("_Color", emission);
            visual.SetPropertyBlock(properties);
        }
    }
}
