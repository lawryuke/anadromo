using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Anadromo.Mechanics
{
    /// <summary>Water fog before transparents, with per-camera indices into URP's shadow atlas.</summary>
    public sealed class WaterVisibilityFeature : ScriptableRendererFeature
    {
        public Material waterMaterial;
        WaterPass pass;
        public override void Create() { pass?.Dispose(); pass = new WaterPass(); }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!waterMaterial || renderingData.cameraData.cameraType == CameraType.Preview) return;
            pass.material = waterMaterial;
            renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing) { pass?.Dispose(); }

        sealed class WaterPass : ScriptableRenderPass
        {
            public Material material;
            const int Limit = 16;
            RTHandle copy;
            sealed class CameraProperties
            {
                public readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
                public readonly Vector4[] spheres = new Vector4[Limit], settings = new Vector4[Limit];
            }
            readonly Dictionary<int, CameraProperties> cameraProperties = new Dictionary<int, CameraProperties>();
            static readonly int CountId = Shader.PropertyToID("_WaterRevealCount");
            static readonly int SpheresId = Shader.PropertyToID("_WaterRevealPositionRadius");
            static readonly int SettingsId = Shader.PropertyToID("_WaterRevealSettings");
            public WaterPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }
            MaterialPropertyBlock Properties(NativeArray<VisibleLight> lights, int mainLightIndex, int lightCount, Camera camera)
            {
                int cameraId = camera.GetInstanceID();
                if (!cameraProperties.TryGetValue(cameraId, out var cached))
                { cached = new CameraProperties(); cameraProperties.Add(cameraId, cached); }
                var properties = cached.block;
                var spheres = cached.spheres;
                var settings = cached.settings;
                int count = 0, additionalIndex = 0;
                // Same ordering as ForwardLights.SetupAdditionalLightConstants (including Forward+).
                for (int i = 0; i < lights.Length && additionalIndex < lightCount; i++)
                {
                    if (i == mainLightIndex) continue;
                    var light = lights[i].light;
                    if (count < Limit && light && (camera.cullingMask & (1 << light.gameObject.layer)) != 0
                        && light.TryGetComponent<WaterRevealLight>(out var reveal) && reveal.CanReveal(light))
                    {
                        Vector3 p = light.transform.position;
                        spheres[count] = new Vector4(p.x, p.y, p.z, Mathf.Min(reveal.radius, light.range));
                        settings[count++] = new Vector4(reveal.strength, additionalIndex, 0, 0);
                    }
                    additionalIndex++;
                }
                properties.SetInt(CountId, count);
                properties.SetVectorArray(SpheresId, spheres);
                properties.SetVectorArray(SettingsId, settings);
                return properties;
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var desc = graph.GetTextureDesc(resources.activeColorTexture);
                desc.name = "Water visibility color"; desc.clearBuffer = false;
                var copyTexture = graph.CreateTexture(desc);
                graph.AddBlitPass(resources.activeColorTexture, copyTexture, Vector2.one, Vector2.zero, passName: "Water color copy");
                var lights = frameData.Get<UniversalLightData>();
                var properties = Properties(lights.visibleLights, lights.mainLightIndex, lights.additionalLightsCount, frameData.Get<UniversalCameraData>().camera);
                var parameters = new RenderGraphUtils.BlitMaterialParameters(copyTexture, resources.activeColorTexture, material, 0,
                    properties, RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle);
                using (var builder = graph.AddBlitPass(parameters, passName: "Water visibility and local lights", returnBuilder: true))
                {
                    builder.UseTexture(resources.cameraDepthTexture);
                    // Track URP shadow atlas lifetime as well as depth; required in RenderGraph.
                    builder.UseAllGlobalTextures(true);
                }
            }
#pragma warning disable CS0618, CS0672
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
            {
                var desc = data.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0; desc.msaaSamples = 1;
                RenderingUtils.ReAllocateHandleIfNeeded(ref copy, desc, name: "Water visibility color");
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                var cmd = CommandBufferPool.Get("Water visibility and local lights");
                var color = data.cameraData.renderer.cameraColorTargetHandle;
                Blitter.BlitCameraTexture(cmd, color, copy);
                var properties = Properties(data.lightData.visibleLights, data.lightData.mainLightIndex, data.lightData.additionalLightsCount, data.cameraData.camera);
                properties.SetTexture("_BlitTexture", copy);
                properties.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                CoreUtils.SetRenderTarget(cmd, color);
                cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, properties);
                context.ExecuteCommandBuffer(cmd); CommandBufferPool.Release(cmd);
            }
#pragma warning restore CS0618, CS0672
            public void Dispose() { copy?.Release(); cameraProperties.Clear(); }
        }
    }
}
