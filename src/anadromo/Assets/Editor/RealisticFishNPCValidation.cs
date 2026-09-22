#if UNITY_EDITOR
using System;
using System.IO;
using Anadromo.AI;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RealisticFishNPCValidation
{
    static RealisticFishNPCValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/realistic-fish.request")) return;
        File.Delete("Temp/realistic-fish.request");
        Run();
    }
    [MenuItem("Anadromo/Validate and Preview PezNPC")]
    public static void Run()
    {
        var preview = new PreviewRenderUtility();
        GameObject fish = null;
        try
        {
            fish = new GameObject("PezNPC preview");
            fish.hideFlags = HideFlags.HideAndDontSave;
            preview.AddSingleGO(fish);
            var filter = fish.AddComponent<MeshFilter>();
            var renderer = fish.AddComponent<MeshRenderer>();
            var source = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Models/OceanViz_SeaBass.asset");
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/PezNPC_RealisticSeaBass.mat");
            filter.sharedMesh = source;
            var npc = fish.AddComponent<RealisticFishNPC>();
            npc.sourceMesh = source;
            npc.BuildMesh();
            var mesh = filter.sharedMesh;
            Check(mesh && mesh != source && mesh.triangles.Length == source.triangles.Length * 16, "Refined independent mesh");
            Check(mesh.uv.Length == mesh.vertexCount && mesh.tangents.Length == mesh.vertexCount, "UV and tangent channels");
            var original = mesh.vertices;
            npc.SendMessage("Animate", .2f);
            var animated = mesh.vertices;
            float maxHead = 0, maxTail = 0;
            for (int i = 0; i < animated.Length; i++)
            {
                Check(!float.IsNaN(animated[i].x) && mesh.bounds.Contains(animated[i]), "Finite vertices inside bounds");
                float shift = Vector3.Distance(original[i], animated[i]);
                if (original[i].z > .12f) maxHead = Mathf.Max(maxHead, shift);
                if (original[i].z < -.25f) maxTail = Mathf.Max(maxTail, shift);
            }
            Check(maxTail > .001f && maxTail > maxHead * 3, "Tail-driven motion with stable head");
            Check(renderer.sharedMaterial.IsKeywordEnabled("_NORMALMAP") && renderer.sharedMaterial.GetTexture("_BumpMap"), "Scale normal map");
            ShaderUtil.CompilePass(renderer.sharedMaterial, 0, true);
            Check(!ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader), "Lit shader compiles");
            preview.camera.transform.position = new Vector3(.65f, .18f, .42f);
            preview.camera.transform.LookAt(new Vector3(0, .02f, -.08f));
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = 10;
            preview.camera.fieldOfView = 40;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.025f, .08f, .105f);
            preview.lights[0].intensity = 1.4f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35, -35, 0);
            preview.lights[1].intensity = .9f;
            preview.lights[1].transform.rotation = Quaternion.Euler(15, 145, 0);
            preview.ambientColor = new Color(.22f, .27f, .3f);
            preview.BeginPreview(new Rect(0, 0, 960, 720), GUIStyle.none);
            preview.Render(true);
            var texture = preview.EndPreview();
            var previous = RenderTexture.active;
            var pixels = new Texture2D(960, 720, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = (RenderTexture)texture;
                pixels.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); pixels.Apply();
                File.WriteAllBytes("Temp/PezNPC-preview.png", pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(pixels); }
            npc.enabled = false;
            Check(filter.sharedMesh == source, "Source mesh restored on disable");
            File.WriteAllText("Temp/realistic-fish-validation.txt", "PASS: refined mesh, UVs, tangents, bounds, tail motion, normal map, Lit shader and cleanup. Preview: Temp/PezNPC-preview.png");
        }
        catch (Exception ex) { File.WriteAllText("Temp/realistic-fish-validation.txt", "FAIL: " + ex); Debug.LogException(ex); }
        finally { preview.Cleanup(); }
    }
    static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
}
#endif
