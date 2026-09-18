#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Anadromo.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Optional smoke check: exercises the real controller and reports new Play Mode errors.
[InitializeOnLoad]
public static class DesktopPlayValidation
{
    private const string PendingKey = "Anadromo.DesktopPlayValidation";
    private static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../tools/validation"));
    private static readonly List<string> Errors = new List<string>();
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static DebugVuelo controller;
    private static Vector3 position;
    private static Quaternion rotation;
    private static double nextStep;
    private static int stage = -1;

    static DesktopPlayValidation()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayState;
        Application.logMessageReceived += OnLog;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (SessionState.GetBool(PendingKey, false) &&
            (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            Errors.Add(message + "\n" + stack);
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0;
            nextStep = EditorApplication.timeSinceStartup + 2;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(PendingKey, false);
            stage = -1;
        }
    }

    private static void Update()
    {
        string request = Path.Combine(Folder, "desktop-play.request");
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists(request))
        {
            // Do not interrupt an unsaved editing session.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return;
            File.Delete(request);
            SessionState.SetBool(PendingKey, true);
            Errors.Clear();
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/Act1_OpenOcean.unity");
            EditorApplication.isPlaying = true;
            return;
        }
        if (stage < 0 || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextStep) return;
        try
        {
            switch (stage++)
            {
                case 0:
                    controller = UnityEngine.Object.FindFirstObjectByType<DebugVuelo>();
                    Require(controller != null && controller.enabled && controller.cameraTransform != null, "Desktop controller available");
                    var xr = UnityEngine.XR.Management.XRGeneralSettings.Instance;
                    Require(xr == null || xr.Manager == null || !xr.Manager.isInitializationComplete, "XR loader remains stopped");
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    mouse = InputSystem.AddDevice<Mouse>();
                    position = controller.transform.position;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    nextStep = EditorApplication.timeSinceStartup + 0.6;
                    break;
                case 1:
                    Require(Vector3.Dot(controller.transform.position - position, controller.cameraTransform.forward) > 0.1f, "W moves the player forward");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    position = controller.transform.position;
                    nextStep = EditorApplication.timeSinceStartup + 0.6;
                    break;
                case 2:
                    Require(controller.transform.position.y > position.y + 0.1f, "E moves the player upward");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    rotation = controller.cameraTransform.rotation;
                    InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(40, 0) }.WithButton(MouseButton.Right));
                    nextStep = EditorApplication.timeSinceStartup + 0.3;
                    break;
                case 3:
                    Require(Quaternion.Angle(rotation, controller.cameraTransform.rotation) > 1f, "Mouse rotates the camera without headset interference");
                    Require(Errors.Count == 0, "No new console errors during Play Mode");
                    Finish("PASS: Keyboard movement, vertical movement, mouse look, stopped XR loader and no new Play Mode errors.");
                    break;
            }
        }
        catch (Exception ex) { Finish("FAIL: " + ex + "\n" + string.Join("\n", Errors)); }
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }

    private static void Finish(string result)
    {
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        keyboard = null;
        mouse = null;
        stage = -1;
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "desktop-play.txt"), result + "\n");
        EditorApplication.isPlaying = false;
    }
}
#endif
