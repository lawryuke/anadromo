#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Anadromo.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Checks an isolated preview scene; never changes the user's open scene or Play state.</summary>
[InitializeOnLoad]
public static class Scenario4Validation
{
    const string PathName = "Assets/_Project/Scenes/Escenario4.unity";
    static Scenario4Validation() { EditorApplication.delayCall += Run; }

    [MenuItem("Anadromo/Escenario 4/Abrir escena")]
    public static void Open()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(PathName);
    }

    [MenuItem("Anadromo/Escenario 4/Validar escena")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += Run; return; }
        Scene preview = default;
        try
        {
            preview = EditorSceneManager.OpenPreviewScene(PathName);
            var roots = preview.GetRootGameObjects();
            var manager = roots.SelectMany(r => r.GetComponentsInChildren<Scenario4Manager>(true)).Single();
            Require(manager.viewer && manager.swimmer && manager.bloop && manager.rocks && manager.vision && manager.exit,
                "Missing encounter references");
            Require(manager.route.Length == 25 && manager.route.All(r => r), "Route not wired");
            Require(manager.bloop.lairOpening && manager.bloop.emergencePoint && manager.bloop.visualPrepared,
                "Bloop emergence setup missing");
            Require(manager.rocks.ceilingAnchors.Length >= 10 && manager.rocks.ceilingAnchors.All(r => r), "Ceiling sectors missing");
            Require(manager.vision.shelters.Length == 3, "Shelters missing");
            Require(manager.vision.minimumVisibility >= 7, "Insufficient reaction visibility");
            Require(manager.rocks.warningDuration >= 1.5f, "Insufficient warning time");
            // Rock indicators own this material. Bloop telegraphs attacks through its body animation.
            Require(manager.rocks.cueMaterial, "Rock warning material missing");
            Require(manager.rocks.diameterRange.x >= .2f && manager.rocks.diameterRange.y <= .75f, "Rock size exceeds dodge budget");
            Require(manager.bloop.maximumRiseSpeed <= .7f && manager.bloop.attackRiseSpeed <= .85f, "Bloop approaches too quickly");
            CheckAttackCycle();
            Require(manager.swimmer.speed > manager.bloop.maximumRiseSpeed, "Escape speed is insufficient");
            Require(manager.bloop.visual && manager.bloop.visual.GetComponentsInChildren<Renderer>().Length > 0, "Bloop model missing");
            Require(manager.bloop.Contains(manager.bloop.transform.position - Vector3.up), "Capture volume misses center");
            Require(!manager.bloop.Contains(manager.bloop.transform.position + Vector3.up * 4), "Capture volume reaches above mouth");
            foreach (var root in roots)
            {
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject) == 0, "Missing script: " + tr.name);
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    Require(filter.sharedMesh, "Missing mesh: " + filter.name);
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        Require(material && material.shader && !ShaderUtil.ShaderHasError(material.shader), "Missing/invalid material: " + renderer.name);
            }
            // Route clearance against actual authored triangles, including shoulders of the swimmer.
            Physics.SyncTransforms();
            var colliders = roots.SelectMany(r => r.GetComponentsInChildren<MeshCollider>()).ToArray();
            var offsets = new[] { Vector3.zero, Vector3.up * .4f, Vector3.down * .4f,
                Vector3.left * .4f, Vector3.right * .4f, Vector3.forward * .4f, Vector3.back * .4f };
            for (int i = 1; i < manager.route.Length; i++)
            {
                Vector3 delta = manager.route[i].position - manager.route[i - 1].position;
                foreach (var offset in offsets)
                    foreach (var collider in colliders)
                        Require(!collider.Raycast(new Ray(manager.route[i - 1].position + offset, delta.normalized),
                            out _, delta.magnitude), "Route blocked at segment " + i + " by " + collider.name);
            }
            Capture(manager.viewer, preview);
            CaptureWarning(manager, preview);
            File.WriteAllText("Temp/scenario4-validation.txt", "PASS v2: references, shaders, smaller rocks, slow Bloop, locked attack target, dodge/recovery/reset and route clearance. Previews: Temp/scenario4-entry.png and Temp/scenario4-warning.png");
            Debug.Log("Escenario 4: validación completa.");
        }
        catch (Exception e)
        {
            File.WriteAllText("Temp/scenario4-validation.txt", "FAIL: " + e);
            Debug.LogException(e);
        }
        finally { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
    }
    static void CheckAttackCycle()
    {
        var cycle = new BloopAttackCycle();
        Vector3 target = new Vector3(4, 6, 0);
        cycle.Tick(1, Vector3.up * 20, Vector3.zero, 3, 4, 6);
        Require(cycle.Phase == BloopAttackPhase.Stalking, "Attacks from outside engagement distance");
        Require(!cycle.Tick(.1f, target, Vector3.zero, 3, 4, 6), "Immediate unannounced strike");
        Require(cycle.Phase == BloopAttackPhase.Warning, "Attack not announced");
        Vector3 escaped = target + Vector3.right * 4;
        Require(!cycle.Tick(2.9f, escaped, Vector3.zero, 3, 4, 6), "Warning too short");
        Require(cycle.Target == target, "Attack follows the escaping player");
        cycle.Tick(.11f, escaped, Vector3.zero, 3, 4, 6);
        Require(cycle.Phase == BloopAttackPhase.Approaching, "Missing approach phase");
        Require(!cycle.Tick(3.9f, escaped, Vector3.zero, 3, 4, 6), "Approach too short");
        Require(cycle.Tick(.11f, escaped, Vector3.zero, 3, 4, 6), "Attack never resolves");
        Require(!cycle.InStrikeZone(escaped) && cycle.InStrikeZone(target), "Dodge has no effect");
        Require(!cycle.Tick(5.9f, target, Vector3.zero, 3, 4, 6) && cycle.Phase == BloopAttackPhase.Recovering,
            "No recovery window");
        cycle.Reset();
        Require(cycle.Phase == BloopAttackPhase.Stalking && cycle.Target == Vector3.zero, "Attack state leaks through restart");
    }
    static void CaptureWarning(Scenario4Manager manager, Scene scene)
    {
        var go = new GameObject("Preview de aviso de roca");
        SceneManager.MoveGameObjectToScene(go, scene);
        Vector3 point = manager.viewer.transform.position + manager.viewer.transform.forward * 3.5f + Vector3.up * 1.1f;
        go.transform.position = point;
        go.transform.localScale = Vector3.one * .55f;
        go.AddComponent<MeshFilter>().sharedMesh = manager.rocks.rockMesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = manager.rocks.rockMaterial;
        var ring = Scenario4Indicators.Line(manager.transform, "Preview de aro", manager.rocks.cueMaterial, .055f, 41);
        var line = Scenario4Indicators.Line(manager.transform, "Preview de columna", manager.rocks.cueMaterial, .018f, 2);
        ring.startColor = ring.endColor = line.startColor = line.endColor = new Color(1, .66f, .18f, .8f);
        Vector3 landing = point - Vector3.up * 2;
        Scenario4Indicators.Ring(ring, landing, .65f);
        line.SetPosition(0, point); line.SetPosition(1, landing);
        Capture(manager.viewer, scene, "Temp/scenario4-warning.png");
    }
    static void Capture(Camera camera, Scene scene, string outputPath = "Temp/scenario4-entry.png")
    {
        var previous = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(outputPath, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
#endif
