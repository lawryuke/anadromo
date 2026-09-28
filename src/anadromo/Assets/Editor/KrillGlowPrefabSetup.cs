#if UNITY_EDITOR
using System;
using System.IO;
using Anadromo.Mechanics;
using Anadromo.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class KrillGlowPrefabSetup
{
    public const string PrefabPath = "Assets/_Project/Prefabs/KrilBrillante.prefab";
    const string TestKey = "KrillGlowPrefabCheck";
    static GameObject player, food;
    static PlayerFeeding feeding;
    static EnergySystem energy;
    static double next;
    static int stage;
    static KrillGlowPrefabSetup()
    {
        EditorApplication.update += Request;
        EditorApplication.playModeStateChanged += Mode;
    }
    static void Request()
    {
        const string path = "Library/KrillGlowPrefab.request";
        if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var action = File.ReadAllText(path).Trim(); File.Delete(path);
        try { if (action == "create") Create(); else if (action == "test") Test(); }
        catch (Exception e) { Report("FAIL: " + e); Debug.LogException(e); }
    }
    static void Report(string result) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/KrillGlowPrefab.txt", result); }
    [MenuItem("Anadromo/Kril/Crear prefab brillante")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) throw new Exception("El prefab ya existe; edítalo desde Project.");
        var shader = Shader.Find("Anadromo/Medusa Glow");
        if (!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Falta shader luminoso compatible.");
        var water = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Esc2-WaterDepthY.mat");
        var orange = MakeMaterial("Kril-NaranjaGlow", shader, new Color(1.5f, .32f, .045f, 1));
        var white = MakeMaterial("Kril-BlancoGlow", shader, new Color(2.4f, 2.15f, 1.7f, 1));
        var root = new GameObject("KrilBrillante");
        try
        {
            root.tag = "Food_PlayerOnly";
            var collider = root.AddComponent<SphereCollider>(); collider.isTrigger = true; collider.radius = .18f;
            root.AddComponent<Prey>().energyValue = 15;
            var krill = root.AddComponent<Krill>(); krill.roamRadius = .4f; krill.speed = .15f;
            var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform, false);
            MakeVisual(visual.transform, "Cuerpo_MVP", new Vector3(.12f, .08f, .24f), orange, water);
            MakeVisual(visual.transform, "Nucleo_MVP", new Vector3(.045f, .04f, .12f), white, water);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        Report("CREATED: " + PrefabPath + "; visual naranja/blanco, comida +15, trigger .18m, sin luz real ni revelado del terreno. Escena sin modificar.");
    }
    static Material MakeMaterial(string name, Shader shader, Color color)
    {
        string path = "Assets/_Project/Art/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material) return material;
        material = new Material(shader) { name = name };
        material.SetColor("_Color", color); material.SetVector("_Visibility", new Vector4(12, 18, 0, 0));
        AssetDatabase.CreateAsset(material, path); return material;
    }
    static void MakeVisual(Transform parent, string name, Vector3 scale, Material material, Material water)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere); obj.name = name;
        obj.transform.SetParent(parent, false); obj.transform.localScale = scale;
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        var renderer = obj.GetComponent<Renderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        var glow = obj.AddComponent<DistantLureGlow>(); glow.waterSettings = water; glow.emission = material.GetColor("_Color");
    }
    [MenuItem("Anadromo/Kril/Verificar prefab y consumo en Play")]
    public static void Test()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetString(TestKey + "Scene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/esc2.unity");
        SessionState.SetBool(TestKey, true); EditorApplication.isPlaying = true;
    }
    static void Mode(PlayModeStateChange mode)
    {
        if (!SessionState.GetBool(TestKey, false)) return;
        if (mode == PlayModeStateChange.EnteredPlayMode)
        { stage = 0; next = EditorApplication.timeSinceStartup + 1; EditorApplication.update += Tick; }
        if (mode == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; SessionState.SetBool(TestKey, false);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(TestKey + "Scene", ""));
        }
    }
    static void Check(bool value, string text) { if (!value) throw new Exception(text); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 1;
        try
        {
            if (stage == 0)
            {
                player = new GameObject("Kril test player"); player.transform.position = new Vector3(2000, -5, 2000);
                var rb = player.AddComponent<Rigidbody>(); rb.useGravity = false; rb.isKinematic = true;
                energy = player.AddComponent<EnergySystem>(); energy.enabled = false; energy.SetEnergy(50);
                player.AddComponent<PlayerEnergyController>(); feeding = player.AddComponent<PlayerFeeding>();
                food = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), player.transform.position + Vector3.right * 10, Quaternion.identity);
                Check(food.GetComponentsInChildren<Collider>().Length == 1, "Un solo collider, separado de Visual");
                Check(food.GetComponent<SphereCollider>().isTrigger && food.CompareTag("Food_PlayerOnly"), "Trigger y tag comestible");
                Check(food.GetComponentsInChildren<Light>().Length == 0 && food.GetComponentsInChildren<WaterRevealLight>().Length == 0, "No ilumina terreno");
                Check(food.GetComponentsInChildren<DistantLureGlow>().Length == 2, "Brillo independiente naranja y blanco");
                Check(food.transform.Find("Visual") && food.GetComponent<Prey>().energyValue == 15, "Visual reemplazable y energia configurada");
            }
            else if (stage == 1)
            {
                Check(food && feeding.TotalConsumed == 0 && energy.CurrentEnergy == 50, "No se consume a distancia");
                food.GetComponent<Krill>().isStatic = true; food.transform.position = player.transform.position;
                Physics.SyncTransforms();
            }
            else
            {
                Check(!food && feeding.TotalConsumed == 1 && Mathf.Abs(energy.CurrentEnergy - 65) < .001f, "Contacto real recupera 15 y consume exactamente una vez");
                Report("PASS: prefab cargado; trigger/tag correctos; naranja/blanco con visibilidad luminosa; visual separado; sin luz real; no consumo lejano; contacto fisico consume una vez y recupera 15 de energia (50 -> 65). Escena guardada sin cambios.");
                EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
            }
            stage++;
        }
        catch (Exception e) { Report("FAIL: " + e); EditorApplication.update -= Tick; EditorApplication.isPlaying = false; }
    }
}
#endif
