using UnityEngine;

namespace Anadromo.AI
{
    // Supplies the same per-fish GPU animation inputs used by OceanViz's ECS renderer.
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class OceanFishAnimation : MonoBehaviour
    {
        private MeshRenderer fishRenderer;
        private MaterialPropertyBlock properties;
        private float offset;
        private void Awake()
        {
            fishRenderer = GetComponent<MeshRenderer>();
            properties = new MaterialPropertyBlock();
            offset = Random.value * 10f;
        }
        private void LateUpdate()
        {
            fishRenderer.GetPropertyBlock(properties);
            properties.SetFloat("_AccumulatedTime", Time.time);
            properties.SetFloat("_AnimationRandomOffset", offset);
            properties.SetVector("_CurrentVector", Vector4.zero);
            fishRenderer.SetPropertyBlock(properties);
        }
    }
}
