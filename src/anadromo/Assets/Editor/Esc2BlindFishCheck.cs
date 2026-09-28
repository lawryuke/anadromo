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
public static class Esc2BlindFishCheck
{
    const string KeyName="Esc2BlindFishCheck";
    static Esc2BlindFish fish;
    static PiranhaPlayerTarget player;
    static Keyboard keyboard;
    static Vector3 center,inspectionPoint;
    static int stage,scenario;
    static float since,inspectionStart,criticalStart;
    static double wallStart;
    static bool inspectionSeen;
    static string failure;
    static Esc2BlindFishCheck()
    {
        EditorApplication.playModeStateChanged+=Mode;
        if(SessionState.GetBool(KeyName,false)) Application.logMessageReceived+=Log;
    }
    static void Log(string text,string trace,LogType type) { if(type==LogType.Error || type==LogType.Exception) failure=text; }
    [MenuItem("Anadromo/Esc2/Pez ciego/Verificar encuentro en Play")]
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
            stage=scenario=0; since=Time.time; wallStart=EditorApplication.timeSinceStartup;
            Application.runInBackground=true;
            foreach(var energy in UnityEngine.Object.FindObjectsByType<Anadromo.Systems.EnergySystem>(FindObjectsSortMode.None)) energy.enabled=false;
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
    static void Check(bool value,string why) { if(!value) throw new Exception(why); }
    static void Keys(bool sprint,bool moving)
    {
        var side=((int)((Time.time-since)/.3f)%2==0)?Key.D:Key.A;
        InputSystem.QueueStateEvent(keyboard,moving ? (sprint?new KeyboardState(Key.LeftShift,side):new KeyboardState(side)) : (sprint?new KeyboardState(Key.LeftShift):new KeyboardState()));
    }
    static void Place()
    {
        var rb=player.GetComponent<Rigidbody>(); player.transform.SetPositionAndRotation(center,Quaternion.identity);
        rb.position=center; rb.rotation=Quaternion.identity; rb.linearVelocity=Vector3.zero;
        player.desktopMovement.SyncLookRotation(); fish.motion.ResetMotion(); Physics.SyncTransforms();
    }
    static void Next(int next) { stage=next; since=Time.time; }
    static void Finish(string result)
    {
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Esc2BlindFishCheck.txt",result);
        if(keyboard!=null) { InputSystem.RemoveDevice(keyboard); keyboard=null; }
        EditorApplication.update-=Tick; EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying) return;
        try
        {
            if(failure!=null) throw new Exception(failure);
            if(Time.time-since>14 || EditorApplication.timeSinceStartup-wallStart>150)
                throw new Exception("Timeout stage="+stage+" scenario="+scenario+" state="+(fish?fish.State.ToString():"?")+" agitation="+(fish?fish.Agitation:0));
            float elapsed=Time.time-since;
            if(stage==0 && elapsed>.5f)
            {
                fish=UnityEngine.Object.FindFirstObjectByType<Esc2BlindFish>(); Check(fish && fish.target && fish.motion,"Referencias guardadas");
                player=fish.target; Check(player.Alive && player.desktopMovement.enabled,"Control normal activo");
                center=fish.transform.Find("Referencia_Inspeccion_Jugador").position;
                keyboard=InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent(); player.ResetEncounter(); Place(); Next(1);
            }
            else if(stage==1)
            {
                Keys(false,true);
                if(elapsed>1.5f) { Check(fish.Agitation==0,"Nado sin Shift silencioso"); Keys(false,false); Next(2); }
            }
            else if(stage==2)
            {
                Keys(true,false);
                if(elapsed>1) { Check(fish.Agitation==0,"Shift sin desplazamiento no genera ruido"); Next(3); }
            }
            else if(stage==3)
            {
                Keys(true,true);
                if(fish.State==Esc2BlindFish.BehaviourState.Critical)
                {
                    Check(player.Alive && fish.Agitation==100,"Alerta completa sin muerte inmediata"); criticalStart=Time.time;
                    Keys(false,scenario==1); inspectionSeen=false; Next(4);
                }
            }
            else if(stage==4)
            {
                Keys(false,scenario==1); // Scenario 1 deliberately releases Shift but keeps swimming.
                if(scenario==1)
                {
                    if(fish.State==Esc2BlindFish.BehaviourState.Attack)
                    { Check(Time.time-criticalStart>=1.8f,"Respeta ventana de gracia"); Next(6); }
                }
                else if(fish.State==Esc2BlindFish.BehaviourState.Inspecting)
                {
                    if(!inspectionSeen)
                    {
                        inspectionSeen=true; inspectionStart=Time.time; inspectionPoint=fish.InspectionPoint;
                        player.transform.rotation=Quaternion.Euler(0,60,0); player.desktopMovement.SyncLookRotation();
                    }
                    Check(Vector3.Distance(inspectionPoint,fish.InspectionPoint)<.01f,"Mirar no hace orbitar al pez");
                    if(scenario==2 && Time.time-inspectionStart>.8f) { Keys(false,true); Next(6); }
                }
                else if(fish.State==Esc2BlindFish.BehaviourState.Retreat)
                {
                    Check(inspectionSeen && Time.time-inspectionStart>=2.9f,"Inspeccion sostenida durante tres segundos");
                    Check(player.Health==100 && fish.Agitation==0,"Quietud salva y reinicia inquietud: energy="+player.Health+" agitation="+fish.Agitation+" decayEnabled="+player.Vital.Energy.enabled+" player="+player.transform.position);
                    Next(5);
                }
            }
            else if(stage==5 && fish.State==Esc2BlindFish.BehaviourState.Chill)
            {
                Check(fish.Agitation==0,"Vuelve a patrulla");
                scenario=1; player.ResetEncounter(); Place(); Next(3);
            }
            else if(stage==6)
            {
                Keys(false,scenario==2 && player.Alive);
                if(!player.Alive)
                {
                    Check(!player.desktopMovement.enabled,"Contacto agota energia y bloquea locomocion");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R)); Next(7);
                }
            }
            else if(stage==7 && player.Alive)
            {
                Keys(false,false);
                Check(fish.State==Esc2BlindFish.BehaviourState.Chill && fish.Agitation==0 && player.Health==100,"R reinicia pez y energia");
                if(scenario==1) { scenario=2; Place(); Next(3); }
                else Finish("PASS: esc2 guardado; movimiento real con teclado; nado normal silencioso; Shift inmovil silencioso; sprint llena inquietud; gracia 2s; quietud inicia inspeccion 3s sin seguir la mirada; retirada y reset; soltar solo Shift lleva al ataque letal; moverse durante inspeccion lleva al ataque; contacto agota energia; R restaura jugador y pez. Tracking XR implementado, pendiente prueba con visor.");
            }
        }
        catch(Exception e) { Finish("FAIL: "+e); }
    }
}
#endif
