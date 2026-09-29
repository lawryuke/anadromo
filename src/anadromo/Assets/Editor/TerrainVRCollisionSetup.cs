using System;
using System.IO;
using System.Linq;
using Anadromo.Locomotion;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TerrainVRCollisionSetup
{
    const string Path = "Assets/_Project/Scenes/TerrainTestVisuales.unity";
    static TerrainVRCollisionSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Temp/terrain-vr-collision.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Delete(request);
        try { Configure(); File.WriteAllText("Temp/terrain-vr-collision.result", "PASS"); }
        catch (Exception e) { File.WriteAllText("Temp/terrain-vr-collision.result", e.ToString()); }
    }

    [MenuItem("Tools/Anadromo/VR/Configure Terrain solid body")]
    public static void Configure()
    {
        var scene = SceneManager.GetSceneByPath(Path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(Path, OpenSceneMode.Additive);
        try
        {
            Directory.CreateDirectory("Library/TerrainCollisionBackup");
            const string backup = "Library/TerrainCollisionBackup/TerrainTestVisuales.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
            var origin = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<XROrigin>())
                .Single(o => o.isActiveAndEnabled);
            var hull = origin.GetComponent<CapsuleCollider>();
            if (!hull) hull = Undo.AddComponent<CapsuleCollider>(origin.gameObject);
            hull.isTrigger = false;
            hull.enabled = true;
            hull.radius = .112f;
            hull.height = .56f;
            hull.direction = 2;
            var follower = origin.GetComponent<VRBodyCollider>();
            if (!follower) follower = Undo.AddComponent<VRBodyCollider>(origin.gameObject);
            follower.head = origin.Camera.transform;
            follower.SyncBody();
            var mouth = origin.GetComponent<SphereCollider>();
            if (mouth) mouth.isTrigger = true;
            var body = origin.GetComponent<Rigidbody>();
            body.detectCollisions = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            EditorUtility.SetDirty(hull); EditorUtility.SetDirty(follower); EditorUtility.SetDirty(body);
            if (mouth) EditorUtility.SetDirty(mouth);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("Logs");
            Physics.SyncTransforms();
            File.WriteAllLines("Logs/TerrainColliderAudit.txt", scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Collider>(true))
                .Select(c => c.name + " | " + c.GetType().Name + " | active=" + c.gameObject.activeInHierarchy +
                    " enabled=" + c.enabled + " trigger=" + c.isTrigger + " layer=" + c.gameObject.layer +
                    " center=" + c.bounds.center + " size=" + c.bounds.size));
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
}
