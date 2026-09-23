using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Anadromo.Systems;
using Anadromo.Locomotion;

namespace Anadromo.Editor
{
    public static class SetupPhase2
    {
        [MenuItem("Anadromo/Setup Flap Locomotion Scene (Fase 2)")]
        public static void Setup()
        {
            // 1. Create or load FlapSettings
            string assetPath = "Assets/_Project/ScriptableObjects/FlapSettings.asset";
            FlapSettings settings = AssetDatabase.LoadAssetAtPath<FlapSettings>(assetPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/ScriptableObjects"))
                {
                    AssetDatabase.CreateFolder("Assets/_Project", "ScriptableObjects");
                }
                settings = ScriptableObject.CreateInstance<FlapSettings>();
                AssetDatabase.CreateAsset(settings, assetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Anadromo] Creado FlapSettings en " + assetPath);
            }

            // 2. Find or Create PoseBridge GameObject
            GameObject poseBridgeGO = GameObject.Find("PoseBridge");
            if (poseBridgeGO == null)
            {
                poseBridgeGO = new GameObject("PoseBridge");
                Undo.RegisterCreatedObjectUndo(poseBridgeGO, "Create PoseBridge");
            }

            // 3. Ensure ExternalCameraReceiver is attached
            var receiver = poseBridgeGO.GetComponent<ExternalCameraReceiver>();
            if (receiver == null)
            {
                receiver = Undo.AddComponent<ExternalCameraReceiver>(poseBridgeGO);
            }

            // 4. Ensure FlapDetector is attached
            var detector = poseBridgeGO.GetComponent<FlapDetector>();
            if (detector == null)
            {
                detector = Undo.AddComponent<FlapDetector>(poseBridgeGO);
            }

            // 5. Link references
            var serializedObject = new SerializedObject(detector);
            serializedObject.Update();
            
            var cameraReceiverProp = serializedObject.FindProperty("cameraReceiver");
            if (cameraReceiverProp != null) cameraReceiverProp.objectReferenceValue = receiver;
            
            var settingsProp = serializedObject.FindProperty("settings");
            if (settingsProp != null) settingsProp.objectReferenceValue = settings;
            
            serializedObject.ApplyModifiedProperties();

            Selection.activeGameObject = poseBridgeGO;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("══════════════════════════════════════════════════════════════");
            Debug.Log("✅ [Anadromo] Setup de escena (Fase 2) COMPLETADO");
            Debug.Log("──────────────────────────────────────────────────────────────");
            Debug.Log("  Elementos configurados:");
            Debug.Log($"    - Asset FlapSettings: {assetPath}");
            Debug.Log("    - GameObject 'PoseBridge' en la escena con:");
            Debug.Log("      - ExternalCameraReceiver");
            Debug.Log("      - FlapDetector (vinculado a Receiver y Settings)");
            Debug.Log("──────────────────────────────────────────────────────────────");
            Debug.Log("  Pasos manuales restantes:");
            Debug.Log("    1. Ejecuta el script de Python: python src/pose_bridge/mediapipe_bridge.py");
            Debug.Log("    2. Dale Play a la escena en Unity.");
            Debug.Log("══════════════════════════════════════════════════════════════");
        }
    }
}
