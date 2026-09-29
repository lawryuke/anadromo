#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Anadromo.Logic;
using Anadromo.Locomotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CaveSceneTransitionCheck
{
    const string Key = "Anadromo.CaveSceneTransitionCheck";
    const string Request = "Temp/cave-scene-transition.request";
    const string Result = "Logs/CaveSceneTransitionCheck.txt";
    static int stage;
    static float since;
    static string error;

    static CaveSceneTransitionCheck()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += Mode;
        Application.logMessageReceived += Log;
    }

    [MenuItem("Tools/Anadromo/VR/Check cave to esc2 transition")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        error = null;
        try { SessionState.SetInt(Key + ".ProgressionChecks", LevelProgressionChecks.Run()); }
        catch (Exception e)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Result, "FAIL: progression rules: " + e);
            return;
        }
        SessionState.SetString(Key + ".Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
            "Assets/_Project/Scenes/TerrainTestVisuales.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    static void Poll()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && File.Exists(Request))
        { File.Delete(Request); Run(); }
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try
        {
            if (error != null) throw new Exception(error);
            if (Time.time - since > 20f) throw new Exception("Timed out at stage " + stage);
            if (stage == 0 && Time.time - since > 1f)
            {
                var manager = UnityEngine.Object.FindFirstObjectByType<LevelManager>();
                Check(manager && manager.IsReady && manager.caveZone && manager.endingEffects,
                    "TerrainTestVisuales cave and ending are configured");
                var transition = typeof(LevelManager).GetMethod("EnterEsc2", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(transition != null, "Cave ending has an esc2 transition");
                manager.StartCoroutine((IEnumerator)transition.Invoke(manager, null));
                stage = 1; since = Time.time;
            }
            else if (stage == 1 && SceneManager.GetActiveScene().name == "esc2")
            {
                var vr = UnityEngine.Object.FindFirstObjectByType<VRPlayerGameplay>();
                Check(vr && vr.isActiveAndEnabled, "esc2 opens with active VR_Player");
                var fade = vr.GetComponentInChildren<SceneArrivalFade>();
                Check(fade && fade.GetComponent<CanvasGroup>().alpha > 0f,
                    "Black arrival fade begins on VR camera");
                Check(vr.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length == 1 &&
                    vr.GetComponentInChildren<UnityEngine.UI.Text>(true).text == "MORISTE" &&
                    !vr.GetComponentInChildren<UnityEngine.UI.Text>(true).gameObject.activeInHierarchy,
                    "Arrival fade has no visible text");
                stage = 2; since = Time.time;
            }
            else if (stage == 2 && Time.time - since > 1.8f)
            {
                var vr = UnityEngine.Object.FindFirstObjectByType<VRPlayerGameplay>();
                Check(vr && !vr.GetComponentInChildren<SceneArrivalFade>(),
                    "Arrival fade clears after showing the new scene");
                Finish("PASS: " + SessionState.GetInt(Key + ".ProgressionChecks", 0) + " level progression checks; cave ending loads esc2; " +
                    "VR_Player starts under a black, text-free fade that clears automatically.");
            }
        }
        catch (Exception e) { Finish("FAIL: " + e); }
    }

    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    static void Log(string message, string trace, LogType type)
    {
        if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            error = message + "\n" + trace;
    }

    static void Finish(string result)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Result, result);
        stage = -1;
        EditorApplication.isPlaying = false;
    }

    static void Mode(PlayModeStateChange mode)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (mode == PlayModeStateChange.EnteredPlayMode) { stage = 0; since = Time.time; }
        if (mode == PlayModeStateChange.EnteredEditMode)
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SessionState.GetString(Key + ".Previous", ""));
            SessionState.SetBool(Key, false);
            stage = -1;
        }
    }
}
#endif
