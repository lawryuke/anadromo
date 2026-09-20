#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class NaturalSwimmingValidation
{
    [MenuItem("Anadromo/Validate Natural Swimming")]
    public static void Run()
    {
        var objects = new List<GameObject>();
        try
        {
            foreach (int fps in new[] { 30, 60, 120 })
            {
                var go = new GameObject("Swim validation"); objects.Add(go);
                var path = go.AddComponent<NaturalSwimPath>();
                path.Begin(new Vector3(0, 42, 0), 4);
                float previousSpeed = 0;
                for (int frame = 0; frame < fps * 30 && path.IsSwimming; frame++)
                {
                    path.Tick(1f / fps);
                    Check(path.Speed <= 4.001f, "Cruise speed is bounded");
                    Check(path.Speed <= previousSpeed + path.acceleration / fps + .001f, "Acceleration is bounded");
                    Check(Mathf.Abs(go.transform.position.x) <= path.courseWidth + .001f, "Course stays inside formation allowance");
                    previousSpeed = path.Speed;
                }
                Check(!path.IsSwimming && Vector3.Distance(go.transform.position, new Vector3(0, 42, 0)) < .001f, "Exact arrival at " + fps + " FPS");
                path.Begin(go.transform.position, 4); path.Tick(1f / fps);
                Check(!path.IsSwimming, "Zero-distance pass");
            }

            var bloop = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/bloopd.fbx"));
            objects.Add(bloop);
            var bones = bloop.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t);
            var initial = bones.ToDictionary(kv => kv.Key, kv => kv.Value.localRotation);
            var animation = bloop.AddComponent<BloopSwimAnimation>();
            animation.Initialize(); animation.Tick(1.25f);
            foreach (string name in new[] { "w_rug2", "w_L_vin1", "w_R_vin1", "w_L_staart2", "w_R_staart2" })
                Check(Quaternion.Angle(initial[name], bones[name].localRotation) > .01f, "Animated bone: " + name);
            Check(Quaternion.Angle(initial["w_hoofd1"], bones["w_hoofd1"].localRotation) < .001f, "Head pose preserved");
            animation.SendMessage("OnDisable");
            Check(bones.All(kv => Quaternion.Angle(initial[kv.Key], kv.Value.localRotation) < .01f), "Rest pose restored");

            var shark = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/WhiteShark.glb"));
            objects.Add(shark);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Art/Materials/WhiteSharkNaturalSwim.shader");
            Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Swim shader imported");
            var sharkAnimation = shark.AddComponent<SharkSwimAnimation>();
            sharkAnimation.Configure(shader); sharkAnimation.Tick(.5f);
            var renderer = shark.GetComponentInChildren<MeshRenderer>();
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            Check(block.GetFloat("_SwimStrength") > 0 && block.GetFloat("_SwimPhase") > 0, "GPU animation receives phase and strength");
            Check(shark.GetComponentInChildren<MeshFilter>().sharedMesh.colors.Length > 0, "Original vertex colours available");
            Debug.Log("PASS: natural swimming at 30/60/120 FPS, Bloop fins/spine/flukes, rest pose and shark GPU animation.");
        }
        finally { foreach (var go in objects) if (go) UnityEngine.Object.DestroyImmediate(go); }
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
