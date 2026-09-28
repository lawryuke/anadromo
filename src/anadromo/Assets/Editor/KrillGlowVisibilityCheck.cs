#if UNITY_EDITOR
using System;
using System.IO;
using Anadromo.Mechanics;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class KrillGlowVisibilityCheck
{
    const string Request = "Temp/krill-glow-visibility.request";
    static KrillGlowVisibilityCheck() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        File.Delete(Request);
        Run();
    }

    [MenuItem("Anadromo/Kril/Verificar visibilidad luminosa")]
    public static void Run()
    {
        GameObject root = null;
        Material settings = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(KrillGlowPrefabSetup.PrefabPath);
            var glow = root.GetComponent<DistantLureGlow>();
            Require(glow && glow.includeChildRenderers, "Falta control de brillo en la raíz");
            Require(AssetDatabase.GetAssetPath(glow.waterSettings) ==
                "Assets/_Project/Art/Materials/TerrainTestVisuales-WaterDepthY.mat", "Material de agua incorrecto");
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                Require(!light.enabled, "Point Light activa");
            int checkedRenderers = 0;
            settings = new Material(glow.waterSettings);
            glow.waterSettings = settings;
            glow.SendMessage("OnEnable");
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                Require(materials.Length == 2 && materials[0] && materials[1] &&
                    materials[1].shader.name == "Anadromo/Medusa Glow", "Falta material original o capa luminosa");
                Require(renderer.sharedMesh.subMeshCount == 1, "La capa luminosa requiere una malla de un submesh");
                Require(!ShaderUtil.ShaderHasError(materials[1].shader), "Shader con errores");
                foreach (var range in new[] { new Vector4(4, 7, 0, 0), new Vector4(20, 40, 0, 0) })
                {
                    settings.SetFloat("_LightVisibilityStart", range.x);
                    settings.SetFloat("_LightVisibilityEnd", range.y);
                    glow.SendMessage("LateUpdate");
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    Require(block.GetVector("_Visibility") == range, "El modelo no recibe los cambios de distancia");
                    Require(block.GetColor("_Color") == glow.emission, "El modelo no recibe la emisión");
                }
                checkedRenderers++;
            }
            Require(checkedRenderers > 0, "No se encontró el modelo animado");
            Require(root.GetComponentInChildren<Animator>(true).runtimeAnimatorController, "Falta animación");
            File.WriteAllText("Temp/krill-glow-visibility.txt",
                "PASS: modelo animado y material original conservados; capa luminosa compatible; " +
                "distancias 4/7 y 20/40 propagadas al renderer; Point Lights apagadas. Sin guardar escenas ni prefab.");
        }
        catch (Exception e) { File.WriteAllText("Temp/krill-glow-visibility.txt", "FAIL: " + e); }
        finally
        {
            if (settings) UnityEngine.Object.DestroyImmediate(settings);
            if (root) PrefabUtility.UnloadPrefabContents(root);
        }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
#endif
