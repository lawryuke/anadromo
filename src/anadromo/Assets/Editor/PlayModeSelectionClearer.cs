using UnityEditor;
using UnityEngine;

namespace Anadromo.Editor
{
    [InitializeOnLoad]
    public static class PlayModeSelectionClearer
    {
        static PlayModeSelectionClearer()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private static void OnBeforeAssemblyReload()
        {
            Selection.activeObject = null;
            Selection.objects = new UnityEngine.Object[0];
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Unity's GameObjectInspector throws MissingReferenceException when entering Play Mode 
            // if a heavily modified or custom-editor object is selected, or if DOTS/Bakers destroy objects.
            Selection.activeObject = null;
            Selection.objects = new UnityEngine.Object[0];
            
            EditorApplication.delayCall += () => 
            {
                Selection.activeObject = null;
                Selection.objects = new UnityEngine.Object[0];
            };
        }
    }
}
