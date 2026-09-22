using System.Collections.Generic;
using UnityEngine;

namespace Anadromo.AI
{
    /// <summary>Close-up sea bass: UV-preserving curved subdivision and speed-coupled swimming.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RealisticFishNPC : MonoBehaviour
    {
        public Mesh sourceMesh;
        [Range(0, 2)] public int subdivisions = 2;
        [Header("Nado (metros y segundos)")]
        [Min(.05f)] public float cruiseSpeed = .35f;
        [Min(.5f)] public float roamRadius = 2.5f;
        [Range(10, 120)] public float turnSpeed = 45;
        [Range(.01f, .08f)] public float tailAmplitude = .035f;
        [Range(.5f, 3)] public float tailFrequency = 1.3f;
        [Range(0, .5f)] public float verticalWander = .15f;
        Mesh mesh, previousMesh;
        MeshFilter filter;
        Vector3[] rest, vertices, normals, restNormals;
        Vector4[] tangents, restTangents;
        Vector3 home;
        float phase, elapsed, speed, bank;
        bool rebuild;

        void OnEnable() { rebuild = true; home = transform.position; elapsed = phase = speed = bank = 0; }
        void OnValidate() { rebuild = true; }
        void Update()
        {
            if (rebuild) { rebuild = false; BuildMesh(); }
            if (!Application.isPlaying || !mesh) return;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            elapsed += dt;
            Move(dt);
            Animate(dt);
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

        void Move(float dt)
        {
            float orbit = elapsed * cruiseSpeed / Mathf.Max(.5f, roamRadius);
            // The target travels continuously; no random target jumps or teleporting at loop boundaries.
            Vector3 target = home + new Vector3(Mathf.Sin(orbit) * roamRadius,
                Mathf.Sin(elapsed * .31f) * verticalWander, Mathf.Cos(orbit) * roamRadius);
            Vector3 desired = (target - transform.position).normalized;
            Vector3 forward = transform.forward;
            float probe = .7f + speed;
            bool blocked = Physics.SphereCast(transform.position, .16f, forward, out var hit,
                probe, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (blocked)
            {
                desired = Vector3.ProjectOnPlane(desired, hit.normal) + hit.normal * .8f;
                if (desired.sqrMagnitude < .01f) desired = transform.right;
                desired.Normalize();
            }
            float yaw = Vector3.Dot(transform.right, desired);
            bank = Mathf.Lerp(bank, Mathf.Clamp(-yaw * 18, -12, 12), 1 - Mathf.Exp(-dt * 3));
            Quaternion heading = Quaternion.LookRotation(desired, Vector3.up) * Quaternion.Euler(0, 0, bank);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, heading, turnSpeed * dt);
            float alignment = Mathf.Clamp01(Vector3.Dot(transform.forward, desired));
            float desiredSpeed = cruiseSpeed * (.85f + .15f * Mathf.Sin(elapsed * .7f)) * alignment;
            if (blocked) desiredSpeed *= Mathf.Clamp01((hit.distance - .18f) / probe);
            speed = Mathf.MoveTowards(speed, desiredSpeed, dt * .25f);
            Vector3 step = transform.forward * speed * dt;
            // Sweep the actual displacement as well as looking ahead; do not swim through walls.
            if (step.sqrMagnitude > 0 && !Physics.SphereCast(transform.position, .16f, step.normalized,
                    out _, step.magnitude + .04f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                transform.position += step;
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
                vertices[i] = p;
                Vector3 normal = restNormals[i]; normal.z -= slope * normal.x;
                normals[i] = normal.normalized;
                Vector4 t = restTangents[i];
                Vector3 tangent = new Vector3(t.x + slope * t.z, t.y, t.z);
                tangent = (tangent - Vector3.Dot(tangent, normals[i]) * normals[i]).normalized;
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, t.w);
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.tangents = tangents;
        }

        void ReleaseMesh()
        {
            if (!mesh) return;
            if (filter && filter.sharedMesh == mesh) filter.sharedMesh = previousMesh;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }
        void OnDisable() { ReleaseMesh(); }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.2f, .8f, 1, .6f);
            Gizmos.DrawWireSphere(Application.isPlaying ? home : transform.position, roamRadius);
        }
    }
}
