using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.OpenXR;

namespace Anadromo.Editor
{
    /// <summary>Activa la fuente de articulaciones de OpenXR para builds Quest.</summary>
    [InitializeOnLoad]
    internal static class EnableQuestHandTracking
    {
        static EnableQuestHandTracking()
        {
            EditorApplication.delayCall += Configure;
        }

        private static void Configure()
        {
            EnableFor(BuildTargetGroup.Android);
            EnableFor(BuildTargetGroup.Standalone);
        }

        private static void EnableFor(BuildTargetGroup target)
        {
            FeatureHelpers.RefreshFeatures(target);
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(target);
            var feature = settings != null ? settings.GetFeature<HandTracking>() : null;
            if (feature == null)
            {
                Debug.LogWarning($"[Anadromo] Falta OpenXR Hand Tracking Subsystem para {target}.");
                return;
            }

            if (feature.enabled) return;
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Anadromo] OpenXR Hand Tracking Subsystem activado para {target}.");
        }
    }
}
