using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Anadromo.AI;
using Anadromo.Locomotion;
using Anadromo.Mechanics;
using Anadromo.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Real esc2 Play Mode regression checks. Runtime changes are discarded on exit.</summary>
[InitializeOnLoad]
public static class VRPlayerPlayCheck
{
    const string Key = "Anadromo.VRPlayerPlayCheck";
    const string Result = "Logs/VRPlayerPlayCheck.txt";
    static readonly List<string> report = new List<string>();
    static string error;
    static int stage;
    static float since, fullSpeed;
    static VRPlayerGameplay vr;
    static PiranhaPlayerTarget target;
    static Rigidbody body;
    static FlapSwimController swim;
    static PlayerFeeding feeding;
    static Collider meal;
    static GameObject wall;
    static int consumed;

    static VRPlayerPlayCheck()
    {
        EditorApplication.playModeStateChanged += Mode;
        EditorApplication.update += Poll;
        Application.logMessageReceived += Log;
    }
    static void Poll()
    {
        const string request = "Temp/vr-play-check.request";
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && File.Exists(request))
        { File.Delete(request); Run(); }
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && !EditorApplication.isPaused && stage >= 0)
            Tick();
    }
    [MenuItem("Tools/Anadromo/VR/Run esc2 Play Mode checks")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        VRPlayerSceneSetup.Validate();
        error = null;
        SessionState.SetString(Key + "Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/esc2.unity");
        SessionState.SetBool(Key, true);
        stage = -1;
        EditorApplication.isPlaying = true;
    }
    static void Mode(PlayModeStateChange mode)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (mode == PlayModeStateChange.EnteredPlayMode)
        { stage = 0; since = Time.time; report.Clear(); }
        if (mode == PlayModeStateChange.EnteredEditMode)
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Previous", ""));
            SessionState.SetBool(Key, false);
            stage = -1;
        }
    }
    static void Log(string message, string trace, LogType type)
    {
        if (SessionState.GetBool(Key, false) && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
            error = message + "\n" + trace;
    }
    static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        report.Add("PASS: " + description);
    }
    static void Invoke(object instance, string method, params object[] args) =>
        instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(instance, args);
    static void Next() { stage++; since = Time.time; }
    static void Place(Vector3 position)
    {
        Vector3 destination = body.transform.position + position - target.Position;
        body.transform.position = destination;
        body.position = destination;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();
        vr.SyncBody();
    }
    static void Finish(string result)
    {
        report.Add(result);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines(Result, report);
        stage = -1;
        EditorApplication.isPlaying = false;
    }
    static void Tick()
    {
        try
        {
            if (error != null) throw new Exception(error);
            if (Time.time - since > 15f) throw new Exception("Timeout stage " + stage);
            if (stage == 0 && Time.time - since > 1f)
            {
                vr = Object.FindFirstObjectByType<VRPlayerGameplay>();
                Check(vr && vr.isActiveAndEnabled, "VR player active");
                target = vr.GetComponent<PiranhaPlayerTarget>();
                body = vr.GetComponent<Rigidbody>();
                swim = vr.GetComponent<FlapSwimController>();
                feeding = vr.GetComponent<PlayerFeeding>();
                VRPlayerSceneSetup.Validate();
                Check(Object.FindObjectsByType<PiranhaPlayerTarget>(FindObjectsSortMode.None).Length == 1,
                    "One active combat player; desktop remains inactive");
                Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.enabled) == 1,
                    "One active audio listener");
                var spawners = Object.FindObjectsByType<BoxObjectSpawner>(FindObjectsSortMode.None);
                Check(spawners.Length == 3 && spawners.All(s => s.AliveCount > 0 && s.markAsPrey && s.spawnedTag == "Food_PlayerOnly"),
                    "All three food boxes spawned edible prey");
                foreach (var component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (component is Esc2Piranha || component is Esc2Lamprey || component is Esc2Anglerfish ||
                        component is Esc2BlindFish || component is Esc2SharkPassage || component is Esc2SharkPatrol ||
                        component is AntiBacktrackTunnel || component is PredatorNaturalMotion) component.enabled = false;
                target.Vital.Energy.enabled = false;
                foreach (var driver in vr.GetComponentsInChildren<UnityEngine.InputSystem.XR.TrackedPoseDriver>()) driver.enabled = false;
                // Keep a deliberately displaced head to catch attacks at the rig origin.
                vr.head.localPosition = new Vector3(.3f, 1.5f, .4f);
                Place(new Vector3(500, 500, 500));
                Check(Vector3.Distance(target.Position, vr.head.position) < .001f &&
                    Vector3.Distance(target.Position, vr.transform.position) > 1f, "Combat position follows offset headset");
                Check(Vector3.Distance(body.transform.TransformPoint(vr.GetComponent<CapsuleCollider>().center), vr.head.position) < .001f,
                    "Solid body follows headset separately from trigger mouth");
                vr.hands.OnLeftFlap.Invoke(1f);
                Next();
            }
            else if (stage == 1 && Time.time - since > .12f)
            {
                fullSpeed = body.linearVelocity.magnitude;
                Check(fullSpeed > .01f, "Tracked flap event propels VR rigidbody");
                swim.enabled = false;
                target.Vital.Energy.SetEnergy(10);
                body.linearVelocity = Vector3.zero;
                swim.enabled = true;
                vr.hands.OnLeftFlap.Invoke(1f);
                Next();
            }
            else if (stage == 2 && Time.time - since > .12f)
            {
                Check(body.linearVelocity.magnitude < fullSpeed * .9f, "Low energy reduces real flap propulsion");
                swim.enabled = false;
                body.linearVelocity = Vector3.zero;
                Invoke(feeding, "FixedUpdate");
                var prey = Object.FindObjectsByType<Prey>(FindObjectsSortMode.None).First(p => p.CompareTag("Food_PlayerOnly"));
                meal = prey.GetComponentInChildren<Collider>();
                prey.transform.position += vr.head.position - meal.bounds.center;
                consumed = feeding.TotalConsumed;
                Physics.SyncTransforms();
                Next();
            }
            else if (stage == 3 && Time.time - since > .15f)
            {
                Check(feeding.TotalConsumed == consumed + 1 && target.Health > 10,
                    "Physical mouth trigger consumes spawned krill and restores energy");
                Check(!feeding.TryEat(meal), "Consumed prey cannot be counted twice");
                target.Vital.ResetEnergy();
                TestEnemies();
                TestBlindFishAndPatrol();
                TestZones();
                target.ResetEncounter();
                swim.enabled = true;
                target.TakeDamage(target.maxHealth);
                Check(!target.Alive && !swim.enabled && body.linearVelocity == Vector3.zero,
                    "Lethal damage stops VR locomotion");
                target.Vital.ConsumeKrill(15);
                Check(!target.Alive, "Feeding cannot revive a dead player");
                Next();
            }
            else if (stage == 4 && Time.time - since > .1f)
            {
                Check(vr.GetComponent<LampreyShakeController>().DeathVisible &&
                    vr.head.GetComponentsInChildren<UnityEngine.UI.Text>(true).Count(t => t.gameObject.activeInHierarchy && t.text == "MORISTE") == 1,
                    "MORISTE appears on the death screen");
                target.ResetEncounter();
                Check(target.Alive && swim.enabled && target.AttachedLampreyCount == 0,
                    "Encounter reset restores energy and VR locomotion");
                swim.enabled = false;
                Place(new Vector3(500, 500, 500));
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = target.Position + Vector3.forward * .5f;
                wall.transform.localScale = new Vector3(2, 2, .1f);
                Physics.SyncTransforms();
                body.linearDamping = 0;
                body.linearVelocity = Vector3.forward * 4;
                Next();
            }
            else if (stage == 5 && Time.time - since > .3f)
            {
                Check(target.Health < target.maxHealth && target.Health > 0, "Solid collider blocks wall and applies impact damage");
                Object.Destroy(wall);
                Next();
                vr.RestartLevel();
            }
            else if (stage == 6 && Time.time - since > 1f)
            {
                vr = Object.FindFirstObjectByType<VRPlayerGameplay>();
                Check(vr && vr.GetComponent<PiranhaPlayerTarget>().Health > 99 && vr.GetComponent<FlapSwimController>().enabled,
                    "VR restart reloads scene with full energy and enabled swimming");
                VRPlayerSceneSetup.Validate();
                Check(Object.FindObjectsByType<AntiBacktrackTunnel>(FindObjectsSortMode.None).All(t =>
                    (int)typeof(AntiBacktrackTunnel).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(t) == 0),
                    "Restart clears all seven one-shot backtrack traps");
                Finish("PASS: VR scene integration checks completed (hardware tracking requires headset).");
            }
        }
        catch (Exception e) { Finish("FAIL stage " + stage + ": " + e); }
    }
    static void TestEnemies()
    {
        var piranha = Object.FindFirstObjectByType<Esc2Piranha>();
        var school = piranha.GetComponentInParent<PiranhaSchool>();
        school.transform.position = target.Position;
        piranha.transform.position = target.Position + Vector3.forward * .1f;
        Physics.SyncTransforms();
        piranha.BeginAttack();
        float before = target.Health;
        piranha.Tick(.02f);
        Check(target.Health < before, "Piranha bites offset VR head");

        var lamprey = Object.FindFirstObjectByType<Esc2Lamprey>();
        lamprey.GetComponentInParent<PiranhaSchool>().transform.position = target.Position;
        lamprey.transform.position = target.Position + Vector3.right * .1f;
        Physics.SyncTransforms();
        Invoke(lamprey, "Tick", .02f);
        Check(lamprey.State == Esc2Lamprey.BehaviourState.Attached && target.SpeedMultiplier < 1f,
            "Lamprey attaches and reduces swimmer speed");
        before = target.Health;
        Invoke(lamprey, "Tick", lamprey.drainInterval + .01f);
        Check(target.Health < before, "Attached lamprey drains VR energy");
        for (int i = 0; i < 5; i++) target.ShakeLampreys(25f);
        Check(lamprey.State == Esc2Lamprey.BehaviourState.Stunned && target.SpeedMultiplier == 1f,
            "VR shake action releases lamprey and restores speed");

        var angler = Object.FindFirstObjectByType<Esc2Anglerfish>();
        angler.GetComponentInParent<PiranhaSchool>().transform.position = target.Position;
        angler.transform.position = target.Position + Vector3.left * .3f;
        Physics.SyncTransforms();
        before = target.Health;
        Invoke(angler, "Tick", .02f);
        for (int i = 0; i < 200 && target.Health == before; i++) Invoke(angler, "Tick", .02f);
        Check(target.Health < before, "Angler detects and damages offset VR head");
    }
    static void TestZones()
    {
        var passage = Object.FindObjectsByType<Esc2SharkPassage>(FindObjectsSortMode.None).First(p => p.activationZone);
        Place(passage.activationZone.transform.TransformPoint(passage.activationZone.center));
        Check((bool)passage.GetType().GetMethod("PlayerNearRoute", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(passage, null), "Shark passage activation box uses headset position");

        var trap = Object.FindObjectsByType<AntiBacktrackTunnel>(FindObjectsSortMode.None).First(t => t.checkoutZone && t.warningZone && t.killZone);
        Place(trap.checkoutZone.transform.TransformPoint(trap.checkoutZone.center));
        Invoke(trap, "Update");
        Place(trap.warningZone.transform.TransformPoint(trap.warningZone.center));
        Invoke(trap, "Update");
        int trapState = (int)typeof(AntiBacktrackTunnel).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(trap);
        Check(trapState == 2, "Backtrack checkout and warning boxes activate medusa");
        Place(trap.killZone.transform.TransformPoint(trap.killZone.center));
        Invoke(trap, "Update");
        var shark = (Esc2SharkInstakill)typeof(AntiBacktrackTunnel).GetField("spawnedShark", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(trap);
        Check(shark && shark.gameObject.activeInHierarchy, "Kill box activates cinematic shark against VR player");
        shark.transform.position = target.Position + Vector3.forward;
        Invoke(shark, "Update");
        Check(!target.Alive, "Cinematic shark kills VR player");
        shark.gameObject.SetActive(false);
    }

    static void TestBlindFishAndPatrol()
    {
        target.Vital.ResetEnergy();
        Place(new Vector3(600, 600, 600));
        var fish = Object.FindFirstObjectByType<Esc2BlindFish>();
        var motion = fish.motion;
        motion.enabled = false;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(BlindFishMotionSensor).GetField("<IsNoisy>k__BackingField", flags).SetValue(motion, true);
        typeof(BlindFishMotionSensor).GetField("<IsStill>k__BackingField", flags).SetValue(motion, false);
        typeof(BlindFishMotionSensor).GetField("<MotionValid>k__BackingField", flags).SetValue(motion, true);
        fish.patrolPoints = new Transform[0];
        fish.body.position = target.Position + Vector3.forward * .3f;
        // Place the territory at the same test position without changing detection thresholds.
        typeof(Esc2BlindFish).GetField("home", flags).SetValue(fish, target.Position);
        Physics.SyncTransforms();
        for (int i = 0; i < 20000 && fish.Agitation < 100; i++) Invoke(fish, "Update");
        Check(fish.State == Esc2BlindFish.BehaviourState.Critical, "Tracking noise agitates blind fish to critical state");
        // Advance the grace timer without depending on the editor's tiny delta during this synchronous check.
        typeof(Esc2BlindFish).GetField("<StateTime>k__BackingField", flags).SetValue(fish, fish.graceDuration + .1f);
        Invoke(fish, "Update");
        Invoke(fish, "Update");
        Check(!target.Alive, "Blind fish attacks VR head after sustained simulated tracking noise (state=" + fish.State + ", agitation=" + fish.Agitation + ")");
        target.ResetEncounter();
        swim.enabled = false;
        var patrol = Object.FindFirstObjectByType<Esc2SharkPatrol>();
        patrol.transform.position = target.Position;
        Invoke(patrol, "Update");
        Check(!target.Alive, "Patrol shark hitbox kills VR player");
        target.ResetEncounter();
        swim.enabled = false;
    }
}
