using UnityEngine;
using UnityEditor;
using Anadromo.Locomotion;

namespace Anadromo.Editor
{
    public static class ApplyTuningFix
    {
        [MenuItem("Anadromo/Fix Locomotion Tuning (Ruido y Velocidad)")]
        public static void FixTuning()
        {
            string flapPath = "Assets/_Project/ScriptableObjects/FlapSettings.asset";
            string swimPath = "Assets/_Project/ScriptableObjects/SwimSettings.asset";

            var flapSettings = AssetDatabase.LoadAssetAtPath<FlapSettings>(flapPath);
            if (flapSettings != null)
            {
                Undo.RecordObject(flapSettings, "Fix FlapSettings");
                flapSettings.flapVelocityThreshold = 0.4f;
                flapSettings.flapCooldown = 0.3f;
                flapSettings.intensityMultiplier = 1.2f;
                flapSettings.smoothingFactor = 0.85f;
                flapSettings.simultaneityWindow = 0.2f;
                EditorUtility.SetDirty(flapSettings);
                Debug.Log("[Anadromo] FlapSettings ajustado para ignorar ruido.");
            }

            var swimSettings = AssetDatabase.LoadAssetAtPath<SwimSettings>(swimPath);
            if (swimSettings != null)
            {
                Undo.RecordObject(swimSettings, "Fix SwimSettings");
                swimSettings.forwardForceMultiplier = 6f;
                swimSettings.rotationTorqueMultiplier = 4f;
                swimSettings.forwardOnSingleFlapRatio = 0.1f;
                swimSettings.maxLinearVelocity = 6f;
                swimSettings.maxAngularVelocity = 3f;
                EditorUtility.SetDirty(swimSettings);
                Debug.Log("[Anadromo] SwimSettings ajustado para evitar teletransporte.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("✅ [Anadromo] Tuning aplicado correctamente. Dale Play para probar de nuevo.");
        }
    }
}
