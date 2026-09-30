using UnityEngine;

namespace Anadromo.Mechanics
{
    [AddComponentMenu("Anadromo/Logic/Zone Limit")]
    public class ZoneLimit : MonoBehaviour
    {
        public Vector3 center = Vector3.zero;
        public Vector3 size = Vector3.one;

        /// <summary>
        /// Comprueba si un punto en el mundo está dentro de los límites de esta zona.
        /// </summary>
        public bool Contains(Vector3 worldPoint)
        {
            if (!enabled || !gameObject.activeInHierarchy) return false;
            Vector3 local = transform.InverseTransformPoint(worldPoint) - center;
            Vector3 half = size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        public Vector3 GetRandomPointInside()
        {
            Vector3 half = size * 0.5f;
            Vector3 randomLocal = new Vector3(
                Random.Range(-half.x, half.x),
                Random.Range(-half.y, half.y),
                Random.Range(-half.z, half.z)
            ) + center;
            return transform.TransformPoint(randomLocal);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
