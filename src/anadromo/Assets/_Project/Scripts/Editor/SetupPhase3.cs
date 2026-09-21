using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Anadromo.Systems;
using Anadromo.Locomotion;
using Unity.XR.CoreUtils;

namespace Anadromo.Editor
{
    public static class SetupPhase3
    {
        [MenuItem("Anadromo/Setup Flap Locomotion Scene (Fase 3)")]
        public static void Setup()
        {
            // 1. Load or Create SwimSettings
            string assetPath = "Assets/_Project/ScriptableObjects/SwimSettings.asset";
            SwimSettings settings = AssetDatabase.LoadAssetAtPath<SwimSettings>(assetPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/ScriptableObjects"))
                {
                    AssetDatabase.CreateFolder("Assets/_Project", "ScriptableObjects");
                }
                settings = ScriptableObject.CreateInstance<SwimSettings>();
                AssetDatabase.CreateAsset(settings, assetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Anadromo] Creado SwimSettings en " + assetPath);
            }

            // 2. Find XR Origin and FlapDetector
            var xrOrigin = Object.FindAnyObjectByType<XROrigin>();
            var flapDetector = Object.FindAnyObjectByType<FlapDetector>();

            if (xrOrigin == null)
            {
                Debug.LogError("[Anadromo] XR Origin no encontrado. Debes correr la Fase 1 primero.");
                return;
            }
            if (flapDetector == null)
            {
                Debug.LogError("[Anadromo] FlapDetector no encontrado. Debes correr la Fase 2 primero.");
                return;
            }

            // 3. Locate Head (Main Camera) and SalmonBody
            Transform mainCameraTransform = null;
            Transform salmonBodyTransform = null;
            foreach (Transform child in xrOrigin.GetComponentsInChildren<Transform>())
            {
                if (child.CompareTag("MainCamera") || child.name.Contains("Main Camera"))
                {
                    mainCameraTransform = child;
                }
                if (child.name == "SalmonBody")
                {
                    salmonBodyTransform = child;
                }
            }

            if (mainCameraTransform == null || salmonBodyTransform == null)
            {
                Debug.LogError("[Anadromo] Faltan Main Camera o SalmonBody dentro del XR Origin. Corre 'Fase 1' nuevamente o créalos.");
                return;
            }

            // 4. Attach and wire FlapSwimController to XR Origin
            var controller = xrOrigin.GetComponent<FlapSwimController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<FlapSwimController>(xrOrigin.gameObject);
            }

            var serializedObject = new SerializedObject(controller);
            serializedObject.Update();
            
            var fdProp = serializedObject.FindProperty("flapDetector");
            if (fdProp != null) fdProp.objectReferenceValue = flapDetector;
            
            var settingsProp = serializedObject.FindProperty("settings");
            if (settingsProp != null) settingsProp.objectReferenceValue = settings;
            
            var headProp = serializedObject.FindProperty("headTransform");
            if (headProp != null) headProp.objectReferenceValue = mainCameraTransform;
            
            var bodyProp = serializedObject.FindProperty("salmonBody");
            if (bodyProp != null) bodyProp.objectReferenceValue = salmonBodyTransform;
            
            serializedObject.ApplyModifiedProperties();

            // Disable the old SwimLocomotion if it's there
            var oldController = xrOrigin.GetComponent<SwimLocomotion>();
            if (oldController != null)
            {
                Undo.RecordObject(oldController, "Disable SwimLocomotion");
                oldController.enabled = false;
                Debug.Log("[Anadromo] SwimLocomotion deshabilitado a favor del nuevo FlapSwimController.");
            }

            Selection.activeGameObject = xrOrigin.gameObject;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("══════════════════════════════════════════════════════════════");
            Debug.Log("✅ [Anadromo] Setup de escena (Fase 3) COMPLETADO");
            Debug.Log("──────────────────────────────────────────────────────────────");
            Debug.Log("  Elementos configurados:");
            Debug.Log($"    - Asset SwimSettings: {assetPath}");
            Debug.Log("    - FlapSwimController agregado a XR Origin.");
            Debug.Log("    - Referencias (Head, Body, FlapDetector) vinculadas correctamente.");
            Debug.Log("──────────────────────────────────────────────────────────────");
            Debug.Log("  Pasos manuales restantes:");
            Debug.Log("    1. Con el puente Python corriendo, dale Play a Unity y pruébalo.");
            Debug.Log("══════════════════════════════════════════════════════════════");
        }
    }
}
