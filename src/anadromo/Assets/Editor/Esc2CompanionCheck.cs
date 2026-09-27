#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Anadromo.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Esc2CompanionCheck
{
    const string Key="Esc2CompanionCheck";
    static int stage;
    static float since;
    static PiranhaSchool school;
    static PiranhaPlayerTarget player;
    static Esc2Lamprey[] lamps;
    static Esc2Anglerfish angler;
    static Vector3[] homes;
    static Vector3 anglerHome;
    static string failure;
    static Esc2CompanionCheck()
    {
        EditorApplication.delayCall+=Request;
        EditorApplication.playModeStateChanged+=Mode;
        if(SessionState.GetBool(Key,false)) Application.logMessageReceived+=Log;
    }
    static void Log(string text,string trace,LogType type)
    { if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert) failure=text; }
    static void Request()
    {
        if(!File.Exists("Library/Esc2Companion.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete("Library/Esc2Companion.request");
        Run();
    }
    [MenuItem("Anadromo/Esc2/Depredadores/Verificar lampreas y pez linterna")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetString(Key+"Previous",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/esc2.unity");
        SessionState.SetBool(Key,true); EditorApplication.isPlaying=true;
    }
    static void Mode(PlayModeStateChange mode)
    {
        if(!SessionState.GetBool(Key,false)) return;
        if(mode==PlayModeStateChange.EnteredPlayMode) {
            foreach(var energy in UnityEngine.Object.FindObjectsByType<Anadromo.Systems.EnergySystem>(FindObjectsSortMode.None)) energy.enabled=false;
 stage=0; since=Time.time; EditorApplication.update+=Tick; }
        if(mode==PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update-=Tick; Application.logMessageReceived-=Log;
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key+"Previous",""));
            SessionState.SetBool(Key,false);
        }
    }
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    static void Place(Vector3 position)
    {
        player.transform.position=position;
        var rb=player.GetComponent<Rigidbody>(); rb.position=position; rb.linearVelocity=Vector3.zero;
        Physics.SyncTransforms();
    }
    static void Next() { stage++; since=Time.time; }
    static void Finish(string result)
    {
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Esc2CompanionCheck.txt",result);
        EditorApplication.update-=Tick; EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try
        {
            if(failure!=null) throw new Exception(failure);
            float elapsed=Time.time-since;
            if(elapsed>15) throw new Exception("Timeout etapa "+stage+" lampreas="+(lamps==null?"":string.Join(",",lamps.Select(l=>l.name+":"+l.State+"@"+l.transform.position)))+" player="+(player?player.transform.position.ToString():"?"));
            if(stage==0 && elapsed>.5f)
            {
                Check(SceneManager.GetActiveScene().name=="esc2","Escena final activa");
                school=UnityEngine.Object.FindFirstObjectByType<PiranhaSchool>(); Check(school && school.target,"Referencias"); player=school.target;
                lamps=school.GetComponentsInChildren<Esc2Lamprey>(); angler=school.GetComponentInChildren<Esc2Anglerfish>();
                Check(lamps.Length==2 && angler,"Cantidad de enemigos");
                homes=lamps.Select(l=>l.transform.position).ToArray(); anglerHome=angler.transform.position;
                Check(Vector3.Distance(homes[0],homes[1])>1,"Spawns distintos guardados");
                foreach(var pos in homes) Check(school.Contains(pos),"Lamprea dentro del territorio");
                Check(school.Contains(anglerHome),"Pez linterna dentro del territorio");
                foreach(var l in lamps) foreach(var c in Physics.OverlapSphere(l.transform.position,.16f,school.obstacleLayers,QueryTriggerInteraction.Ignore)) Check(!school.IsObstacle(c),"Lamprea dentro de "+c.name);
                foreach(var p in school.GetComponentsInChildren<Esc2Piranha>()) p.enabled=false;
                angler.enabled=false; player.desktopMovement.enabled=false;
                Place(school.transform.position);
                var report=new System.Text.StringBuilder();
                report.AppendLine("zone="+school.transform.position+" size="+school.zoneSize+" contains="+school.Contains(player.transform.position)+" alive="+player.Alive);
                foreach(var l in lamps)
                {
                    report.AppendLine(l.name+" enabled="+l.isActiveAndEnabled+" target="+l.Target+" pos="+l.transform.position);
                    Vector3 d=player.transform.position-l.transform.position;
                    foreach(var h in Physics.RaycastAll(l.transform.position,d.normalized,d.magnitude,school.obstacleLayers,QueryTriggerInteraction.Ignore)) report.AppendLine("hit "+h.collider.name+" layer="+h.collider.gameObject.layer+" trigger="+h.collider.isTrigger+" obstacle="+school.IsObstacle(h.collider)+" distance="+h.distance);
                }
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Esc2CompanionDetails.txt",report.ToString());
                Next();
            }
            else if(stage==1 && lamps.All(l=>l.State==Esc2Lamprey.BehaviourState.Attached))
            {
                Check(Mathf.Approximately(player.SpeedMultiplier,.7f),"Dos penalizaciones");
                Check(Vector3.Distance(lamps[0].transform.position,lamps[1].transform.position)>.5f,"Adhesiones separadas"); Next();
            }
            else if(stage==2 && elapsed>2.2f)
            {
                Check(player.Health<=90,"Drenaje natural de ambas lampreas");
                player.ShakeLampreys(35); player.ShakeLampreys(35); player.ShakeLampreys(35);
                Check(lamps.All(l=>l.State==Esc2Lamprey.BehaviourState.Stunned),"Sacudidas desprenden");
                Check(player.SpeedMultiplier==1 && player.desktopMovement.externalSpeedMultiplier==1,"Velocidad restaurada");
                player.ResetEncounter();
                for(int i=0;i<lamps.Length;i++) Check(Vector3.Distance(lamps[i].transform.position,homes[i])<.01f,"Spawn original tras reinicio");
                foreach(var l in lamps) l.enabled=false;
                angler.enabled=true; angler.ResetFish(); Place(anglerHome+Vector3.back*1.5f); Next();
            }
            else if(stage==3 && angler.State==Esc2Anglerfish.BehaviourState.Disturbed)
            {
                Check(!angler.lureLight.enabled && !angler.lure.GetComponent<Renderer>().enabled,"Señuelo apagado completo"); Next();
            }
            else if(stage==4 && player.Health<100)
            {
                Check(player.Health==75,"Embestida natural aplica 25");
                player.ResetEncounter(); Check(Vector3.Distance(angler.transform.position,anglerHome)<.01f && angler.State==Esc2Anglerfish.BehaviourState.Chill,"Reinicio del pez linterna");
                Place(anglerHome+Vector3.back*1.5f); Next();
            }
            else if(stage==5 && angler.State==Esc2Anglerfish.BehaviourState.Attack)
            { Place(player.transform.position+Vector3.up*1.1f); Next(); }
            else if(stage==6 && elapsed>1.5f)
            {
                Check(player.Health==100,"Esquiva vertical evita embestida");
                player.TakeDamage(100); Check(!player.Alive,"Muerte"); player.ResetEncounter();
                Check(player.Health==100 && player.SpeedMultiplier==1,"Reinicio completo");
                Finish("PASS: esc2 guardado; spawns libres y separados; persecución y adhesión natural de dos lampreas; posiciones distintas en cámara; ralentización 30%; drenaje; sacudidas; reinicio de spawns; señuelo completamente apagado; ataque natural 25 de daño; esquiva vertical; muerte y reinicio. Sin invocar Attach ni modificar velocidades de enemigos.");
            }
        }
        catch(Exception e) { Finish("FAIL etapa "+stage+": "+e); }
    }
}
#endif
