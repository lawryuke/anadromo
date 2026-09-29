#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Anadromo.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Esc2PredatorArtSetup
{
    static Esc2PredatorArtSetup() { EditorApplication.update += Request; }
    static void Request()
    {
        const string path = "Library/PredatorArt.request";
        if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action = File.ReadAllText(path).Trim(); File.Delete(path);
        try { if (action == "install") Install(); if (action == "render") { Validate(); RenderPreviews(); } if (action == "play") Esc2PredatorPlayCheck.Run(); }
        catch (Exception e) { File.WriteAllText("Logs/PredatorArtFailure.txt", e.ToString()); Debug.LogException(e); }
    }
    const string Folder = "Assets/_Project/Art/Predators";
    const string ScenePath = "Assets/_Project/Scenes/esc2.unity";
    [Serializable] class MaterialSpec { public string name, @base, normal; public float roughness; public float[] color; }
    [Serializable] class MaterialSpecs { public MaterialSpec[] materials; }
    [MenuItem("Anadromo/Esc2/Depredadores/Instalar modelos naturales")]
    public static void Install()
    {
        AssetDatabase.Refresh();
        Directory.CreateDirectory(Folder + "/Materials");
        Directory.CreateDirectory(Folder + "/Prefabs");
        foreach (var spec in JsonUtility.FromJson<MaterialSpecs>(File.ReadAllText(Folder + "/materials.json")).materials)
        {
            if (!string.IsNullOrEmpty(spec.normal))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(Folder + "/Textures/" + spec.normal);
                ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport();
            }
            string path = Folder + "/Materials/" + spec.name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", new Color(spec.color[0], spec.color[1], spec.color[2], 1));
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + spec.@base));
            m.SetFloat("_Smoothness", 1 - spec.roughness); m.SetFloat("_Metallic", 0);
            m.SetFloat("_Cull", 0); // Thin fins must remain visible from both sides.
            if (spec.name == "PiranhaSkin")
            { m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", .35f); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = 2450; m.SetOverrideTag("RenderType", "TransparentCutout"); }
            if (!string.IsNullOrEmpty(spec.normal))
            { m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + spec.normal)); m.SetFloat("_BumpScale", .65f); m.EnableKeyword("_NORMALMAP"); }
            if (spec.name.StartsWith("Angler"))
            {
                string rough = Folder + "/Textures/" + spec.name.Substring(6) + "_1001_roughness.jpg";
                if (File.Exists(rough))
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true); texture.LoadImage(File.ReadAllBytes(rough));
                    var pixels = texture.GetPixels32();
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, (byte)(255 - pixels[i].r));
                    texture.SetPixels32(pixels); texture.Apply(); string mask = Folder + "/Textures/" + spec.name + "_Smoothness.png";
                    File.WriteAllBytes(mask, texture.EncodeToPNG()); Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(mask);
                    var ti = (TextureImporter)AssetImporter.GetAtPath(mask); ti.sRGBTexture = false; ti.SaveAndReimport();
                    m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(mask)); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", .8f);
                }
            }
            EditorUtility.SetDirty(m);
        }
        foreach (string species in new[] { "Piranha", "Lamprey", "Angler" })
        {
            string modelPath = Folder + "/" + species + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = false; importer.importBlendShapes = true;
            importer.isReadable = false; importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var root = new GameObject(species + " Natural Visual");
            var model = Object.Instantiate(source, root.transform); model.name = species + " Model";
            foreach (var animator in model.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(animator);
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                r.sharedMaterials = r.sharedMaterials.Select(m => AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + m.name + ".mat")).ToArray();
                if (r.sharedMaterials.Any(m => !m)) throw new Exception("Missing material on " + r.name);
                r.updateWhenOffscreen = false;
                var bounds = r.localBounds; bounds.Expand(.7f / Mathf.Abs(r.transform.lossyScale.x)); r.localBounds = bounds;
                r.quality = SkinQuality.Bone4;
            }
            var motion = root.AddComponent<PredatorNaturalMotion>();
            motion.species = (PredatorNaturalMotion.Species)Enum.Parse(typeof(PredatorNaturalMotion.Species), species);
            motion.model = model.transform; motion.Configure();
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Prefabs/" + species + ".prefab"); Object.DestroyImmediate(root);
        }
        var scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new Exception("La escena tiene cambios sin guardar; guarda antes de instalar modelos.");
            Directory.CreateDirectory("Library/PredatorArtBackup");
            File.Copy(ScenePath, "Library/PredatorArtBackup/esc2.unity", true);
            var enemies = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c is Esc2Piranha || c is Esc2Lamprey || c is Esc2Anglerfish).ToArray();
            foreach (var enemy in enemies)
            {
                var old = enemy.GetComponentInChildren<PredatorNaturalMotion>(true);
                if (old)
                {
                    var existingAngler = enemy as Esc2Anglerfish;
                    if (existingAngler && existingAngler.lure && existingAngler.lure.parent.name == "Lure meter space")
                    {
                        var anchor = existingAngler.lure.parent;
                        anchor.localScale = Vector3.one / Mathf.Abs(anchor.parent.lossyScale.x);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(anchor);
                    }
                    continue;
                }
                var p = enemy as Esc2Piranha; var l = enemy as Esc2Lamprey; var a = enemy as Esc2Anglerfish;
                string species = p ? "Piranha" : l ? "Lamprey" : "Angler";
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/" + species + ".prefab"), enemy.transform);
                var body = p ? p.body : l ? l.body : a.body;
                if (body) { body.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(body); }
                if (a)
                {
                    if (a.lure) { a.lure.gameObject.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(a.lure.gameObject); }
                    var tip = visual.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Esca_9_end") ?? visual.GetComponentsInChildren<Transform>().First(t => t.name == "Esca_9");
                    var anchor = new GameObject("Lure meter space").transform; anchor.SetParent(tip, false);
                    anchor.localScale = Vector3.one / Mathf.Abs(tip.lossyScale.x);
                    var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere); bulb.name = "Senuelo natural";
                    Object.DestroyImmediate(bulb.GetComponent<Collider>()); bulb.transform.SetParent(anchor, false);
                    bulb.transform.localScale = Vector3.one * .03f;
                    string glowPath = Folder + "/Materials/LureGlow.mat";
                    var glow = AssetDatabase.LoadAssetAtPath<Material>(glowPath);
                    if (!glow) { glow = new Material(Shader.Find("Universal Render Pipeline/Lit")); glow.SetColor("_BaseColor", new Color(.35f, .9f, .75f)); glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(.3f, 1, .75f) * 2); AssetDatabase.CreateAsset(glow, glowPath); }
                    bulb.GetComponent<Renderer>().sharedMaterial = glow;
                    a.lure = bulb.transform; a.lureLight = bulb.AddComponent<Light>();
                    a.lureLight.color = new Color(.35f, 1, .8f); a.lureLight.range = 1.6f; a.lureLight.intensity = .65f; a.lureLight.shadows = LightShadows.None;
                    EditorUtility.SetDirty(a); PrefabUtility.RecordPrefabInstancePropertyModifications(a);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            File.WriteAllText("Logs/PredatorArtInstall.txt", "Installed visual replacements: " + enemies.Length + "\n" + string.Join("\n", enemies.Select(e => e.name + " " + e.transform.position)));
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        Validate();
        Esc2PiranhaValidation.Run();
    }

    public static void Validate()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var motions = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PredatorNaturalMotion>(true)).ToArray();
            if (motions.Length == 0) throw new Exception("No installed predator visuals");
            foreach (var original in motions)
            {
                var sample = Object.Instantiate(original.gameObject);
                try
                {
                var motion = sample.GetComponent<PredatorNaturalMotion>();
                if (motion.joints.Length < 5) throw new Exception("Missing joints: " + motion.name);
                foreach (var r in motion.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!r.sharedMesh || r.bones.Length == 0 || r.sharedMaterials.Any(m => !m || !m.mainTexture)) throw new Exception("Incomplete mesh/material: " + r.name);
                    if (r.sharedMaterials.Any(m => ShaderUtil.ShaderHasError(m.shader))) throw new Exception("Shader error");
                }
                var geometry = new Bounds();
                foreach (var r in motion.GetComponentsInChildren<SkinnedMeshRenderer>())
                { var baked = new Mesh(); r.BakeMesh(baked, false); foreach (var v in baked.vertices) geometry.Encapsulate(motion.transform.InverseTransformPoint(r.transform.TransformPoint(v))); Object.DestroyImmediate(baked); }
                if (geometry.size.magnitude < .3f || geometry.size.magnitude > 2f) throw new Exception("Unexpected model size: " + motion.name + " " + geometry);
                var snapshot = motion.joints.Select(j => j.bone.localRotation).ToArray();
                var skin = motion.GetComponentInChildren<SkinnedMeshRenderer>();
                var before = new Mesh(); skin.BakeMesh(before, false);
                var beforeVertices = before.vertices; Object.DestroyImmediate(before);
                foreach (int fps in new[] { 30, 60, 120 })
                    for (int i = 0; i < fps; i++) motion.Tick(1f / fps);
                if (!motion.joints.Where((j, i) => Quaternion.Angle(j.bone.localRotation, snapshot[i]) > .1f).Any()) throw new Exception("No animation: " + motion.name);
                var after = new Mesh(); skin.BakeMesh(after, false);
                var afterVertices = after.vertices; Object.DestroyImmediate(after);
                if (!afterVertices.Where((v, i) => (v - beforeVertices[i]).sqrMagnitude > .00000001f).Any()) throw new Exception("Skin does not deform: " + motion.name);
                if (motion.species != PredatorNaturalMotion.Species.Lamprey)
                {
                    motion.Bite(); motion.Tick(.15f);
                    if (!motion.joints.Where(j => j.channel == 1).Any(j => Quaternion.Angle(j.rest, j.bone.localRotation) > .1f)) throw new Exception("Jaw does not move: " + motion.name);
                }
                }
                finally { Object.DestroyImmediate(sample); }
            }
            File.WriteAllText("Logs/PredatorArtValidation.txt", "PASS: " + motions.Length + " visual replacements; model scale, skin deformation, textures, shader compilation, jaw articulation, bone motion at 30/60/120 FPS.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    public static void RenderPreviews()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var oldActive = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(scene);
        var oldAmbient = RenderSettings.ambientMode; var oldColor = RenderSettings.ambientLight;
        bool oldFog = RenderSettings.fog;
        try
        {
            RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.45f, .5f, .55f);
            var camera = new GameObject("Preview camera").AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .055f, .075f); camera.fieldOfView = 32; camera.nearClipPlane = .01f;
            camera.cullingMask = 1 << 31;
            var cameraData = camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); cameraData.renderPostProcessing = false; cameraData.volumeLayerMask = 0;
            var light = new GameObject("Key").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(35, -35, 0);
            var fill = new GameObject("Fill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .8f; fill.transform.rotation = Quaternion.Euler(0, 140, 0);
            Directory.CreateDirectory("Docs/PredatorPreviews");
            foreach (string species in new[] { "Piranha", "Lamprey", "Angler" })
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/" + species + ".prefab"));
                go.transform.position = new Vector3(1000, 1000, 1000);
                foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
                var motion = go.GetComponent<PredatorNaturalMotion>();
                var bounds = new Bounds(go.transform.position, Vector3.zero);
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                { var baked = new Mesh(); r.BakeMesh(baked, false); foreach (var v in baked.vertices) bounds.Encapsulate(r.transform.TransformPoint(v)); Object.DestroyImmediate(baked); }
                File.AppendAllText("Logs/PredatorPreviewBounds.txt", species + ": " + bounds + "\n" + string.Join("\n", motion.joints.Select(j => j.bone.name + " " + go.transform.InverseTransformPoint(j.bone.position))) + "\n");
                var center = bounds.center; float distance = bounds.size.magnitude * 1.9f;
                camera.transform.position = center + new Vector3(1, .35f, .8f).normalized * distance; camera.transform.LookAt(center);
                var rt = new RenderTexture(1100, 800, 24); camera.targetTexture = rt;
                for (int frame = 0; frame < 3; frame++)
                {
                    if (frame == 1) for (int i = 0; i < 20; i++) motion.Tick(1f / 60);
                    if (frame == 2) { foreach (var j in motion.joints.Where(j => j.channel == 1)) j.bone.localRotation = j.rest * Quaternion.AngleAxis(j.amplitude * .65f, j.axis); }
                    camera.Render(); RenderTexture.active = rt;
                    var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                    File.WriteAllBytes("Docs/PredatorPreviews/" + species + "_" + frame + ".png", tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                }
                camera.targetTexture = null; RenderTexture.active = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
            }
        }
        finally { RenderSettings.fog = oldFog; RenderSettings.ambientMode = oldAmbient; RenderSettings.ambientLight = oldColor; SceneManager.SetActiveScene(oldActive); EditorSceneManager.CloseScene(scene, true); }
    }
}
#endif
