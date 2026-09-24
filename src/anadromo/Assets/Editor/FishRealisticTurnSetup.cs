#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Anadromo.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FishRealisticTurnSetup
{
    const string Request = "Temp/fish-turn-setup.request";
    const string Result = "Temp/fish-turn-setup-result.txt";
    static FishRealisticTurnSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Configure();
    }

    [MenuItem("Anadromo/FishRealistic/Configurar giro flexible de Person1")]
    public static void Configure()
    {
        try
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "Person1").gameObject;
            var source = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Models/OceanViz_SeaBass.asset");
            Validate(source, player.GetComponentInChildren<Camera>(true));
            var fish = player.GetComponent<RealisticFishNPC>();
            if (!fish) fish = Undo.AddComponent<RealisticFishNPC>(player);
            Undo.RecordObject(fish, "Configurar giro flexible FishRealistic");
            var filter = player.GetComponent<MeshFilter>();
            // Keep the selected model if readable; otherwise use the existing editable Sea Bass mesh.
            if (!fish.sourceMesh)
                fish.sourceMesh = filter.sharedMesh && filter.sharedMesh.isReadable ? filter.sharedMesh : source;
            fish.turnLag = true;
            fish.tailFollowTime = .65f;
            fish.maxTurnLag = 160f;
            fish.BuildMesh();
            EditorUtility.SetDirty(fish);
            EditorSceneManager.MarkSceneDirty(scene);
            File.WriteAllText(Result, "PASS: left/right turns, head stability, tail in camera view, bounds, return to rest, 30/60/120 fps, disable restores mesh. Person1 configured in open scene; save scene to persist.");
        }
        catch (Exception ex) { File.WriteAllText(Result, "FAIL: " + ex); Debug.LogException(ex); }
    }

    static void Validate(Mesh source, Camera reference)
    {
        if (!source || !source.isReadable) throw new Exception("Missing readable Sea Bass source");
        foreach (int fps in new[] { 30, 60, 120 })
        foreach (float angle in new[] { -170f, 170f })
        {
            var go = new GameObject("Fish turn validation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                go.transform.localScale = Vector3.one * 1.4f;
                var filter = go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
                filter.sharedMesh = source;
                var fish = go.AddComponent<RealisticFishNPC>();
                fish.sourceMesh = source; fish.turnLag = true; fish.tailAmplitude = 0;
                fish.BuildMesh();
                fish.SendMessage("Animate", 0f);
                var original = filter.sharedMesh.vertices;
                var cameraObject = new GameObject("Validation camera");
                cameraObject.transform.SetParent(go.transform, false);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.transform.localPosition = reference ? reference.transform.localPosition : new Vector3(0,.0331f,.1452f);
                camera.transform.localRotation = reference ? reference.transform.localRotation : Quaternion.Euler(28.856f,0,0);
                camera.fieldOfView = reference ? reference.fieldOfView : 60;
                camera.aspect = reference ? reference.aspect : 16f / 9f;
                camera.nearClipPlane = .01f;
                go.transform.rotation = Quaternion.Euler(0, angle, 0);
                fish.SendMessage("UpdateTurnLag", 1f / fps);
                fish.SendMessage("Animate", 0f);
                var bent = filter.sharedMesh.vertices;
                bool visibleTail = false;
                float maxTailShift = 0;
                for (int i=0;i<bent.Length;i++)
                {
                    if (float.IsNaN(bent[i].x) || !filter.sharedMesh.bounds.Contains(bent[i])) throw new Exception("Invalid bounds");
                    if (original[i].z >= .1f && Vector3.Distance(original[i],bent[i]) > .00001f) throw new Exception("Head moved");
                    if (original[i].z < -.25f)
                    {
                        maxTailShift = Mathf.Max(maxTailShift,Vector3.Distance(original[i],bent[i]));
                        Vector3 v = camera.WorldToViewportPoint(go.transform.TransformPoint(bent[i]));
                        visibleTail |= v.z > camera.nearClipPlane && v.x > 0 && v.x < 1 && v.y > 0 && v.y < 1;
                    }
                }
                if (maxTailShift < .2f || !visibleTail) throw new Exception("Tail not visible at " + fps + " fps, angle " + angle);
                for(int frame=0;frame<fps*6;frame++) fish.SendMessage("UpdateTurnLag",1f/fps);
                fish.SendMessage("Animate",0f);
                var settled = filter.sharedMesh.vertices;
                for(int i=0;i<settled.Length;i++)
                    if(Vector3.Distance(settled[i],original[i]) > .001f) throw new Exception("Body did not settle");
                fish.enabled = false;
                if(filter.sharedMesh != source) throw new Exception("Source not restored");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
#endif
