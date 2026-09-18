#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Anadromo.AI;
using Anadromo.Mechanics;
using Anadromo.Locomotion;
using OceanViz3;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

[InitializeOnLoad]
public static class OceanEnvironmentValidation
{
    private const string ScenePath = "Assets/_Project/Scenes/Act1_OpenOcean.unity";
    private static string OutputFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../tools/validation"));
    static OceanEnvironmentValidation() { EditorApplication.update += PendingValidation; }
    private static void PendingValidation()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string request = Path.Combine(OutputFolder, "ocean-environment.request");
        if (!File.Exists(request)) return;
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(request);
        Run();
    }
    [MenuItem("Anadromo/Validate Ocean Environment")]
    public static void Run()
    {
        var report = new List<string>();
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var objects = scene.GetRootGameObjects();
            var terrain = objects.SelectMany(o => o.GetComponentsInChildren<Terrain>()).Where(t => t.gameObject.activeInHierarchy).ToArray();
            Check(terrain.Length == 3, "Three Mediterranean terrain tiles", report);
            Check(terrain.All(t => t.terrainData != null && t.GetComponent<TerrainCollider>() != null), "Terrain data and colliders", report);
            Check(objects.SelectMany(o => o.GetComponentsInChildren<ReflectionProbe>()).Any(p => p.mode == ReflectionProbeMode.Realtime), "Realtime reflections", report);
            Check(objects.SelectMany(o => o.GetComponentsInChildren<VisualEffect>()).Any(v => v.visualEffectAsset != null), "OceanViz debris VFX", report);
            Check(objects.SelectMany(o => o.GetComponentsInChildren<ParticleSystem>()).Any(), "OceanViz particle systems", report);
            Check(objects.SelectMany(o => o.GetComponentsInChildren<Volume>()).Any(v => v.sharedProfile != null), "Mediterranean volume", report);
            var transforms = objects.SelectMany(o => o.GetComponentsInChildren<Transform>()).ToArray();
            Check(transforms.Any(t => t.name == "WaterSurface"), "OceanViz water surface", report);
            var player = transforms.First(t => t.name == "XR Origin (VR)");
            Check(player.GetComponent<DebugVuelo>().enabled, "Keyboard and mouse controller enabled", report);
            Check(!player.GetComponent<SwimLocomotion>().enabled, "VR swim locomotion disabled", report);
            Check(!player.GetComponent<Unity.XR.CoreUtils.XROrigin>().enabled, "XR origin disabled", report);
            Check(!player.GetComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>().enabled, "XR input actions disabled", report);
            Check(player.GetComponentsInChildren<UnityEngine.InputSystem.XR.TrackedPoseDriver>(true).All(driver => !driver.enabled), "Headset tracking disabled", report);
            var xrSettings = File.ReadAllText("Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            Check(!xrSettings.Contains("m_InitManagerOnStart: 1"), "Automatic XR startup disabled", report);
            foreach (string path in new[] { "Assets/New Terrain.asset", "Assets/New Terrain 1.asset" })
                Check(AssetDatabase.LoadAssetAtPath<TerrainData>(path) != null, "Compatible terrain: " + path, report);
            foreach (var t in terrain)
            {
                var local = player.position - t.transform.position;
                if (local.x < 0 || local.z < 0 || local.x > t.terrainData.size.x || local.z > t.terrainData.size.z) continue;
                float floor = t.SampleHeight(player.position) + t.transform.position.y;
                report.Add("Player Y=" + player.position.y + "; terrain Y=" + floor);
                Check(player.position.y > floor + 1f, "Player starts above seabed", report);
            }
            var fish = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Creatures/PezCardumen.prefab");
            Check(fish != null && fish.GetComponent<FishBoid>() != null, "Existing school behavior", report);
            var mesh = fish.GetComponentInChildren<MeshFilter>().sharedMesh;
            Check(mesh != null && mesh.vertexCount == 291 && mesh.triangles.Length == 1350, "Original OceanViz Sea Bass geometry", report);
            Check(mesh.normals.Length == 291 && mesh.uv.Length == 291 && mesh.tangents.Length == 291, "Fish normals, UVs and tangents", report);
            var material = fish.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Check(material != null && material.shader.name == "Shader Graphs/FishAdvancedShaderGraph" && !ShaderUtil.ShaderHasError(material.shader), "Fish swim shader", report);
            Check(transforms.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "Scene scripts resolve", report);
            Check(fish.GetComponentInChildren<OceanFishAnimation>() != null, "GPU animation inputs", report);
            var effect = objects.SelectMany(o => o.GetComponentsInChildren<VisualEffect>()).First();
            var graphSource = File.ReadAllText("Assets/VFX/DebrisEffectGraph.vfx");
            Check(graphSource.Contains("c1d66f9e59448b247b62112124b23213") && transforms.Any(t => t.GetComponent<Camera>() != null && t.CompareTag("MainCamera")), "Debris graph follows main player camera", report);
            ValidateCurrent(report);
            Capture(scene, objects, fish, OutputFolder);
            report.Add("PASS: Ocean environment validation");
            Debug.Log(string.Join("\n", report));
        }
        catch (Exception ex) { report.Add("FAIL: " + ex); Debug.LogException(ex); }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            Directory.CreateDirectory(OutputFolder);
            File.WriteAllLines(Path.Combine(OutputFolder, "ocean-environment.txt"), report);
        }
    }
    private static void ValidateCurrent(List<string> report)
    {
        using (var builder = new BlobBuilder(Allocator.Temp))
        {
            ref var root = ref builder.ConstructRoot<WaterCurrentNoiseBlob>();
            root.Size = 2;
            var samples = builder.Allocate(ref root.Samples, 4);
            for (int i = 0; i < 4; i++) samples[i] = 0.75f;
            var blob = builder.CreateBlobAssetReference<WaterCurrentNoiseBlob>(Allocator.Persistent);
            try
            {
                var settings = new WaterCurrentSettings { Enabled = 1, NoiseScale = 400f, NoiseSpeed = 0.02f, CurrentStrengthMultiplier = 5f, MaxAffectedBoidSize = 1f, HorizontalDirection = new float2(1f, -2.17f), NoiseBlob = blob };
                float3 current = WaterCurrentUtility.SampleHorizontalCurrentVelocity(settings, float3.zero, new float3(1f), 1f, 0.6f, 0.54f, 1f, 1f, false);
                Check(math.length(current) > 0f && current.x > 0f && current.z < 0f && current.y == 0f, "OceanViz current magnitude and direction", report);
                float3 unaffected = WaterCurrentUtility.SampleHorizontalCurrentVelocity(settings, float3.zero, new float3(1f), 1f, 0.6f, 0.54f, 1f, 0f, false);
                Check(math.lengthsq(unaffected) == 0f, "Zero current influence", report);
                settings.Enabled = 0;
                Check(math.lengthsq(WaterCurrentUtility.SampleHorizontalCurrentVelocity(settings, float3.zero, new float3(1f), 1f, 0.6f, 0.54f, 1f, 1f, false)) == 0f, "Disabled currents", report);
            }
            finally { blob.Dispose(); }
        }
    }

    private static void Capture(UnityEngine.SceneManagement.Scene scene, GameObject[] roots, GameObject fish, string folder)
    {
        var transforms = roots.SelectMany(o => o.GetComponentsInChildren<Transform>()).ToArray();
        var flock = transforms.First(t => t.name == "FlockManager");
        for (int i = 0; i < 16; i++)
        {
            var specimen = (GameObject)PrefabUtility.InstantiatePrefab(fish, scene);
            specimen.transform.position = flock.position + new Vector3((i % 4 - 1.5f) * 0.8f, (i / 4 - 1.5f) * 0.4f, 3f + (i % 3));
            specimen.transform.rotation = Quaternion.Euler(0, 40, 0);
        }
        var camera = roots.SelectMany(o => o.GetComponentsInChildren<Camera>()).First();
        camera.cameraType = CameraType.Game;
        var texture = new RenderTexture(960, 540, 24);
        var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            pixels.Apply();
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "ocean-environment.png"), pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    private static void Check(bool condition, string label, List<string> report)
    {
        if (!condition) throw new InvalidOperationException(label);
        report.Add("PASS: " + label);
    }
}
#endif
