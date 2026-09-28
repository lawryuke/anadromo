using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Anadromo.Locomotion;
using Anadromo.Systems;

public class MigrateTerrainTestVisuales 
{
    [MenuItem("Anadromo/Migrate TerrainTestVisuales")]
    public static void MigrateScene()
    {
        string scenePath = "Assets/_Project/Scenes/TerrainTestVisuales.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        
        GameObject[] rootObjects = scene.GetRootGameObjects();
        
        GameObject person1 = null;
        GameObject xrOrigin = null;
        GameObject poseBridge = null;

        foreach (var go in rootObjects)
        {
            if (go.name == "Person1") person1 = go;
            if (go.name == "XR Origin (VR)") xrOrigin = go;
            if (go.name == "PoseBridge") poseBridge = go;
        }

        if (person1 != null)
        {
            person1.SetActive(false);
            Debug.Log("Disabled Person1 (PC Player)");
        }

        if (xrOrigin != null)
        {
            xrOrigin.SetActive(true);
            Debug.Log("Enabled XR Origin (VR)");
        }

        if (poseBridge != null)
        {
            poseBridge.SetActive(true);
            var detector = poseBridge.GetComponent<FlapDetector>();
            if (detector != null && xrOrigin != null)
            {
                Transform head = null;
                foreach (var child in xrOrigin.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "Main Camera") head = child;
                }

                if (head == null)
                {
                    Debug.LogError("XR Origin no tiene cámara de visor.");
                    return;
                }

                var serialized = new SerializedObject(detector);
                serialized.FindProperty("trackingSource").enumValueIndex = 1;
                serialized.FindProperty("cameraReceiver").objectReferenceValue = null;
                serialized.FindProperty("headTransform").objectReferenceValue = head;
                serialized.FindProperty("xrOrigin").objectReferenceValue = xrOrigin.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var swim = xrOrigin != null ? xrOrigin.GetComponent<FlapSwimController>() : null;
            if (swim != null)
            {
                swim.enableJoystickMovement = false;
                swim.enableJoystickTurn = false;
            }

            var actionReceiver = poseBridge.GetComponent<PoseActionReceiver>();
            if (actionReceiver != null) Object.DestroyImmediate(actionReceiver);
            var debugUI = poseBridge.GetComponent<PoseBridgeDebugUI>();
            if (debugUI != null) Object.DestroyImmediate(debugUI);
            var cameraReceiver = poseBridge.GetComponent<ExternalCameraReceiver>();
            if (cameraReceiver != null) Object.DestroyImmediate(cameraReceiver);
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Scene saved successfully!");
    }
}
