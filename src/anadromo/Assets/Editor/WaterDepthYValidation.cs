#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Read-only asset validation: never opens, saves or replaces the user's live scene.
[InitializeOnLoad]
public static class WaterDepthYValidation
{
    static WaterDepthYValidation() { EditorApplication.delayCall += Run; }

    [MenuItem("Anadromo/Validate Water Depth Y")]
    public static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Run;
            return;
        }
        try
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Materials/TerrainTestVisuales-WaterDepthY.mat");
            if (!material || !material.shader) throw new Exception("Missing depth material/shader");
            // Force compilation of the current graphics API variant without touching the scene.
            if (!material.SetPass(0) || ShaderUtil.ShaderHasError(material.shader))
                throw new Exception("Water Depth Y shader failed compilation");
            if (material.GetFloat("_ShallowY") <= material.GetFloat("_DeepY"))
                throw new Exception("Shallow Y must be above deep Y");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                "Assets/Settings/TerrainTestVisuales-Renderer.asset");
            bool found = false;
            foreach (var feature in renderer.rendererFeatures)
                if (feature is FullScreenPassRendererFeature pass && pass.passMaterial == material)
                    found = pass.isActive && (pass.requirements & ScriptableRenderPassInput.Depth) != 0;
            if (!found) throw new Exception("Depth pass missing or depth texture not requested");
            File.WriteAllText("Temp/water-depth-y-validation.txt", "PASS: shader compiled; material, Y limits and URP depth pass validated. Visual tuning still requires Game view.");
        }
        catch (Exception e)
        {
            File.WriteAllText("Temp/water-depth-y-validation.txt", "FAIL: " + e);
            Debug.LogException(e);
        }
    }
}
#endif
