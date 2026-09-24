#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Anadromo.Logic;
using Anadromo.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class VisualLevelValidation
{
    const string Request = "Temp/visual-level-validation.request";
    static VisualLevelValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        File.Delete(Request);
        Run();
    }

    [MenuItem("Anadromo/Validate TerrainTestVisuales Level Logic")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/TerrainTestVisuales.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var manager = roots.SelectMany(g => g.GetComponentsInChildren<LevelManager>(true)).Single();
            Require(manager.ValidateConfiguration(out string error), error);
            Require(manager.currentPhase == GamePhase.WaitingForStart, "Scene starts at the button");
            Require(manager.playerEatenCount == 0, "Meal count starts at zero");
            Require(LevelManager.Contains(manager.initialZone, manager.playerCamera.transform.position), "Player starts inside InitZoneTrigger");
            foreach (var spawner in roots.SelectMany(g => g.GetComponentsInChildren<BoxObjectSpawner>(true)))
            {
                if (!spawner.gameObject.activeInHierarchy || spawner.sourceObject == null) continue;
                Require(!spawner.sourceObject.activeSelf, "Reference must stay inactive: " + spawner.sourceObject.name);
                if (spawner.markAsPrey) Require(!string.IsNullOrEmpty(spawner.spawnedTag), "Prey has explicit tag: " + spawner.name);
            }
            foreach (var group in manager.orcaGroups)
                Require(!group.allowKeyboard && group.GetComponent<BoxObjectSpawner>() != null, "Orca group owned by sequence");
            Require(!manager.bloopMovement.allowKeyboard, "Bloop owned by sequence");
            foreach (var behaviour in roots.SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if (behaviour == null) throw new InvalidOperationException("Missing script in scene");
                if (behaviour is TriggerHuntOnExit || behaviour is TriggerHuntOnGroupEmpty ||
                    behaviour is TriggerNormalOnScaryEmpty || behaviour is TriggerSharkStampede)
                    Require(!behaviour.isActiveAndEnabled, "Legacy event is still enabled: " + behaviour.name);
            }
            int count = LevelProgressionChecks.Run();
            File.WriteAllText("Temp/visual-level-validation.txt",
                "PASS: saved scene references, ordered height limits, spawn tags, inactive templates, initial player zone, " +
                "single phase owner and " + count + " progression checks.");
        }
        catch (Exception e)
        {
            File.WriteAllText("Temp/visual-level-validation.txt", "FAIL: " + e);
            Debug.LogException(e);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
}
#endif
