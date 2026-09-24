using System.Collections.Generic;
using UnityEngine;

namespace Anadromo.AI
{
    /// <summary>Sea bass mesh refinement and animation. Movement belongs to the swim controller.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RealisticFishNPC : MonoBehaviour
    {
        public Mesh sourceMesh;
        [Range(0, 2)] public int subdivisions = 2;
        [Header("Animación de nado (sin desplazamiento)")]
        [Tooltip("Velocidad de referencia para alcanzar la intensidad máxima de la cola; no mueve el pez.")]
        [Min(.05f)] public float cruiseSpeed = .35f;
        [Range(.01f, .08f)] public float tailAmplitude = .035f;
        [Range(.5f, 3)] public float tailFrequency = 1.3f;
        [Header("Retraso del cuerpo al girar")]
        public bool turnLag;
        [Range(.05f, 1.5f)] public float tailFollowTime = .65f;
        [Range(30f, 170f)] public float maxTurnLag = 160f;
        const int SpineSegments = 16;
        const float NeckZ = .10f, TailZ = -.354f;
        readonly Quaternion[] spineWorld = new Quaternion[SpineSegments + 1];
        readonly Quaternion[] spineLocal = new Quaternion[SpineSegments + 1];
        readonly Vector3[] spinePosition = new Vector3[SpineSegments + 1];
        Mesh mesh, previousMesh;
        MeshFilter filter;
        Vector3[] rest, vertices, normals, restNormals;
        Vector4[] tangents, restTangents;
        Vector3 previousPosition;
        float phase, speed;
        bool rebuild;

        void OnEnable()
        {
            rebuild = true;
            previousPosition = transform.position;
            phase = (GetInstanceID() & 0xFFFF) * 2.399963f;
            speed = 0;
            ResetSpine();
        }
        void OnValidate() { rebuild = true; }
        void LateUpdate()
        {
            if (rebuild) { rebuild = false; BuildMesh(); }
            if (!Application.isPlaying || !mesh) return;
            float frameTime = Time.deltaTime;
            float measuredSpeed = frameTime > 0 ? Vector3.Distance(previousPosition, transform.position) / frameTime : 0;
            previousPosition = transform.position;
            float dt = Mathf.Min(frameTime, .05f);
            speed = Mathf.Lerp(speed, measuredSpeed, 1 - Mathf.Exp(-dt * 8));
            UpdateTurnLag(frameTime);
            Animate(dt);
        }

        void ResetSpine()
        {
            for (int i = 0; i <= SpineSegments; i++) spineWorld[i] = transform.rotation;
            UpdateTurnLag(0);
        }

        void UpdateTurnLag(float dt)
        {
            Quaternion head = transform.rotation;
            Quaternion inverse = Quaternion.Inverse(head);
            float segmentLength = (NeckZ - TailZ) / SpineSegments;
            spinePosition[0] = new Vector3(0, 0, NeckZ);
            spineLocal[0] = Quaternion.identity;
            spineWorld[0] = head;
            for (int i = 1; i <= SpineSegments; i++)
            {
                float q = (float)i / SpineSegments;
                if (!turnLag) spineWorld[i] = head;
                else if (dt > 0)
                {
                    float response = Mathf.Max(.01f, tailFollowTime * q);
                    spineWorld[i] = Quaternion.Slerp(spineWorld[i], head, 1 - Mathf.Exp(-dt / response));
                    spineWorld[i] = Quaternion.RotateTowards(head, spineWorld[i], Mathf.Clamp(maxTurnLag, 0, 170));
                }
                spineLocal[i] = inverse * spineWorld[i];
                // Integrate a flexible spine instead of rotating the entire mesh about one pivot.
                Quaternion direction = Quaternion.Slerp(spineLocal[i - 1], spineLocal[i], .5f);
                spinePosition[i] = spinePosition[i - 1] + direction * (Vector3.back * segmentLength);
            }
        }

        void BendWithSpine(ref Vector3 point, ref Vector3 normal, ref Vector3 tangent)
        {
            if (!turnLag || point.z >= NeckZ) return;
            float along = Mathf.Clamp01((NeckZ - point.z) / (NeckZ - TailZ)) * SpineSegments;
            int segment = Mathf.Min((int)along, SpineSegments - 1);
            float t = along - segment;
            Quaternion rotation = Quaternion.Slerp(spineLocal[segment], spineLocal[segment + 1], t);
            Vector3 center = Vector3.Lerp(spinePosition[segment], spinePosition[segment + 1], t);
            point = center + rotation * new Vector3(point.x, point.y, Mathf.Min(0, point.z - TailZ));
            normal = rotation * normal;
            tangent = rotation * tangent;
        }

        public void BuildMesh()
        {
            ReleaseMesh();
            if (!sourceMesh || !sourceMesh.isReadable) return;
            filter = GetComponent<MeshFilter>();
            previousMesh = filter.sharedMesh;
            var p = new List<Vector3>(sourceMesh.vertices);
            var n = new List<Vector3>(sourceMesh.normals);
            var uv = new List<Vector2>(sourceMesh.uv);
            var indices = new List<int>(sourceMesh.triangles);
            if (p.Count != n.Count || p.Count != uv.Count) return;
            for (int pass = 0; pass < Mathf.Clamp(subdivisions, 0, 2); pass++)
            {
                var edges = new Dictionary<ulong, int>();
                var next = new List<int>(indices.Count * 4);
                int Midpoint(int a, int b)
                {
                    ulong key = ((ulong)(uint)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                    if (edges.TryGetValue(key, out int existing)) return existing;
                    Vector3 middle = (p[a] + p[b]) * .5f;
                    // Project onto endpoint tangent planes to soften the body without shrinking fins.
                    Vector3 projectedA = middle - Vector3.Dot(middle - p[a], n[a]) * n[a];
                    Vector3 projectedB = middle - Vector3.Dot(middle - p[b], n[b]) * n[b];
                    int index = p.Count;
                    p.Add(Vector3.Lerp(middle, (projectedA + projectedB) * .5f, .65f));
                    n.Add((n[a] + n[b]).normalized);
                    uv.Add((uv[a] + uv[b]) * .5f);
                    edges.Add(key, index);
                    return index;
                }
                for (int i = 0; i < indices.Count; i += 3)
                {
                    int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a);
                    next.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
                }
                indices = next;
            }
            mesh = new Mesh { name = "PezNPC - refined sea bass", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            mesh.SetVertices(p); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            bounds.Expand(.25f);
            mesh.bounds = bounds;
            rest = mesh.vertices; restNormals = mesh.normals; restTangents = mesh.tangents;
            vertices = new Vector3[rest.Length]; normals = new Vector3[rest.Length]; tangents = new Vector4[rest.Length];
            filter.sharedMesh = mesh;
        }

        void Animate(float dt)
        {
            float effort = Mathf.Clamp01(speed / Mathf.Max(.05f, cruiseSpeed));
            phase = Mathf.Repeat(phase + dt * tailFrequency * Mathf.Lerp(.45f, 1, effort) * Mathf.PI * 2, Mathf.PI * 4);
            float amplitude = tailAmplitude * Mathf.Lerp(.25f, 1, effort);
            const float length = .537f;
            for (int i = 0; i < rest.Length; i++)
            {
                Vector3 p = rest[i];
                float q = Mathf.Clamp01((.183f - p.z) / length);
                float wave = phase - q * 5.2f;
                float bend = amplitude * q * q * Mathf.Sin(wave);
                float slope = -amplitude / length * (2 * q * Mathf.Sin(wave) - 5.2f * q * q * Mathf.Cos(wave));
                p.x += bend;
                // Small, slower pectoral movement, isolated from the head and caudal fin.
                float fin = Mathf.Clamp01((Mathf.Abs(p.x - bend) - .036f) / .025f)
                    * (1 - Mathf.SmoothStep(0, .12f, Mathf.Abs(p.z - .035f)))
                    * (1 - Mathf.SmoothStep(0, .035f, Mathf.Abs(p.y + .012f)));
                p.y += Mathf.Sin(phase * .5f + Mathf.Sign(p.x) * .5f) * fin * .003f;

                Vector3 normal = restNormals[i]; normal.z -= slope * normal.x;
                normal.Normalize();
                Vector4 t = restTangents[i];
                Vector3 tangent = new Vector3(t.x + slope * t.z, t.y, t.z);
                tangent = (tangent - Vector3.Dot(tangent, normal) * normal).normalized;
                BendWithSpine(ref p, ref normal, ref tangent);
                vertices[i] = p;
                normals[i] = normal;
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, t.w);
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.tangents = tangents;
            if (turnLag) mesh.RecalculateBounds();
        }

        void ReleaseMesh()
        {
            if (!mesh) return;
            if (filter && filter.sharedMesh == mesh) filter.sharedMesh = previousMesh;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }
        void OnDisable() { ReleaseMesh(); }
    }
}
