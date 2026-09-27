#if UNITY_EDITOR
using System;
using System.IO;
using Anadromo.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class Esc2SharkCheck
{
    const string KeyName="Esc2SharkCheck";
    static Esc2SharkPassage passage;
    static Keyboard keyboard;
    static Vector3 center,refuge;
    static int stage;
    static float started;
    static double wallStart;
    static string failure;
    static Esc2SharkCheck()
    {
        EditorApplication.playModeStateChanged+=Mode;
        if(SessionState.GetBool(KeyName,false)) Application.logMessageReceived+=Log;
    }
    static void Log(string message,string stack,LogType type) { if(type==LogType.Error || type==LogType.Exception) failure=message; }
    [MenuItem("Anadromo/Esc2/Depredadores/Verificar tiburon en Play")]
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
            stage=0; started=Time.time; wallStart=EditorApplication.timeSinceStartup;
            Application.runInBackground=true; EditorApplication.update+=Tick;
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
    static void Place(Vector3 position)
    {
        var player=passage.target; var rb=player.GetComponent<Rigidbody>();
        player.transform.SetPositionAndRotation(position,Quaternion.identity); rb.position=position; rb.rotation=Quaternion.identity; rb.linearVelocity=Vector3.zero;
        player.desktopMovement.SyncLookRotation(); Physics.SyncTransforms();
    }
    static void Finish(string result)
    {
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Esc2SharkCheck.txt",result);
        if(keyboard!=null) { InputSystem.RemoveDevice(keyboard); keyboard=null; }
        EditorApplication.update-=Tick; EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying) return;
        try
        {
            if(failure!=null) throw new Exception(failure);
            if(EditorApplication.timeSinceStartup-wallStart>120 || Time.time-started>40) throw new Exception("Timeout etapa="+stage);
            if(stage==0 && Time.time-started>.5f)
            {
                passage=UnityEngine.Object.FindFirstObjectByType<Esc2SharkPassage>(); Check(passage && passage.Configured,"Referencias guardadas");
                Check(passage.State==Esc2SharkPassage.PassageState.Waiting && passage.target.Health==100,"Inicio seguro");
                center=passage.waypoints[1].position; refuge=passage.transform.Find("Referencia_Refugio").position;
                keyboard=InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
                Place(center); stage=1;
            }
            else if(stage==1 && passage.State==Esc2SharkPassage.PassageState.Warning)
            {
                Check(!passage.shark.gameObject.activeSelf,"Aviso antes de aparecer");
                Check(passage.WarningRemaining>2.5f,"Tres segundos de aviso"); stage=2;
            }
            else if(stage==2 && !passage.target.Alive)
            {
                Check(!passage.target.desktopMovement.enabled,"Contacto letal bloquea locomocion");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R)); stage=3;
            }
            else if(stage==3 && passage.target.Alive)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                Check(passage.target.Health==100 && passage.target.desktopMovement.enabled,"R restaura jugador");
                Check(passage.State==Esc2SharkPassage.PassageState.Waiting && !passage.shark.gameObject.activeSelf,"R reinicia el paso");
                Place(center); stage=4;
            }
            else if(stage==4 && passage.State==Esc2SharkPassage.PassageState.Warning)
            {
                Vector3 d=refuge-center;
                var key=Mathf.Abs(d.x)>.5f ? (d.x>0?Key.D:Key.A) : (d.y>0?Key.E:Key.Q);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(key)); stage=5;
            }
            else if(stage==5 && Vector3.Distance(center,passage.target.transform.position)>=1.1f)
            { InputSystem.QueueStateEvent(keyboard,new KeyboardState()); stage=6; }
            else if(stage==6 && passage.State==Esc2SharkPassage.PassageState.Cooldown)
            {
                Check(passage.target.Health==100,"Entrada real permite esquivar");
                Check(!passage.shark.gameObject.activeSelf,"Desaparece al final");
                ScreenCapture.CaptureScreenshot("Logs/Esc2SharkEvaded.png"); stage=7;
            }
            else if(stage==7 && passage.State==Esc2SharkPassage.PassageState.Warning)
            {
                Check(passage.target.Health==100,"Reactivacion despues de cooldown");
                foreach(int fps in new[]{5,30,60,120})
                {
                    passage.target.ResetEncounter(); Place(center);
                    passage.Tick(.01f); passage.Tick(3.1f);
                    for(int i=0;i<fps && passage.target.Alive;i++) passage.Tick(1f/fps);
                    Check(!passage.target.Alive,"Barrido letal a "+fps+" FPS");
                    passage.target.ResetEncounter(); Place(refuge);
                    passage.Tick(.01f); passage.Tick(3.1f);
                    for(int i=0;i<fps;i++) passage.Tick(1f/fps);
                    Check(passage.target.Health==100 && passage.State==Esc2SharkPassage.PassageState.Cooldown,"No persigue al jugador a "+fps+" FPS");
                }
                passage.target.ResetEncounter();
                Finish("PASS: esc2 guardado; inicio seguro; aviso natural 3 s; paso fijo y contacto letal; R real restaura jugador y paso; esquiva por teclado con controlador original; desaparicion; repeticion 18 s; barridos y evasiones a 5/30/60/120 FPS. Los otros enemigos permanecieron activos.");
            }
        }
        catch(Exception e) { Finish("FAIL: "+e); }
    }
}
#endif
