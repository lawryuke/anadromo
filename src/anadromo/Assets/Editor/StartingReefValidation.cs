#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Anadromo.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class StartingReefValidation
{
    // Also usable in a small isolated authoring project, without touching an open scene.
    public static void RunIsolated()
    {
        if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/Temp/ReefReview/Assets"))
            throw new InvalidOperationException("Run this capture only in the isolated Temp/ReefReview batch project.");
        foreach(string path in Directory.GetFiles("Assets", "*.shadersubgraph", SearchOption.AllDirectories))
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset("Assets/ShadersGeneric/StaticEntityShaderGraph.shadergraph", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
        var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
        var serialized = new SerializedObject(pipeline);
        serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        var garden = new GameObject("Starting shelf").AddComponent<StartingReefGarden>();
        garden.seabedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/StartingReefSeabed.mat");
        garden.vegetationMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/StartingReefAlgae.mat");
        garden.coralMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/StartingReefCoral.mat");
        garden.seaFanMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/StartingReefSeaFan.mat");
        garden.flatRock = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment3DAssets/Seabed Flat Rock A/Prefabs/Seabed Flat Rock A.prefab");
        garden.rockScatter = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment3DAssets/Seabed Rock Scatter A/Prefabs/Seabed Rock Scatter A.prefab");
        garden.redCoral = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment3DAssets/Red Coral/RedCoral.prefab");
        garden.seaFan = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment3DAssets/Gorgonian Coral/Gorgonian Coral.prefab");
        if(!garden.flatRock || !garden.rockScatter || !garden.redCoral || !garden.seaFan)
            throw new Exception("Missing reef source prefab");
        garden.Rebuild();
        int count = garden.GetComponentsInChildren<MeshRenderer>().Length;
        if(count < 40) throw new Exception("Missing garden geometry");
        var colliders = garden.GetComponentsInChildren<Collider>().Where(c=>c.enabled).ToArray();
        if(colliders.Length != 1) throw new Exception("Only the continuous floor should collide");
        Physics.SyncTransforms();
        for(float z=-4.5f;z<4.5f;z+=.5f)
        {
            if(!colliders[0].Raycast(new Ray(new Vector3(0,2,z),Vector3.down),out var hit,3))
                throw new Exception("Hole in swim lane");
            if(hit.point.y>.1f) throw new Exception("Floor too high under player");
        }
        var mesh = garden.GetComponentsInChildren<MeshFilter>().First().sharedMesh;
        var vertices = mesh.vertices;
        garden.Rebuild();
        if(!vertices.SequenceEqual(garden.GetComponentsInChildren<MeshFilter>().First().sharedMesh.vertices))
            throw new Exception("Rebuild must be deterministic");
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type=LightType.Directional; sun.intensity=1.2f;sun.color=new Color(.88f,.97f,1);
        sun.transform.rotation=Quaternion.Euler(48,-35,0);
        RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.4f,.53f,.56f);
        RenderSettings.fog=false;
        var camera=new GameObject("Reef review camera").AddComponent<Camera>();
        camera.transform.position=new Vector3(12,12,-16);
        camera.transform.LookAt(new Vector3(0,0,.4f));
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.035f,.18f,.21f);
        camera.fieldOfView=53;
        camera.nearClipPlane=.1f;camera.farClipPlane=100;
        var rt=new RenderTexture(1440,900,24);
        var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);
        camera.targetTexture=rt;
        camera.Render();
        var previous=RenderTexture.active;
        RenderTexture.active=rt;
        pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();
        Directory.CreateDirectory("Review");
        File.WriteAllBytes("Review/starting-reef.png",pixels.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;
        UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);
        foreach(var material in garden.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct())
            if(!material || !material.shader || ShaderUtil.ShaderHasError(material.shader) || !material.shader.isSupported)
                throw new Exception("Reef material failed: "+(material ? material.name : "missing"));
        File.WriteAllText("Review/validation.txt",$"PASS: {count} renderers; independent continuous floor; 18 swim-lane samples; deterministic rebuild; shader compiled; screenshot rendered.");
        Debug.Log("PASS: Starting reef validation");
    }
}
#endif
