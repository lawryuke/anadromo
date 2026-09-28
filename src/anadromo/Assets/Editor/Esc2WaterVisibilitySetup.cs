#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Anadromo.AI;
using Anadromo.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Esc2WaterVisibilitySetup
{
    const string WaterPath = "Assets/_Project/Art/Materials/Esc2-WaterDepthY.mat";
    const string LurePath = "Assets/_Project/Art/Materials/Esc2-LureGlow.mat";
    static Esc2WaterVisibilitySetup() { EditorApplication.update += Request; }
    static void Request()
    {
        const string request = "Library/Esc2WaterVisibility.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action = File.ReadAllText(request).Trim(); File.Delete(request);
        try
        {
            if (action == "install") Install();
            if (action == "test") Esc2WaterVisibilityCheck.Run();
        }
        catch (Exception e) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Esc2WaterVisibilitySetup.txt", "FAIL: " + e); Debug.LogException(e); }
    }
    [MenuItem("Anadromo/Esc2/Visibilidad/Configurar luces")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/esc2.unity") throw new Exception("Abre esc2 antes de configurar.");
        Directory.CreateDirectory("Library/Esc2PiranhaBackup");
        EditorSceneManager.SaveScene(scene, "Library/Esc2PiranhaBackup/esc2-before-light-visibility.unity", true);
        var water = AssetDatabase.LoadAssetAtPath<Material>(WaterPath);
        if (!water || ShaderUtil.ShaderHasError(water.shader)) throw new Exception("Shader de agua no valido.");
        water.SetFloat("_LightVisibilityStart", 12); water.SetFloat("_LightVisibilityEnd", 18);
        EditorUtility.SetDirty(water);
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (!pipeline) throw new Exception("Se necesita URP.");
        var pipelineSettings = new SerializedObject(pipeline);
        pipelineSettings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
        pipelineSettings.ApplyModifiedProperties(); EditorUtility.SetDirty(pipeline);
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Esc2-Renderer.asset");
        foreach (var old in renderer.rendererFeatures.OfType<FullScreenPassRendererFeature>())
            if (old.passMaterial == water) { old.SetActive(false); EditorUtility.SetDirty(old); }
        var feature = renderer.rendererFeatures.OfType<WaterVisibilityFeature>().FirstOrDefault();
        if (!feature)
        {
            feature = ScriptableObject.CreateInstance<WaterVisibilityFeature>(); feature.name = "Water visibility and local lights";
            AssetDatabase.AddObjectToAsset(feature, renderer); renderer.rendererFeatures.Add(feature);
        }
        feature.waterMaterial = water; feature.SetActive(true); feature.Create();
        EditorUtility.SetDirty(feature); renderer.SetDirty(); EditorUtility.SetDirty(renderer);
        var rendererSettings = new SerializedObject(renderer);
        var featureMap = rendererSettings.FindProperty("m_RendererFeatureMap");
        featureMap.arraySize = renderer.rendererFeatures.Count;
        for (int i = 0; i < renderer.rendererFeatures.Count; i++)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string _, out long fileId))
                throw new Exception("No se pudo serializar el renderer feature.");
            featureMap.GetArrayElementAtIndex(i).longValue = fileId;
        }
        rendererSettings.ApplyModifiedProperties();

        int medusas = 0, anglers = 0;
        foreach (var glow in UnityEngine.Object.FindObjectsByType<MedusaGlow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (glow.gameObject.scene != scene) continue;
            glow.waterSettings = water; glow.debugIgnoreDistanceFade = false;
            var light = glow.GetComponent<Light>(); light.shadows = LightShadows.Soft; light.shadowStrength = 1;
            var reveal = glow.GetComponent<WaterRevealLight>();
            if (!reveal) reveal = Undo.AddComponent<WaterRevealLight>(glow.gameObject);
            reveal.radius = 3; reveal.strength = 1;
            Record(glow); Record(light); Record(reveal); medusas++;
        }
        var lureMaterial = AssetDatabase.LoadAssetAtPath<Material>(LurePath);
        if (!lureMaterial)
        {
            lureMaterial = new Material(Shader.Find("Anadromo/Medusa Glow")) { name = "Esc2-LureGlow" };
            lureMaterial.SetColor("_Color", new Color(.75f, 3, 2.1f, 1));
            lureMaterial.SetVector("_Visibility", new Vector4(12, 18, 0, 0));
            AssetDatabase.CreateAsset(lureMaterial, LurePath);
        }
        const string prefabPath = "Assets/_Project/Prefabs/Enemies/PezLinternaEsc2.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try { ConfigureLure(prefab.GetComponentInChildren<Esc2Anglerfish>(true), water, lureMaterial); PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        foreach (var fish in UnityEngine.Object.FindObjectsByType<Esc2Anglerfish>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (fish.gameObject.scene == scene) { ConfigureLure(fish, water, lureMaterial); anglers++; }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/Esc2WaterVisibilitySetup.txt", $"PASS: medusas={medusas}; peces linterna={anglers}; objetos luminosos=12/18m; revelado medusa=3m con sombras; prefab actualizado; distancias del terreno conservadas.");
    }
    static void Record(UnityEngine.Object obj)
    { EditorUtility.SetDirty(obj); if (PrefabUtility.IsPartOfPrefabInstance(obj)) PrefabUtility.RecordPrefabInstancePropertyModifications(obj); }
    static void ConfigureLure(Esc2Anglerfish fish, Material water, Material material)
    {
        if (!fish || !fish.lure) throw new Exception("Pez linterna sin señuelo asignado.");
        if (fish.lureLight) { fish.lureLight.enabled = false; Record(fish.lureLight); }
        var visual = fish.lure.GetComponent<Renderer>();
        visual.sharedMaterial = material; visual.shadowCastingMode = ShadowCastingMode.Off; visual.receiveShadows = false; Record(visual);
        var glow = fish.lure.GetComponent<DistantLureGlow>();
        if (!glow) glow = fish.lure.gameObject.AddComponent<DistantLureGlow>();
        glow.waterSettings = water; Record(glow);
        var reveal = fish.lure.GetComponent<WaterRevealLight>();
        if (reveal) { reveal.enabled = false; Record(reveal); }
    }
}
#endif
