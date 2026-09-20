#if UNITY_EDITOR
using System;
using System.Linq;
using Anadromo.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TerrainTunnelValidation
{
    [MenuItem("Anadromo/Validate TerrainTestCero Tunnel")]
    public static void Validate()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/TerrainTestCero.unity" || Application.isPlaying)
            throw new InvalidOperationException("Abre TerrainTestCero fuera de Play Mode para validar el paso.");

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var surface in root.GetComponentsInChildren<ContinuousRockTunnelInterior>()) surface.Rebuild();
            foreach (var infill in root.GetComponentsInChildren<TunnelGapInfill>()) infill.Rebuild();
        }
        Physics.SyncTransforms();
        var player = scene.GetRootGameObjects().First(go => go.name == "Person1");
        var capsule = player.GetComponent<CapsuleCollider>();
        float radius = capsule.radius * Mathf.Max(player.transform.lossyScale.x, player.transform.lossyScale.y);
        float halfSegment = Mathf.Max(0, capsule.height * player.transform.lossyScale.z * .5f - radius);
        Vector3 axis = Vector3.forward * halfSegment;
        int passed = 0;
        foreach (float x in new[] { 12.05f, 12.245f, 12.44f })
        foreach (float y in new[] { 3.03f, 3.23f, 3.43f })
        foreach (int direction in new[] { 1, -1 })
        {
            Vector3 start = new Vector3(x, y, direction == 1 ? 19 : 30);
            var hits = Physics.CapsuleCastAll(start - axis, start + axis, radius,
                Vector3.forward * direction, 11, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.gameObject.scene == scene && !h.collider.transform.IsChildOf(player.transform))
                .ToArray();
            if (hits.Length > 0)
                throw new InvalidOperationException($"Paso bloqueado en {start}, sentido {direction}: " +
                    string.Join(", ", hits.Select(h => AnimationUtility.CalculateTransformPath(h.collider.transform, null))));
            passed++;
        }
        Debug.Log($"PASS: {passed} recorridos libres por el tunel, en ambos sentidos, con la capsula del jugador.");
    }
}
#endif
