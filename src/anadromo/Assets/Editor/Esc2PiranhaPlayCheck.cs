#if UNITY_EDITOR
using System;
using System.IO;
using Anadromo.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class Esc2PiranhaPlayCheck
{
    const string Key="Esc2PiranhaPlayCheck";
    static double start;
    static string failure;
    static int stage;
    static Esc2PiranhaPlayCheck()
    {
        EditorApplication.playModeStateChanged+=Mode;
        EditorApplication.delayCall+=Request;
        if(SessionState.GetBool(Key,false)) Application.logMessageReceived+=Log;
    }
    static void Request()
    {
        const string path="Library/Esc2PiranhaPlay.request";
        if(!File.Exists(path) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(path); Run();
    }
    [MenuItem("Anadromo/Esc2/Pirañas/Probar escena en Play Mode")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        Esc2PiranhaValidation.Run();
        SessionState.SetString(Key+"Previous",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/esc2.unity");
        SessionState.SetBool(Key,true); EditorApplication.isPlaying=true;
    }
    static void Log(string message,string stack,LogType type) { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) failure=message; }
    static void Mode(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false)) return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            start=EditorApplication.timeSinceStartup; stage=0; failure=null;
            SessionState.SetInt(Key+"FPS",Application.targetFrameRate); Application.targetFrameRate=30;
            EditorApplication.update+=Tick;
        }
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update-=Tick; Application.logMessageReceived-=Log;
            Application.targetFrameRate=SessionState.GetInt(Key+"FPS",-1);
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key+"Previous",""));
            SessionState.SetBool(Key,false);
        }
    }
    static void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }
    static void Tick()
    {
        if(!EditorApplication.isPlaying) return;
        try
        {
            if(failure!=null) throw new InvalidOperationException(failure);
            if(EditorApplication.timeSinceStartup-start<3) return;
            var school=UnityEngine.Object.FindFirstObjectByType<PiranhaSchool>();
            Check(school && school.target,"Cardumen y jugador conectados");
            var player=school.target; var fish=school.GetComponentsInChildren<Esc2Piranha>();
            var lampreys=school.GetComponentsInChildren<Esc2Lamprey>();
            var angler=school.GetComponentInChildren<Esc2Anglerfish>();
            if(stage==0)
            {
                Check(fish.Length==2,"Exactamente dos pirañas");
                Check(lampreys.Length==2 && angler,"Dos lampreas y un pez linterna en zona A");
                Check(player.Health==100,"Inicio seguro fuera del territorio");
                Check(player.GetComponent<SimpleFlyCamera>() && !player.GetComponent<Anadromo.CavernMVP.CavernPlayer>(),"Locomoción original sin controlador MVP");
                foreach(var f in fish) Check(f.School==school && school.Contains(f.transform.position),"Registro y límites de patrulla");
                var details=new System.Text.StringBuilder();
                details.AppendLine("player="+player.transform.position+" zone="+school.transform.position+" size="+school.zoneSize);
                foreach(var component in school.GetComponentsInChildren<MonoBehaviour>())
                    if(component is Esc2Piranha || component is Esc2Lamprey || component is Esc2Anglerfish)
                    {
                        var r=component.GetComponentInChildren<Renderer>();
                        details.AppendLine(component.name+" position="+component.transform.position+" visible="+(r && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(Camera.main),r.bounds))+" clear="+school.ClearPath(player.transform.position,component.transform.position));
                    }
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Esc2PiranhaDetails.txt",details.ToString());
                ScreenCapture.CaptureScreenshot("Logs/Esc2Piranhas.png");
                var copy=UnityEngine.Object.Instantiate(fish[0].gameObject,school.transform); var extra=copy.GetComponent<Esc2Piranha>();
                Check(extra.School==school,"Instancia duplicada descubre cardumen automáticamente");
                school.Alert(fish[0]); Check(extra.State==Esc2Piranha.BehaviourState.Attack,"Duplicado recibe alerta");
                UnityEngine.Object.Destroy(copy);
                var rb=player.GetComponent<Rigidbody>(); rb.position=school.transform.position;
                player.transform.position=school.transform.position; Physics.SyncTransforms();
                fish[0].transform.position=player.transform.position+Vector3.forward*.1f;
                fish[0].BeginAttack(); fish[0].Tick(.001f);
                Check(player.Health==92,"Mordida real aplica daño");
                player.TakeDamage(100); Check(!player.Alive && !player.desktopMovement.enabled,"Muerte bloquea locomoción");
                player.ResetEncounter(); Check(player.Health==100 && player.desktopMovement.enabled,"Reinicio recupera salud y locomoción");
                foreach(var f in fish) Check(f.State==Esc2Piranha.BehaviourState.Chill,"Reinicio calma cardumen");
                stage=1;
            }
            else if(EditorApplication.timeSinceStartup-start>5)
            {
                Check(fish.Length==2 && player.Health==100,"Reinicio estable sin duplicados ni daño inicial");
                File.WriteAllText("Logs/Esc2PiranhaPlayCheck.txt","PASS: esc2 con dos pirañas, dos lampreas y un pez linterna; referencias, territorio, inicio seguro, duplicación automática, alerta, mordida, muerte y reinicio. Sin errores de ejecución.");
                EditorApplication.update-=Tick; EditorApplication.isPlaying=false;
            }
        }
        catch(Exception e)
        {
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Esc2PiranhaPlayCheck.txt","FAIL: "+e);
            EditorApplication.update-=Tick; EditorApplication.isPlaying=false;
        }
    }
}
#endif
