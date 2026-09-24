#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

[InitializeOnLoad]
public static class DiegeticIntroValidation
{
    static DiegeticIntroValidation() { EditorApplication.delayCall += Run; }
    [MenuItem("Anadromo/Validate Diegetic Intro")]
    public static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        GameObject test = null;
        try
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/VFX/DebrisDiegeticIntro.vfx");
            test = new GameObject("Intro VFX validation") { hideFlags = HideFlags.HideAndDontSave };
            var effect = test.AddComponent<VisualEffect>();
            effect.visualEffectAsset = asset;
            if (!asset || !effect.HasVector3("IntroVelocity")) throw new Exception("VFX IntroVelocity not imported");
            effect.SetVector3("IntroVelocity", Vector3.back * 1.2f);
            if (effect.GetVector3("IntroVelocity") != Vector3.back * 1.2f) throw new Exception("Cannot set sprint velocity");
            effect.SetVector3("IntroVelocity", Vector3.zero);
            if (effect.GetVector3("IntroVelocity") != Vector3.zero) throw new Exception("Cannot stop debris velocity");
            File.WriteAllText("Temp/diegetic-intro-validation.txt", "PASS: VFX exposes IntroVelocity; -Z 1.2 m/s and stop round-trip correctly. Play-mode sequence still requires visual verification.");
        }
        catch (Exception e)
        {
            File.WriteAllText("Temp/diegetic-intro-validation.txt", "FAIL: " + e);
            Debug.LogException(e);
        }
        finally { if (test) UnityEngine.Object.DestroyImmediate(test); }
    }
}
#endif
