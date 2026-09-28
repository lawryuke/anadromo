#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Anadromo.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class Esc2PredatorPlayCheck
{
    const string Key = "Esc2PredatorArtSmoke";
    static double started;
    static string failure;
    static Esc2PredatorPlayCheck()
    {
        EditorApplication.playModeStateChanged += Mode;
        if (SessionState.GetBool(Key, false)) Application.logMessageReceived += Log;
    }
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first");
        SessionState.SetString(Key + "Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/esc2.unity");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    static void Log(string message, string stack, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error) &&
            (stack.Contains("PredatorNaturalMotion") || stack.Contains("Esc2Anglerfish") || stack.Contains("Esc2Piranha") || stack.Contains("Esc2Lamprey"))) failure = message;
    }
    static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) { started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Previous", ""));
            SessionState.SetBool(Key, false);
        }
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - started < 6) return;
        try
        {
            if (failure != null) throw new Exception(failure);
            var motions = UnityEngine.Object.FindObjectsByType<PredatorNaturalMotion>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (motions.Length != 7) throw new Exception("Expected 7 predators, found " + motions.Length);
            foreach (var m in motions)
            {
                if (!m.isActiveAndEnabled) continue; // Existing zone activation remains in charge.
                if (m.joints.Any(j => !j.bone || float.IsNaN(j.bone.localRotation.x))) throw new Exception("Invalid skeleton: " + m.name);
                if (!m.joints.Any(j => Quaternion.Angle(j.rest, j.bone.localRotation) > .1f)) throw new Exception("Frozen skeleton: " + m.name);
            }
            int active = motions.Count(m => m.isActiveAndEnabled);
            if (active == 0) throw new Exception("No active predators");
            File.WriteAllText("Logs/PredatorPlayValidation.txt", "PASS: esc2 in Play Mode, 7 installed predators, " + active + " active at the starting phase; valid moving skeletons, no predator runtime errors during 6 seconds. Existing zone activation preserved.");
        }
        catch (Exception e) { File.WriteAllText("Logs/PredatorPlayValidation.txt", "FAIL: " + e); }
        finally { EditorApplication.update -= Tick; EditorApplication.isPlaying = false; }
    }
}
#endif
