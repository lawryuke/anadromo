#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Anadromo.AI;
using Anadromo.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Rendered-image regression, in Play only. The saved scene is never moved for testing.
[InitializeOnLoad]
public static class TerrainWaterVisibilityCheck
{
    const string Key = "TerrainWaterVisibilityCheck";
    static Camera camera;
    static RenderTexture output;
    static GameObject fixture, wall, blocker, glow;
    static WaterRevealLight reveal;
    static Color baseline, lit, distant, middle;
    static int stage;
    static double next, deadline;
    static string error;
    static TerrainWaterVisibilityCheck() { EditorApplication.playModeStateChanged += Mode; EditorApplication.update += Request; }
    static void Request() { if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists("Temp/terrain-water-check.request")) { File.Delete("Temp/terrain-water-check.request"); Run(); } }
    [MenuItem("Anadromo/Validate Terrain Water Light Reveal")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetString(Key + "Scene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/TerrainTestVisuales.unity");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0; error = null; next = EditorApplication.timeSinceStartup + 2; deadline = next + 90;
            Application.runInBackground = true; Application.logMessageReceived += Log;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Scene", ""));
            SessionState.SetBool(Key, false);
            if (output) { output.Release(); UnityEngine.Object.DestroyImmediate(output); }
        }
    }
    static void Log(string message, string trace, LogType type)
    { if (type == LogType.Error || type == LogType.Exception) error = message; }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static GameObject Shape(string name, PrimitiveType type, Vector3 p, Vector3 scale, Material material)
    {
        var obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.SetParent(fixture.transform, false);
        obj.transform.localPosition = p; obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }
    static Color Pixel(string label)
    {
        var previous = RenderTexture.active; RenderTexture.active = output;
        var image = new Texture2D(256, 256, TextureFormat.RGB24, false, true);
        image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply();
        Directory.CreateDirectory("Logs/TerrainWaterVisibility"); File.WriteAllBytes("Logs/TerrainWaterVisibility/" + label + ".png", image.EncodeToPNG());
        Color pixel = image.GetPixel(128, 128);
        UnityEngine.Object.DestroyImmediate(image); RenderTexture.active = previous;
        return pixel;
    }
    static float Difference(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r-b.r), Mathf.Abs(a.g-b.g), Mathf.Abs(a.b-b.b));
    static void Finish(string result)
    {
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/TerrainWaterVisibilityCheck.txt", result);
        EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (error != null) throw new Exception(error);
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout stage=" + stage);
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 1;
            if (stage == 0)
            {
                var water = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/TerrainTestVisuales-WaterDepthY.mat");
                Check(water.GetFloat("_VisibilityStart") == 8 && water.GetFloat("_VisibilityEnd") == 11, "Conserva visibilidad 4/7");
                Check(water.GetFloat("_LightVisibilityStart") == 20 && water.GetFloat("_LightVisibilityEnd") == 40, "Luces 12/18");
                Check(!ShaderUtil.ShaderHasError(water.shader), "Shader agua compilado");
                foreach (var fish in UnityEngine.Object.FindObjectsByType<Esc2Anglerfish>(FindObjectsSortMode.None))
                {
                    Check(fish.lure.GetComponent<DistantLureGlow>(), "Señuelo con visibilidad propia");
                    Check(!fish.lureLight || !fish.lureLight.enabled, "Señuelo no ilumina");
                    fish.ResetFish(); Check(!fish.lureLight || !fish.lureLight.enabled, "Reinicio no enciende luz real");
                }
                foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
                foreach (var energy in UnityEngine.Object.FindObjectsByType<Anadromo.Systems.EnergySystem>(FindObjectsSortMode.None)) energy.enabled = false;
                fixture = new GameObject("Visibility render test"); fixture.transform.position = new Vector3(1000, -5, 1000);
                var cameraObject = new GameObject("Visibility test camera"); cameraObject.transform.SetParent(fixture.transform, false);
                cameraObject.transform.localPosition = new Vector3(0, 0, -18);
                camera = cameraObject.AddComponent<Camera>(); camera.nearClipPlane = .1f; camera.farClipPlane = 80; camera.fieldOfView = 60;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.allowHDR = true;
                var additional = camera.GetUniversalAdditionalCameraData(); additional.SetRenderer(1); additional.renderPostProcessing = false;
                output = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBHalf); output.Create(); camera.targetTexture = output;
                // Lit supplies a ShadowCaster pass; emission keeps pixel comparisons independent of ambient light.
                var stone = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                stone.SetColor("_BaseColor", Color.black); stone.SetColor("_EmissionColor", Color.white * .8f); stone.EnableKeyword("_EMISSION");
                wall = Shape("Terrain beyond normal visibility", PrimitiveType.Quad, Vector3.zero, new Vector3(12, 12, 1), stone);
                wall.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
                var lightObject = new GameObject("Reveal light"); lightObject.transform.SetParent(fixture.transform, false);
                lightObject.transform.localPosition = new Vector3(-1, 0, -1);
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Point; light.range = 4; light.intensity = 5;
                light.shadows = LightShadows.Hard; light.shadowBias = .01f; light.shadowNormalBias = .01f;
                reveal = lightObject.AddComponent<WaterRevealLight>(); reveal.radius = 3; reveal.enabled = false;
                blocker = Shape("Shadow blocker", PrimitiveType.Cube, new Vector3(-.5f, 0, -.5f), new Vector3(.35f, .8f, .15f), stone);
                blocker.SetActive(false);
                var glowMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Esc2-LureGlow.mat");
                glow = Shape("Luminous ball", PrimitiveType.Sphere, new Vector3(0, 0, 0), Vector3.one * .5f, glowMaterial);
                var distantGlow = glow.AddComponent<DistantLureGlow>(); distantGlow.waterSettings = water; distantGlow.emission = new Color(1, 0, 0, 1);
                glow.SetActive(false);
            }
            else if (stage == 1) { baseline = Pixel("01-terrain-hidden"); reveal.enabled = true; }
            else if (stage == 2)
            {
                lit = Pixel("02-local-reveal"); Check(Difference(lit, baseline) > .08f, "Zona local visible: baseline=" + baseline + " lit=" + lit);
                blocker.SetActive(true);
            }
            else if (stage == 3)
            {
                var shadow = Pixel("03-shadow-occlusion"); Check(Difference(shadow, baseline) < .04f, "Pared bloquea revelado: " + shadow + " baseline=" + baseline);
                blocker.SetActive(false); reveal.enabled = false;
            }
            else if (stage == 4)
            {
                Check(Difference(Pixel("04-light-disabled"), baseline) < .02f, "Apagar elimina revelado sin datos residuales");
                wall.SetActive(false); glow.SetActive(true);
            }
            else if (stage == 5) { distant = Pixel("05-glow-10m"); glow.transform.localPosition = new Vector3(0, 0, 12); }
            else if (stage == 6) { middle = Pixel("06-glow-15m"); glow.transform.localPosition = new Vector3(0, 0, 23); }
            else if (stage == 7)
            {
                var far = Pixel("07-glow-19m");
                Check(distant.r > middle.r + .2f && middle.r > far.r + .2f, "Desvanecimiento luminoso independiente 10/15/19m: " + distant + " / " + middle + " / " + far);
                glow.transform.localPosition = Vector3.zero;
                blocker.transform.localPosition = new Vector3(0, 0, -2); blocker.transform.localScale = Vector3.one * 2; blocker.SetActive(true);
            }
            else if (stage == 8)
            {
                Check(Pixel("08-glow-wall-occlusion").r < distant.r - .4f, "Bola no atraviesa paredes");
                Finish("PASS: terrain renderer; visibility 8/11 and luminous 20/40 preserved; local surface revealed at 18m; shadows occlude reveal; disabling light restores fog; glow fades at 18/30/41m; walls occlude glow. Headset verification pending.");
            }
            stage++;
        }
        catch (Exception e) { Finish("FAIL stage=" + stage + ": " + e); }
    }
}
#endif
