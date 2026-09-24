#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Anadromo.Locomotion;
using Anadromo.AI;

namespace Anadromo.EditorScripts
{
    public class TestSceneBuilder
    {
        [MenuItem("Anadromo/Crear Escena Test Depredador")]
        public static void BuildTestScene()
        {
            // 1. Crear una nueva escena vacía
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 2. Crear Suelo
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(5, 1, 5); // 50x50 metros
            
            var renderer = ground.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Standard"));
            renderer.sharedMaterial.color = new Color(0.2f, 0.2f, 0.2f); // Gris oscuro

            // 3. Crear Jugador (Salmón)
            GameObject salmonAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/salmon.Fbx");
            GameObject player;
            if (salmonAsset != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(salmonAsset);
                player.name = "PlayerTest_Salmon";
            }
            else
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "PlayerTest_Fallback";
            }
            
            player.transform.position = new Vector3(0, 1, 0);
            
            // Añadir colisionador físico para que el depredador lo detecte
            if (player.GetComponent<Collider>() == null)
            {
                var col = player.AddComponent<BoxCollider>();
                col.size = new Vector3(1f, 1f, 3f); // Tamaño aproximado de pez
            }

            player.AddComponent<PlayerTestController>();
            
            // Ajustar Cámara Principal para Primera Persona (dentro del salmón)
            GameObject mainCam = Camera.main.gameObject;
            mainCam.transform.SetParent(player.transform);
            mainCam.transform.localPosition = new Vector3(0, 0.5f, 1.5f); // Posición aproximada en la cabeza del pez
            mainCam.transform.localRotation = Quaternion.identity;

            // 4. Crear Depredador (Orca)
            GameObject orcaAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/Orca/Orca.prefab");
            GameObject predator;
            if (orcaAsset != null)
            {
                predator = (GameObject)PrefabUtility.InstantiatePrefab(orcaAsset);
                predator.name = "Predator_Orca";
            }
            else
            {
                predator = GameObject.CreatePrimitive(PrimitiveType.Cube);
                predator.name = "Predator_Fallback";
            }
            
            predator.transform.position = new Vector3(10, 0.5f, 10);
            
            if (predator.GetComponent<Collider>() == null)
            {
                var pCol = predator.AddComponent<BoxCollider>();
                pCol.size = new Vector3(2f, 2f, 6f); // Tamaño aproximado de orca
                pCol.isTrigger = true; // Para detectar la colisión más fácil
            }

            var predatorRb = predator.GetComponent<Rigidbody>();
            if (predatorRb == null) predatorRb = predator.AddComponent<Rigidbody>();
            predatorRb.isKinematic = true; 
            
            var pController = predator.AddComponent<PredatorController>();
            pController.player = player.transform;

            // 5. Crear Canvas de Muerte
            GameObject canvasGo = new GameObject("DeathCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject textGo = new GameObject("DeathText");
            textGo.transform.SetParent(canvasGo.transform);
            Text text = textGo.AddComponent<Text>();
            text.text = "MORISTE";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 100;
            text.color = new Color(0.6f, 0, 0, 1);
            text.alignment = TextAnchor.MiddleCenter;
            
            RectTransform rectTransform = text.GetComponent<RectTransform>();
            rectTransform.localPosition = Vector3.zero;
            rectTransform.sizeDelta = new Vector2(600, 200);

            canvasGo.SetActive(false);
            pController.deathScreenUI = canvasGo;

            // Cargar y asignar el sonido
            AudioClip predatorClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/predator.wav");
            pController.predatorSound = predatorClip;

            // 6. Guardar Escena
            string scenePath = "Assets/_Project/Scenes/scene2test.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log($"Escena de prueba creada exitosamente en: {scenePath}");
        }
    }
}
#endif
