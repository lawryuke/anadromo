using System;
using System.Collections.Generic;
using System.Linq;
using Anadromo.AI;
using Anadromo.Locomotion;
using Anadromo.Mechanics;
using Anadromo.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static partial class VRPlayerSceneSetup
{
    const string ScenePath = "Assets/_Project/Scenes/esc2.unity";
    static VRPlayerSceneSetup() { EditorApplication.update += ProcessRequest; }
    static void ProcessRequest()
    {
        const string request = "Temp/vr-player-setup.request";
        if (!System.IO.File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        string command = System.IO.File.ReadAllText(request).Trim();
        System.IO.File.Delete(request);
        try
        {
            if (command == "refresh") AssetDatabase.Refresh();
            else if (command == "shake") IntegrateShake();
            else if (command == "validate") Validate();
            System.IO.File.WriteAllText("Temp/vr-player-setup.result", "PASS: " + command);
        }
        catch (Exception e) { System.IO.File.WriteAllText("Temp/vr-player-setup.result", e.ToString()); }
    }

    [MenuItem("Tools/Anadromo/VR/Integrate lamprey shake")]
    public static void IntegrateShake()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open esc2 first.");
        System.IO.Directory.CreateDirectory("Library/ShakeIntegrationBackup");
        const string backup = "Library/ShakeIntegrationBackup/esc2-before-shake.unity";
        if (!System.IO.File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
        var vr = All<VRPlayerGameplay>(scene).Single();
        var shake = vr.GetComponent<LampreyShakeController>();
        if (!shake) shake = Undo.AddComponent<LampreyShakeController>(vr.gameObject);
        shake.gripPerShake = 50f;
        shake.minimumTravel = .06f;
        shake.minimumSpeed = .25f;
        shake.reversalWindow = .8f;
        shake.rollFrequency = .25f;
        shake.heartbeatClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Ambient/heartbeat.wav");
        if (!shake.heartbeatClip) throw new InvalidOperationException("Missing heartbeat.wav audio clip.");
        EditorUtility.SetDirty(shake);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Validate();
    }
    static IEnumerable<T> All<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

    [MenuItem("Tools/Anadromo/VR/Configure esc2 player")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before configuring VR.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open esc2 first.");
        // Keep a copy including any unsaved editor changes before making the migration.
        if (!System.IO.File.Exists("Temp/esc2-before-vr.unity"))
            EditorSceneManager.SaveScene(scene, "Temp/esc2-before-vr.unity", true);
        var transforms = All<Transform>(scene).ToArray();
        var vr = transforms.Single(t => t.name == "VR_Player").gameObject;
        var km = transforms.Single(t => t.name == "KM_Player").gameObject;
        Undo.RegisterFullObjectHierarchyUndo(vr, "Configure VR gameplay");
        var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        foreach (Type type in new[] { typeof(EnergySystem), typeof(PlayerEnergyController),
            typeof(EnergyVisualFeedback), typeof(PiranhaPlayerTarget), typeof(BlindFishMotionSensor) })
        {
            var source = km.GetComponent(type);
            var destination = vr.GetComponent(type);
            if (!destination)
            {
                destination = Undo.AddComponent(vr, type);
                EditorUtility.CopySerialized(source, destination);
            }
            map[source] = destination;
        }
        var camera = vr.GetComponentInChildren<Camera>(true);
        var feeding = vr.GetComponent<PlayerFeeding>();
        feeding.mouthTarget = camera.transform;
        feeding.enabled = true;
        vr.GetComponent<SphereCollider>().isTrigger = true;
        vr.GetComponent<PiranhaPlayerTarget>().desktopMovement = null;
        var hull = vr.GetComponent<CapsuleCollider>();
        if (!hull) hull = Undo.AddComponent<CapsuleCollider>(vr);
        var desktopHull = km.GetComponent<CapsuleCollider>();
        hull.isTrigger = false;
        hull.radius = desktopHull.radius * km.transform.lossyScale.x / vr.transform.lossyScale.x;
        hull.height = desktopHull.height * km.transform.lossyScale.z / vr.transform.lossyScale.z;
        hull.direction = desktopHull.direction;
        hull.center = vr.transform.InverseTransformPoint(camera.transform.position);
        var gameplay = vr.GetComponent<VRPlayerGameplay>();
        if (!gameplay) gameplay = Undo.AddComponent<VRPlayerGameplay>(vr);
        var shake = vr.GetComponent<LampreyShakeController>();
        if (!shake) shake = Undo.AddComponent<LampreyShakeController>(vr);
        shake.gripPerShake = 50f;
        shake.minimumTravel = .06f;
        shake.minimumSpeed = .25f;
        shake.reversalWindow = .8f;
        shake.rollFrequency = .25f;
        shake.heartbeatClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Ambient/heartbeat.wav");
        if (!shake.heartbeatClip) throw new InvalidOperationException("Missing heartbeat.wav audio clip.");
        EditorUtility.SetDirty(shake);
        gameplay.head = camera.transform;
        gameplay.hands = All<FlapDetector>(scene).FirstOrDefault();
        map[km.GetComponent<PlayerFeeding>()] = feeding;
        map[km.GetComponentInChildren<Camera>(true)] = camera;
        map[km.transform] = vr.transform;
        // Serialized references include prefab overrides, inactive enemies and UnityEvents.
        int rebound = 0;
        foreach (var component in All<MonoBehaviour>(scene).Where(c => c && !c.transform.IsChildOf(km.transform)))
        {
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue && map.TryGetValue(property.objectReferenceValue, out var replacement))
                { property.objectReferenceValue = replacement; changed = true; rebound++; }
            }
            if (changed) serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
        }
        foreach (var spawner in All<BoxObjectSpawner>(scene))
        {
            // These three esc2 food boxes otherwise inherit a non-player food tag.
            if (!spawner.name.Equals("krils", StringComparison.OrdinalIgnoreCase)) continue;
            spawner.spawnedTag = "Food_PlayerOnly";
            spawner.markAsPrey = true;
            EditorUtility.SetDirty(spawner);
        }
        km.SetActive(false);
        vr.SetActive(true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        var buildScenes = EditorBuildSettings.scenes.ToList();
        var buildScene = buildScenes.FirstOrDefault(s => s.path == ScenePath);
        if (buildScene == null) buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        else buildScene.enabled = true;
        EditorBuildSettings.scenes = buildScenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("VR gameplay configured; rebound references: " + rebound);
    }

    [MenuItem("Tools/Anadromo/VR/Validate esc2 player")]
    public static void Validate()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        var vr = All<VRPlayerGameplay>(scene).Single();
        var target = vr.GetComponent<PiranhaPlayerTarget>();
        if (!target || !vr.GetComponent<EnergySystem>() || !vr.GetComponent<PlayerEnergyController>() ||
            !vr.head || !vr.hands || !vr.GetComponent<LampreyShakeController>() ||
            !vr.GetComponent<LampreyShakeController>().heartbeatClip || vr.GetComponent<CapsuleCollider>().isTrigger)
            throw new InvalidOperationException("Incomplete VR gameplay configuration.");
        foreach (var school in All<PiranhaSchool>(scene))
            if (school.target != target) throw new InvalidOperationException("Wrong school target: " + school.name);
        foreach (var fish in All<Esc2BlindFish>(scene))
            if (fish.target != target || fish.motion != vr.GetComponent<BlindFishMotionSensor>())
                throw new InvalidOperationException("Wrong blind fish target: " + fish.name);
        foreach (var passage in All<Esc2SharkPassage>(scene))
            if (passage.target != target) throw new InvalidOperationException("Wrong passage target: " + passage.name);
        foreach (var transform in All<Transform>(scene))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                throw new InvalidOperationException("Missing script: " + transform.name);
        var desktop = All<Transform>(scene).Single(t => t.name == "KM_Player");
        foreach (var component in All<MonoBehaviour>(scene).Where(c => c && !c.transform.IsChildOf(desktop)))
        {
            var property = new SerializedObject(component).GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue is Component reference && reference.transform.IsChildOf(desktop))
                    throw new InvalidOperationException(component.name + "." + property.propertyPath + " still references KM_Player");
        }
        System.IO.Directory.CreateDirectory("Logs");
        System.IO.File.WriteAllLines("Logs/VRPlayerSceneAudit.txt", new[] {
            "PASS: esc2 VR player references, required components and missing-script audit",
            "Territories: " + All<PiranhaSchool>(scene).Count(),
            "Piranhas: " + All<Esc2Piranha>(scene).Count(),
            "Anglerfish: " + All<Esc2Anglerfish>(scene).Count(),
            "Lampreys: " + All<Esc2Lamprey>(scene).Count(),
            "Blind fish: " + All<Esc2BlindFish>(scene).Count(),
            "Shark passages: " + All<Esc2SharkPassage>(scene).Count(),
            "Patrol sharks: " + All<Esc2SharkPatrol>(scene).Count(),
            "Backtrack systems: " + All<AntiBacktrackTunnel>(scene).Count(),
            "Mathematical zones: " + All<ZoneLimit>(scene).Count(),
            "Food spawners: " + All<BoxObjectSpawner>(scene).Count(),
            "Guides: " + All<LuzViajera>(scene).Count()
        });
        Debug.Log("VR scene validation passed.");
    }
}
