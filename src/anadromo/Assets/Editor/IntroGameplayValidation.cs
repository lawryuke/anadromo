#if UNITY_EDITOR
using System;
using System.IO;
using Anadromo.Logic;
using Anadromo.Mechanics;
using Anadromo.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Exercises the saved scene, including its actual spawned prey and physics triggers.
[InitializeOnLoad]
public static class IntroGameplayValidation
{
    const string Key = "Anadromo.IntroGameplayValidation";
    const string Request = "Temp/intro-gameplay-check.request";
    const string ScenePath = "Assets/_Project/Scenes/TerrainTestVisuales.unity";
    static LevelManager manager;
    static EnergySystem energy;
    static EnergyVisualFeedback visual;
    static float since, initial, beforeMeal;
    static int stage = -1, meals;
    static double deadline;
    static string failure;

    static IntroGameplayValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Mode;
        Application.logMessageReceived += Log;
    }

    [MenuItem("Anadromo/Validate Intro to Gameplay")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        bool useOpenScene = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                if (SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().path != ScenePath)
                {
                    Debug.LogWarning("Intro validation needs TerrainTestVisuales; other unsaved scenes were left untouched.");
                    return;
                }
                // Play the current scene so unsaved edits survive the check without a save prompt.
                useOpenScene = true;
            }
        SessionState.SetString(Key + "Scene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key + "Background", Application.runInBackground);
        SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = useOpenScene ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        EditorApplication.isPlaying = true;
    }

    static void Mode(PlayModeStateChange mode)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (mode == PlayModeStateChange.EnteredPlayMode)
        {
            failure = null;
            stage = 0;
            deadline = EditorApplication.timeSinceStartup + 150;
            Application.runInBackground = true;
        }
        if (mode == PlayModeStateChange.ExitingPlayMode) Time.timeScale = 1;
        if (mode == PlayModeStateChange.EnteredEditMode)
        {
            stage = -1;
            Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SessionState.GetString(Key + "Scene", ""));
            SessionState.SetBool(Key, false);
        }
    }

    static void Log(string message, string trace, LogType type)
    {
        if (stage >= 0 && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
            failure = message + "\n" + trace;
    }

    static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    static void Next() { stage++; since = Time.time; }

    static void Tick()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling &&
            !EditorApplication.isUpdating && File.Exists(Request))
        {
            File.Delete(Request);
            Run();
        }
        if (stage < 0 || !EditorApplication.isPlaying) return;
        try
        {
            if (failure != null) throw new Exception(failure);
            Check(EditorApplication.timeSinceStartup < deadline, "Timeout at stage " + stage);
            switch (stage)
            {
                case 0:
                    manager = LevelManager.Instance;
                    if (!manager || !manager.IsReady) return;
                    Check(manager.swimIntro && manager.openingSlideshow, "Saved intro references");
                    energy = manager.PlayerRoot.GetComponent<EnergySystem>();
                    visual = manager.PlayerRoot.GetComponent<EnergyVisualFeedback>();
                    Check(energy && visual, "Energy and vignette present");
                    initial = energy.StartingEnergy;
                    Check(energy.ConsumptionPaused && energy.CurrentEnergy == initial, "Energy protected from first frame");
                    Next();
                    break;
                case 1:
                    Check(energy.CurrentEnergy == initial, "Story must not consume energy");
                    if (manager.openingSlideshow.IsPlaying || !manager.swimIntro.Revealed) return;
                    // Spend more than the original entire energy budget waiting at the start button.
                    Time.timeScale = 10;
                    Next();
                    break;
                case 2:
                    Check(energy.CurrentEnergy == initial, "Idle menu must not consume energy");
                    if (Time.time - since < 60) return;
                    Time.timeScale = 1;
                    manager.PlayerRoot.GetComponent<PlayerEnergyController>().TakeDamage(100);
                    Check(energy.CurrentEnergy == initial, "Scripted intro protects against damage");
                    manager.StartGame(); // Same entry point as the start button.
                    Check(manager.swimIntro.Launched && !manager.swimIntro.Released, "Button launches scripted swim");
                    Check(energy.ConsumptionPaused && !manager.playerFeeding.consumptionEnabled, "Launch still holds energy and feeding");
                    Next();
                    break;
                case 3:
                    if (Time.time - since < 1) return;
                    Check(energy.CurrentEnergy == initial, "Scripted swim must not consume energy");
                    var zone = manager.initialZone;
                    MoveHead(zone.transform.TransformPoint(zone.center + Vector3.forward * (zone.size.z * .5f + 2)));
                    Next();
                    break;
                case 4:
                    if (!manager.swimIntro.Released) return;
                    Check(manager.currentPhase == GamePhase.KrillFeeding && manager.playerFeeding.consumptionEnabled,
                        "Release enables feeding");
                    Check(!energy.ConsumptionPaused, "Release resumes energy");
                    visual.ApplyFeedback();
                    Check(visual.ExhaustionIntensity > .1f, "Black fatigue vignette remains visible");
                    Next();
                    break;
                case 5:
                    if (Time.time - since < 1) return;
                    Check(energy.CurrentEnergy < initial && energy.CurrentEnergy > 0, "Gameplay drains energy normally");
                    Prey food = null;
                    foreach (var item in manager.krillFirstMid.GeneratedObjects)
                        if (item && item.activeInHierarchy) { food = item.GetComponent<Prey>(); break; }
                    Check(food && food.GetComponentInChildren<Collider>(), "Real first-group krill has edible collider");
                    meals = manager.playerFeeding.TotalConsumed;
                    beforeMeal = energy.CurrentEnergy;
                    MoveHead(food.transform.position);
                    Next();
                    break;
                case 6:
                    if (manager.playerFeeding.TotalConsumed == meals)
                    {
                        Check(Time.time - since < 4, "Real physics trigger must eat first-group krill");
                        return;
                    }
                    Check(energy.CurrentEnergy > beforeMeal, "Krill restores energy");
                    Check(manager.playerFeeding.LastConsumedTag == "Food_PlayerOnly_First", "Correct first-food tag consumed");
                    energy.SetEnergy(0);
                    visual.ApplyFeedback();
                    Check(visual.ExhaustionIntensity >= visual.maximumVignette && visual.Saturation <= -99,
                        "Zero energy preserves fatigue visuals");
                    Check(LevelProgressionChecks.Run() > 0, "Progression regression checks");
                    Finish("PASS: full story; 60 simulated seconds at start menu; scripted swim protects energy; " +
                        "release resumes decay and feeding; real spawned krill eaten via physics restores energy; " +
                        "vignette remains at release and zero energy; level progression checks pass.");
                    break;
            }
        }
        catch (Exception e) { Finish("FAIL stage " + stage + ": " + e); }
    }

    static void MoveHead(Vector3 destination)
    {
        manager.PlayerRoot.position += destination - manager.playerCamera.transform.position;
        var body = manager.PlayerRoot.GetComponent<Rigidbody>();
        if (body) body.position = manager.PlayerRoot.position;
        Physics.SyncTransforms();
    }

    static void Finish(string result)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/IntroGameplayValidation.txt", result + "\n");
        stage = -1;
        Time.timeScale = 1;
        EditorApplication.isPlaying = false;
    }
}
#endif
