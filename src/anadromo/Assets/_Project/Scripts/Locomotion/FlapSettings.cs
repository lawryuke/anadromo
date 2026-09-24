using UnityEngine;

namespace Anadromo.Locomotion
{
    [CreateAssetMenu(fileName = "FlapSettings", menuName = "Anadromo/Flap Settings")]
    public class FlapSettings : ScriptableObject
    {
        [Header("Ciclos de aleteo relativos al cuerpo")]
        public ArmStrokeCycle.Parameters cycle = ArmStrokeCycle.Parameters.Default;

        [Header("Tracking")]
        [Range(0.1f, 0.9f)] public float minimumVisibility = 0.5f;
        [Tooltip("Interrupciones mayores reinician los ciclos sin generar impulso.")]
        [Range(0.1f, 1f)] public float trackingTimeout = 0.3f;
    }
}
