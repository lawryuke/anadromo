using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OrcaSceneValidation
{
    [MenuItem("Anadromo/Validate TerrainTestVisuales Orcas")]
    public static void Run()
    {
        const string scenePath = "Assets/_Project/Scenes/TerrainTestVisuales.unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        GameObject sample = null;
        try
        {
            var groups = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OrcaGroupMovement>(true)).ToArray();
            Check(groups.Length == 2 && groups.Sum(g => g.transform.childCount) == 28, "Two groups / 28 orcas");
            Check(!scene.GetRootGameObjects().Any(r => r.GetComponentInChildren<SharkGroupMovement>(true)), "No shark controllers");
            foreach (var group in groups)
                foreach (Transform animal in group.transform)
                {
                    Check(animal.GetComponent<OrcaSwimAnimation>(), "Orca animation is serialized");
                    var mesh = animal.GetComponent<MeshFilter>().sharedMesh;
                    Check(mesh && mesh.vertexCount > 5000 && mesh.subMeshCount == 2, "Orca mesh and eye/body submeshes");
                    Check(mesh.uv.Length == mesh.vertexCount && mesh.tangents.Length == mesh.vertexCount, "UV and tangent detail");
                    foreach (var mat in animal.GetComponent<Renderer>().sharedMaterials)
                    {
                        Check(mat && mat.shader.name == "Anadromo/Orca Natural Swim", "Orca material");
                        Check(mat.GetTexture("_BaseMap") && mat.GetTexture("_BumpMap") && mat.GetTexture("_RoughnessMap"), "Skin maps connected");
                        Check(!ShaderUtil.ShaderHasError(mat.shader), "Orca shader compiles");
                    }
                }
            sample = UnityEngine.Object.Instantiate(groups[0].transform.GetChild(0).gameObject);
            var path = sample.AddComponent<NaturalSwimPath>();
            var animation = sample.GetComponent<OrcaSwimAnimation>();
            var block = new MaterialPropertyBlock();
            foreach (int fps in new[] { 30, 60, 120 })
            {
                path.acceleration = 1.2f;
                Vector3 target = sample.transform.position + Vector3.up * 42;
                path.Begin(target, 4);
                for (int i = 0; i < fps * 25 && path.IsSwimming; i++)
                {
                    path.Tick(1f / fps);
                    animation.Tick(1f / fps);
                    sample.GetComponent<Renderer>().GetPropertyBlock(block);
                    Check(block.GetFloat("_SwimStrength") > 0 && block.GetFloat("_SwimStrength") <= 1, "Stroke strength bounded");
                }
                Check(!path.IsSwimming && Vector3.Distance(sample.transform.position, target) < .001f, "Arrival at " + fps + " FPS");
            }
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/orca-validation.txt", "PASS: 28 orcas, 2 groups, mesh/UV/materials/shader, cadence, arrival at 30/60/120 FPS.");
            Debug.Log("PASS: TerrainTestVisuales orcas");
        }
        finally
        {
            if (sample) UnityEngine.Object.DestroyImmediate(sample);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
