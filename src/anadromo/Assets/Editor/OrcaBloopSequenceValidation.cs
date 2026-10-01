#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using Anadromo.Logic;
using Anadromo.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class OrcaBloopSequenceValidation
{
    const string Key = "Anadromo.OrcaBloopSequenceValidation";
    static LevelManager manager;
    static OrcaGroupMovement pool;
    static Mouse mouse;
    static int stage = -1, launches, oldManagerId;
    static float since, phaseStarted;
    static double deadline;
    static string failure;
    static Vector3 spawn;

    static OrcaBloopSequenceValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Mode;
        Application.logMessageReceived += Log;
    }

    [MenuItem("Anadromo/Validate Orca Duration and Bloop Death")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + "Background", Application.runInBackground);
        SessionState.SetString(Key + "Scene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = null;
        EditorApplication.isPlaying = true;
    }

    static void Mode(PlayModeStateChange mode)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (mode == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0; failure = null;
            deadline = EditorApplication.timeSinceStartup + 90;
            Application.runInBackground = true;
            mouse = InputSystem.AddDevice<Mouse>();
        }
        if (mode == PlayModeStateChange.ExitingPlayMode && mouse != null)
        { InputSystem.RemoveDevice(mouse); mouse = null; }
        if (mode == PlayModeStateChange.EnteredEditMode)
        {
            stage = -1;
            Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Scene", ""));
            SessionState.SetBool(Key, false);
        }
    }

    static void Log(string message, string trace, LogType type)
    {
        if (stage >= 0 && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            failure = message + "\n" + trace;
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Next() { stage++; since = Time.time; }
    static void EnterPhase() => typeof(LevelManager).GetMethod("EnterPhase", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);

    static void SetupPool()
    {
        var go = new GameObject("Continuous orca regression pod");
        go.transform.position = new Vector3(1000, 1000, 1000);
        var animal = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Art/Models/Orca/Orca.prefab"), go.transform);
        animal.transform.localPosition = Vector3.zero;
        foreach (var source in animal.GetComponentsInChildren<AudioSource>()) source.Stop();
        pool = go.AddComponent<OrcaGroupMovement>();
        pool.allowKeyboard = false; pool.velocidad = 10; pool.aceleracion = 1000;
        pool.delayMinimo = pool.delayMaximo = 0;
        pool.BeginAscent(1000.3f, 1.5f);
    }

    static void Tick()
    {
        const string request = "Temp/orca-bloop-check.request";
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists(request))
        { File.Delete(request); Run(); }
        if (stage < 0 || !EditorApplication.isPlaying) return;
        try
        {
            if (failure != null) throw new Exception(failure);
            Check(EditorApplication.timeSinceStartup < deadline, "Timeout stage " + stage);
            switch (stage)
            {
                case 0:
                    manager = LevelManager.Instance;
                    if (!manager || !manager.IsReady) return;
                    Check(LevelProgressionChecks.Run() > 0, "Phase progression regression");
                    spawn = manager.PlayerRoot.position;
                    Check(manager.bloopMovement.lethalBody && !manager.bloopMovement.lethalBody.enabled, "Fitted Bloop capsule dormant before spawn");
                    SetupPool();
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                    Next(); break;
                case 1:
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    if (Time.time - since < 1.8f || !manager.swimIntro.Revealed) return;
                    Check(pool.TotalLaunches > pool.MemberCount && pool.MemberCount == 1 && pool.transform.childCount == 1,
                        "Continuous launches exceed initial population with bounded objects");
                    Check(!pool.IsSpawning, "Timed pool stops");
                    launches = pool.TotalLaunches;
                    Next(); break;
                case 2:
                    if (Time.time - since < .3f) return;
                    Check(pool.TotalLaunches == launches, "No late launches after duration");
                    UnityEngine.Object.Destroy(pool.gameObject);
                    manager.StartGame();
                    Check(manager.currentPhase == GamePhase.Init, "Start entry point");
                    // Drive the pure food milestones; test the real manager's phase effects and timer.
                    var progress = (LevelProgression)typeof(LevelManager).GetField("progress", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                    progress.Tick(true, false, false, 0, 1, false, false, false, -100, -5, 12, false, true);
                    EnterPhase();
                    progress.Tick(true, true, false, 0, 1, false, false, false, -100, -5, 12, false);
                    EnterPhase();
                    progress.Tick(true, true, true, 1, 1, false, false, false, -100, -5, 12, false);
                    manager.orcaSpawnDuration = 1.5f;
                    EnterPhase();
                    phaseStarted = Time.time;
                    Next(); break;
                case 3:
                    if (Time.time - phaseStarted < 1.4f)
                    {
                        Check(!manager.bloopMovement.HasStarted, "Bloop cannot launch early");
                        return;
                    }
                    if (!manager.bloopMovement.HasStarted) return;
                    Check(Time.time - phaseStarted >= 1.5f, "Bloop starts after N seconds");
                    foreach (var group in manager.orcaGroups) Check(!group.IsSpawning, "All scene pods stop before Bloop");
                    Check(manager.bloopMovement.lethalBody.enabled, "Bloop capsule armed at launch");
                    var mouth = manager.playerFeeding.GetComponent<SphereCollider>();
                    Check(!manager.KillPlayerFromBloop(mouth), "Mouth trigger cannot cause a proximity death");
                    var capsule = manager.bloopMovement.lethalBody;
                    Vector3 destination = capsule.transform.TransformPoint(capsule.center);
                    var body = manager.PlayerRoot.GetComponent<Rigidbody>();
                    manager.PlayerRoot.position += destination - manager.playerCamera.transform.position;
                    body.position = manager.PlayerRoot.position;
                    Physics.SyncTransforms();
                    oldManagerId = manager.GetInstanceID();
                    Next(); break;
                case 4:
                    if (!manager.IsRestarting) { Check(Time.time - since < 2, "Real Bloop capsule must kill player"); return; }
                    Check(manager.PlayerRoot.GetComponent<EnergySystem>().CurrentEnergy == 0, "Contact kills instantly");
                    Check(!manager.playerFeeding.consumptionEnabled, "Dead player cannot eat");
                    Check(!manager.KillPlayerFromBloop(manager.PlayerRoot.GetComponent<CapsuleCollider>()), "Repeated contact cannot reload twice");
                    Next(); break;
                case 5:
                    var restarted = LevelManager.Instance;
                    if (!restarted || restarted.GetInstanceID() == oldManagerId || !restarted.IsReady) return;
                    Check(restarted.currentPhase == GamePhase.WaitingForStart && !restarted.IsRestarting, "Fresh level at initial phase");
                    Check(Vector3.Distance(restarted.PlayerRoot.position, spawn) < .05f, "Player respawns at initial position");
                    var energy = restarted.PlayerRoot.GetComponent<EnergySystem>();
                    Check(energy.CurrentEnergy == energy.StartingEnergy && energy.ConsumptionPaused, "Fresh protected starting energy");
                    Check(!restarted.bloopMovement.HasStarted && !restarted.bloopMovement.lethalBody.enabled, "Bloop reset on restart");
                    foreach (var group in restarted.orcaGroups) Check(group.TotalLaunches == 0 && !group.IsSpawning, "Orcas reset on restart");
                    Finish("PASS: repeated orca launches with bounded pool; N-second stop; no premature Bloop; " +
                        "real capsule contact kills once; mouth trigger ignored; full scene reload restores spawn, energy, orcas and Bloop; phase regressions pass.");
                    break;
            }
        }
        catch (Exception e) { Finish("FAIL stage " + stage + ": " + e); }
    }

    static void Finish(string result)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/OrcaBloopSequenceValidation.txt", result + "\n");
        stage = -1;
        EditorApplication.isPlaying = false;
    }
}
#endif
