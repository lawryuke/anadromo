using System.Collections.Generic;
using UnityEngine;

namespace Anadromo.Mechanics
{
    public enum InitialOrientationMode
    {
        Target,
        FixedView
    }

    [AddComponentMenu("Anadromo/Box Object Spawner")]
    public class BoxObjectSpawner : MonoBehaviour
    {
        [Header("Objeto y cantidad")]
        [Tooltip("Objeto de Hierarchy o prefab. Puede estar desactivado; las copias se activan.")]
        public GameObject sourceObject;
        [Min(0)] public int count = 20;
        public bool generateOnStart = true;

        [Header("Orientación inicial")]
        public InitialOrientationMode orientationMode = InitialOrientationMode.Target;
        [Tooltip("Al generar, cada copia apunta a este objeto conservando la corrección del modelo. Aplica si el modo es Target.")]
        public Transform targetObject;
        [Tooltip("Ángulos fijos (X,Y,Z) que tendrán todas las copias. Aplica si el modo es FixedView.")]
        public Vector3 fixedViewAngles = new Vector3(-90f, 180f, 0f);

        [Header("Orientación del modelo (compartida con el nado)")]
        [Tooltip("Conservar la inclinación y el giro lateral del Source Object, quitando solo su rumbo Y. Por ejemplo, X=-90 en un modelo importado vertical.")]
        public bool useSourceModelRotation = true;
        [Tooltip("Corrección del modelo respecto a un rumbo +Z con arriba +Y. Se usa si Use Source Model Rotation está desactivado. Regenera tras cambiarla.")]
        public Vector3 modelRotationOffset;

        public Quaternion ModelRotationCorrection
        {
            get
            {
                if (!useSourceModelRotation || sourceObject == null)
                    return Quaternion.Euler(modelRotationOffset);
                Quaternion sourceRotation = sourceObject.transform.rotation;
                return Quaternion.Inverse(Quaternion.Euler(0f, sourceRotation.eulerAngles.y, 0f)) * sourceRotation;
            }
        }

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
        [Tooltip("Incluir triggers de la escena como obstáculos. Los colliders de las copias, incluidos triggers, siempre definen su volumen.")]
        public bool includeTriggers;
        [Min(1)] public int attemptsPerObject = 100;
        [SerializeField, HideInInspector] private List<GameObject> generated = new List<GameObject>();

        [SerializeField, HideInInspector] private string lastGenerationMessage;
        public string LastGenerationMessage => lastGenerationMessage;
        public IReadOnlyList<GameObject> GeneratedObjects => generated;

        public Quaternion GetInitialRotation(Vector3 position, Quaternion fallback)
        {
            Vector3 targetPosition;
            if (targetObject != null) targetPosition = targetObject.position;
            else
            {
                SwimGroupController controller = GetComponent<SwimGroupController>();
                if (controller == null || !controller.isActiveAndEnabled ||
                    !controller.TryGetSharkInitialTarget(out targetPosition)) return fallback;
            }
            Vector3 direction = targetPosition - position;
            if (direction.sqrMagnitude < 0.00000001f) return fallback;
            Vector3 forward = direction.normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(forward, up) * ModelRotationCorrection;
        }

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
            Quaternion baseRotation = Quaternion.Euler(0f, sourceObject.transform.eulerAngles.y, 0f) * ModelRotationCorrection;
            var placedColliders = new List<Collider>();
            var placedVisualBounds = new List<Bounds>();
            int outsideAttempts = 0;
            int overlapAttempts = 0;
            lastGenerationMessage = string.Empty;
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
                bool hasColliders = System.Array.Exists(colliders, IsUsable);
                if (avoidOverlaps && !hasColliders && !System.Array.Exists(renderers, r => r.enabled))
                {
                    RemoveObject(candidate, false);
                    lastGenerationMessage = "No se puede comprobar el volumen: el objeto no tiene colliders ni renderers habilitados. Añade uno o desactiva Avoid Overlaps.";
                    Debug.LogWarning(lastGenerationMessage, this);
                    return;
                }

                bool accepted = false;
                for (int attempt = 0; attempt < Mathf.Max(1, attemptsPerObject); attempt++)
                {
                    Vector3 localPoint = center + Vector3.Scale(size, new Vector3(
                        (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f,
                        (float)random.NextDouble() - 0.5f));
                    Vector3 position = transform.TransformPoint(localPoint);
                    Quaternion rotation;
                    if (orientationMode == InitialOrientationMode.FixedView)
                    {
                        rotation = Quaternion.Euler(fixedViewAngles);
                    }
                    else
                    {
                        rotation = randomYaw
                            ? Quaternion.AngleAxis((float)random.NextDouble() * 360f, transform.up) * baseRotation
                            : baseRotation;
                        rotation = GetInitialRotation(position, rotation);
                    }
                    // Validate containment and collisions with the final initial orientation.
                    candidate.transform.SetPositionAndRotation(position, rotation);
                    Physics.SyncTransforms();
                    if (containWholeObject && !FitsInside(colliders, renderers))
                    {
                        outsideAttempts++;
                        continue;
                    }
                    if (avoidOverlaps && HasOverlap(candidate.transform, colliders, renderers, placedColliders, placedVisualBounds))
                    {
                        overlapAttempts++;
                        continue;
                    }
                    accepted = true;
                    break;
                }
                // Fallback: grid-based systematic search when random attempts fail
                if (!accepted)
                {
                    int gridRes = Mathf.Max(3, Mathf.CeilToInt(Mathf.Pow(count * 3, 1f / 3f)));
                    var gridCells = new List<Vector3Int>(gridRes * gridRes * gridRes);
                    for (int gx = 0; gx < gridRes; gx++)
                        for (int gy = 0; gy < gridRes; gy++)
                            for (int gz = 0; gz < gridRes; gz++)
                                gridCells.Add(new Vector3Int(gx, gy, gz));
                    // Shuffle grid cells for variety
                    for (int g = gridCells.Count - 1; g > 0; g--)
                    {
                        int k = random.Next(g + 1);
                        var tmp = gridCells[g]; gridCells[g] = gridCells[k]; gridCells[k] = tmp;
                    }
                    foreach (var cell in gridCells)
                    {
                        Vector3 localPoint = center + new Vector3(
                            ((cell.x + 0.5f) / gridRes - 0.5f) * size.x,
                            ((cell.y + 0.5f) / gridRes - 0.5f) * size.y,
                            ((cell.z + 0.5f) / gridRes - 0.5f) * size.z);
                        Vector3 position = transform.TransformPoint(localPoint);
                        Quaternion rotation;
                        if (orientationMode == InitialOrientationMode.FixedView)
                        {
                            rotation = Quaternion.Euler(fixedViewAngles);
                        }
                        else
                        {
                            rotation = randomYaw
                                ? Quaternion.AngleAxis((float)random.NextDouble() * 360f, transform.up) * baseRotation
                                : baseRotation;
                            rotation = GetInitialRotation(position, rotation);
                        }
                        candidate.transform.SetPositionAndRotation(position, rotation);
                        Physics.SyncTransforms();
                        if (containWholeObject && !FitsInside(colliders, renderers)) continue;
                        if (avoidOverlaps && HasOverlap(candidate.transform, colliders, renderers, placedColliders, placedVisualBounds)) continue;
                        accepted = true;
                        break;
                    }
                }
                // Third fallback: compact neighbors to free space
                if (!accepted && generated.Count > 1)
                {
                    for (int ni = 0; ni < generated.Count && !accepted; ni++)
                    {
                        GameObject neighbor = generated[ni];
                        if (neighbor == null) continue;
                        Vector3 originalNeighborPos = neighbor.transform.position;

                        // Find nearest sibling to nudge toward
                        float nearestDist = float.MaxValue;
                        Vector3 nearestSiblingPos = originalNeighborPos;
                        for (int nj = 0; nj < generated.Count; nj++)
                        {
                            if (ni == nj || generated[nj] == null) continue;
                            float d = Vector3.Distance(originalNeighborPos, generated[nj].transform.position);
                            if (d < nearestDist) { nearestDist = d; nearestSiblingPos = generated[nj].transform.position; }
                        }
                        if (nearestDist >= float.MaxValue) continue;

                        Vector3 nudgeDir = (nearestSiblingPos - originalNeighborPos).normalized;
                        Collider[] neighborColliders = neighbor.GetComponentsInChildren<Collider>();

                        // Build a placed list excluding this neighbor's own colliders
                        var placedWithoutNeighbor = new List<Collider>(placedColliders.Count);
                        foreach (var pc in placedColliders)
                        {
                            bool isSelf = false;
                            foreach (var nc in neighborColliders)
                                if (pc == nc) { isSelf = true; break; }
                            if (!isSelf) placedWithoutNeighbor.Add(pc);
                        }

                        for (float frac = 0.15f; frac <= 0.5f; frac += 0.1f)
                        {
                            Vector3 nudgedPos = originalNeighborPos + nudgeDir * (nearestDist * frac);
                            // Verify nudged pos is inside the box
                            Vector3 nudgedLocal = transform.InverseTransformPoint(nudgedPos) - center;
                            Vector3 hs = size * 0.5f;
                            if (Mathf.Abs(nudgedLocal.x) > hs.x || Mathf.Abs(nudgedLocal.y) > hs.y || Mathf.Abs(nudgedLocal.z) > hs.z) continue;

                            neighbor.transform.position = nudgedPos;
                            Physics.SyncTransforms();

                            // Check nudged neighbor doesn't now overlap with others
                            Renderer[] neighborRenderers = neighbor.GetComponentsInChildren<Renderer>();
                            if (avoidOverlaps && HasOverlap(neighbor.transform, neighborColliders, neighborRenderers, placedWithoutNeighbor, placedVisualBounds))
                            {
                                neighbor.transform.position = originalNeighborPos;
                                Physics.SyncTransforms();
                                continue;
                            }

                            // Try placing candidate at the freed position
                            Vector3 candidatePos = originalNeighborPos;
                            Quaternion rotation;
                            if (orientationMode == InitialOrientationMode.FixedView)
                            {
                                rotation = Quaternion.Euler(fixedViewAngles);
                            }
                            else
                            {
                                rotation = randomYaw
                                    ? Quaternion.AngleAxis((float)random.NextDouble() * 360f, transform.up) * baseRotation
                                    : baseRotation;
                                rotation = GetInitialRotation(candidatePos, rotation);
                            }
                            candidate.transform.SetPositionAndRotation(candidatePos, rotation);
                            Physics.SyncTransforms();

                            if (containWholeObject && !FitsInside(colliders, renderers))
                            {
                                neighbor.transform.position = originalNeighborPos;
                                Physics.SyncTransforms();
                                continue;
                            }
                            if (avoidOverlaps && HasOverlap(candidate.transform, colliders, renderers, placedColliders, placedVisualBounds))
                            {
                                neighbor.transform.position = originalNeighborPos;
                                Physics.SyncTransforms();
                                continue;
                            }
                            accepted = true;
                            break;
                        }
                        if (!accepted)
                        {
                            neighbor.transform.position = originalNeighborPos;
                            Physics.SyncTransforms();
                        }
                    }
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
                if (!hasColliders)
                    foreach (Renderer renderer in renderers)
                        if (renderer.enabled) placedVisualBounds.Add(renderer.bounds);
            }
            lastGenerationMessage = $"Generados {generated.Count}/{count}. Intentos fuera de la caja: {outsideAttempts}. Intentos con solapamiento: {overlapAttempts}.";
            if (generated.Count < count)
                Debug.LogWarning(lastGenerationMessage, this);
        }

        public void ClearGenerated()
        {
            foreach (GameObject instance in generated)
                if (instance != null) RemoveObject(instance);
            generated.Clear();
            lastGenerationMessage = string.Empty;
        }

        private bool IsUsable(Collider collider)
        {
            return collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
        }

        private bool HasOverlap(Transform candidate, Collider[] colliders, Renderer[] renderers,
            List<Collider> placed, List<Bounds> placedVisualBounds)
        {
            bool hasColliders = false;
            foreach (Collider collider in colliders)
            {
                if (!IsUsable(collider)) continue;
                hasColliders = true;
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
                foreach (Bounds other in placedVisualBounds)
                    if (bounds.Intersects(other)) return true;
            }
            if (!hasColliders)
            {
                // Visual bounds supply a conservative placement volume without adding
                // physical colliders to objects that intentionally do not have them.
                foreach (Renderer renderer in renderers)
                {
                    if (!renderer.enabled) continue;
                    Bounds bounds = renderer.bounds;
                    Collider[] obstacles = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity,
                        obstacleLayers, includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);
                    foreach (Collider other in obstacles)
                        if (!other.transform.IsChildOf(candidate)) return true;
                    foreach (Collider other in placed)
                        if (IsUsable(other) && bounds.Intersects(other.bounds)) return true;
                    foreach (Bounds other in placedVisualBounds)
                        if (bounds.Intersects(other)) return true;
                }
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
