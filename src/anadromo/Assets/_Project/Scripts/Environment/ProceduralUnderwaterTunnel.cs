using UnityEngine;

namespace Anadromo.Environment
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ProceduralUnderwaterTunnel : MonoBehaviour
    {
        [Header("Geometría del Túnel")]
        [Range(2f, 25f)] public float radioBase = 8f;
        [Range(10f, 250f)] public float longitud = 80f;
        [Range(12, 64)] public int segmentosRadiales = 32;
        [Range(16, 128)] public int segmentosLongitudinales = 64;

        [Header("Variación Orgánica de Paredes (Smooth Noise)")]
        [Range(0f, 3f)] public float amplitudRuido = 0.85f;
        [Range(0.05f, 1f)] public float frecuenciaRuido = 0.25f;

        [Header("Curvatura del Túnel")]
        [Range(-30f, 30f)] public float curvaturaHorizontal = 8f;
        [Range(-15f, 15f)] public float curvaturaVertical = -3f;

        [Header("Mapeado UV")]
        public Vector2 escalaUV = new Vector2(4f, 12f);

        [Header("Material")]
        public Material materialTunel;

        public void GenerarMallaTunel()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

            Mesh mesh = new Mesh();
            mesh.name = "Malla_Tunel_Procedural";

            int numVertices = (segmentosRadiales + 1) * (segmentosLongitudinales + 1);
            Vector3[] vertices = new Vector3[numVertices];
            Vector3[] normales = new Vector3[numVertices];
            Vector2[] uvs = new Vector2[numVertices];
            int[] triangulos = new int[segmentosRadiales * segmentosLongitudinales * 6];

            int vertIdx = 0;
            int triIdx = 0;

            for (int z = 0; z <= segmentosLongitudinales; z++)
            {
                float tZ = (float)z / segmentosLongitudinales;
                float posZ = tZ * longitud;

                // Curvatura suave de la trayectoria central
                float offsetX = Mathf.Sin(tZ * Mathf.PI) * curvaturaHorizontal;
                float offsetY = Mathf.Sin(tZ * Mathf.PI) * curvaturaVertical;
                Vector3 centroSpine = new Vector3(offsetX, offsetY, posZ);

                for (int r = 0; r <= segmentosRadiales; r++)
                {
                    float tR = (float)r / segmentosRadiales;
                    float angulo = tR * Mathf.PI * 2f;

                    // Ruido Perlin suave para pared orgánica (sin deformaciones bruscas)
                    float noiseVal = Mathf.PerlinNoise(Mathf.Cos(angulo) * frecuenciaRuido + 10f, tZ * longitud * frecuenciaRuido * 0.1f);
                    float radioVar = radioBase + (noiseVal - 0.5f) * amplitudRuido * 2f;

                    float cosA = Mathf.Cos(angulo);
                    float sinA = Mathf.Sin(angulo);

                    Vector3 localPos = new Vector3(cosA * radioVar, sinA * radioVar, 0f);
                    vertices[vertIdx] = centroSpine + localPos;

                    // Normales apuntando hacia adentro (interior de la cueva)
                    normales[vertIdx] = -new Vector3(cosA, sinA, 0f).normalized;

                    // UVs limpios y proporcionales
                    uvs[vertIdx] = new Vector2(tR * escalaUV.x, tZ * escalaUV.y);

                    vertIdx++;
                }
            }

            // Triangulación para paredes interiores visibles
            for (int z = 0; z < segmentosLongitudinales; z++)
            {
                for (int r = 0; r < segmentosRadiales; r++)
                {
                    int currentIdx = z * (segmentosRadiales + 1) + r;
                    int nextIdx = currentIdx + (segmentosRadiales + 1);

                    // Cara 1
                    triangulos[triIdx++] = currentIdx;
                    triangulos[triIdx++] = currentIdx + 1;
                    triangulos[triIdx++] = nextIdx;

                    // Cara 2
                    triangulos[triIdx++] = nextIdx;
                    triangulos[triIdx++] = currentIdx + 1;
                    triangulos[triIdx++] = nextIdx + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normales;
            mesh.uv = uvs;
            mesh.triangles = triangulos;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            meshFilter.sharedMesh = mesh;

            if (materialTunel != null)
            {
                meshRenderer.sharedMaterial = materialTunel;
            }

            // MeshCollider para colisión fluida
            MeshCollider collider = GetComponent<MeshCollider>();
            if (collider == null) collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        private void Start()
        {
            if (GetComponent<MeshFilter>().sharedMesh == null)
            {
                GenerarMallaTunel();
            }
        }
    }
}
