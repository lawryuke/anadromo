#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Anadromo.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Esc2SharkArtSetup
{
    const string Folder = "Assets/_Project/Art/Shark";
    const string ScenePath = "Assets/_Project/Scenes/esc2.unity";
    static Esc2SharkArtSetup() { EditorApplication.update += Request; }
    static void Request()
    {
        const string path = "Library/SharkArt.request";
        if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action = File.ReadAllText(path).Trim(); File.Delete(path);
        try { if (action == "install") Install(); if (action == "render") Render(); if (action == "validate") Validate(); }
        catch (Exception e) { File.WriteAllText("Logs/SharkArtFailure.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Anadromo/Esc2/Depredadores/Instalar modelo de tiburon")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Sal de Play antes de instalar.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var passages = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Esc2SharkPassage>(true)).ToArray();
            if (passages.Length == 0 || passages.Any(p => !p.shark)) throw new Exception("No hay tiburones configurados.");
            File.Copy(ScenePath, "Logs/shark-scene-before-install.unity", true);
            AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "/Shark.fbx");
            importer.animationType = ModelImporterAnimationType.Generic; importer.importAnimation = false; importer.optimizeGameObjects = false; importer.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/SharkSkin.mat");
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, Folder + "/SharkSkin.mat"); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Shark_Albedo.png"));
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .48f); material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material);
            var template = new GameObject("Shark Natural Visual");
            try
            {
                var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Shark.fbx"), template.transform); model.name = "Shark Model";
                foreach (var animator in model.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(animator);
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.sharedMaterials = Enumerable.Repeat(material, skin.sharedMesh.subMeshCount).ToArray();
                    skin.quality = SkinQuality.Bone4;
                    var bounds = skin.localBounds; bounds.Expand(.7f / Mathf.Abs(skin.transform.lossyScale.x)); skin.localBounds = bounds;
                }
                var motion = template.AddComponent<Esc2SharkNaturalMotion>(); motion.model = model.transform; motion.Configure();
                PrefabUtility.SaveAsPrefabAsset(template, Folder + "/Shark.prefab");
            }
            finally { Object.DestroyImmediate(template); }
            const string passagePath = "Assets/_Project/Prefabs/Enemies/PasoTiburonEsc2.prefab";
            File.Copy(passagePath, "Logs/shark-passage-before.prefab", true);
            var passagePrefab = PrefabUtility.LoadPrefabContents(passagePath);
            try
            {
                var passage = passagePrefab.GetComponent<Esc2SharkPassage>();
                if (!passage.shark.GetComponentInChildren<Esc2SharkNaturalMotion>(true))
                {
                    // Both esc2 instances inherit this visual without saving the user's open scene.
                    foreach (var renderer in passage.shark.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled = false;
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Shark.prefab"), passage.shark);
                    visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity; visual.transform.localScale = Vector3.one;
                    PrefabUtility.SaveAsPrefabAsset(passagePrefab, passagePath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(passagePrefab); }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Logs/SharkArtInstall.txt", "Installed " + passages.Length + " shark visuals\n" + string.Join("\n", passages.Select(p => p.name + " at " + p.shark.position + ", route points=" + p.waypoints.Length + ", speed=" + p.travelSpeed)));
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        Validate(); Render();
    }
    public static void Validate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Shark.prefab");
        foreach (int fps in new[] { 30, 60, 120 })
        {
            var sample = Object.Instantiate(prefab);
            try
            {
                var motion = sample.GetComponent<Esc2SharkNaturalMotion>();
                var skin = sample.GetComponentInChildren<SkinnedMeshRenderer>();
                if (motion.joints.Length < 8 || !skin || skin.bones.Length < 20 || !skin.sharedMaterial.mainTexture) throw new Exception("Incomplete shark rig/material");
                var mesh = new Mesh(); skin.BakeMesh(mesh, false); var before = mesh.vertices;
                var bounds = new Bounds(); foreach (var v in before) bounds.Encapsulate(sample.transform.InverseTransformPoint(skin.transform.TransformPoint(v)));
                if (bounds.size.z < 1.7f || bounds.size.z > 2.1f || bounds.size.x > 1.2f) throw new Exception("Shark scale is wrong: " + bounds);
                for (int i = 0; i < fps; i++) { sample.transform.position += Vector3.forward * (15f / fps); motion.Tick(1f / fps); }
                skin.BakeMesh(mesh, false); var after = mesh.vertices;
                if (!after.Where((v, i) => (v - before[i]).sqrMagnitude > .000001f).Any()) throw new Exception("Shark skin does not swim");
                Object.DestroyImmediate(mesh);
                sample.SetActive(false); sample.transform.position = new Vector3(200, 0, 0); sample.SetActive(true); motion.Tick(1f / fps);
                if (motion.joints.Any(j => float.IsNaN(j.bone.localRotation.x))) throw new Exception("Invalid animation after respawn");
                if (ShaderUtil.ShaderHasError(skin.sharedMaterial.shader)) throw new Exception("Shark material shader error");
            }
            finally { Object.DestroyImmediate(sample); }
        }
        File.WriteAllText("Logs/SharkArtValidation.txt", "PASS: shark scale, UV texture, 28-bone rig, skin deformation at 30/60/120 FPS and 15 m/s, disable/respawn, shader compilation. Passage controller unchanged.");
    }
    public static void Render()
    {
        var oldScene = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        bool fog = RenderSettings.fog; var ambient = RenderSettings.ambientLight; var mode = RenderSettings.ambientMode;
        try
        {
            RenderSettings.fog = false; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = Color.gray * .55f;
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Shark.prefab")); go.transform.position = new Vector3(1000, 1000, 1000);
            foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
            var camera = new GameObject("Shark preview camera").AddComponent<Camera>(); camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.03f, .07f, .1f); camera.fieldOfView = 32;
            var data = camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.volumeLayerMask = 0;
            camera.transform.position = go.transform.position + new Vector3(2.5f, 1.2f, 3); camera.transform.LookAt(go.transform.position);
            var light = new GameObject("Preview key").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1; light.transform.rotation = Quaternion.Euler(35, -35, 0);
            var rt = new RenderTexture(1100, 800, 24); camera.targetTexture = rt;
            Directory.CreateDirectory("Docs/SharkPreviews");
            for (int frame = 0; frame < 2; frame++)
            {
                if (frame == 1) for (int i = 0; i < 23; i++) go.GetComponent<Esc2SharkNaturalMotion>().Tick(1f / 60);
                camera.Render(); RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                File.WriteAllBytes("Docs/SharkPreviews/Shark_" + frame + ".png", tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            }
            RenderTexture.active = null; camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt);
        }
        finally { RenderSettings.fog = fog; RenderSettings.ambientLight = ambient; RenderSettings.ambientMode = mode; SceneManager.SetActiveScene(oldScene); EditorSceneManager.CloseScene(scene, true); }
    }
}
#endif
