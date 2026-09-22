using System.Collections.Generic;
using UnityEngine;

namespace Anadromo.Environment
{
    /// <summary>Fills the existing rectangle around an existing curved bore.
    /// Reads the source mesh; never moves or replaces it or the surrounding surfaces.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class TunnelGapInfill : MonoBehaviour
    {
        public Mesh tunnelMesh;
        public Transform tunnelRoot;
        public Vector3 meshOffset = new Vector3(-4.15f, 0, 1.43f);
        public BoxCollider leftWall, rightWall, floor, ceiling;
        public Material rockMaterial;
        [Range(.01f, .1f)] public float overlap = .035f;
        [Range(3, 16)] public int lipSegments = 8;
        GameObject generated;
        Mesh generatedMesh, collisionMesh;
        bool pending;

        void OnEnable() => pending = true;
        void OnValidate() => pending = true;
        void Update()
        {
            if (!pending) return;
            pending = false;
            Rebuild();
        }

        [ContextMenu("Reconstruir relleno alrededor del tunel existente")]
        public void Rebuild()
        {
            Clear();
            if (!tunnelMesh || !tunnelRoot || !leftWall || !rightWall || !floor || !ceiling || !rockMaterial)
                return;
            if (!tunnelMesh.isReadable || tunnelMesh.subMeshCount < 2)
            { Debug.LogError("El relleno necesita el submesh interior legible del tunel original.", this); return; }

            Matrix4x4 sourceToWorld = tunnelRoot.localToWorldMatrix * Matrix4x4.Translate(meshOffset);
            Vector3[] source = tunnelMesh.vertices;
            var ids = new HashSet<int>(tunnelMesh.GetTriangles(1));
            float endZ = float.PositiveInfinity;
            foreach (int id in ids) endZ = Mathf.Min(endZ, source[id].z);
            var loop = new List<Vector2>();
            float frontOfBore = float.NegativeInfinity;
            float backOfBore = 0;
            foreach (int id in ids)
            {
                Vector3 p = sourceToWorld.MultiplyPoint3x4(source[id]);
                if (Mathf.Abs(source[id].z - endZ) > .001f)
                { frontOfBore = Mathf.Max(frontOfBore, p.z); continue; }
                backOfBore = p.z;
                Vector2 xy = new Vector2(p.x, p.y);
                if (!loop.Exists(q => (q - xy).sqrMagnitude < 1e-10f)) loop.Add(xy);
            }
            if (loop.Count < 8) { Debug.LogError("No se encontro el contorno del tunel.", this); return; }
            Vector2 min = loop[0], max = loop[0];
            foreach (Vector2 p in loop) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            Vector2 center = (min + max) * .5f;
            loop.Sort((a, b) => Mathf.Atan2(a.y - center.y, a.x - center.x)
                .CompareTo(Mathf.Atan2(b.y - center.y, b.x - center.x)));

            // These references describe the existing axis-aligned opening in TerrainTestVisuales.
            Vector2 boxMin = new Vector2(leftWall.bounds.max.x - overlap, floor.bounds.max.y - overlap);
            Vector2 boxMax = new Vector2(rightWall.bounds.min.x + overlap, ceiling.bounds.min.y + overlap);
            if (min.x <= boxMin.x || min.y <= boxMin.y || max.x >= boxMax.x || max.y >= boxMax.y)
            { Debug.LogError("La abertura curva no cabe dentro de los limites de relleno.", this); return; }
            float frontOuter = Mathf.Min(leftWall.bounds.min.z, rightWall.bounds.min.z,
                floor.bounds.min.z, ceiling.bounds.min.z) - overlap;
            float backOuter = Mathf.Max(leftWall.bounds.max.z, rightWall.bounds.max.z,
                floor.bounds.max.z, ceiling.bounds.max.z) - overlap;
            float frontInner = Mathf.Max(frontOfBore + .02f, frontOuter + .1f);
            if (frontInner >= backOuter || backOuter > backOfBore)
            { Debug.LogError("Profundidad del relleno incompatible con el tunel original.", this); return; }

            // Insert the four square-corner rays so the outer contour is a complete rectangle.
            // The inner points remain on the original curved polygon, not a new circle.
            foreach (Vector2 corner in new[] { boxMin, new Vector2(boxMax.x, boxMin.y),
                boxMax, new Vector2(boxMin.x, boxMax.y) })
            {
                Vector2 direction = (corner - center).normalized;
                for (int i = 0; i < loop.Count; i++)
                {
                    Vector2 a = loop[i] - center, edge = loop[(i + 1) % loop.Count] - loop[i];
                    float denom = Cross(direction, edge);
                    if (Mathf.Abs(denom) < 1e-7f) continue;
                    float ray = Cross(a, edge) / denom, segment = Cross(a, direction) / denom;
                    if (ray <= 0 || segment < 0 || segment > 1) continue;
                    Vector2 point = center + direction * ray;
                    if (!loop.Exists(q => (q - point).sqrMagnitude < 1e-10f)) loop.Insert(i + 1, point);
                    break;
                }
            }
            int count = loop.Count, steps = Mathf.Clamp(lipSegments, 3, 16);
            var outer = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 d = (loop[i] - center).normalized;
                float tx = Mathf.Abs(d.x) < 1e-7f ? float.PositiveInfinity :
                    ((d.x > 0 ? boxMax.x : boxMin.x) - center.x) / d.x;
                float ty = Mathf.Abs(d.y) < 1e-7f ? float.PositiveInfinity :
                    ((d.y > 0 ? boxMax.y : boxMin.y) - center.y) / d.y;
                outer[i] = center + d * Mathf.Min(tx, ty);
            }
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            // A continuous closed shell: front lip -> outer wall -> rear lip -> inner wall.
            // The inner skin is used for collision only: the original visible bore stays in place.
            for (int row = 0; row <= steps * 2 + 2; row++)
            {
                for (int i = 0; i <= count; i++)
                {
                    int j = i % count;
                    Vector2 inner = loop[j];
                    Vector2 xy; float z;
                    if (row <= steps)
                    {
                        float t = (float)row / steps;
                        float a = t * Mathf.PI * .5f;
                        xy = Vector2.Lerp(inner, outer[j], 1 - Mathf.Cos(a));
                        z = Mathf.Lerp(frontInner, frontOuter, Mathf.Sin(a));
                    }
                    else if (row <= steps * 2 + 1)
                    {
                        float t = (float)(row - steps - 1) / steps;
                        float a = t * Mathf.PI * .5f;
                        xy = Vector2.Lerp(outer[j], inner, Mathf.Sin(a));
                        z = Mathf.Lerp(backOuter, backOfBore, 1 - Mathf.Cos(a));
                    }
                    else { xy = inner; z = frontInner; }
                    Vector3 p = new Vector3(xy.x, xy.y, z);
                    vertices.Add(transform.InverseTransformPoint(p));
                    uv.Add(xy);
                    if (row == 0 || i == 0) continue;
                    // The vertex just appended closes the previous quad. Include the
                    // duplicated seam endpoint so every radial segment is connected.
                    int b = row * (count + 1) + i - 1, aIndex = b - count - 1;
                    triangles.Add(aIndex); triangles.Add(b); triangles.Add(aIndex + 1);
                    triangles.Add(aIndex + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }
            // Verify winding with the front annulus: its visible side must face the entrance (-Z).
            Vector3 faceNormal = Vector3.Cross(vertices[triangles[1]] - vertices[triangles[0]],
                vertices[triangles[2]] - vertices[triangles[0]]);
            if (transform.TransformDirection(faceNormal).z > 0)
                for (int i = 0; i < triangles.Count; i += 3)
                { int temp = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = temp; }
            generatedMesh = new Mesh { name = "Relleno_del_hueco_original", hideFlags = HideFlags.DontSave };
            generatedMesh.SetVertices(vertices); generatedMesh.SetUVs(0, uv); generatedMesh.SetTriangles(triangles, 0);
            generatedMesh.RecalculateNormals(); generatedMesh.RecalculateBounds();
            Vector3[] normals = generatedMesh.normals;
            int rows = vertices.Count / (count + 1);
            for (int row = 0; row < rows; row++)
            {
                int a = row * (count + 1), b = a + count;
                normals[a] = normals[b] = (normals[a] + normals[b]).normalized;
            }
            for (int i = 0; i <= count; i++)
            {
                int end = (rows - 1) * (count + 1) + i;
                normals[i] = normals[end] = (normals[i] + normals[end]).normalized;
            }
            generatedMesh.normals = normals;
            collisionMesh = Instantiate(generatedMesh);
            collisionMesh.name = "Relleno_colision_cerrada";
            collisionMesh.hideFlags = HideFlags.DontSave;
            // Omit the duplicate bore skin from rendering to prevent coplanar flickering.
            generatedMesh.SetTriangles(triangles.GetRange(0, triangles.Count - count * 6), 0);
            generated = new GameObject("Roca de union - relleno del hueco");
            generated.hideFlags = HideFlags.DontSave;
            generated.transform.SetParent(transform, false);
            generated.AddComponent<MeshFilter>().sharedMesh = generatedMesh;
            var renderer = generated.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = rockMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            generated.AddComponent<MeshCollider>().sharedMesh = collisionMesh;
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        void OnDisable() => Clear();
        void Clear()
        {
            if (generated) generated.SetActive(false);
            Dispose(generated); Dispose(generatedMesh); Dispose(collisionMesh);
            generated = null; generatedMesh = null; collisionMesh = null;
        }
        static void Dispose(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
