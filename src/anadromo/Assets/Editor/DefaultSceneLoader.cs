#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Ensures that the main scene at Assets/_Project/Scenes/Act1_OpenOcean.unity
/// is always used as the Play Mode start scene after the editor reloads.
/// </summary>
[InitializeOnLoad]
public static class DefaultSceneLoader
{
    private const string MainScenePath = "Assets/_Project/Scenes/Act1_OpenOcean.unity";

    static DefaultSceneLoader()
    {
        // Wait until the asset database has completed the domain reload/import.
        EditorApplication.delayCall += SetStartScene;
    }

    private static void SetStartScene()
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);

        if (sceneAsset == null)
        {
            return;
        }

        EditorSceneManager.playModeStartScene = null;
    }
}
#endif
