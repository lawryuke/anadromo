using System.Collections.Generic;
using UnityEngine;

namespace Anadromo.Mechanics
{
    [AddComponentMenu("Anadromo/Box Object Spawner")]
    public class BoxObjectSpawner : MonoBehaviour
    {
        [Header("Objeto y cantidad")]
        [Tooltip("Objeto de Hierarchy o prefab. Puede estar desactivado; las copias se activan.")]
        public GameObject sourceObject;
        [Min(0)] public int count = 20;
        public bool generateOnStart = true;

        [Header("Orientación inicial (opcional)")]
        [Tooltip("Al generar, el eje local +Z de cada copia apunta a este objeto. Tiene prioridad sobre Random Yaw; no hay seguimiento posterior.")]
        public Transform targetObject;

        [Header("Caja local (se mueve, rota y escala con este objeto)")]
        public Vector3 center;
        public Vector3 size = new Vector3(10f, 10f, 10f);
        [Tooltip("Exige que los bounds de colliders y renderers estén dentro de la caja, además del pivote.")]
        public bool containWholeObject = true;
        public bool randomYaw = true;
        public int seed = 12345;

        [Header("Evitar solapamientos al generar (opcional)")]
        public bool avoidOverlaps = true;
        [Tooltip("Capas de obstáculos de la escena. Las copias siempre se comprueban entre sí.")]
        public LayerMask obstacleLayers = ~0;
        public bool includeTriggers;
        [Min(1)] public int attemptsPerObject = 100;
        [SerializeField, HideInInspector] private List<GameObject> generated = new List<GameObject>();

        private void Start()
        {
            if (generateOnStart && generated.Count == 0) Generate();
        }

        public void Generate()
        {
            if (sourceObject == null || sourceObject.transform == transform ||
                transform.IsChildOf(sourceObject.transform) ||
                sourceObject.GetComponentInChildren<BoxObjectSpawner>(true) != null)
            {
                Debug.LogWarning("Asigna un objeto sin generadores y que no contenga esta caja.", this);
                return;
            }
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f ||
                Mathf.Abs(transform.localToWorldMatrix.determinant) < 0.000001f)
            {
                Debug.LogWarning("La caja necesita tamaño y escala distintos de cero.", this);
                return;
            }
            foreach (GameObject previous in generated)
            {
                if (previous != null && sourceObject.transform.IsChildOf(previous.transform))
                {
                    Debug.LogWarning("Usa el objeto original como fuente, no una copia generada.", this);
                    return;
                }
            }

            ClearGenerated();
            var random = new System.Random(seed);
            var placedColliders = new List<Collider>();
            Physics.SyncTransforms();
            for (int i = 0; i < Mathf.Max(0, count); i++)
            {
                // Keep the source's world scale, including when it has a scaled parent.
                GameObject candidate = Instantiate(sourceObject, sourceObject.transform.position,
                    sourceObject.transform.rotation, sourceObject.transform.parent);
                candidate.transform.SetParent(transform, true);
                candidate.name = sourceObject.name + " (Generated " + (i + 1) + ")";
                candidate.SetActive(true);
                Collider[] colliders = candidate.GetComponentsInChildren<Collider>();
                Renderer[] renderers = candidate.GetComponentsInChildren<Renderer>();
                if (avoidOverlaps && !System.Array.Exists(colliders, IsUsable))
                {
                    RemoveObject(candidate, false);
                    Debug.LogWarning("Evitar solapamientos requiere un Collider habilitado en el objeto o sus hijos.", this);
                    break;
                }

                bool accepted = false;
                for (int attempt = 0; attempt < Mathf.Max(1, attemptsPerObject); attempt++)
                {
                    Vector3 localPoint = center + Vector3.Scale(size, new Vector3(
                        (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f,
                        (float)random.NextDouble() - 0.5f));
                    Vector3 position = transform.TransformPoint(localPoint);
                    Quaternion rotation = randomYaw
                        ? Quaternion.AngleAxis((float)random.NextDouble() * 360f, transform.up) * sourceObject.transform.rotation
                        : sourceObject.transform.rotation;
                    if (targetObject != null)
                    {
                        Vector3 direction = targetObject.position - position;
                        if (direction.sqrMagnitude > 0.00000001f)
                        {
                            Vector3 forward = direction.normalized;
                            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f
                                ? Vector3.forward : Vector3.up;
                            rotation = Quaternion.LookRotation(forward, up);
                        }
                    }
                    // Validate containment and collisions with the final initial orientation.
                    candidate.transform.SetPositionAndRotation(position, rotation);
                    Physics.SyncTransforms();
                    if (containWholeObject && !FitsInside(colliders, renderers)) continue;
                    if (avoidOverlaps && HasOverlap(candidate.transform, colliders, placedColliders)) continue;
                    accepted = true;
                    break;
                }
                if (!accepted)
                {
                    RemoveObject(candidate, false);
                    continue;
                }
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(candidate, "Generar objetos");
#endif
                generated.Add(candidate);
                foreach (Collider collider in colliders)
                    if (IsUsable(collider)) placedColliders.Add(collider);
            }
            if (generated.Count < count)
                Debug.LogWarning($"Se generaron {generated.Count}/{count} objetos. Amplía la caja o aumenta los intentos.", this);
        }

        public void ClearGenerated()
        {
            foreach (GameObject instance in generated)
                if (instance != null) RemoveObject(instance);
            generated.Clear();
        }

        private bool IsUsable(Collider collider)
        {
            return collider != null && collider.enabled && collider.gameObject.activeInHierarchy &&
                (includeTriggers || !collider.isTrigger);
        }

        private bool HasOverlap(Transform candidate, Collider[] colliders, List<Collider> placed)
        {
            foreach (Collider collider in colliders)
            {
                if (!IsUsable(collider)) continue;
                Bounds bounds = collider.bounds;
                Collider[] obstacles = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity,
                    obstacleLayers, includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);
                foreach (Collider other in obstacles)
                {
                    if (other.transform.IsChildOf(candidate)) continue;
                    if (Intersects(collider, other)) return true;
                }
                foreach (Collider other in placed)
                    if (IsUsable(other) && Intersects(collider, other)) return true;
            }
            return false;
        }

        private static bool Intersects(Collider a, Collider b)
        {
            if (!a.bounds.Intersects(b.bounds)) return false;
            // Concave meshes may contain another collider without a surface intersection.
            // Use conservative bounds for them instead of silently accepting an overlap.
            if (!SupportsPenetration(a) || !SupportsPenetration(b)) return true;
            return Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                b, b.transform.position, b.transform.rotation, out _, out _);
        }

        private static bool SupportsPenetration(Collider collider)
        {
            return collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider ||
                (collider is MeshCollider mesh && mesh.convex);
        }

        private bool FitsInside(Collider[] colliders, Renderer[] renderers)
        {
            foreach (Collider collider in colliders)
                if (collider.enabled && !ContainsBounds(collider.bounds)) return false;
            foreach (Renderer renderer in renderers)
                if (renderer.enabled && !ContainsBounds(renderer.bounds)) return false;
            return true;
        }

        private bool ContainsBounds(Bounds bounds)
        {
            Vector3 halfSize = size * 0.5f;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));
                Vector3 local = transform.InverseTransformPoint(point) - center;
                if (Mathf.Abs(local.x) > halfSize.x || Mathf.Abs(local.y) > halfSize.y ||
                    Mathf.Abs(local.z) > halfSize.z) return false;
            }
            return true;
        }

        private static void RemoveObject(GameObject instance, bool recordUndo = true)
        {
            if (Application.isPlaying)
            {
                instance.SetActive(false);
                Destroy(instance);
            }
#if UNITY_EDITOR
            else if (recordUndo) UnityEditor.Undo.DestroyObjectImmediate(instance);
            else DestroyImmediate(instance);
#endif
        }

        private void OnDrawGizmosSelected()
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0f, 1f, 1f, 0.12f);
            Gizmos.DrawCube(center, size);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, size);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
