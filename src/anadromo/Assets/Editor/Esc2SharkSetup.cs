#if UNITY_EDITOR
using System;
using System.IO;
using Anadromo.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Esc2SharkSetup
{
    static Esc2SharkSetup() { EditorApplication.update+=Request; }
    static void Request()
    {
        const string path="Library/Esc2Shark.request";
        if(!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action=File.ReadAllText(path).Trim(); File.Delete(path);
        try { if(action=="install") Install(); if(action=="test") Esc2SharkCheck.Run(); }
        catch(Exception e) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Esc2SharkSetup.txt","FAIL: "+e); Debug.LogException(e); }
    }
    [MenuItem("Anadromo/Esc2/Depredadores/Instalar paso de tiburon")]
    public static void Install()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/_Project/Scenes/esc2.unity") throw new InvalidOperationException("Abre esc2 antes de instalar.");
        if(UnityEngine.Object.FindFirstObjectByType<Esc2SharkPassage>()) return;
        var target=UnityEngine.Object.FindFirstObjectByType<PiranhaPlayerTarget>();
        if(!target) throw new InvalidOperationException("Falta el jugador del encuentro.");
        Directory.CreateDirectory("Library/Esc2PiranhaBackup");
        EditorSceneManager.SaveScene(scene,"Library/Esc2PiranhaBackup/esc2-before-shark.unity",true);
        Physics.SyncTransforms();
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
        Vector3 center=Vector3.zero,escape=Vector3.zero; float best=float.PositiveInfinity;
        for(float z=9;z<=18;z+=.5f)
        for(float y=-2.5f;y<=1.5f;y+=.5f)
        for(float x=-2;x<=2;x+=.5f)
        {
            Vector3 p=new Vector3(x,y,z);
            if(!Clear(p-Vector3.forward*4,p+Vector3.forward*4,.5f)) continue;
            Vector3 safe=Vector3.zero; bool found=false;
            foreach(var side in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down})
            {
                var candidate=p+side*1.2f;
                if(Clear(p,candidate,.24f)) { safe=candidate; found=true; break; }
            }
            if(!found) continue;
            float score=(p-new Vector3(0,-1,12)).sqrMagnitude;
            if(score<best) { best=score; center=p; escape=safe; }
        }
        if(float.IsInfinity(best)) throw new InvalidOperationException("No hay un tramo libre de ocho metros con refugio lateral en el paso anterior a zona A.");
        const string folder="Assets/_Project/Prefabs/Enemies/";
        var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"TiburonEsc2.mat");
        if(!material)
        {
            material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color=new Color(.95f,.38f,.08f);
            AssetDatabase.CreateAsset(material,folder+"TiburonEsc2.mat");
        }
        var root=new GameObject("Paso_Tiburon"); root.transform.position=center;
        var passage=root.AddComponent<Esc2SharkPassage>();
        var visual=new GameObject("Tiburon_Naranja"); visual.transform.SetParent(root.transform,false); passage.shark=visual.transform;
        void Shape(string name,PrimitiveType type,Vector3 position,Vector3 scale,Quaternion rotation)
        {
            var shape=GameObject.CreatePrimitive(type); shape.name=name; shape.transform.SetParent(visual.transform,false);
            shape.transform.localPosition=position; shape.transform.localScale=scale; shape.transform.localRotation=rotation;
            UnityEngine.Object.DestroyImmediate(shape.GetComponent<Collider>()); shape.GetComponent<Renderer>().sharedMaterial=material;
        }
        Shape("Cuerpo",PrimitiveType.Sphere,Vector3.zero,new Vector3(.7f,.55f,1.8f),Quaternion.identity);
        Shape("Aleta_dorsal",PrimitiveType.Cube,new Vector3(0,.27f,-.05f),new Vector3(.08f,.36f,.38f),Quaternion.Euler(25,0,0));
        Shape("Aleta_izquierda",PrimitiveType.Cube,new Vector3(-.35f,-.05f,-.12f),new Vector3(.35f,.06f,.3f),Quaternion.Euler(0,25,0));
        Shape("Aleta_derecha",PrimitiveType.Cube,new Vector3(.35f,-.05f,-.12f),new Vector3(.35f,.06f,.3f),Quaternion.Euler(0,-25,0));
        Shape("Cola",PrimitiveType.Cube,new Vector3(0,0,-.9f),new Vector3(.08f,.55f,.2f),Quaternion.identity);
        passage.waypoints=new Transform[3];
        for(int i=0;i<3;i++)
        {
            var point=new GameObject("Ruta_0"+i); point.transform.SetParent(root.transform,false); point.transform.localPosition=Vector3.forward*((i-1)*3);
            passage.waypoints[i]=point.transform;
        }
        var refuge=new GameObject("Referencia_Refugio"); refuge.transform.SetParent(root.transform,false); refuge.transform.position=escape;
        passage.shark.position=passage.waypoints[0].position;
        // Save a reusable prefab without a reference to a scene-only player.
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"PasoTiburonEsc2.prefab"); UnityEngine.Object.DestroyImmediate(root);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene); Undo.RegisterCreatedObjectUndo(instance,"Instalar paso de tiburon");
        var installed=instance.GetComponent<Esc2SharkPassage>(); installed.target=target;
        PrefabUtility.RecordPrefabInstancePropertyModifications(installed);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Selection.activeGameObject=instance;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/Esc2SharkSetup.txt","PASS: ruta libre centro="+center+" inicio="+installed.waypoints[0].position+" fin="+installed.waypoints[2].position+" refugio="+escape+" jugador="+target.name);
    }
}
#endif
