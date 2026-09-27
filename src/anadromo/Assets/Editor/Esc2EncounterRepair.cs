#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Anadromo.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Esc2EncounterRepair
{
    static Esc2EncounterRepair() { EditorApplication.update+=Request; }
    static void Request()
    {
        const string path="Library/Esc2Encounter.request";
        if(File.Exists(path) && File.ReadAllText(path).Trim()=="stop")
        { File.Delete(path); EditorApplication.isPaused=false; EditorApplication.isPlaying=false; return; }
        if(!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action=File.ReadAllText(path).Trim(); File.Delete(path);
        if(action=="repair") Repair();
        if(action=="test") Esc2CompanionCheck.Run();
    }
    [MenuItem("Anadromo/Esc2/Depredadores/Reparar referencias y colores")]
    public static void Repair()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/_Project/Scenes/esc2.unity") throw new System.InvalidOperationException("Abre esc2.");
        Directory.CreateDirectory("Library/Esc2PiranhaBackup");
        EditorSceneManager.SaveScene(scene,"Library/Esc2PiranhaBackup/esc2-before-reference-repair.unity",true);
        var lamps=Object.FindObjectsByType<Esc2Lamprey>(FindObjectsSortMode.None);
        var angler=Object.FindFirstObjectByType<Esc2Anglerfish>();
        var player=Object.FindFirstObjectByType<PiranhaPlayerTarget>();
        if(!angler || lamps.Length==0 || !player) throw new System.InvalidOperationException("Faltan enemigos o jugador.");
        var parent=angler.transform.parent;
        var school=parent.GetComponent<PiranhaSchool>();
        if(!school) school=Undo.AddComponent<PiranhaSchool>(parent.gameObject);
        Undo.RecordObject(school,"Conectar territorio y jugador"); school.target=player;
        // Keep the user's grouping and positions. Include every fish in the local territory.
        Vector3 extent=new Vector3(3,1.3f,3.5f);
        foreach(var enemy in parent.GetComponentsInChildren<MonoBehaviour>())
        {
            if(!(enemy is Esc2Lamprey) && !(enemy is Esc2Anglerfish) && !(enemy is Esc2Piranha)) continue;
            var p=parent.InverseTransformPoint(enemy.transform.position);
            extent=Vector3.Max(extent,new Vector3(Mathf.Abs(p.x),Mathf.Abs(p.y),Mathf.Abs(p.z))+Vector3.one*.8f);
        }
        school.zoneSize=extent*2;
        const string folder="Assets/_Project/Prefabs/Enemies/";
        Material Make(string name,Color color,bool emissive=false)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(folder+name+".mat");
            if(!m) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,folder+name+".mat"); }
            m.color=color;
            if(emissive) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",color*2); }
            EditorUtility.SetDirty(m); return m;
        }
        var purple=Make("LampreaEsc2",new Color(.7f,.3f,.75f));
        var blue=Make("PezLinternaEsc2",new Color(.32f,.35f,.52f));
        var glow=Make("SenueloEsc2",new Color(.25f,1,.7f),true);
        void Assign(Renderer r,Material m)
        { Undo.RecordObject(r,"Color del enemigo"); r.sharedMaterial=m; PrefabUtility.RecordPrefabInstancePropertyModifications(r); }
        foreach(var l in lamps) Assign(l.body,purple);
        Assign(angler.body,blue); Assign(angler.lure.GetComponent<Renderer>(),glow);
        foreach(string name in new[]{"LampreaEsc2","PezLinternaEsc2"})
        {
            string path=folder+name+".prefab"; var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var l=root.GetComponent<Esc2Lamprey>(); var a=root.GetComponent<Esc2Anglerfish>();
                if(l) l.body.sharedMaterial=purple;
                if(a) { a.body.sharedMaterial=blue; a.lure.GetComponent<Renderer>().sharedMaterial=glow; }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        EditorUtility.SetDirty(school); EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/Esc2EncounterRepair.txt","school="+school.name+" position="+school.transform.position+" size="+school.zoneSize+" player="+player.name+" position="+player.transform.position+"\n"+string.Join("\n",lamps.Select(l=>l.name+" position="+l.transform.position))+"\nangler="+angler.transform.position);
        Selection.activeGameObject=parent.gameObject;
    }
}
#endif
