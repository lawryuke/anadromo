using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anadromo.Environment
{
    /// <summary>Rebuilds only submesh 1 of the existing cliff. Exterior triangles and all
    /// source mesh assets stay intact; the fragmented interior becomes one continuous skin.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter))]
    public sealed class ContinuousRockTunnelInterior : MonoBehaviour
    {
        public Mesh sourceMesh;
        [Range(16, 80)] public int lengthSegments = 40;
        [Range(0, .06f)] public float relief = .035f;
        Mesh preview;
        [SerializeField, HideInInspector] Mesh originalCollider;
        bool pending;
        void OnEnable() => pending = true;
        void OnValidate() => pending = true;
        void Update()
        {
            if (!pending) return;
            pending = false;
            Rebuild();
        }

        [ContextMenu("Reconstruir pared rocosa continua")]
        public void Rebuild()
        {
            Restore();
            if (!sourceMesh || !sourceMesh.isReadable || sourceMesh.subMeshCount != 2) return;
            Vector3[] points = sourceMesh.vertices;
            int[] sourceInterior = sourceMesh.GetTriangles(1);
            var selected = new HashSet<int>(sourceInterior);
            float back = float.PositiveInfinity;
            foreach (int id in selected) back = Mathf.Min(back, points[id].z);
            var contour = new List<Vector3>();
            foreach (int id in selected)
            {
                if (Mathf.Abs(points[id].z - back) > .001f) continue;
                Vector3 p = points[id];
                if (contour.Exists(q => ((Vector2)(q - p)).sqrMagnitude < 1e-10f)) continue;
                // Pair each rear rim point with its front endpoint; welded position rather than
                // source indices is essential because every old strip had duplicated vertices.
                float nearest = float.PositiveInfinity, front = back;
                foreach (int candidate in selected)
                {
                    Vector3 q = points[candidate];
                    if (q.z <= back + .01f) continue;
                    float d = ((Vector2)(q - p)).sqrMagnitude;
                    if (d < nearest) { nearest = d; front = q.z; }
                }
                p.z = front;
                contour.Add(p);
            }
            if (contour.Count < 8) return;
            Vector2 min = contour[0], max = contour[0];
            foreach (Vector3 p in contour) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            Vector2 center = (min + max) * .5f;
            contour.Sort((a, b) => Mathf.Atan2(a.y - center.y, a.x - center.x)
                .CompareTo(Mathf.Atan2(b.y - center.y, b.x - center.x)));
            // Retain every original boundary sample and subdivide broad edges for rounded relief.
            var rim = new List<Vector3>();
            for (int i = 0; i < contour.Count; i++)
            {
                Vector3 a = contour[i], b = contour[(i + 1) % contour.Count];
                int subdivisions = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / .045f));
                for (int j = 0; j < subdivisions; j++) rim.Add(Vector3.Lerp(a, b, (float)j / subdivisions));
            }
            int sides = rim.Count, stride = sides + 1, rows = Mathf.Clamp(lengthSegments, 16, 80);
            var skin = new Vector3[(rows + 1) * stride];
            var uv = new Vector2[skin.Length];
            for (int row = 0; row <= rows; row++)
            {
                float t = (float)row / rows;
                for (int col = 0; col <= sides; col++)
                {
                    Vector3 start = rim[col % sides];
                    Vector2 radial = ((Vector2)start - center).normalized;
                    float angle = Mathf.Atan2(radial.y, radial.x);
                    float z = Mathf.Lerp(start.z, back, t);
                    // Broad, irregular rocky ribs. Fade to the exact original contour at both
                    // mouths; expand outward only so the usable passage never becomes smaller.
                    float fade = Mathf.Sin(Mathf.PI * t);
                    float coarse = Mathf.PerlinNoise(radial.x * 2.3f + 12, z * 1.35f + radial.y * 1.8f + 20);
                    float strata = .5f + .5f * Mathf.Sin(z * 7.5f + angle * 3 + coarse * 3);
                    float displacement = Mathf.Clamp(relief, 0, .06f) * fade * fade * (.2f + .55f * coarse + .25f * strata);
                    Vector2 xy = (Vector2)start + radial * displacement;
                    int id = row * stride + col;
                    skin[id] = new Vector3(xy.x, xy.y, z);
                    uv[id] = new Vector2((float)col / sides * 3, (start.z - z) / 1.2f);
                }
            }
            var normals = new Vector3[skin.Length];
            for (int row = 0; row <= rows; row++)
            for (int col = 0; col < sides; col++)
            {
                Vector3 across = skin[row * stride + (col + 1) % sides] - skin[row * stride + (col + sides - 1) % sides];
                Vector3 along = skin[Mathf.Min(rows, row + 1) * stride + col] - skin[Mathf.Max(0, row - 1) * stride + col];
                normals[row * stride + col] = Vector3.Cross(across, along).normalized;
            }
            for (int row = 0; row <= rows; row++) normals[row * stride + sides] = normals[row * stride];

            int offset = points.Length;
            var vertices = new List<Vector3>(points); vertices.AddRange(skin);
            var allNormals = new List<Vector3>(sourceMesh.normals); allNormals.AddRange(normals);
            var allUV = new List<Vector2>(sourceMesh.uv); allUV.AddRange(uv);
            var triangles = new int[rows * sides * 6];
            int cursor = 0;
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < sides; col++)
            {
                int a = offset + row * stride + col, b = a + stride;
                triangles[cursor++] = a; triangles[cursor++] = a + 1; triangles[cursor++] = b;
                triangles[cursor++] = a + 1; triangles[cursor++] = b + 1; triangles[cursor++] = b;
            }
            preview = new Mesh { name = sourceMesh.name + "_ContinuousRock", hideFlags = HideFlags.DontSave,
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            preview.SetVertices(vertices); preview.SetNormals(allNormals); preview.SetUVs(0, allUV);
            preview.subMeshCount = 2;
            preview.SetTriangles(sourceMesh.GetTriangles(0), 0);
            preview.SetTriangles(triangles, 1);
            preview.RecalculateTangents(); preview.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = preview;
            var collider = GetComponent<MeshCollider>();
            if (collider)
            {
                if (!originalCollider) originalCollider = collider.sharedMesh ? collider.sharedMesh : sourceMesh;
                collider.sharedMesh = null;
                collider.sharedMesh = preview;
            }
        }

        void OnDisable() => Restore();
        void Restore()
        {
            if (!preview) return;
            GetComponent<MeshFilter>().sharedMesh = sourceMesh;
            var collider = GetComponent<MeshCollider>();
            if (collider) collider.sharedMesh = originalCollider;
            if (Application.isPlaying) Destroy(preview); else DestroyImmediate(preview);
            preview = null;
        }
    }
}
