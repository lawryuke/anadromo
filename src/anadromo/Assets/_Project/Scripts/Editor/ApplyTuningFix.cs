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
                flapSettings.cycle = ArmStrokeCycle.Parameters.Default;
                flapSettings.minimumVisibility = 0.5f;
                flapSettings.trackingTimeout = 0.3f;
                EditorUtility.SetDirty(flapSettings);
                Debug.Log("[Anadromo] FlapSettings ajustado para ignorar ruido.");
            }

            var swimSettings = AssetDatabase.LoadAssetAtPath<SwimSettings>(swimPath);
            if (swimSettings != null)
            {
                Undo.RecordObject(swimSettings, "Fix SwimSettings");
                swimSettings.forwardForceMultiplier = 6f;
                swimSettings.headTurnDeadzone = 15f;
                swimSettings.headTurnFullSpeedAngle = 30f;
                swimSettings.headTurnMaxSpeed = 60f;
                swimSettings.yawLerpSpeed = 8f;
                swimSettings.simultaneityWindow = 0.12f;
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
