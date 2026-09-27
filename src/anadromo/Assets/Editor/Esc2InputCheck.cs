#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Anadromo.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class Esc2InputCheck
{
    const string KeyName="Esc2InputCheck";
    static Keyboard keyboard;
    static PiranhaSchool school;
    static Esc2Lamprey[] lamps;
    static Esc2Anglerfish angler;
    static Vector3 startPosition;
    static float started,shakeAt;
    static double wallStart;
    static int stage,press;
    static bool sawAngler;
    static string failure;
    static Esc2InputCheck()
    {
        EditorApplication.update+=Request;
        EditorApplication.playModeStateChanged+=Mode;
        if(SessionState.GetBool(KeyName,false)) Application.logMessageReceived+=Log;
    }
    static void Log(string message,string stack,LogType type) { if(type==LogType.Error || type==LogType.Exception) failure=message; }
    static void Request()
    {
        const string path="Library/Esc2Input.request";
        if(!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(path); Run();
    }
    [MenuItem("Anadromo/Esc2/Depredadores/Probar entrada W y sacudidas A D")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetString(KeyName+"Scene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/esc2.unity");
        SessionState.SetBool(KeyName,true); EditorApplication.isPlaying=true;
    }
    static void Mode(PlayModeStateChange mode)
    {
        if(!SessionState.GetBool(KeyName,false)) return;
        if(mode==PlayModeStateChange.EnteredPlayMode)
        {
            stage=0; press=0; sawAngler=false; failure=null; started=Time.time; wallStart=EditorApplication.timeSinceStartup;
            Application.runInBackground=true;
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            EditorApplication.update+=Tick;
        }
        if(mode==PlayModeStateChange.ExitingPlayMode && keyboard!=null) { InputSystem.RemoveDevice(keyboard); keyboard=null; }
        if(mode==PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update-=Tick; Application.logMessageReceived-=Log;
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(KeyName+"Scene",""));
            SessionState.SetBool(KeyName,false);
        }
    }
    static void Check(bool value,string reason) { if(!value) throw new Exception(reason); }
    static void Finish(string result)
    {
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Esc2InputCheck.txt",result);
        if(keyboard!=null) { InputSystem.RemoveDevice(keyboard); keyboard=null; }
        EditorApplication.update-=Tick; EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying) return;
        try
        {
            if(failure!=null) throw new Exception(failure);
            if(EditorApplication.timeSinceStartup-wallStart>90 || Time.time-started>18) throw new Exception("Timeout stage="+stage+" player="+(school?school.target.transform.position.ToString():"?")+" lamps="+(lamps==null?"?":string.Join(",",lamps.Select(l=>l.State.ToString()))));
            if(stage==0 && Time.time-started>.5f)
            {
                school=UnityEngine.Object.FindFirstObjectByType<PiranhaSchool>(); Check(school && school.target,"Territorio conectado");
                lamps=school.GetComponentsInChildren<Esc2Lamprey>(); angler=school.GetComponentInChildren<Esc2Anglerfish>();
                Check(lamps.Length==2 && angler,"Cinco enemigos agrupados");
                Check(lamps.All(l=>l.isActiveAndEnabled) && angler.isActiveAndEnabled && school.GetComponentsInChildren<Esc2Piranha>().All(p=>p.isActiveAndEnabled),"Todos los enemigos activos");
                Check(lamps[0].body.sharedMaterial!=angler.body.sharedMaterial,"Materiales distintos guardados");
                var player=school.target; Check(player.desktopMovement.isActiveAndEnabled,"Controlador normal activo");
                // Start at the encounter entrance, then use the actual Input System and Rigidbody controller.
                startPosition=angler.transform.position+Vector3.back*3.8f;
                var rb=player.GetComponent<Rigidbody>(); player.transform.SetPositionAndRotation(startPosition,Quaternion.identity);
                rb.position=startPosition; rb.rotation=Quaternion.identity; rb.linearVelocity=Vector3.zero;
                player.desktopMovement.SyncLookRotation(); Physics.SyncTransforms();
                keyboard=InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W)); stage=1;
            }
            else if(stage==1)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
                sawAngler |= angler.State==Esc2Anglerfish.BehaviourState.Attack || angler.State==Esc2Anglerfish.BehaviourState.Recover;
                if(!lamps.All(l=>l.State==Esc2Lamprey.BehaviourState.Attached) || !sawAngler) return;
                Check(Vector3.Distance(startPosition,school.target.transform.position)>1,"W mueve al jugador real");
                Check(school.target.Alive,"Jugador vivo durante el encuentro");
                if(school.target.Health==100) return; // Drain has its own two-second timer after attachment.
                Check(Mathf.Approximately(school.target.desktopMovement.externalSpeedMultiplier,.7f),"Penalizacion real 30%");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); stage=2; shakeAt=Time.time;
                ScreenCapture.CaptureScreenshot("Logs/Esc2InputAttached.png");
            }
            else if(stage==2 && Time.time-shakeAt>=.22f)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(press%2==0?Key.A:Key.D)); press++; shakeAt=Time.time;
                if(press>=4) stage=3;
            }
            else if(stage==3 && Time.time-shakeAt>.15f)
            {
                Check(lamps.All(l=>l.State==Esc2Lamprey.BehaviourState.Stunned),"A D A D desprende ambas lampreas mediante entrada real");
                Check(school.target.desktopMovement.externalSpeedMultiplier==1,"Velocidad restaurada");
                Finish("PASS: escena esc2 guardada; todos los enemigos activos; materiales distintos; desplazamiento con W mediante Input System y SimpleFlyCamera; ambas lampreas persiguen y se adhieren; ralentizacion 30%; pez linterna embiste; dano real; A D A D desprende ambas y restaura velocidad. Solo se coloca al jugador una vez en la entrada del encuentro.");
            }
        }
        catch(Exception e) { Finish("FAIL: "+e); }
    }
}
#endif
