using System.Collections.Generic;
using UnityEngine;

namespace Anadromo.Locomotion
{
    /// <summary>Aletas pectorales visibles que siguen cada mano óptica, sin colisionadores.</summary>
    public class SalmonFinHands : MonoBehaviour
    {
        [SerializeField] private FlapDetector detector;
        [SerializeField] private FlapSwimController swimController;
        [SerializeField] private Material finMaterial;
        [Tooltip("Longitud de cada aleta en metros.")]
        [SerializeField] private float finLength = 0.28f;
        [SerializeField] private Vector3 rotationOffset;
        private readonly MeshRenderer[] fins = new MeshRenderer[2];
        private readonly float[] pulses = new float[2];
        private readonly Mesh[] meshes = new Mesh[2];
        private Material membrane, rays;
        private MaterialPropertyBlock properties;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly Color restingColor = new Color(0.48f, 0.42f, 0.32f);

        private void Awake()
        {
            if (detector == null || finMaterial == null)
            {
                Debug.LogWarning("[Anadromo] Asigna detector y material de las aletas.", this);
                enabled = false;
                return;
            }
            // Material referenciado por la escena: el shader se incluye en el build.
            membrane = new Material(finMaterial) { name = "Salmon fin membrane" };
            membrane.SetTexture("_BaseMap", null);
            membrane.SetColor(BaseColor, restingColor);
            membrane.SetFloat("_Smoothness", 0.35f);
            rays = new Material(membrane) { name = "Salmon fin rays" };
            rays.SetColor(BaseColor, new Color(0.25f, 0.20f, 0.14f));
            properties = new MaterialPropertyBlock();
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(i == 0 ? "Aleta izquierda" : "Aleta derecha");
                go.transform.SetParent(transform, false);
                meshes[i] = BuildFin(i == 0 ? -1f : 1f);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                fins[i] = go.AddComponent<MeshRenderer>();
                fins[i].sharedMaterials = new[] { membrane, rays };
                fins[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (detector == null) return;
            detector.OnLeftFlap.AddListener(LeftStroke);
            detector.OnRightFlap.AddListener(RightStroke);
            Application.onBeforeRender += UpdatePoses;
        }

        private void OnDisable()
        {
            if (detector != null)
            {
                detector.OnLeftFlap.RemoveListener(LeftStroke);
                detector.OnRightFlap.RemoveListener(RightStroke);
            }
            Application.onBeforeRender -= UpdatePoses;
            foreach (var fin in fins) if (fin != null) fin.gameObject.SetActive(false);
            pulses[0] = pulses[1] = 0f;
        }

        private void LeftStroke(float intensity) => ShowStroke(0, intensity);
        private void RightStroke(float intensity) => ShowStroke(1, intensity);
        private void ShowStroke(int hand, float intensity)
        {
            // No anunciar impulso mientras la introducción bloquea la locomoción.
            if (swimController != null && swimController.isActiveAndEnabled)
                pulses[hand] = Mathf.Clamp01(intensity);
        }

        private void LateUpdate()
        {
            for (int i = 0; i < 2; i++)
            {
                pulses[i] = Mathf.MoveTowards(pulses[i], 0f, Time.deltaTime * 3f);
                if (fins[i] == null) continue;
                properties.SetColor(BaseColor, Color.Lerp(restingColor,
                    new Color(0.65f, 0.9f, 0.75f), pulses[i]));
                fins[i].SetPropertyBlock(properties, 0);
            }
            UpdatePoses();
        }

        private void UpdatePoses()
        {
            if (detector == null) return;
            for (int i = 0; i < 2; i++)
            {
                if (fins[i] == null) continue;
                bool tracked = detector.TryGetTrackedWrist(i == 0, out Pose pose);
                fins[i].gameObject.SetActive(tracked);
                if (!tracked) { pulses[i] = 0f; continue; }
                fins[i].transform.SetPositionAndRotation(pose.position,
                    pose.rotation * Quaternion.Euler(rotationOffset));
            }
        }

        private Mesh BuildFin(float side)
        {
            var vertices = new List<Vector3>();
            var membraneTriangles = new List<int>();
            var rayTriangles = new List<int>();
            const int segments = 12;
            // Abanico asimétrico, con borde curvo y nervaduras radiales a ambos lados.
            for (int face = 0; face < 2; face++)
            {
                float y = face == 0 ? 0.002f : -0.002f;
                int root = vertices.Count;
                vertices.Add(new Vector3(0f, y, 0f));
                for (int j = 0; j <= segments; j++)
                {
                    float t = j / (float)segments;
                    float angle = Mathf.Lerp(-30f, 65f, t) * Mathf.Deg2Rad;
                    float length = finLength * Mathf.Lerp(0.8f, 1f, Mathf.Sin(t * Mathf.PI));
                    vertices.Add(new Vector3(side * Mathf.Sin(angle) * length,
                        y, Mathf.Cos(angle) * length));
                    if (j == 0) continue;
                    int a = root + j, b = root + j + 1;
                    bool reverse = (face == 0) == (side < 0f);
                    membraneTriangles.Add(root);
                    membraneTriangles.Add(reverse ? b : a);
                    membraneTriangles.Add(reverse ? a : b);
                }
                for (int j = 1; j < segments; j++)
                {
                    Vector3 tip = vertices[root + j + 1];
                    tip.y = y * 1.5f;
                    int start = vertices.Count;
                    vertices.Add(new Vector3(-0.001f, tip.y, 0.01f));
                    vertices.Add(tip + Vector3.right * 0.001f);
                    vertices.Add(tip - Vector3.right * 0.001f);
                    rayTriangles.Add(start);
                    rayTriangles.Add(face == 0 ? start + 2 : start + 1);
                    rayTriangles.Add(face == 0 ? start + 1 : start + 2);
                }
            }
            var mesh = new Mesh { name = "Salmon pectoral fin", subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(membraneTriangles, 0);
            mesh.SetTriangles(rayTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (var fin in fins) if (fin != null) Destroy(fin.gameObject);
            if (membrane != null) Destroy(membrane);
            if (rays != null) Destroy(rays);
        }
    }
}
