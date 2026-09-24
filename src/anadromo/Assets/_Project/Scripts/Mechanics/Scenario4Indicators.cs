using UnityEngine;

namespace Anadromo.Mechanics
{
    /// <summary>World-space cues: depth tested, never drawn through cave walls.</summary>
    public static class Scenario4Indicators
    {
        public static LineRenderer Line(Transform parent, string name, Material material, float width, int points)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            var line = item.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.positionCount = points;
            line.numCapVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        public static void Ring(LineRenderer line, Vector3 center, float radius)
        {
            int count = line.positionCount;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / (count - 1);
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
            }
        }
    }
}
