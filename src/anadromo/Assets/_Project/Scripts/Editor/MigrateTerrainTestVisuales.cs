using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Anadromo.Locomotion;

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
            Debug.Log("Enabled PoseBridge");
            
            // Add PoseActionReceiver if not present
            var receiver = poseBridge.GetComponent<PoseActionReceiver>();
            if (receiver == null)
            {
                receiver = poseBridge.AddComponent<PoseActionReceiver>();
                receiver.udpPort = 5065;
                Debug.Log("Added PoseActionReceiver to PoseBridge");
            }
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Scene saved successfully!");
    }
}
