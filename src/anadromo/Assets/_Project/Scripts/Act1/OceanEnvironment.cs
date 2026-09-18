using OceanViz3;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Anadromo.Act1
{
    // Connects OceanViz's shared procedural water field to the existing GameObject actors.
    public sealed class OceanEnvironment : MonoBehaviour
    {
        public static OceanEnvironment Instance { get; private set; }
        [SerializeField] private float playerInfluence = 0.15f;
        private World world;
        private EntityQuery currentQuery;
        private bool hasQuery;
        private int sampledFrame = -1;
        private WaterCurrentSettings settings;

        private void OnEnable()
        {
            Instance = this;
            // Mediterranean location settings, matching OceanViz3.
            NoiseTextureManager.Instance.ApplyLocationWaterCurrentSettings(
                true, 512, 50f, 400f, 0.02f, 5f, 4f, 0.02f,
                1f, 0.24f, 0.34f, 17f, 1f, new Vector2(1f, -2.17f));
        }

        private void LateUpdate()
        {
            // The narrative uses GameObject actors, so animation follows the game clock.
            Shader.SetGlobalFloat(WaterCurrentShaderProperties.Time, Time.time);
        }

        public static Vector3 CurrentAt(Vector3 position, float speed = 0.6f, float size = 0.54f, float influence = 1f)
        {
            var environment = Instance;
            return environment == null ? Vector3.zero : environment.Sample(position, speed, size, influence);
        }

        public static Vector3 PlayerCurrentAt(Vector3 position)
        {
            return Instance == null ? Vector3.zero : CurrentAt(position, 0.6f, 0.54f, Instance.playerInfluence);
        }

        private Vector3 Sample(Vector3 position, float speed, float size, float influence)
        {
            var activeWorld = World.DefaultGameObjectInjectionWorld;
            if (activeWorld == null || !activeWorld.IsCreated) return Vector3.zero;
            if (!hasQuery || world != activeWorld)
            {
                ReleaseQuery();
                world = activeWorld;
                currentQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<WaterCurrentSettings>());
                hasQuery = true;
                sampledFrame = -1;
            }
            if (sampledFrame != Time.frameCount)
            {
                sampledFrame = Time.frameCount;
                settings = currentQuery.CalculateEntityCount() == 1
                    ? currentQuery.GetSingleton<WaterCurrentSettings>() : default;
            }
            if (!settings.NoiseBlob.IsCreated || settings.Enabled == 0) return Vector3.zero;
            float3 current = WaterCurrentUtility.SampleHorizontalCurrentVelocity(
                settings, (float3)position, new float3(1f), Time.time, speed, size, 1f, influence, false);
            return (Vector3)current;
        }

        private void ReleaseQuery()
        {
            if (hasQuery && world != null && world.IsCreated) currentQuery.Dispose();
            hasQuery = false;
        }
        private void OnDisable()
        {
            ReleaseQuery();
            if (Instance == this) Instance = null;
        }
    }
}
