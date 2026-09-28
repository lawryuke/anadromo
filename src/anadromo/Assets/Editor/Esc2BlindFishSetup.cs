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
public static class Esc2BlindFishSetup
{
    static Esc2BlindFishSetup() { EditorApplication.update+=Request; }
    static void Request()
    {
        const string path="Library/Esc2BlindFish.request";
        if(!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action=File.ReadAllText(path).Trim(); File.Delete(path);
        try { if(action=="install") Install(); if(action=="test") Esc2BlindFishCheck.Run(); }
        catch(Exception e) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Esc2BlindFishSetup.txt","FAIL: "+e); Debug.LogException(e); }
    }
    [MenuItem("Anadromo/Esc2/Pez ciego/Instalar encuentro")]
    public static void Install()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/_Project/Scenes/esc2.unity") throw new InvalidOperationException("Abre esc2.");
        if(UnityEngine.Object.FindFirstObjectByType<Esc2BlindFish>()) return;
        var target=UnityEngine.Object.FindFirstObjectByType<PiranhaPlayerTarget>();
        if(!target) throw new InvalidOperationException("Falta KM_Player.");
        Directory.CreateDirectory("Library/Esc2PiranhaBackup");
        EditorSceneManager.SaveScene(scene,"Library/Esc2PiranhaBackup/esc2-before-blind-fish.unity",true);
        Physics.SyncTransforms();
        var enemies=UnityEngine.Object.FindObjectsByType<Esc2Lamprey>(FindObjectsSortMode.None).Select(e=>e.transform.position)
            .Concat(UnityEngine.Object.FindObjectsByType<Esc2Anglerfish>(FindObjectsSortMode.None).Select(e=>e.transform.position))
            .Concat(UnityEngine.Object.FindObjectsByType<Esc2SharkPassage>(FindObjectsSortMode.None).SelectMany(e=>e.waypoints.Select(p=>p.position))).ToArray();
        bool Wall(Collider c) => !c.transform.IsChildOf(target.transform.root);
        bool Free(Vector3 p,float radius)
        { foreach(var c in Physics.OverlapSphere(p,radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) if(Wall(c)) return false; return true; }
        bool Clear(Vector3 a,Vector3 b,float radius)
        {
            if(!Free(a,radius) || !Free(b,radius)) return false;
            Vector3 d=b-a;
            foreach(var h in Physics.SphereCastAll(a,radius,d.normalized,d.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) if(Wall(h.collider)) return false;
            return true;
        }
        Vector3 center=Vector3.zero; float best=float.PositiveInfinity;
        for(float z=28;z<=52;z+=2)
        for(float y=-10;y<=2;y+=1)
        for(float x=-18;x<=18;x+=2)
        {
            Vector3 p=new Vector3(x,y,z);
            if(enemies.Any(e=>Vector3.Distance(p,e)<12)) continue;
            if(!Clear(p-Vector3.right*1.7f,p+Vector3.right*1.7f,.4f)) continue;
            if(!Clear(p-Vector3.forward*2,p+Vector3.forward*2,.4f)) continue;
            if(!Clear(p+new Vector3(-.7f,0,2),p+new Vector3(.7f,0,2),.4f)) continue;
            // Require an actual cave floor/ceiling, not empty space outside the mesh.
            bool floor=Physics.RaycastAll(p,Vector3.down,5,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(h=>Wall(h.collider));
            bool ceiling=Physics.RaycastAll(p,Vector3.up,6,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(h=>Wall(h.collider));
            if(!floor || !ceiling) continue;
            float score=(p-new Vector3(0,-2,36)).sqrMagnitude;
            if(score<best) { best=score; center=p; }
        }
        if(float.IsInfinity(best)) throw new InvalidOperationException("No se encontro un tramo interior despejado y separado de los otros enemigos.");
        var sensor=target.GetComponent<BlindFishMotionSensor>();
        if(!sensor) sensor=Undo.AddComponent<BlindFishMotionSensor>(target.gameObject);
        const string folder="Assets/_Project/Prefabs/Enemies/";
        var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"PezCiegoEsc2.mat");
        if(!material)
        {
            material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color=new Color(.22f,.46f,.65f);
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",material.color*.25f);
            AssetDatabase.CreateAsset(material,folder+"PezCiegoEsc2.mat");
        }
        var root=new GameObject("Encuentro_Pez_Ciego"); root.transform.position=center;
        var fish=root.AddComponent<Esc2BlindFish>();
        var body=GameObject.CreatePrimitive(PrimitiveType.Sphere); body.name="Cuerpo_Pez_Ciego";
        body.transform.SetParent(root.transform,false); body.transform.localPosition=Vector3.forward*2; body.transform.localScale=new Vector3(.65f,.5f,1);
        UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().sharedMaterial=material; fish.body=body.transform; fish.bodyRenderer=body.GetComponent<Renderer>();
        fish.patrolPoints=new Transform[2];
        for(int i=0;i<2;i++)
        {
            var point=new GameObject("Ruta_0"+i); point.transform.SetParent(root.transform,false);
            point.transform.localPosition=new Vector3(i==0?-.7f:.7f,0,2); fish.patrolPoints[i]=point.transform;
        }
        var entry=new GameObject("Referencia_Inspeccion_Jugador"); entry.transform.SetParent(root.transform,false);
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"PezCiegoEsc2.prefab"); UnityEngine.Object.DestroyImmediate(root);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
        Undo.RegisterCreatedObjectUndo(instance,"Instalar pez ciego");
        var installed=instance.GetComponent<Esc2BlindFish>(); installed.target=target; installed.motion=sensor;
        PrefabUtility.RecordPrefabInstancePropertyModifications(installed);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Selection.activeGameObject=instance;
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Esc2BlindFishSetup.txt","PASS: centro="+center+" cuerpo="+installed.body.position+" jugador="+target.name+"; ruta libre, suelo y techo detectados, minimo 12 m de lampreas/linterna/rutas tiburon.");
    }
}
#endif
