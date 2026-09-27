#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Anadromo.AI;

[InitializeOnLoad]
public static class Esc2PiranhaSetup
{
    // One-shot editor helpers preserve the active scene while generating saved encounters.
    static Esc2PiranhaSetup() { EditorApplication.delayCall += Request; }
    static void Request()
    {
        const string path = "Library/Esc2Piranha.request";
        if (!File.Exists(path) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action = File.ReadAllText(path).Trim(); File.Delete(path);
        if (action == "install") { Install(); Esc2PiranhaValidation.Run(); }
        if(action=="companions")
        {
            if(SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/esc2.unity") EditorSceneManager.OpenScene("Assets/_Project/Scenes/esc2.unity");
            AddCompanionsToScene(); Esc2PiranhaValidation.Run(); Esc2PiranhaPlayCheck.Run();
        }

    }
    [MenuItem("Anadromo/Esc2/Depredadores/Instalar pirañas en zona A")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/esc2.unity") throw new System.InvalidOperationException("Abre esc2 antes de instalar.");
        var existingSchool=Object.FindFirstObjectByType<PiranhaSchool>();
        if(existingSchool) { AddCompanions(existingSchool); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); return; }
        var controller = Object.FindFirstObjectByType<SimpleFlyCamera>();
        if (!controller) throw new System.InvalidOperationException("Falta el controlador de esc2.");
        Directory.CreateDirectory("Library/Esc2PiranhaBackup");
        EditorSceneManager.SaveScene(scene,"Library/Esc2PiranhaBackup/esc2-before-piranhas.unity",true);
        var target = controller.GetComponent<PiranhaPlayerTarget>();
        if (!target) target = Undo.AddComponent<PiranhaPlayerTarget>(controller.gameObject); target.desktopMovement = controller;
        var root = new GameObject("Zona A - Cardumen Piranas"); Undo.RegisterCreatedObjectUndo(root,"Instalar pirañas");
        root.transform.position = new Vector3(0,-.3f,1);
        var school = root.AddComponent<PiranhaSchool>(); school.target = target; school.zoneSize = new Vector3(6,2.6f,7);
        const string folder = "Assets/_Project/Prefabs/Enemies";
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var material = AssetDatabase.LoadAssetAtPath<Material>(folder+"/PiranhaEsc2.mat");
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = new Color(.2f,.75f,.4f);
            AssetDatabase.CreateAsset(material,folder+"/PiranhaEsc2.mat");
        }
        var template = new GameObject("Pirana Esc2");
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere); visual.name = "Cuerpo"; visual.transform.SetParent(template.transform,false);
        visual.transform.localScale = new Vector3(.4f,.3f,.75f); Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.GetComponent<Renderer>().sharedMaterial = material;
        var fish = template.AddComponent<Esc2Piranha>(); fish.body = visual.GetComponent<Renderer>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(template,folder+"/PiranaEsc2.prefab"); Object.DestroyImmediate(template);
        for (int i=0;i<2;i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform);
            instance.name = "Pirana_A_0"+(i+1);
            instance.transform.position = i==0 ? new Vector3(-1.3f,-.3f,1.2f) : new Vector3(1.2f,-.2f,2.2f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        }
        AddCompanions(school);
        Physics.SyncTransforms();
        foreach (var f in school.GetComponentsInChildren<Esc2Piranha>())
            foreach (var c in Physics.OverlapSphere(f.transform.position,.25f,school.obstacleLayers,QueryTriggerInteraction.Ignore))
                if (school.IsObstacle(c)) throw new System.InvalidOperationException("Spawn obstruido: "+f.name+" / "+c.name);
        EditorUtility.SetDirty(target); EditorUtility.SetDirty(school);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log("Dos pirañas instaladas y guardadas en zona A. Ctrl+D dentro del cardumen registra nuevas integrantes automáticamente.");
    }
    [MenuItem("Anadromo/Esc2/Pirañas/Instalar lamprea y pez linterna en zona A")]
    public static void AddCompanionsToScene()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/_Project/Scenes/esc2.unity") throw new System.InvalidOperationException("Abre esc2 antes de instalar.");
        var school=Object.FindFirstObjectByType<PiranhaSchool>();
        if(!school) throw new System.InvalidOperationException("Primero instala el cardumen de pirañas en zona A.");
        AddCompanions(school); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
    static void AddCompanions(PiranhaSchool school)
    {
        var root=school.gameObject; const string folder="Assets/_Project/Prefabs/Enemies";
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"/PiranhaEsc2.mat");
        GameObject MakePrefab(string fileName,string objectName,Vector3 bodyScale,System.Action<GameObject> configure)
        {
            var go=new GameObject(objectName);
            var shape=GameObject.CreatePrimitive(PrimitiveType.Capsule); shape.name="Cuerpo"; shape.transform.SetParent(go.transform,false);
            shape.transform.localRotation=Quaternion.Euler(90,0,0); shape.transform.localScale=bodyScale;
            Object.DestroyImmediate(shape.GetComponent<Collider>()); shape.GetComponent<Renderer>().sharedMaterial=material;
            configure(go); var saved=PrefabUtility.SaveAsPrefabAsset(go,folder+"/"+fileName+".prefab"); Object.DestroyImmediate(go); return saved;
        }
        var anglerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/PezLinternaEsc2.prefab");
        if(!anglerPrefab) anglerPrefab=MakePrefab("PezLinternaEsc2","Pez Linterna Esc2",new Vector3(.65f,.48f,1.05f),go=>
        {
            var enemy=go.AddComponent<Esc2Anglerfish>(); enemy.body=go.GetComponentInChildren<Renderer>();
            var lure=GameObject.CreatePrimitive(PrimitiveType.Sphere); lure.name="Señuelo"; lure.transform.SetParent(go.transform,false);
            lure.transform.localPosition=new Vector3(0,.12f,-.85f); lure.transform.localScale=Vector3.one*.23f;
            Object.DestroyImmediate(lure.GetComponent<Collider>()); lure.GetComponent<Renderer>().sharedMaterial=material;
            enemy.lure=lure.transform; enemy.lureLight=lure.AddComponent<Light>(); enemy.lureLight.type=LightType.Point; enemy.lureLight.color=new Color(.25f,1,.7f); enemy.lureLight.range=2.5f; enemy.lureLight.intensity=.8f;
        });
        var lampreyPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/LampreaEsc2.prefab");
        if(!lampreyPrefab) lampreyPrefab=MakePrefab("LampreaEsc2","Lamprea Esc2",new Vector3(.22f,.2f,.85f),go=>
        { var enemy=go.AddComponent<Esc2Lamprey>(); enemy.body=go.GetComponentInChildren<Renderer>(); });
        if(!root.GetComponentInChildren<Esc2Anglerfish>())
        { var fish=(GameObject)PrefabUtility.InstantiatePrefab(anglerPrefab,root.transform); fish.name="Pez_Linterna_A_01"; fish.transform.SetPositionAndRotation(new Vector3(.1f,-.35f,2.65f),Quaternion.identity); }
        var spawn=new[]{new Vector3(-2,-.35f,3.05f),new Vector3(2,-.35f,3.2f)}; var existing=root.GetComponentsInChildren<Esc2Lamprey>();
        for(int i=existing.Length;i<spawn.Length;i++)
        { var parasite=(GameObject)PrefabUtility.InstantiatePrefab(lampreyPrefab,root.transform); parasite.name="Lamprea_A_0"+(i+1); parasite.transform.position=spawn[i]; }
        EditorUtility.SetDirty(school); EditorUtility.SetDirty(school.target);
        Debug.Log("Lamprea y pez linterna añadidos a la zona A.");
    }
}
#endif
