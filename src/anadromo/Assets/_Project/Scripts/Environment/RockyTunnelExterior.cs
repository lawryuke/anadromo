using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anadromo.Environment
{
    /// <summary>Reversible rocky dressing of the authored terrain surfaces and exit portal.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class RockyTunnelExterior : MonoBehaviour
    {
        [Serializable]
        public sealed class Surface
        {
            public MeshFilter filter;
            public Mesh originalMesh;
            public Material originalMaterial;
        }
        public Surface[] surfaces;
        public TunnelGapInfill passage;
        public Material rockMaterial;
        public GameObject layeredCliffPrefab;
        public GameObject boulderPrefab;
        [Range(.05f, .4f)] public float edgeRoundness = .22f;
        [Range(0, .25f)] public float rockRelief = .12f;
        [Range(.35f, 1.5f)] public float spacing = .65f;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<GameObject> collisionShells = new List<GameObject>();
        GameObject dressing;
        bool pending;

        void OnEnable() => pending = true;
        void OnValidate() => pending = true;
        void Update()
        {
            if (!pending) return;
            pending = false;
            Rebuild();
        }

        [ContextMenu("Reconstruir acabado rocoso exterior")]
        public void Rebuild()
        {
            Restore();
            if (!passage || !rockMaterial || surfaces == null || !passage.leftWall ||
                !passage.rightWall || !passage.floor || !passage.ceiling) return;
            float xmin = passage.leftWall.bounds.max.x, xmax = passage.rightWall.bounds.min.x;
            float ymin = passage.floor.bounds.max.y, ymax = passage.ceiling.bounds.min.y;
            float zmin = passage.leftWall.bounds.min.z - 2;
            float zmax = passage.leftWall.bounds.max.z + 2;
            foreach (Surface surface in surfaces)
            {
                if (surface == null || !surface.filter || !surface.originalMesh) continue;
                Mesh mesh = BuildSurface(surface, xmin, xmax, ymin, ymax, zmin, zmax);
                meshes.Add(mesh);
                surface.filter.sharedMesh = mesh;
                var renderer = surface.filter.GetComponent<MeshRenderer>();
                if (renderer) renderer.sharedMaterial = rockMaterial;
                var collision = new GameObject("Colision del revestimiento rocoso");
                collision.hideFlags = HideFlags.DontSave;
                collision.layer = surface.filter.gameObject.layer;
                collision.transform.SetParent(surface.filter.transform, false);
                collision.AddComponent<MeshCollider>().sharedMesh = mesh;
                collisionShells.Add(collision);
            }
            dressing = new GameObject("Salida - acantilados y rocas OceanViz");
            dressing.hideFlags = HideFlags.DontSave;
            dressing.transform.SetParent(transform, false);
            float center = (xmin + xmax) * .5f;
            float back = Mathf.Max(passage.leftWall.bounds.max.z, passage.floor.bounds.max.z) + .1f;
            // Every module fits completely outside the opening. Asymmetric sizes/rotations
            // create a rocky arch without placing a solid cliff across the player's path.
            AddRock(boulderPrefab, new Bounds(new Vector3(xmin - 1.16f, ymin + .65f, back),
                new Vector3(2.2f, 2.3f, 1.8f)), new Vector3(9, 18, -14));
            AddRock(boulderPrefab, new Bounds(new Vector3(xmax + 1.46f, ymin + .9f, back - .08f),
                new Vector3(2.8f, 2.8f, 1.9f)), new Vector3(-11, 139, 21));
            AddRock(layeredCliffPrefab, new Bounds(new Vector3(center - .35f, ymax + 1.02f, back - .18f),
                new Vector3(6.6f, 1.9f, 2.2f)), new Vector3(0, 180, -7));
            AddRock(boulderPrefab, new Bounds(new Vector3(xmin - .78f, ymax + .67f, back + .1f),
                new Vector3(1.45f, 1.8f, 1.65f)), new Vector3(17, 69, -27));
            AddRock(boulderPrefab, new Bounds(new Vector3(xmax + .95f, ymax + .53f, back + .05f),
                new Vector3(1.75f, 1.65f, 1.8f)), new Vector3(-6, 215, 12));
            AddRock(boulderPrefab, new Bounds(new Vector3(center - .75f, ymin - .62f, back),
                new Vector3(2.8f, 1.1f, 1.9f)), new Vector3(0, 37, 9));
            AddRock(boulderPrefab, new Bounds(new Vector3(center + 1.38f, ymin - .48f, back + .12f),
                new Vector3(2.1f, .8f, 1.55f)), new Vector3(8, 123, -19));
        }

        Mesh BuildSurface(Surface surface, float xmin, float xmax, float ymin, float ymax, float zmin, float zmax)
        {
            Transform owner = surface.filter.transform;
            Bounds bounds = surface.originalMesh.bounds;
            Vector3 scale = owner.lossyScale;
            scale = new Vector3(Mathf.Max(.001f, Mathf.Abs(scale.x)), Mathf.Max(.001f, Mathf.Abs(scale.y)),
                Mathf.Max(.001f, Mathf.Abs(scale.z)));
            Vector3 size = Vector3.Scale(bounds.size, scale);
            Vector3 center = Vector3.Scale(bounds.center, scale);
            // A plane becomes a low rocky shelf, with thickness below its original surface.
            if (size.y < .01f) { size.y = .3f; center.y -= .15f; }
            Vector3 half = size * .5f;
            float radius = Mathf.Clamp(edgeRoundness, .05f, .4f);
            var positions = new List<Vector3>();
            var triangles = new List<int>();
            var welded = new Dictionary<Vector3Int, int>();
            for (int axis = 0; axis < 3; axis++)
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                float[] xs = Axis(half[u], radius), ys = Axis(half[v], radius);
                int[,] face = new int[ys.Length, xs.Length];
                for (int y = 0; y < ys.Length; y++)
                for (int x = 0; x < xs.Length; x++)
                {
                    Vector3 p = Vector3.zero;
                    p[axis] = sign * (half[axis] + radius); p[u] = xs[x]; p[v] = ys[y];
                    Vector3 core = new Vector3(Mathf.Clamp(p.x, -half.x, half.x),
                        Mathf.Clamp(p.y, -half.y, half.y), Mathf.Clamp(p.z, -half.z, half.z));
                    Vector3 normal = (p - core).normalized;
                    Vector3 baseLocal = Divide(core + center, scale);
                    Vector3 baseWorld = owner.TransformPoint(baseLocal);
                    float dx = Mathf.Max(xmin - baseWorld.x, 0, baseWorld.x - xmax);
                    float dy = Mathf.Max(ymin - baseWorld.y, 0, baseWorld.y - ymax);
                    // A protected collar around the original rectangular aperture keeps all
                    // existing infill joins and traversable dimensions fixed.
                    float mask = baseWorld.z < zmin || baseWorld.z > zmax ? 1 :
                        Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .95f, Mathf.Sqrt(dx * dx + dy * dy)));
                    float noise = .5f * (Mathf.PerlinNoise(baseWorld.x * .83f + baseWorld.z * .27f + 14,
                        baseWorld.y * .9f + 31) + Mathf.PerlinNoise(baseWorld.z * .71f + 9, baseWorld.x * .59f + 47));
                    float height = Mathf.Clamp(rockRelief, 0, .25f) * (.2f + noise * .8f);
                    Vector3 local = Divide(core + center + normal * (radius + height) * mask, scale);
                    var key = new Vector3Int(Mathf.RoundToInt(local.x * 100000),
                        Mathf.RoundToInt(local.y * 100000), Mathf.RoundToInt(local.z * 100000));
                    if (!welded.TryGetValue(key, out int id))
                    { id = positions.Count; welded.Add(key, id); positions.Add(local); }
                    face[y, x] = id;
                }
                for (int y = 0; y < ys.Length - 1; y++)
                for (int x = 0; x < xs.Length - 1; x++)
                {
                    int a = face[y, x], b = face[y, x + 1], c = face[y + 1, x], d = face[y + 1, x + 1];
                    AddTriangle(triangles, a, sign > 0 ? b : c, sign > 0 ? c : b);
                    AddTriangle(triangles, b, sign > 0 ? d : c, sign > 0 ? c : d);
                }
            }
            var mesh = new Mesh { name = surface.filter.name + "_RockySurface", hideFlags = HideFlags.DontSave,
                indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(positions); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        float[] Axis(float half, float radius)
        {
            var values = new SortedSet<float>();
            int steps = Mathf.Clamp(Mathf.CeilToInt(half * 2 / Mathf.Max(.35f, spacing)), 2, 28);
            for (int i = 0; i <= steps; i++) values.Add(Mathf.Lerp(-half, half, (float)i / steps));
            for (int i = 1; i <= 4; i++) { values.Add(-half - radius * i / 4); values.Add(half + radius * i / 4); }
            var result = new float[values.Count]; values.CopyTo(result); return result;
        }
        static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        static void AddTriangle(List<int> indices, int a, int b, int c)
        {
            if (a == b || b == c || c == a) return;
            indices.Add(a); indices.Add(b); indices.Add(c);
        }

        void AddRock(GameObject prefab, Bounds target, Vector3 euler)
        {
            if (!prefab) return;
            GameObject rock = Instantiate(prefab, dressing.transform);
            rock.name = "OceanViz - " + prefab.name;
            foreach (Transform child in rock.GetComponentsInChildren<Transform>(true))
            { child.gameObject.hideFlags = HideFlags.DontSave; child.gameObject.isStatic = false; }
            rock.transform.rotation = Quaternion.Euler(euler);
            Bounds bounds = RendererBounds(rock);
            if (bounds.size.sqrMagnitude < .0001f) return;
            float factor = Mathf.Min(target.size.x / Mathf.Max(.001f, bounds.size.x),
                target.size.y / Mathf.Max(.001f, bounds.size.y), target.size.z / Mathf.Max(.001f, bounds.size.z));
            rock.transform.localScale *= factor;
            bounds = RendererBounds(rock);
            rock.transform.position += target.center - bounds.center;
            // Keep the imported UVs, scanned textures and LODs from OceanViz intact.
        }
        static Bounds RendererBounds(GameObject rock)
        {
            Renderer[] renderers = rock.GetComponentsInChildren<Renderer>(true);
            Bounds result = renderers.Length > 0 ? renderers[0].bounds : new Bounds(rock.transform.position, Vector3.zero);
            for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
            foreach (Collider collider in rock.GetComponentsInChildren<Collider>())
                if (collider.enabled) result.Encapsulate(collider.bounds);
            return result;
        }
        void OnDisable() => Restore();
        void Restore()
        {
            if (surfaces != null)
                foreach (Surface surface in surfaces)
                {
                    if (surface == null || !surface.filter || !meshes.Contains(surface.filter.sharedMesh)) continue;
                    surface.filter.sharedMesh = surface.originalMesh;
                    var renderer = surface.filter.GetComponent<MeshRenderer>();
                    if (renderer) renderer.sharedMaterial = surface.originalMaterial;
                }
            if (dressing) dressing.SetActive(false);
            Dispose(dressing); dressing = null;
            foreach (GameObject shell in collisionShells)
            { if (shell) shell.SetActive(false); Dispose(shell); }
            collisionShells.Clear();
            foreach (Mesh mesh in meshes) Dispose(mesh);
            meshes.Clear();
        }
        static void Dispose(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
