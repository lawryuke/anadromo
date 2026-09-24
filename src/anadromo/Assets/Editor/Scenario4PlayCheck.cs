#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Anadromo.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Short real-Play regression: observes falling rigidbodies and emergence, restores the editor afterward.</summary>
[InitializeOnLoad]
public static class Scenario4PlayCheck
{
    const string Key = "Anadromo.Scenario4.PlayCheck";
    const string TestScene = "Assets/_Project/Scenes/Escenario4_PlayVerification.unity";
    static double started;
    static bool began, fallingObserved, captured;
    static float previousRockY = float.NaN;
    static FallingRock observed;
    public static void RunBatch()
    {
        File.WriteAllText("Temp/scenario4-playcheck-request", "run");
    }
    static Scenario4PlayCheck() { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += OnState; }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false))
        {
            if (!File.Exists("Temp/scenario4-playcheck-request") || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!File.Exists("Temp/scenario4-art-upgrade.txt") || !File.ReadAllText("Temp/scenario4-art-upgrade.txt").StartsWith("PASS")) return;
            File.Delete("Temp/scenario4-playcheck-request");
            if (!AssetDatabase.CopyAsset("Assets/_Project/Scenes/Escenario4.unity", TestScene))
            { File.WriteAllText("Temp/scenario4-playcheck.txt", "FAIL: could not create isolated Play scene"); return; }
            SessionState.SetString(Key + ".previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(Key, true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene);
            EditorApplication.isPlaying = true;
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try
        {
            var manager = UnityEngine.Object.FindObjectsByType<Scenario4Manager>(FindObjectsSortMode.None)
                .FirstOrDefault(m => m.gameObject.scene.name == "Escenario4_PlayVerification");
            if (!manager) return;
            if (!began)
            {
                if (manager.Phase != Scenario4Phase.Ready) return;
                // Start has created the runtime water volume and rock pool by this point.
                if (!manager.vision.GetComponent<UnityEngine.Rendering.Volume>()) return;
                started = EditorApplication.timeSinceStartup; began = true;
                manager.BeginEscape();
            }
            double elapsed = EditorApplication.timeSinceStartup - started;
            var rocks = manager.rocks.GetComponentsInChildren<FallingRock>().Where(r => r.IsFalling).ToArray();
            if (observed && observed.isActiveAndEnabled && !float.IsNaN(previousRockY) && observed.transform.position.y < previousRockY - .01f)
                fallingObserved = true;
            if (rocks.Length > 0) { observed = rocks[0]; previousRockY = observed.transform.position.y; }
            if (!captured && elapsed >= 7)
            {
                Capture(manager.viewer, "Temp/scenario4-play-entry.png");
                Quaternion rotation = manager.viewer.transform.rotation;
                manager.viewer.transform.LookAt(manager.bloop.transform.position + Vector3.up);
                Capture(manager.viewer, "Temp/scenario4-play-lair.png");
                manager.viewer.transform.rotation = rotation;
                captured = true;
            }
            if (elapsed < 13) return;
            if (manager.rocks.ReleasedCount < 2 || !fallingObserved)
                throw new Exception("No visible physical falling rocks: spawned=" + manager.rocks.SpawnedCount + ", released=" + manager.rocks.ReleasedCount);
            if (manager.bloop.transform.position.y < manager.bloop.lairOpening.position.y)
                throw new Exception("Bloop did not emerge above its hole");
            File.WriteAllText("Temp/scenario4-playcheck.txt", "PASS: actual Play; " + manager.rocks.SpawnedCount +
                " rocks spawned, " + manager.rocks.ReleasedCount + " released, downward displacement observed; Bloop emerged through aperture. Runtime screenshots saved.");
            EditorApplication.isPlaying = false;
        }
        catch (Exception error)
        {
            File.WriteAllText("Temp/scenario4-playcheck.txt", "FAIL: " + error);
            Debug.LogException(error);
            EditorApplication.isPlaying = false;
        }
    }
    static void OnState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + ".previous", ""));
        SessionState.SetBool(Key, false);
        AssetDatabase.DeleteAsset(TestScene);
        began = fallingObserved = captured = false;
        if (Application.isBatchMode)
            EditorApplication.Exit(File.ReadAllText("Temp/scenario4-playcheck.txt").StartsWith("PASS") ? 0 : 1);
    }
    static void Capture(Camera camera, string path)
    {
        RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1440, 900, 24);
        var image = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
#endif
