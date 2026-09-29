using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class MedusaAnimationSetup
{
    const string Request = "Library/MedusaAnimation.request";
    const string Report = "Library/MedusaAnimation.result";
    const string ClipPath = "Assets/_Project/Prefabs/MedusaSwim.anim";
    const string ControllerPath = "Assets/_Project/Prefabs/MedusaSwim.controller";
    static MedusaAnimationSetup() { EditorApplication.update += ProcessRequest; }

    static void ProcessRequest()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Configure(); }
        catch (Exception e) { File.WriteAllText(Report, "FAIL: " + e); Debug.LogException(e); }
    }

    [MenuItem("Anadromo/Medusa/Configurar animacion en bucle")]
    public static void Configure()
    {
        var source = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Models/simple_jellyfish.glb")
            .OfType<AnimationClip>().FirstOrDefault(c => c.name == "StandardMoving");
        if (!source) throw new InvalidOperationException("Falta el clip StandardMoving de la medusa.");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (!clip)
        {
            clip = UnityEngine.Object.Instantiate(source);
            AssetDatabase.CreateAsset(clip, ClipPath);
        }
        else EditorUtility.CopySerialized(source, clip);
        clip.name = "MedusaSwim";
        clip.legacy = false;
        clip.wrapMode = WrapMode.Loop;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Nado");
        if (!state) state = machine.AddState("Nado");
        state.motion = clip;
        state.speed = 1f;
        machine.defaultState = state;
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);

        const string prefabPath = "Assets/_Project/Prefabs/medusa.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                if (!string.IsNullOrEmpty(binding.path) && !prefab.transform.Find(binding.path))
                    throw new InvalidOperationException("Hueso de animacion no encontrado: " + binding.path);
            var animator = prefab.GetComponent<Animator>();
            if (!animator) animator = prefab.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        File.WriteAllText(Report, "PASS: StandardMoving copied with Loop Time; default Nado state; prefab Animator assigned; bone paths validated. Length: " + clip.length);
        Debug.Log("Medusa: animacion StandardMoving configurada en bucle.");
    }
}
