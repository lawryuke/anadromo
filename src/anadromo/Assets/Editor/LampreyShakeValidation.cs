#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Anadromo.AI;
using Anadromo.Locomotion;
using Anadromo.Mechanics;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LampreyShakeValidation
{
    const string Key = "LampreyShakeValidation";
    static PiranhaPlayerTarget player;
    static LampreyShakeController shake;
    static FlapSwimController swim;
    static Esc2Lamprey[] lamps;
    static Vector3 heldPosition;
    static Quaternion heldRotation;
    static int stage;
    static float since;
    static double started;
    static bool failed;

    static LampreyShakeValidation()
    {
        EditorApplication.playModeStateChanged += Mode;
        EditorApplication.update += Poll;
    }
    static void Poll()
    {
        const string request = "Temp/lamprey-shake-validation.request";
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Delete(request); Run();
    }
    public static void RunBatch() { SessionState.SetBool(Key + "Batch", true); Run(); }

    [MenuItem("Anadromo/Esc2/Lampreas/Verificar sacudida y rig Oculus")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/LampreyShakeProgress.txt", "Running gesture checks\n");
        try { CheckDetector(); }
        catch (Exception e) { Finish("FAIL: " + e, true); return; }
        SessionState.SetString(Key + "StartScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/esc2.unity");
        SessionState.SetBool(Key, true);
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }

    static void Require(bool valid, string message) { if (!valid) throw new Exception(message); }
    static bool Sample(ShakeStrokeDetector d, float x, float dt = .02f) => d.Sample(new Vector3(x, 0, 0), dt, .08f, .35f, .65f);
    static void CheckDetector()
    {
        var d = new ShakeStrokeDetector();
        for (int i = 0; i < 100; i++) Require(!Sample(d, i * .02f), "Unidirectional motion counted as shaking");
        d.Reset();
        for (int i = 0; i < 100; i++) Require(!Sample(d, Mathf.Sin(i) * .002f), "Jitter counted as shaking");
        d.Reset();
        for (int i = 0; i < 100; i++) Require(!Sample(d, Mathf.Sin(i * .1f) * .01f), "Slow motion counted as shaking");
        d.Reset(); Sample(d, 0); Sample(d, .04f); Sample(d, .09f);
        Sample(d, .045f); Require(Sample(d, 0), "Fast reversal not recognized");
        Require(!Sample(d, 10), "Tracking jump counted");
        Require(!Sample(d, 0), "Tracking recovery counted");
        d.Reset(); Sample(d, 0); Sample(d, .04f); Sample(d, .09f);
        for (int i = 0; i < 40; i++) Sample(d, .09f);
        Sample(d, .04f); Require(!Sample(d, 0), "Late reversal counted");
    }

    static void Mode(PlayModeStateChange mode)
    {
        if (!SessionState.GetBool(Key, false)) return;
        File.AppendAllText("Logs/LampreyShakeProgress.txt", mode + "\n");
        if (mode == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0; since = Time.time; started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
        if (mode == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "StartScene", ""));
            SessionState.SetBool(Key, false);
            if (SessionState.GetBool(Key + "Batch", false)) EditorApplication.Exit(SessionState.GetBool(Key + "Failed", false) ? 1 : 0);
        }
    }
    static void Next() { stage++; since = Time.time; File.AppendAllText("Logs/LampreyShakeProgress.txt", "Stage " + stage + "\n"); }
    static void Attach(Esc2Lamprey lamprey) => typeof(Esc2Lamprey).GetMethod("Attach", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(lamprey, new object[] { player });
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 60, "Timeout stage " + stage);
            if (stage == 0 && Time.time - since > .5f)
            {
                player = UnityEngine.Object.FindFirstObjectByType<PiranhaPlayerTarget>();
                Require(player, "Missing enemy target");
                shake = player.GetComponent<LampreyShakeController>(); swim = player.GetComponent<FlapSwimController>();
                var origin = player.GetComponent<XROrigin>();
                Require(origin && origin.Camera && swim && swim.Detector && shake, "Incomplete saved XR rig");
                Require(swim.enabled && !player.desktopMovement.enabled, "VR and desktop locomotion overlap");
                Require(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled) == 1, "Multiple audio listeners");
                var schools = UnityEngine.Object.FindObjectsByType<PiranhaSchool>(FindObjectsSortMode.None);
                Require(schools.All(s => s.target == player), "School targets another player");
                lamps = UnityEngine.Object.FindObjectsByType<Esc2Lamprey>(FindObjectsSortMode.None).Take(2).ToArray();
                Require(lamps.Length == 2, "Need two serialized lampreys");
                // Isolate the encounter under test while retaining the actual saved rig and enemy instances.
                foreach (var ai in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (ai.GetType().Namespace == "Anadromo.AI" && ai != player) ai.enabled = false;
                player.Vital.Energy.enabled = false; player.Vital.ResetEnergy();
                heldPosition = player.transform.position; heldRotation = player.transform.rotation;
                Attach(lamps[0]);
                Require(shake.MovementLocked && !swim.enabled && swim.Detector.SuppressFlaps && player.GetComponent<Rigidbody>().isKinematic, "Attachment did not lock locomotion");
                Next();
            }
            else if (stage == 1 && Time.time - since > .3f)
            {
                float remaining = shake.RemainingSeconds;
                Attach(lamps[1]);
                Require(shake.RemainingSeconds <= remaining + .01f, "Second lamprey reset the deadline");
                swim.Detector.OnLeftFlap.Invoke(1); swim.Detector.OnRightFlap.Invoke(1);
                Require(Vector3.Distance(player.transform.position, heldPosition) < .001f && Quaternion.Angle(player.transform.rotation, heldRotation) < .01f, "Root moved during attachment");
                Require(Quaternion.Angle(player.GetComponent<XROrigin>().CameraFloorOffsetObject.transform.localRotation, Quaternion.identity) > .1f, "No visual roll");
                lamps[0].ResetLamprey();
                Require(shake.MovementLocked && !swim.enabled, "Movement restored while second lamprey attached");
                for (int i = 0; i < 4; i++) player.ShakeLampreys(25);
                Require(lamps[1].State == Esc2Lamprey.BehaviourState.Stunned && player.AttachedLampreyCount == 0, "Grip did not release");
                Require(swim.enabled && !swim.Detector.SuppressFlaps && !player.GetComponent<Rigidbody>().isKinematic, "Successful escape did not restore VR");
                Require(Quaternion.Angle(player.GetComponent<XROrigin>().CameraFloorOffsetObject.transform.localRotation, Quaternion.identity) < .01f, "Roll was not restored");
                shake.escapeSeconds = 1; Attach(lamps[0]); Next();
            }
            else if (stage == 2 && Time.time - since > 1.2f)
            {
                Require(!player.Alive && !swim.enabled && shake.MovementLocked, "Timeout did not kill and lock player");
                Require(player.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t => t.text.Contains("HAS PERDIDO")), "Missing VR death screen");
                lamps[0].ResetLamprey(); Require(!swim.enabled, "Detaching after death restored movement");
                player.ResetEncounter();
                Require(player.Alive && swim.enabled && !swim.Detector.SuppressFlaps && !shake.MovementLocked, "Retry did not restore encounter");
                // Death from another enemy uses the same lock/retry path.
                player.TakeDamage(player.maxHealth); Require(!swim.enabled, "Non-lamprey death leaves swimming enabled");
                player.ResetEncounter(); Require(swim.enabled && !player.desktopMovement.enabled, "Retry changed the previous movement mode");
                Finish("PASS: raw gesture reversals, jitter/slow/jump rejection; saved Oculus rig and school references; attachment lock; no flap advance; visual roll; multiple lampreys; release/stun; timeout/death HUD; death lock; retry and restored VR mode.", false);
            }
        }
        catch (Exception e) { Finish("FAIL: " + e, true); }
    }
    static void Finish(string result, bool failure)
    {
        failed = failure; SessionState.SetBool(Key + "Failed", failed);
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/LampreyShakeCheck.txt", result);
        Debug.Log(result); EditorApplication.update -= Tick;
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        else if (SessionState.GetBool(Key + "Batch", false)) EditorApplication.Exit(failed ? 1 : 0);
    }
}
#endif
