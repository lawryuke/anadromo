#if UNITY_EDITOR
using System;
using System.IO;
using Anadromo.AI;
using Anadromo.Mechanics;
using Anadromo.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class Esc2EnergyCheck
{
    const string KeyName="Esc2EnergyCheck";
    static PiranhaPlayerTarget player;
    static PlayerEnergyController vital;
    static EnergyVisualFeedback visual;
    static Keyboard keyboard;
    static PlayerFeeding feeding;
    static int stage,meals;
    static float since,initial;
    static double wallStart;
    static string failure;
    static Esc2EnergyCheck()
    {
        EditorApplication.playModeStateChanged+=Mode;
        if(SessionState.GetBool(KeyName,false)) Application.logMessageReceived+=Log;
    }
    static void Log(string message,string trace,LogType type) { if(type==LogType.Error || type==LogType.Exception) failure=message; }
    [MenuItem("Anadromo/Esc2/Energia/Verificar ciclo de energia en Play")]
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
        { stage=0; since=Time.time; wallStart=EditorApplication.timeSinceStartup; Application.runInBackground=true; EditorApplication.update+=Tick; }
        if(mode==PlayModeStateChange.ExitingPlayMode && keyboard!=null) { InputSystem.RemoveDevice(keyboard); keyboard=null; }
        if(mode==PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update-=Tick; Application.logMessageReceived-=Log;
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(KeyName+"Scene",""));
            SessionState.SetBool(KeyName,false);
        }
    }
    static void Check(bool value,string why) { if(!value) throw new Exception(why); }
    static void Next() { stage++; since=Time.time; }
    static void Finish(string result)
    {
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Esc2EnergyCheck.txt",result);
        if(keyboard!=null) { InputSystem.RemoveDevice(keyboard); keyboard=null; }
        EditorApplication.update-=Tick; EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying) return;
        try
        {
            if(failure!=null) throw new Exception(failure);
            if(Time.time-since>8 || EditorApplication.timeSinceStartup-wallStart>90) throw new Exception("Timeout etapa="+stage);
            if(stage==0 && Time.time-since>.5f)
            {
                player=UnityEngine.Object.FindFirstObjectByType<PiranhaPlayerTarget>(); vital=player.Vital; visual=player.GetComponent<EnergyVisualFeedback>();
                Check(vital && visual && !player.showDebugStatus,"Energia y visuales conectados, HUD desactivado");
                Check(player.GetComponents<EnergySystem>().Length==1,"Unico deposito de energia");
                initial=vital.Current; Next();
            }
            else if(stage==1 && Time.time-since>1)
            {
                Check(initial-vital.Current>.8f && initial-vital.Current<1.4f,"Drenaje pasivo unico 1/s");
                vital.Energy.enabled=false; // Deterministic amounts for the remaining assertions.
                vital.Energy.SetEnergy(70); player.TakeDamage(8); visual.ApplyFeedback();
                Check(vital.Current==62 && player.Health==62,"Ataque resta del mismo recurso");
                Check(vital.ImpactPulse>.9f && visual.Saturation<-37,"Impacto y desaturacion");
                Next();
            }
            else if(stage==2 && Time.time-since>.5f)
            {
                Check(vital.ImpactPulse==0,"Pulso breve termina");
                vital.Energy.SetEnergy(20); visual.ApplyFeedback();
                Check(vital.SwimMultiplier<1 && vital.TurnMultiplier<1 && vital.CurrentMultiplier>1,"Penalizaciones criticas");
                Check(visual.Saturation<=-79 && visual.ExhaustionIntensity>.3f,"Agotamiento visible");
                keyboard=InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W)); Next();
            }
            else if(stage==3 && Time.time-since>.25f)
            {
                float baseSpeed=Anadromo.Config.GameSettings.I ? Anadromo.Config.GameSettings.I.playerSpeed : player.desktopMovement.movementSpeed;
                Check(Mathf.Abs(player.GetComponent<Rigidbody>().linearVelocity.magnitude-baseSpeed*vital.SwimMultiplier)<.2f,"Ralentizacion en controlador real");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                feeding=player.GetComponent<PlayerFeeding>(); meals=feeding.TotalConsumed;
                var mouth=feeding.GetComponent<SphereCollider>();
                var food=GameObject.CreatePrimitive(PrimitiveType.Sphere); food.name="Krill prueba energia"; food.tag=feeding.edibleTags[0];
                food.transform.position=feeding.transform.TransformPoint(mouth.center); food.transform.localScale=Vector3.one*.08f;
                food.GetComponent<Collider>().isTrigger=true; food.AddComponent<Prey>().energyValue=15;
                Physics.SyncTransforms(); Next();
            }
            else if(stage==4 && feeding.TotalConsumed>meals)
            {
                visual.ApplyFeedback();
                Check(vital.Current==35 && vital.RecoveryPulse>0 && vital.SwimMultiplier==1,"Krill real recupera energia, pulso y velocidad");
                Check(visual.Saturation>-66,"Recuperacion de color");
                ScreenCapture.CaptureScreenshot("Logs/Esc2EnergyRecovery.png"); Next();
            }
            else if(stage==5 && Time.time-since>.5f)
            {
                player.TakeDamage(vital.Maximum);
                Check(!player.Alive && !player.desktopMovement.enabled,"Agotamiento bloquea locomocion");
                vital.ConsumeKrill(15); Check(vital.Current==0,"Comida no resucita tras agotarse");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R)); Next();
            }
            else if(stage==6 && player.Alive)
            {
                Check(vital.Current==100 && player.desktopMovement.enabled && vital.ImpactPulse==0 && vital.RecoveryPulse==0,"R restaura recurso, control y visuales");
                visual.ApplyFeedback(); Check(visual.Saturation==0 && visual.ExhaustionIntensity==0,"Vision restaurada");
                Finish("PASS: esc2 guardado; una energia; drenaje pasivo; ataque consume energia; pulso breve; agotamiento/desaturacion; penalizaciones; W real ralentizado; krill consumido por trigger real restaura energia y color; cero bloquea movimiento; R real reinicia recurso y visuales.");
            }
        }
        catch(Exception e) { Finish("FAIL: "+e); }
    }
}
#endif
