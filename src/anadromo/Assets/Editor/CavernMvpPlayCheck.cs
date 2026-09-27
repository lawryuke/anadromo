#if UNITY_EDITOR
using System;
using System.IO;
using Anadromo.CavernMVP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Integration check using the saved scene and real MonoBehaviour lifecycle.</summary>
[InitializeOnLoad]
public static class CavernMvpPlayCheck
{
    const string Key = "CavernMvpPlayCheck";
    const string Request = "Library/CavernMvpPlayCheck.request";
    static double started;
    static int stage;
    static CavernMvpWorld world;
    static CavernPlayer oldPlayer;
    static string failure;
    static CavernMvpPlayCheck()
    {
        EditorApplication.playModeStateChanged += ModeChanged;
        EditorApplication.delayCall += CheckRequest;
        if (SessionState.GetBool(Key,false)) Application.logMessageReceived += Log;
    }
    static void CheckRequest()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request); Run();
    }
    [MenuItem("Anadromo/Cueva MVP/Probar escena en Play Mode")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        CavernMvpEditor.Validate();
        SessionState.SetString(Key+"PreviousScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key,true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(CavernMvpEditor.ScenePath);
        EditorApplication.isPlaying = true;
    }
    static void Log(string message,string stack,LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failure = message + "\n" + stack;
    }
    static void ModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key,false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            started = EditorApplication.timeSinceStartup; stage = 0; failure = null;
            SessionState.SetInt(Key+"FrameRate",Application.targetFrameRate);
            Application.targetFrameRate = 30;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Application.targetFrameRate = SessionState.GetInt(Key+"FrameRate",-1);
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key+"PreviousScene",""));
            SessionState.SetBool(Key,false);
        }
    }
    static void Require(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (failure != null) throw new InvalidOperationException(failure);
            double seconds = EditorApplication.timeSinceStartup-started;
            if (stage == 0 && seconds > 3)
            {
                world = UnityEngine.Object.FindFirstObjectByType<CavernMvpWorld>();
                Require(world && world.player && world.player.view,"Escena, jugador y cámara cargados");
                Require(world.GetComponentsInChildren<CavernPiranha>().Length == 8,"Ocho pirañas serializadas");
                Require(world.GetComponentsInChildren<CavernAngler>().Length == 2,"Dos peces linterna serializados");
                Require(world.GetComponentsInChildren<CavernLamprey>().Length == 4,"Cuatro lampreas serializadas");
                Require(world.player.Health == 100,"Inicio seguro sin daño");
                foreach (var renderer in world.GetComponentsInChildren<Renderer>()) Require(renderer.sharedMaterial && renderer.sharedMaterial.shader && renderer.sharedMaterial.shader.isSupported,"Material válido");
                foreach (var t in world.GetComponentsInChildren<Transform>()) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Sin scripts perdidos");
                Directory.CreateDirectory("Logs");
                ScreenCapture.CaptureScreenshot("Logs/CavernMvpPlay.png");
                var cc = world.player.GetComponent<CharacterController>();
                cc.Move(Vector3.down*20);
                Require(world.player.transform.position.y > .3f,"Suelo bloquea al jugador");
                cc.Move(Vector3.up*30);
                Require(world.player.transform.position.y < 5.7f,"Techo bloquea al jugador");
                var lamps = world.GetComponentsInChildren<CavernLamprey>();
                lamps[0].Attach(); lamps[1].Attach(); world.player.Shake(100);
                Require(world.player.AttachedCount==0 && Mathf.Approximately(world.player.SpeedMultiplier,1),"Sacudida real limpia dos lampreas");
                world.player.TakeDamage(100); Require(!world.player.Active,"Muerte detiene juego");
                oldPlayer = world.player; world.Restart(); stage++;
            }
            else if (stage == 1 && seconds > 5)
            {
                Require(!oldPlayer && world.player && world.player.Health==100,"Reinicio destruye jugador anterior y restaura salud");
                Require(world.GetComponentsInChildren<Camera>().Length==1,"Reinicio conserva una sola cámara");
                var cc = world.player.GetComponent<CharacterController>(); cc.enabled = false;
                world.player.transform.position = CavernMvpWorld.RoomPosition(7); cc.enabled = true; stage++;
            }
            else if (stage == 2 && seconds > 6)
            {
                Require(world.player.Finished,"Llegar a H completa partida");
                File.WriteAllText("Logs/CavernMvpPlayCheck.txt","PASS: escena serializada, 14 enemigos, materiales, scripts, inicio seguro, suelo/techo, sacudidas, muerte, reinicio y salida. Sin errores de ejecución.");
                Debug.Log("PASS: integración Play Mode de Cueva MVP.");
                EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
            }
        }
        catch (Exception e)
        {
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/CavernMvpPlayCheck.txt","FAIL: " + e);
            EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
        }
    }
}
#endif
