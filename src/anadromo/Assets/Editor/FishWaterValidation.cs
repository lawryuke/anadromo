#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Anadromo.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FishWaterValidation
{
    const string Request = "Temp/fish-water.request";
    static FishWaterValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        File.Delete(Request);
        Run();
    }

    [MenuItem("Anadromo/Validate TerrainTestVisuales Fish Water")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/TerrainTestVisuales.unity");
        string report;
        try
        {
            var water = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FishWaterExperience>()).Single();
            Check(water.viewer && water.surface && water.bubbleMaterial && water.snellMaterial, "Serialized references");
            Check(water.viewer.GetComponentInParent<SimpleFlyCamera>() != null, "Desktop camera controller");
            Check(water.visibility == 10, "Ten metre visual range");
            var quiet = water.SampleFlow(new Vector3(1000, 0, 1000), 4, out float calm);
            Check(quiet == Vector3.zero && calm == 0, "No force or haptics outside currents");
            var shelter = water.SampleFlow(water.zones[3].center, 4, out float sheltered);
            Check(shelter == Vector3.zero && sheltered == 0, "Rock shelter suppresses overlapping current");
            Vector3 flow = water.SampleFlow(water.zones[1].center, 4, out float rough);
            Check(rough > .5f && flow.magnitude > .01f, "Tunnel turbulence active");
            for (int t = 0; t < 200; t++)
                Check(water.SampleFlow(water.zones[1].center, t * .13f, out _).magnitude <= water.maximumDrift + .0001f, "Comfort velocity bound");
            Check(water.SampleFlow(water.zones[1].center, 4, out _) == flow, "Repeatable noise sampling");
            Check(water.SampleFlow(water.zones[1].center, 6, out _) != flow, "Time-varying eddies");
            foreach (var mat in new[] { water.bubbleMaterial, water.snellMaterial })
            {
                ShaderUtil.CompilePass(mat, 0, true);
                Check(!ShaderUtil.ShaderHasError(mat.shader), "Shader: " + mat.shader.name);
            }
            report = "PASS: scene references, 10m visibility, calm water, overlapping shelter, active turbulence, bounded drift, deterministic/time-varying flow, material shader compilation.\n";
        }
        catch (Exception ex) { report = "FAIL: " + ex; Debug.LogException(ex); }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        File.WriteAllText("Temp/fish-water-validation.txt", report);
        Debug.Log(report);
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
