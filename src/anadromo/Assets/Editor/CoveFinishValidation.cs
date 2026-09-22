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

public static class CoveFinishValidation
{
    public static void RunIsolated()
    {
        if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').EndsWith("/Library/CoveReview/Assets"))
            throw new InvalidOperationException("Use only the isolated Library/CoveReview project.");
        foreach(string path in Directory.GetFiles("Assets","*.shadersubgraph",SearchOption.AllDirectories))
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/TerrainTestVisuales.unity");
        var roots=scene.GetRootGameObjects();
        foreach(var root in roots)
        {
            foreach(var surface in root.GetComponentsInChildren<ContinuousRockTunnelInterior>())surface.Rebuild();
            foreach(var infill in root.GetComponentsInChildren<TunnelGapInfill>())infill.Rebuild();
            foreach(var garden in root.GetComponentsInChildren<StartingReefGarden>())garden.Rebuild();
        }
        TerrainTunnelValidation.Validate();
        var reef=roots.SelectMany(r=>r.GetComponentsInChildren<StartingReefGarden>()).Single();
        var floor=reef.GetComponentsInChildren<MeshCollider>().Single(c=>c.name=="Cornisa irregular - roca y arena");
        var foundation=reef.GetComponentsInChildren<MeshCollider>().Single(c=>c.name=="Macizo rocoso continuo hasta el fondo");
        if(foundation.bounds.min.y > -15)throw new Exception("Foundation does not reach seabed level");
        for(float z=-4.4f;z<4.5f;z+=.35f)
        {
            Vector3 p=reef.transform.TransformPoint(new Vector3(-.43258f,2,z));
            if(!floor.Raycast(new Ray(p,Vector3.down),out var hit,3))throw new Exception("Hole in starting shelf");
            if(hit.point.y>5.16f)throw new Exception("Surface intersects spawn clearance");
        }
        var player=roots.Single(r=>r.name=="Person1");
        Vector3 axis=Vector3.forward*.238f;
        var overlaps=Physics.OverlapCapsule(player.transform.position-axis,player.transform.position+axis,.112f)
            .Where(c=>!c.transform.IsChildOf(player.transform)).ToArray();
        if(overlaps.Length>0)throw new Exception("Spawn intersects "+string.Join(", ",overlaps.Select(c=>c.name)));
        int count=reef.GetComponentsInChildren<Renderer>().Length;
        reef.Rebuild();
        if(count!=reef.GetComponentsInChildren<Renderer>().Length)throw new Exception("Duplicate geometry after rebuild");
        var pipeline=ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
        var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
        var settings=new SerializedObject(pipeline);
        settings.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue=renderer;
        settings.ApplyModifiedPropertiesWithoutUndo();
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.37f,.45f,.47f);
        foreach(var camera in roots.SelectMany(r=>r.GetComponentsInChildren<Camera>()))camera.enabled=false;
        var sun=roots.SelectMany(r=>r.GetComponentsInChildren<Light>()).First();
        sun.intensity=1.25f;sun.color=new Color(.93f,.98f,1);sun.shadows=LightShadows.Soft;
        sun.transform.rotation=Quaternion.Euler(42,-35,0);
        var view=new GameObject("Review camera").AddComponent<Camera>();
        view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.025f,.11f,.14f);
        view.fieldOfView=53;view.nearClipPlane=.03f;view.farClipPlane=160;
        Directory.CreateDirectory("Review");
        Capture(view,new Vector3(33,14,-18),new Vector3(10,-3,14),"cove-overview");
        Capture(view,new Vector3(29,9,-10),new Vector3(10,-3,4),"rock-supported-platform");
        Capture(view,new Vector3(12.245f,3.3f,20),new Vector3(12.245f,3.3f,27),"closed-cave");
        var materials=roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>()).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct();
        foreach(var material in materials)
            if(!material.shader || ShaderUtil.ShaderHasError(material.shader) || !material.shader.isSupported)
                throw new Exception("Invalid material: "+material.name);
        File.WriteAllText("Review/validation.txt","PASS: cave entrance and return route clear; rear wall sealed; spawn clear; continuous irregular shelf; rock foundation to seabed; deterministic rebuild; all materials compiled; three views rendered.");
        Debug.Log("PASS: Cove finish validated");
    }

    static void Capture(Camera camera,Vector3 position,Vector3 target,string name)
    {
        camera.transform.position=position;camera.transform.LookAt(target);
        var rt=new RenderTexture(1440,900,24);var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();File.WriteAllBytes("Review/"+name+".png",pixels.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
}
#endif
