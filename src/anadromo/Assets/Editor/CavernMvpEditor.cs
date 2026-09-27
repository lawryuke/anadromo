#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Anadromo.CavernMVP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Creates the separate MVP once after import, without replacing or saving the user's open scene.
[InitializeOnLoad]
public static class CavernMvpEditor
{
    public const string ScenePath = "Assets/_Project/Scenes/Esc2_Enemigos_MVP.unity";
    const string MaterialFolder = "Assets/_Project/Art/Materials/CavernMVP";
    static CavernMvpEditor() { EditorApplication.delayCall += CreateIfMissing; }
    static void CreateIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ScenePath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += CreateIfMissing; return; }
        try { Create(); Validate(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    [MenuItem("Anadromo/Cueva MVP/Crear escena si falta")]
    public static void Create()
    {
        if (File.Exists(ScenePath)) { Debug.Log("La escena MVP ya existe: " + ScenePath); return; }
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var root = new GameObject("Anádromo · enemigos MVP");
            var world = root.AddComponent<CavernMvpWorld>(); world.Build();
            Directory.CreateDirectory(MaterialFolder); AssetDatabase.Refresh();
            world.stone = Persist(world.stone,"Stone"); world.floor = Persist(world.floor,"Floor");
            world.creature = Persist(world.creature,"Creature"); world.marker = Persist(world.marker,"Marker");
            EditorSceneManager.SaveScene(scene,ScenePath);
            Debug.Log("MVP creado: " + ScenePath + ". Abrir desde Anadromo > Cueva MVP > Abrir escena.");
        }
        finally
        {
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene,true);
        }
    }
    static Material Persist(Material material,string name)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing) { EditorUtility.CopySerialized(material,existing); return existing; }
        AssetDatabase.CreateAsset(material,path); return material;
    }
    [MenuItem("Anadromo/Cueva MVP/Abrir escena")]
    public static void Open()
    {
        Create();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
        EditorSceneManager.playModeStartScene = null;
    }

    [MenuItem("Anadromo/Cueva MVP/Validar mecánicas")]
    public static void Validate()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Ejecuta la validación fuera de Play Mode.");
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        int checks = 0;
        // Additive scenes share the default physics world. Keep the open level's
        // colliders out of these small fixtures, then restore their exact state.
        var existingColliders = new List<Collider>();
        foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            if (collider.enabled) existingColliders.Add(collider);
        Action<bool,string> check = (ok,message) => { if (!ok) throw new InvalidOperationException("Cueva MVP: " + message); checks++; };
        try
        {
            foreach (var collider in existingColliders) collider.enabled = false;
            Physics.SyncTransforms();
            SceneManager.SetActiveScene(scene);
            var cells = CavernMvpWorld.WalkableCells();
            var reached = new HashSet<Vector2Int>(); var queue = new Queue<Vector2Int>();
            queue.Enqueue(CavernMvpWorld.Rooms[0]); reached.Add(CavernMvpWorld.Rooms[0]);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
                    if (cells.Contains(p+d) && reached.Add(p+d)) queue.Enqueue(p+d);
            }
            check(reached.Count == cells.Count,"Todo el suelo es accesible");
            foreach (var room in CavernMvpWorld.Rooms) check(reached.Contains(room),"Sala accesible");
            check(CavernMvpWorld.Edges.GetLength(0)-CavernMvpWorld.Rooms.Length+1 == 3,"Tres circuitos en el grafo");
            foreach (int fps in new[]{30,60,120})
            {
                var objects = new List<GameObject>();
                Func<string,GameObject> make = label => { var go = new GameObject(label); objects.Add(go); return go; };
                try
                {
                    var p = make("Test player").AddComponent<CavernPlayer>(); p.gameObject.layer = 2;
                    p.view = make("Test camera").AddComponent<Camera>(); p.view.transform.SetParent(p.transform,false);
                    var fish = make("Test piranha").AddComponent<CavernPiranha>(); fish.target = p; fish.transform.position = Vector3.forward*4;
                    var ally = make("Test ally").AddComponent<CavernPiranha>(); ally.target = p; ally.transform.position = Vector3.forward*6;
                    fish.school = new[]{fish,ally}; fish.disturbanceDelay = 1;
                    fish.CheckStatus(.5f); check(fish.State == EnemyState.DISTURBED,"Piraña espera si no hay ruido");
                    fish.CheckStatus(.6f); check(fish.State == EnemyState.ATTACK && ally.State == EnemyState.ATTACK,"Ataque contagia el cardumen");
                    fish.transform.position = Vector3.forward*40; fish.CheckStatus(3.1f); check(fish.State == EnemyState.CHILL,"Piraña pierde interés");
                    fish.transform.position = Vector3.forward*4; fish.noiseThreshold = 0; fish.CheckStatus(.001f); check(fish.State == EnemyState.ATTACK,"Ruido dispara ataque inmediato");
                    var angler = make("Test angler").AddComponent<CavernAngler>(); angler.target = p; angler.transform.position = Vector3.forward*4;
                    angler.lure = angler.gameObject.AddComponent<Light>();
                    angler.disturbanceDelay = 1.8f;
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(wall); wall.transform.position = Vector3.forward*2;
                    Physics.SyncTransforms(); angler.CheckStatus(.1f); angler.Move(.1f);
                    check(angler.State == EnemyState.CHILL && angler.lure.enabled,"Pared impide detección; señuelo encendido en reposo");
                    UnityEngine.Object.DestroyImmediate(wall); Physics.SyncTransforms();
                    angler.CheckStatus(.1f); angler.Move(.1f);
                    check(angler.State == EnemyState.DISTURBED && !angler.lure.enabled,"Señuelo apagado avisa de la emboscada");
                    check(angler.transform.position == Vector3.forward*4,"Preparación inmóvil");
                    for (int frame = 0; frame < fps*2; frame++) angler.CheckStatus(1f/fps);
                    check(angler.State == EnemyState.ATTACK,"Mirar y permanecer quieto no cancelan la emboscada");
                    p.transform.position = Vector3.right*4;
                    for (int frame = 0; frame < fps; frame++) angler.Move(1f/fps);
                    check(Mathf.Abs(angler.transform.position.x)<.001f && angler.transform.position.z<0,"Embestida fija permite esquivar lateralmente");
                    angler.Move(.4f); angler.ExecuteAttack(.4f);
                    check(angler.State == EnemyState.CHILL && p.Health == 100,"Esquiva evita daño y ataque termina");
                    Vector3 stopped = angler.transform.position;
                    angler.CheckStatus(.5f); angler.Move(.5f);
                    check(angler.transform.position == stopped && !angler.lure.enabled,"Recuperación inmóvil y sin señuelo");
                    p.transform.position = Vector3.right*40;
                    angler.CheckStatus(2); angler.Move(.1f);
                    check(angler.lure.enabled,"Señuelo vuelve tras recuperación");
                    p.transform.position = Vector3.zero;
                    var lamprey = make("Test lamprey").AddComponent<CavernLamprey>(); lamprey.target = p;
                    var lamprey2 = make("Test lamprey 2").AddComponent<CavernLamprey>(); lamprey2.target = p;
                    lamprey.Attach(); lamprey2.Attach(); check(p.AttachedCount == 2 && Mathf.Abs(p.SpeedMultiplier-.7f)<.001f,"Dos lampreas restan 30% de velocidad");
                    for (int frame = 0; frame < fps*2+1; frame++) lamprey.ExecuteAttack(1f/fps);
                    check(Mathf.Abs(p.Health-95)<.001f,"Drena 5 de salud cada dos segundos a " + fps + " FPS");
                    p.Shake(35); p.Shake(35); p.Shake(35);
                    check(p.AttachedCount == 0 && Mathf.Approximately(p.SpeedMultiplier,1),"Sacudidas sueltan todas y restauran velocidad");
                    check(lamprey.GripState == CavernLamprey.LampreyState.STUNNED,"Lamprea queda aturdida");
                    lamprey.CheckStatus(3.1f); check(lamprey.GripState == CavernLamprey.LampreyState.CHILL && lamprey.Grip == 100,"Recupera agarre tras aturdimiento");
                    var shark = make("Test shark").AddComponent<CavernShark>(); shark.target = p;
                    shark.transform.position = Vector3.back*5; shark.waypoints = new[]{Vector3.zero,Vector3.forward*5};
                    shark.Move(1); check(p.Health == 0,"Tiburón letal incluso cruzando al jugador en un solo frame");
                    check(shark.WaypointIndex == 2 && shark.transform.position == Vector3.forward*5,"Ruta sin overshoot al cruzar varios waypoints");
                }
                finally { foreach (var go in objects) if (go) UnityEngine.Object.DestroyImmediate(go); }
            }
            string result = "PASS: Cueva MVP · " + checks + " comprobaciones. Grafo, emboscada sin luz propia/evasión/oclusión, cardumen, agarre/drenaje/sacudidas a 30/60/120 FPS y tiburón letal con barrido.";
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/CavernMvpValidation.txt",result); Debug.Log(result);
        }
        finally
        {
            foreach (var collider in existingColliders) if (collider) collider.enabled = true;
            Physics.SyncTransforms();
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene,true);
        }
    }
}
#endif
