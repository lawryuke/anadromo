using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.XR.CoreUtils;
using Anadromo.Locomotion;
using Anadromo.Systems;
using Anadromo.Mechanics;

namespace Anadromo.Editor
{
    /// <summary>
    /// Configura automáticamente los componentes de Fase 4 (debug UI) y Fase 5 (pulido).
    /// 
    /// Menú: Anadromo > Setup Polish (Fase 4+5)
    /// 
    /// Acciones:
    ///   1. Agrega PoseBridgeDebugUI al PoseBridge (F4.2)
    ///   2. Crea un Volume global con Vignette y agrega ComfortVignette (F5.2)
    ///   3. Agrega FlapFeedback al XR Origin (F5.4)
    ///   4. Agrega LocomotionModeManager al XR Origin (F5.6)
    ///   5. Vincula todas las referencias automáticamente
    /// </summary>
    public static class SetupPhase5
    {
        [MenuItem("Anadromo/Setup Polish (Fase 4+5)")]
        public static void Setup()
        {
            // ─── Buscar dependencias existentes ───
            var xrOrigin = Object.FindAnyObjectByType<XROrigin>();
            if (xrOrigin == null)
            {
                EditorUtility.DisplayDialog("Error", "No se encontró XR Origin. Ejecuta Fase 1 primero.", "OK");
                return;
            }

            var flapDetector = Object.FindAnyObjectByType<FlapDetector>();
            var receiver = Object.FindAnyObjectByType<ExternalCameraReceiver>();
            var flapController = xrOrigin.GetComponent<FlapSwimController>();

            if (flapDetector == null || flapController == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "No se encontraron FlapDetector y/o FlapSwimController.\nEjecuta Fase 2 y Fase 3 primero.", "OK");
                return;
            }

            var rb = xrOrigin.GetComponent<Rigidbody>();
            Transform mainCamera = null;
            foreach (var cam in xrOrigin.GetComponentsInChildren<Camera>(true))
            {
                if (cam.CompareTag("MainCamera") || cam.gameObject.name.Contains("Main Camera"))
                {
                    mainCamera = cam.transform;
                    break;
                }
            }

            // ─── F4.2: Debug UI ───
            var poseBridgeGO = flapDetector.gameObject;
            var debugUI = poseBridgeGO.GetComponent<PoseBridgeDebugUI>();
            if (debugUI == null)
            {
                debugUI = Undo.AddComponent<PoseBridgeDebugUI>(poseBridgeGO);
            }
            // Wire references
            var debugSO = new SerializedObject(debugUI);
            debugSO.Update();
            SetProperty(debugSO, "receiver", receiver);
            SetProperty(debugSO, "flapDetector", flapDetector);
            debugSO.ApplyModifiedProperties();
            Debug.Log("[Anadromo] ✅ PoseBridgeDebugUI agregado (F3 para toggle).");

            // ─── F5.2: Comfort Vignette ───
            SetupComfortVignette(xrOrigin, rb, mainCamera);

            // ─── F5.4: Flap Feedback ───
            var feedback = xrOrigin.GetComponent<FlapFeedback>();
            if (feedback == null)
            {
                feedback = Undo.AddComponent<FlapFeedback>(xrOrigin.gameObject);
            }
            var feedbackSO = new SerializedObject(feedback);
            feedbackSO.Update();
            SetProperty(feedbackSO, "flapDetector", flapDetector);
            feedbackSO.ApplyModifiedProperties();
            Debug.Log("[Anadromo] ✅ FlapFeedback agregado (asigna AudioClips manualmente).");

            // ─── F5.6: Locomotion Mode Manager ───
            var modeManager = xrOrigin.GetComponent<LocomotionModeManager>();
            if (modeManager == null)
            {
                modeManager = Undo.AddComponent<LocomotionModeManager>(xrOrigin.gameObject);
            }
            var mmSO = new SerializedObject(modeManager);
            mmSO.Update();
            SetProperty(mmSO, "flapController", flapController);
            // Buscar DebugVuelo si existe en la escena
            var debugVuelo = Object.FindAnyObjectByType<DebugVuelo>();
            if (debugVuelo != null)
            {
                SetProperty(mmSO, "debugVuelo", debugVuelo);
                Debug.Log("[Anadromo] ✅ DebugVuelo vinculado al LocomotionModeManager.");
            }
            mmSO.ApplyModifiedProperties();
            Debug.Log("[Anadromo] ✅ LocomotionModeManager agregado (F1 para toggle VR/Debug).");

            // ─── Finalizar ───
            Selection.activeGameObject = xrOrigin.gameObject;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("══════════════════════════════════════════════════════════════");
            Debug.Log("✅ [Anadromo] Setup de Polish (Fase 4+5) COMPLETADO");
            Debug.Log("──────────────────────────────────────────────────────────────");
            Debug.Log("  Componentes agregados:");
            Debug.Log("    📊 PoseBridgeDebugUI     → PoseBridge    (F3 toggle)");
            Debug.Log("    🌑 ComfortVignette       → XR Origin     (auto)");
            Debug.Log("    🔊 FlapFeedback           → XR Origin     (asignar AudioClips)");
            Debug.Log("    🔄 LocomotionModeManager  → XR Origin     (F1 toggle)");
            Debug.Log("──────────────────────────────────────────────────────────────");
            Debug.Log("  Pasos manuales opcionales:");
            Debug.Log("    - Asignar AudioClips en FlapFeedback (sonidos de splash)");
            Debug.Log("    - Ajustar intensidad de la viñeta en ComfortVignette");
            Debug.Log("    - Ajustar umbrales en FlapSettings / SwimSettings");
            Debug.Log("══════════════════════════════════════════════════════════════");
        }

        private static void SetupComfortVignette(XROrigin xrOrigin, Rigidbody rb, Transform mainCamera)
        {
            // Buscar o crear Volume global
            Volume volume = Object.FindAnyObjectByType<Volume>();
            GameObject volumeGO;

            if (volume == null)
            {
                volumeGO = new GameObject("Comfort Volume (Global)");
                Undo.RegisterCreatedObjectUndo(volumeGO, "Create Comfort Volume");
                volume = volumeGO.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10;
            }
            else
            {
                volumeGO = volume.gameObject;
            }

            // Crear o reusar VolumeProfile
            if (volume.profile == null)
            {
                string profilePath = "Assets/_Project/ScriptableObjects/ComfortVolumeProfile.asset";

                // Verificar carpeta
                if (!AssetDatabase.IsValidFolder("Assets/_Project/ScriptableObjects"))
                {
                    AssetDatabase.CreateFolder("Assets/_Project", "ScriptableObjects");
                }

                var existingProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
                if (existingProfile != null)
                {
                    volume.profile = existingProfile;
                }
                else
                {
                    var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(profile, profilePath);
                    AssetDatabase.SaveAssets();
                    volume.profile = profile;
                }
            }

            // Agregar Vignette override si no existe
            if (!volume.profile.Has<Vignette>())
            {
                var vignette = volume.profile.Add<Vignette>(true);
                vignette.intensity.Override(0f); // Empieza en 0, el script la controla
                vignette.smoothness.Override(0.4f);
                vignette.rounded.Override(true);
                EditorUtility.SetDirty(volume.profile);
                AssetDatabase.SaveAssets();
            }

            // Agregar ComfortVignette script
            var comfortVignette = xrOrigin.GetComponent<ComfortVignette>();
            if (comfortVignette == null)
            {
                comfortVignette = Undo.AddComponent<ComfortVignette>(xrOrigin.gameObject);
            }

            var cvSO = new SerializedObject(comfortVignette);
            cvSO.Update();
            SetProperty(cvSO, "targetRigidbody", rb);
            SetProperty(cvSO, "volume", volume);
            cvSO.ApplyModifiedProperties();

            Debug.Log("[Anadromo] ✅ ComfortVignette + Volume con Vignette configurados.");
        }

        private static void SetProperty(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
            }
        }
    }
}
