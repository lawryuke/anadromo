#if UNITY_EDITOR
using System;
using System.IO;
using Anadromo.Locomotion;
using Anadromo.Mechanics;
using Anadromo.Systems;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class OrcaPushValidation
{
    const string Key = "Anadromo.OrcaPushValidation";
    static GameObject player, orca;
    static Rigidbody body;
    static EnergySystem energy;
    static PlayerOrcaImpact impact;
    static EnergyVisualFeedback visual;
    static SwimSettings settings;
    static Keyboard keyboard;
    static Mouse mouse;
    static Vector3 contact, away, start;
    static float since;
    static double deadline;
    static int stage = -1, scenario;
    static string failure;

    static OrcaPushValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Mode;
        Application.logMessageReceived += Log;
    }

    [MenuItem("Anadromo/Validate Orca Push")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + "Background", Application.runInBackground);
        // Use the open scene: Unity restores any unsaved edits when leaving Play.
        SessionState.SetString(Key + "Scene", AssetDatabase.GetAssetPath(UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene));
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null;
        EditorApplication.isPlaying = true;
    }

    static void Mode(PlayModeStateChange mode)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (mode == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true;
            stage = scenario = 0;
            failure = null;
            deadline = EditorApplication.timeSinceStartup + 90;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
        }
        if (mode == PlayModeStateChange.ExitingPlayMode)
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            keyboard = null; mouse = null;
        }
        if (mode == PlayModeStateChange.EnteredEditMode)
        {
            stage = -1;
            Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Scene", ""));
            SessionState.SetBool(Key, false);
        }
    }

    static void Log(string message, string stack, LogType type)
    {
        if (stage >= 0 && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            failure = message + "\n" + stack;
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next() { stage++; since = Time.time; }
    static void Move(Vector3 position)
    {
        body.position = player.transform.position = position;
        body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
    }

    static void Setup()
    {
        Vector3 location = new Vector3(1000 + scenario * 40, 1000, 1000);
        string model = scenario % 2 == 0 ? "Orca" : "Orca2";
        orca = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Art/Models/" + model + "/Orca.prefab"), location, Quaternion.identity);
        foreach (var source in orca.GetComponentsInChildren<AudioSource>()) source.Stop();
        var push = orca.GetComponent<OrcaPlayerPush>();
        Check(push && push.pushClip && AssetDatabase.GetAssetPath(push.pushClip) == "Assets/_Project/Audio/SFX/empuje.wav", "Prefab binds empuje.wav");
        var hull = orca.GetComponent<CapsuleCollider>();
        Check(hull && hull.enabled && hull.isTrigger, "Orca has fitted body volume");
        Vector3 radial = Vector3.zero;
        radial[(hull.direction + 1) % 3] = 1;
        away = orca.transform.TransformDirection(radial).normalized;
        float scale = Mathf.Max(Mathf.Abs(orca.transform.lossyScale[(hull.direction + 1) % 3]),
            Mathf.Abs(orca.transform.lossyScale[(hull.direction + 2) % 3]));
        contact = orca.transform.TransformPoint(hull.center) + away * (hull.radius * scale + .1f);
        player = new GameObject("Orca push regression player");
        player.SetActive(false);
        player.transform.position = contact + away * 5;
        body = player.AddComponent<Rigidbody>();
        body.useGravity = false; body.linearDamping = 2;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        player.AddComponent<SphereCollider>().radius = .2f;
        var camera = new GameObject("Test camera").AddComponent<Camera>();
        camera.transform.SetParent(player.transform, false);
        camera.enabled = false;
        player.AddComponent<PlayerEnergyController>();
        energy = player.GetComponent<EnergySystem>();
        energy.enabled = false;
        energy.SetConsumptionPaused(true);
        visual = player.AddComponent<EnergyVisualFeedback>();
        if (scenario < 2) player.AddComponent<SimpleFlyCamera>();
        else
        {
            settings = ScriptableObject.CreateInstance<SwimSettings>();
            var fins = new GameObject("Swim direction").transform;
            fins.SetParent(player.transform, false);
            var flap = player.AddComponent<FlapSwimController>();
            var serialized = new SerializedObject(flap);
            serialized.FindProperty("settings").objectReferenceValue = settings;
            serialized.FindProperty("headTransform").objectReferenceValue = camera.transform;
            serialized.FindProperty("salmonBody").objectReferenceValue = fins;
            serialized.FindProperty("applyOceanCurrents").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        player.SetActive(true);
        impact = player.AddComponent<PlayerOrcaImpact>();
    }

    static void Tick()
    {
        const string request = "Temp/orca-push-check.request";
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists(request))
        { File.Delete(request); Run(); }
        if (stage < 0 || !EditorApplication.isPlaying) return;
        try
        {
            if (failure != null) throw new Exception(failure);
            Check(EditorApplication.timeSinceStartup < deadline, "Timeout");
            switch (stage)
            {
                case 0: Setup(); Next(); break;
                case 1:
                    if (Time.time - since < .1f) return;
                    Move(contact); Next(); break;
                case 2:
                    if (Time.time - since < .15f) return;
                    Check(impact.ImpactCount == 0, "Intro/menu rejects impact");
                    Move(contact + away * 5);
                    energy.SetConsumptionPaused(false);
                    Next(); break;
                case 3:
                    if (Time.time - since < .1f) return;
                    Move(contact); start = body.position;
                    Next(); break;
                case 4:
                    if (impact.ImpactCount == 0) { Check(Time.time - since < 2, "Real body contact triggers push"); return; }
                    Check(Vector3.Dot(impact.PushVelocity, away) > 0, "Push points away from orca");
                    Check(impact.ImpactAudio.isPlaying && impact.ImpactAudio.clip.name == "empuje", "Impact audio playing");
                    Check(!impact.Push(away * 3, 2, impact.ImpactAudio.clip, 1), "Repeated impact cooldown");
                    Next(); break;
                case 5:
                    if (Time.time - since < .15f) return;
                    Check(Vector3.Dot(body.position - start, away) > .05f, "Locomotion preserves real physical displacement");
                    visual.ApplyFeedback();
                    Check(visual.ExhaustionIntensity > .35f, "Impact vignette active");
                    var volume = player.GetComponentInChildren<Volume>();
                    Check(volume.sharedProfile.TryGet<Vignette>(out var vignette) && vignette.color.value == Color.black, "Impact vignette is black");
                    var pivot = player.transform.Find("Orca impact camera shake");
                    Check(pivot && pivot.localPosition.sqrMagnitude > 0, "Camera trembles");
                    Check(impact.ImpactCount == 1, "Sustained contact does not spam sound");
                    Next(); break;
                case 6:
                    if (Time.time - since < 2.1f) return;
                    Check(impact.Strength == 0 && impact.PushVelocity == Vector3.zero, "Impact expires");
                    Check(player.transform.Find("Orca impact camera shake").localPosition == Vector3.zero, "Camera returns to baseline");
                    visual.ApplyFeedback();
                    Check(Mathf.Abs(visual.ExhaustionIntensity - .24f) < .01f, "Original energy vignette restored");
                    UnityEngine.Object.Destroy(player); UnityEngine.Object.Destroy(orca);
                    if (settings) UnityEngine.Object.Destroy(settings);
                    scenario++;
                    if (scenario < 4) stage = 0;
                    else Finish("PASS: both orca prefabs; desktop and flap locomotion; real body contact and outward displacement; " +
                        "intro protection; empuje.wav playback; cooldown; black vignette; camera shake; recovery after 2 seconds.");
                    break;
            }
        }
        catch (Exception e) { Finish("FAIL scenario " + scenario + " stage " + stage + ": " + e); }
    }

    static void Finish(string result)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/OrcaPushValidation.txt", result + "\n");
        stage = -1;
        EditorApplication.isPlaying = false;
    }
}
#endif
