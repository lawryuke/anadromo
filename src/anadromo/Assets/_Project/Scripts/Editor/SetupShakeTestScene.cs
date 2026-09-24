using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Anadromo.Systems;
using Anadromo.Locomotion;
using Anadromo.Mechanics;

namespace Anadromo.Editor
{
    public static class SetupShakeTestScene
    {
        [MenuItem("Anadromo/Build Scene2Shake")]
        public static void BuildScene()
        {
            string sourceScenePath = "Assets/_Project/Scenes/scene2test.unity";
            string targetScenePath = "Assets/_Project/Scenes/scene2shake.unity";

            if (!System.IO.File.Exists(sourceScenePath))
            {
                Debug.LogError("No se encontró scene2test.unity en " + sourceScenePath);
                return;
            }

            // Copiar la escena si no existe
            if (!System.IO.File.Exists(targetScenePath))
            {
                AssetDatabase.CopyAsset(sourceScenePath, targetScenePath);
                AssetDatabase.Refresh();
            }

            // Abrir la nueva escena
            Scene scene = EditorSceneManager.OpenScene(targetScenePath, OpenSceneMode.Single);

            // 0. Eliminar a las Orcas (si existen)
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (GameObject go in allObjects)
            {
                if (go.name.ToLower().Contains("orca"))
                {
                    Object.DestroyImmediate(go);
                }
            }

            // 1. Deshabilitar XR y configurar cámara de simulación en primera persona
            Camera mainCam = Camera.main;
            GameObject playerGO;
            if (mainCam != null)
            {
                var xrOrigin = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (xrOrigin != null)
                {
                    xrOrigin.gameObject.SetActive(false); // Desactivar origin XR
                }
                
                playerGO = new GameObject("SimulatedPlayerCamera");
                Camera simCam = playerGO.AddComponent<Camera>();
                simCam.tag = "MainCamera";
                playerGO.transform.position = new Vector3(0, 1.6f, 0);

                mainCam.gameObject.SetActive(false);
                mainCam = simCam;
            }
            else
            {
                playerGO = new GameObject("SimulatedPlayerCamera");
                mainCam = playerGO.AddComponent<Camera>();
                mainCam.tag = "MainCamera";
                playerGO.transform.position = new Vector3(0, 1.6f, 0);
            }

            // Configurar el controlador de primera persona para probar
            playerGO.tag = "Player";
            SimpleFlyCamera flyCam = playerGO.AddComponent<SimpleFlyCamera>();
            flyCam.movementSpeed = 8f;
            flyCam.fastMovementSpeed = 15f;
            
            SphereCollider playerCollider = playerGO.AddComponent<SphereCollider>();
            playerCollider.radius = 0.5f;
            Rigidbody playerRb = playerGO.AddComponent<Rigidbody>();
            playerRb.isKinematic = true;

            // 2. Asegurar ExternalCameraReceiver
            ExternalCameraReceiver receiver = Object.FindAnyObjectByType<ExternalCameraReceiver>();
            if (receiver == null)
            {
                GameObject receiverGO = new GameObject("ExternalCameraReceiver");
                receiver = receiverGO.AddComponent<ExternalCameraReceiver>();
            }

            // 3. Crear Shake Settings Asset si no existe
            ShakeSettings shakeSettings = AssetDatabase.LoadAssetAtPath<ShakeSettings>("Assets/_Project/Scripts/Locomotion/DefaultShakeSettings.asset");
            if (shakeSettings == null)
            {
                shakeSettings = ScriptableObject.CreateInstance<ShakeSettings>();
                AssetDatabase.CreateAsset(shakeSettings, "Assets/_Project/Scripts/Locomotion/DefaultShakeSettings.asset");
                AssetDatabase.SaveAssets();
            }

            // 4. Agregar ShakeDetector
            ShakeDetector detector = playerGO.AddComponent<ShakeDetector>();
            SerializedObject detectorObj = new SerializedObject(detector);
            detectorObj.FindProperty("cameraReceiver").objectReferenceValue = receiver;
            detectorObj.FindProperty("settings").objectReferenceValue = shakeSettings;
            detectorObj.ApplyModifiedProperties();

            // 5. Configurar el enemigo Lamprea
            GameObject lampreyGO = new GameObject("LampreyEnemy");
            lampreyGO.transform.position = new Vector3(0, 1.6f, 20f);
            
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/lamprey.glb");
            if (modelPrefab != null)
            {
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
                modelInstance.transform.SetParent(lampreyGO.transform, false);
            }
            else
            {
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallback.transform.SetParent(lampreyGO.transform, false);
                fallback.GetComponent<Renderer>().sharedMaterial.color = Color.red;
                Object.DestroyImmediate(fallback.GetComponent<Collider>());
            }

            BoxCollider trigger = lampreyGO.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2f, 2f, 2f);

            LampreyAttackManager attackManager = lampreyGO.AddComponent<LampreyAttackManager>();
            attackManager.shakeDetector = detector;
            attackManager.settings = shakeSettings;
            attackManager.playerCamera = mainCam.transform;
            attackManager.pursuitSpeed = 4f; // Más lento que el jugador (8f)
            attackManager.visionRange = 15f;

            // 6. Configurar UI de daño (Vignette)
            GameObject canvasGO = new GameObject("DamageCanvas");
            canvasGO.transform.SetParent(playerGO.transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            GameObject vignetteGO = new GameObject("RedVignette");
            vignetteGO.transform.SetParent(canvasGO.transform, false);
            UnityEngine.UI.Image vignetteImg = vignetteGO.AddComponent<UnityEngine.UI.Image>();
            vignetteImg.color = new Color(1f, 0f, 0f, 0f); // Rojo transparente
            vignetteImg.raycastTarget = false;
            
            RectTransform rect = vignetteImg.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            attackManager.damageVignette = vignetteImg;

            GameObject lampreyOrigin = new GameObject("LampreyOrigin");
            lampreyOrigin.transform.position = new Vector3(0, 1.6f, 20f);
            attackManager.originPoint = lampreyOrigin.transform;

            // Guardar Escena
            EditorSceneManager.SaveScene(scene);
            
            Debug.Log("[Anadromo] Escena scene2shake construida correctamente.");
        }
    }
}
