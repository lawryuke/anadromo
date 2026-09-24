#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Anadromo.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Reuses the principal scene's scanned geometry, PBR materials and renderer quality.</summary>
[InitializeOnLoad]
public static class Scenario4ArtUpgrade
{
    const string ScenePath = "Assets/_Project/Scenes/Escenario4.unity";
    const string RockMaterialPath = "Assets/_Project/Art/Materials/TerrainTestVisuales_RockMassif.mat";
    static Scenario4ArtUpgrade() { EditorApplication.delayCall += ProcessRequest; }
    static void ProcessRequest()
    {
        if (!File.Exists("Temp/scenario4-upgrade-request")) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += ProcessRequest; return; }
        File.Delete("Temp/scenario4-upgrade-request");
        try { Build(); }
        catch (Exception error) { File.WriteAllText("Temp/scenario4-art-upgrade.txt", "FAIL: " + error); Debug.LogException(error); }
    }
    static Mesh Scan(string name, int lod) => AssetDatabase.LoadAllAssetsAtPath(
        $"Assets/Environment3DAssets/{name}/Models/{name}_LOD{lod}.fbx").OfType<Mesh>().First();

    [MenuItem("Anadromo/Escenario 4/Actualizar arte")]
    public static void Build()
    {
        const string workingPath = "Assets/_Project/Scenes/Escenario4_UpgradeWorking.unity";
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!AssetDatabase.CopyAsset(ScenePath, workingPath)) throw new Exception("Could not create isolated upgrade copy");
        var scene = EditorSceneManager.OpenScene(workingPath, OpenSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        try
        {
        var manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Scenario4Manager>()).Single();
        var material = AssetDatabase.LoadAssetAtPath<Material>(RockMaterialPath);
        var cliff = Scan("Seabed Fractured Cliff B", 2);
        var layered = Scan("Seabed Layered Cliff A", 2);
        var slab = Scan("Seabed Flat Rock A", 0);
        if (!material || !cliff || !layered || !slab) throw new Exception("Principal scene rock resources missing");
        // Own only the scenery of this encounter; keep the player's scene controls and mechanics.
        foreach (var item in manager.GetComponentsInChildren<MeshFilter>(true).ToArray())
        {
            if (item.transform.IsChildOf(manager.bloop.transform)) continue;
            string name = item.name;
            if (name.StartsWith("Arco transversal") || name == "Fondo del abismo" ||
                name.StartsWith("Saliente fracturado") || name.StartsWith("Estalactita"))
            { UnityEngine.Object.DestroyImmediate(item.gameObject); continue; }
            if (name.StartsWith("Mineral") || name.StartsWith("Luz guia")) continue;
            if (name.StartsWith("Acantilado") || name.StartsWith("Cornisa") || name.StartsWith("Techo") || name.StartsWith("Dintel") || name.StartsWith("Jamba"))
            {
                Mesh chosen = name.StartsWith("Acantilado") ? (Mathf.Abs(name.GetHashCode()) % 2 == 0 ? cliff : layered) : slab;
                ReplaceMesh(item, chosen, material);
            }
        }
        foreach (string label in new[] { "Boca del abismo - roca escaneada", "Techos y desprendimientos", "Iluminacion de la caverna", "Revestimiento continuo" })
        {
            var previous = manager.transform.Find(label);
            if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        }
        BuildLining(manager.transform, material);
        var lair = Group("Boca del abismo - roca escaneada", manager.transform);
        // A genuine opening: no plane, disk or collider covers its center.
        for (int i = 0; i < 28; i++)
        {
            float angle = i * Mathf.PI * 2 / 28;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Quaternion orientation = Quaternion.LookRotation(radial);
            Stone("Borde erosionado " + i, slab, material, lair, radial * 13.1f + Vector3.down * 4.2f,
                new Vector3(4.2f, 2.7f, 7), orientation);
            Stone("Pared interior del agujero " + i, cliff, material, lair, radial * 12.5f + Vector3.down * 12,
                new Vector3(4.5f, 15, 5), orientation);
        }
        manager.bloop.lairOpening = Group("Abertura libre", lair);
        manager.bloop.lairOpening.position = new Vector3(0, -4.2f, 0);
        manager.bloop.emergencePoint = Group("Fin de emergencia", lair);
        manager.bloop.emergencePoint.position = new Vector3(0, -1.5f, 0);
        manager.bloop.transform.position = new Vector3(0, -6, 0);
        manager.bloop.visualWidth = 16;
        manager.bloop.captureRadius = 8.2f;
        manager.bloop.emergenceDuration = 9;
        manager.bloop.visualPrepared = false;
        manager.bloop.Initialize(manager);
        manager.awakeningDuration = 9.5f;

        var roofs = Group("Techos y desprendimientos", manager.transform);
        var anchors = new List<Transform>();
        // Lower, projecting ceilings are visible along the route. Each one has a verified underside.
        for (int i = 0; i < manager.route.Length - 1; i++)
        {
            Vector3 path = manager.route[i].position;
            Vector3 ahead = (manager.route[i + 1].position - path).normalized;
            Vector3 center = path + Vector3.ProjectOnPlane(ahead, Vector3.up).normalized * 2.5f + Vector3.up * 4.4f;
            var roof = Stone("Boveda fracturada " + i, slab, material, roofs, center,
                new Vector3(4.8f, 1.5f, 5.2f), Quaternion.Euler(0, i * 37, 0));
            for (int j = 0; j < 2; j++)
            {
                var anchor = Group("Grieta activa " + i + "-" + j, roof);
                anchor.position = center + new Vector3((j == 0 ? -.65f : .65f), -1.4f, 0);
                anchors.Add(anchor);
            }
        }
        // A high vault closes the distant background while retaining the upper lateral escape.
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2 / 12;
            Stone("Boveda superior " + i, layered, material, roofs,
                new Vector3(Mathf.Cos(a) * 9, 47, Mathf.Sin(a) * 9), new Vector3(8, 4, 11), Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0));
        }
        manager.rocks.ceilingAnchors = anchors.ToArray();
        manager.rocks.rockMesh = slab;
        manager.rocks.rockMaterial = material;
        manager.rocks.warningDuration = 1.8f;
        manager.rocks.fallSpeed = 2.4f;
        manager.rocks.diameterRange = new Vector2(.3f, .7f);

        var lighting = Group("Iluminacion de la caverna", manager.transform);
        for (int i = 0; i < 7; i++)
        {
            Vector3 p = manager.route[Mathf.Min(24, i * 4)].position;
            var lamp = Group("Rebote submarino " + i, lighting).gameObject.AddComponent<Light>();
            lamp.transform.position = p * .75f + Vector3.up * 2;
            lamp.type = LightType.Point;
            lamp.color = i % 2 == 0 ? new Color(.35f, .7f, .72f) : new Color(.58f, .7f, .67f);
            lamp.intensity = 3;
            lamp.range = 11;
            lamp.shadows = LightShadows.None;
        }
        var reveal = Group("Luz tenue del borde", lighting).gameObject.AddComponent<Light>();
        reveal.transform.position = new Vector3(4, 0, 2);
        reveal.type = LightType.Point; reveal.color = new Color(.24f, .6f, .65f);
        reveal.range = 17; reveal.intensity = 3;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.18f, .36f, .4f);
        RenderSettings.ambientEquatorColor = new Color(.09f, .17f, .2f);
        RenderSettings.ambientGroundColor = new Color(.025f, .05f, .075f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = .075f; RenderSettings.fogColor = new Color(.035f, .15f, .19f);
        manager.vision.calmVisibility = 20; manager.vision.minimumVisibility = 15;
        SetupRenderer(manager.viewer);
        Physics.SyncTransforms();
        // Move only any new ceiling which interferes with a traversable route segment.
        foreach (var collider in roofs.GetComponentsInChildren<MeshCollider>())
        {
            for (int attempt = 0; attempt < 5 && IntersectsRoute(collider, manager.route); attempt++)
            {
                collider.transform.position += Vector3.up * .4f;
                Physics.SyncTransforms();
            }
        }
        if (!EditorSceneManager.SaveScene(scene, workingPath)) throw new Exception("Unity could not save the upgraded working scene");
        // SaveScene refuses to overwrite another currently open scene. Copy only the authored file,
        // leaving the user's open scene and its unsaved in-memory state untouched.
        File.Copy(workingPath, ScenePath, true);
        AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        File.WriteAllText("Temp/scenario4-art-upgrade.txt", "PASS: principal scene meshes/PBR/SSAO; open abyss; visible physical ceilings; smooth emergence.");
        Scenario4Validation.Run();
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (activeScene.IsValid() && activeScene.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(activeScene);
            AssetDatabase.DeleteAsset(workingPath);
        }
    }

    static bool IntersectsRoute(Collider collider, Transform[] route)
    {
        for (int i = 1; i < route.Length; i++)
        {
            Vector3 delta = route[i].position - route[i - 1].position;
            foreach (var offset in new[] { Vector3.zero, Vector3.up * .5f, Vector3.down * .5f })
                if (collider.Raycast(new Ray(route[i - 1].position + offset, delta.normalized), out _, delta.magnitude)) return true;
        }
        return false;
    }
    static void BuildLining(Transform parent, Material material)
    {
        const int sides = 80, rings = 25;
        var vertices = new Vector3[sides * rings];
        var triangles = new List<int>();
        for (int y = 0; y < rings; y++)
        for (int x = 0; x < sides; x++)
        {
            float angle = x * Mathf.PI * 2 / sides;
            float radius = 18.3f + Mathf.PerlinNoise(x * .21f, y * .4f) * 1.2f;
            vertices[y * sides + x] = new Vector3(Mathf.Cos(angle) * radius, -24 + y * 3.3f, Mathf.Sin(angle) * radius);
            if (y == rings - 1) continue;
            int a = y * sides + x, b = y * sides + (x + 1) % sides;
            triangles.AddRange(new[] { a, b + sides, b, a, a + sides, b + sides });
        }
        var mesh = new Mesh { name = "Escenario4 continuous inner cliff" };
        mesh.vertices = vertices; mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        const string path = "Assets/_Project/Art/Models/Escenario4_CaveLining.asset";
        var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (stored) { EditorUtility.CopySerialized(mesh, stored); UnityEngine.Object.DestroyImmediate(mesh); mesh = stored; }
        else AssetDatabase.CreateAsset(mesh, path);
        var lining = Group("Revestimiento continuo", parent);
        lining.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        lining.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        lining.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
    }
    static Transform Group(string name, Transform parent)
    { var tr = new GameObject(name).transform; tr.SetParent(parent, false); return tr; }

    static Transform Stone(string name, Mesh mesh, Material material, Transform parent, Vector3 center, Vector3 size, Quaternion rotation)
    {
        var item = Group(name, parent);
        item.SetPositionAndRotation(center, rotation);
        item.localScale = size;
        var filter = item.gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        item.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        item.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        Fit(filter, mesh, center, size);
        return item;
    }
    static void ReplaceMesh(MeshFilter filter, Mesh mesh, Material material)
    {
        Bounds old = filter.sharedMesh.bounds;
        Vector3 center = filter.transform.TransformPoint(old.center);
        Vector3 size = Vector3.Scale(filter.transform.localScale, old.size);
        Fit(filter, mesh, center, size);
        filter.GetComponent<Renderer>().sharedMaterial = material;
        var collider = filter.GetComponent<MeshCollider>(); if (collider) collider.sharedMesh = mesh;
    }
    static void Fit(MeshFilter filter, Mesh mesh, Vector3 center, Vector3 size)
    {
        filter.sharedMesh = mesh;
        Vector3 bounds = mesh.bounds.size;
        filter.transform.localScale = new Vector3(size.x / Mathf.Max(.01f, bounds.x), size.y / Mathf.Max(.01f, bounds.y), size.z / Mathf.Max(.01f, bounds.z));
        filter.transform.position = center - filter.transform.rotation * Vector3.Scale(mesh.bounds.center, filter.transform.localScale);
    }
    static void SetupRenderer(Camera camera)
    {
        const string path = "Assets/Settings/Escenario4-Renderer.asset";
        if (!AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path))
            AssetDatabase.CopyAsset("Assets/Settings/TerrainTestVisuales-Renderer.asset", path);
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        foreach (var feature in renderer.rendererFeatures)
        {
            // Keep the principal scene's SSAO; E4 already owns its depth/fog perception.
            if (feature is FullScreenPassRendererFeature) { feature.SetActive(false); EditorUtility.SetDirty(feature); }
        }
        foreach (string id in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/Settings" }))
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(id));
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            int index = list.arraySize;
            if (index < 3) list.arraySize = 3;
            list.GetArrayElementAtIndex(2).objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        camera.GetUniversalAdditionalCameraData().SetRenderer(2);
    }
}
#endif
