using System;
using System.IO;
using System.Linq;
using Anadromo.Locomotion;
using Anadromo.Logic;
using Anadromo.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TerrainVRCollisionCheck
{
    const string Key = "TerrainVRCollisionCheck";
    static int stage, meals;
    static float since;
    static string error;
    static VRBodyCollider player;
    static Rigidbody body;
    static Vector3 surface, normal;
    static PlayerFeeding feeding;

    static TerrainVRCollisionCheck()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += Mode;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception))
                error = message + "\n" + trace;
        };
    }
    [MenuItem("Tools/Anadromo/VR/Test Terrain wall collision")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        error = null;
        SessionState.SetString(Key + "Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/TerrainTestVisuales.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    static void Mode(PlayModeStateChange mode)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (mode == PlayModeStateChange.EnteredPlayMode) { stage = 0; since = Time.time; }
        if (mode == PlayModeStateChange.EnteredEditMode)
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Previous", ""));
            SessionState.SetBool(Key, false);
        }
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Poll()
    {
        const string request = "Temp/terrain-vr-collision-check.request";
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && File.Exists(request))
        { File.Delete(request); Run(); }
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isPaused || stage < 0) return;
        try
        {
            if (error != null) throw new Exception(error);
            Require(Time.time - since < 20f, "Test timeout");
            if (stage == 0 && Time.time - since > 1f)
            {
                var manager = UnityEngine.Object.FindFirstObjectByType<LevelManager>();
                Require(manager && manager.IsReady, "Terrain level did not initialize");
                manager.enabled = false;
                if (manager.swimIntro) manager.swimIntro.enabled = false;
                player = UnityEngine.Object.FindFirstObjectByType<VRBodyCollider>();
                Require(player && player.head, "Saved VR solid body missing");
                player.GetComponent<FlapSwimController>().enabled = false;
                foreach (var driver in player.GetComponentsInChildren<UnityEngine.InputSystem.XR.TrackedPoseDriver>()) driver.enabled = false;
                player.head.localPosition = new Vector3(.3f, 1.5f, .4f);
                body = player.GetComponent<Rigidbody>();
                body.isKinematic = false; body.useGravity = false; body.linearDamping = 0;
                var hull = player.GetComponent<CapsuleCollider>();
                Require(!hull.isTrigger && body.detectCollisions &&
                    body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic, "Body does not block walls");
                Require(player.GetComponent<SphereCollider>().isTrigger, "Feeding mouth is not independent from solid body");
                var wall = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
                    .Single(c => c.name == "Seabed Layered Cliff A LOD0 (1)" && c.enabled);
                Physics.SyncTransforms();
                RaycastHit hit = default;
                bool found = false;
                foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down })
                {
                    float distance = wall.bounds.extents.magnitude + 3f;
                    var ray = new Ray(wall.bounds.center - direction * distance, direction);
                    if (wall.Raycast(ray, out hit, distance * 2f)) { found = true; break; }
                }
                Require(found, "Bloop wall has no collision surface");
                surface = hit.point; normal = hit.normal;
                // Isolate this real wall so another overlapping rock cannot make the check pass.
                foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                    if (collider != wall && !collider.transform.IsChildOf(player.transform)) collider.enabled = false;
                Vector3 destination = player.transform.position + surface + normal * 1.2f - player.head.position;
                player.transform.position = destination; body.position = destination;
                player.SyncBody(); Physics.SyncTransforms();
                Require(Vector3.Distance(hull.transform.TransformPoint(hull.center), player.head.position) < .001f,
                    "Solid body did not follow displaced headset");
                body.linearVelocity = -normal * 3.2f;
                stage = 1; since = Time.time;
            }
            else if (stage == 1 && Time.time - since > .9f)
            {
                Require(Vector3.Dot(player.head.position - surface, normal) > .05f &&
                    Vector3.Dot(body.linearVelocity, normal) > -.1f, "VR swimmer passed through the real Bloop wall");
                feeding = player.GetComponent<PlayerFeeding>();
                feeding.consumptionEnabled = true; feeding.edibleTags = new[] { "Food_PlayerOnly" };
                meals = feeding.TotalConsumed;
                var prey = new GameObject("Collision test krill", typeof(SphereCollider), typeof(Prey));
                prey.tag = "Food_PlayerOnly";
                prey.GetComponent<SphereCollider>().isTrigger = true;
                prey.GetComponent<SphereCollider>().radius = .03f;
                prey.transform.position = player.head.position;
                Physics.SyncTransforms();
                stage = 2; since = Time.time;
            }
            else if (stage == 2 && Time.time - since > .2f)
            {
                Require(feeding.TotalConsumed == meals + 1, "Solid body prevented feeding trigger");
                Finish("PASS: saved VR capsule follows offset headset; continuous rigidbody stops at the real Bloop wall; independent mouth trigger still consumes krill.");
            }
        }
        catch (Exception e) { Finish("FAIL: " + e); }
    }
    static void Finish(string result)
    {
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/TerrainVRCollisionCheck.txt", result);
        stage = -1; EditorApplication.isPlaying = false;
    }
}
