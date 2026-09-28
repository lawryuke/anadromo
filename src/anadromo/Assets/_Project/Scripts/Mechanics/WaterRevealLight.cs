using UnityEngine;

namespace Anadromo.Mechanics
{
    /// <summary>A real point light that also opens a soft, shadowed window in underwater fog.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Light))]
    [AddComponentMenu("Anadromo/Efectos/Water Reveal Light")]
    public sealed class WaterRevealLight : MonoBehaviour
    {
        [Min(.01f), Tooltip("Radio revelado en metros; nunca supera el Range de la luz.")]
        public float radius = 3;
        [Range(0, 1)] public float strength = 1;
        public bool CanReveal(Light source) => isActiveAndEnabled && source.isActiveAndEnabled
            && source.type == LightType.Point && source.intensity > 0
            && source.shadows != LightShadows.None && strength > 0;

        void OnDrawGizmosSelected()
        {
            var source = GetComponent<Light>();
            Gizmos.color = source.color;
            Gizmos.DrawWireSphere(transform.position, Mathf.Min(radius, source.range));
        }
    }
}
