using System.Collections.Generic;
using UnityEngine;

namespace Anadromo.Environment
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ProceduralUnderwaterTunnel : MonoBehaviour
    {
        [Header("Geometría del Túnel / Cueva")]
        [Range(2f, 25f)] public float radioBase = 8f;
        [Range(10f, 250f)] public float longitud = 80f;
        [Range(12, 64)] public int segmentosRadiales = 32;
        [Range(16, 128)] public int segmentosLongitudinales = 64;

        [Header("Cierre Posterior (Cueva de Ocultamiento)")]
        public bool cerrarFondo = true;
        [Range(0.1f, 2.5f)] public float profundidadCúpulaFondo = 0.85f;

        [Header("Variación Orgánica de Paredes (Smooth Noise)")]
        [Range(0f, 3f)] public float amplitudRuido = 0.85f;
        [Range(0.05f, 1f)] public float frecuenciaRuido = 0.25f;

        [Header("Curvatura de la Cueva")]
        [Range(-30f, 30f)] public float curvaturaHorizontal = 8f;
        [Range(-15f, 15f)] public float curvaturaVertical = -3f;

        [Header("Mapeado UV")]
        public Vector2 escalaUV = new Vector2(4f, 12f);

        [Header("Material PBR")]
        public Material materialTunel;

        public void GenerarMallaTunel()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

            Mesh mesh = new Mesh();
            mesh.name = "Malla_Cueva_Procedural";

            int mainVertsCount = (segmentosRadiales + 1) * (segmentosLongitudinales + 1);
            int extraCapVerts = cerrarFondo ? (segmentosRadiales + 2) : 0;
            int totalVerts = mainVertsCount + extraCapVerts;

            List<Vector3> vertices = new List<Vector3>(totalVerts);
            List<Vector3> normales = new List<Vector3>(totalVerts);
            List<Vector2> uvs = new List<Vector2>(totalVerts);
            List<int> triangulos = new List<int>();

            Vector3 centroSpineFin = Vector3.zero;

            for (int z = 0; z <= segmentosLongitudinales; z++)
            {
                float tZ = (float)z / segmentosLongitudinales;
                float posZ = tZ * longitud;

                // Curvatura suave de la trayectoria central de la cueva
                float offsetX = Mathf.Sin(tZ * Mathf.PI) * curvaturaHorizontal;
                float offsetY = Mathf.Sin(tZ * Mathf.PI) * curvaturaVertical;
                Vector3 centroSpine = new Vector3(offsetX, offsetY, posZ);
                if (z == segmentosLongitudinales) centroSpineFin = centroSpine;

                for (int r = 0; r <= segmentosRadiales; r++)
                {
                    float tR = (float)r / segmentosRadiales;
                    float angulo = tR * Mathf.PI * 2f;

                    // Ruido Perlin suave para paredes orgánicas hiperrealistas
                    float noiseVal = Mathf.PerlinNoise(Mathf.Cos(angulo) * frecuenciaRuido + 10f, tZ * longitud * frecuenciaRuido * 0.1f);
                    float radioVar = radioBase + (noiseVal - 0.5f) * amplitudRuido * 2f;

                    float cosA = Mathf.Cos(angulo);
                    float sinA = Mathf.Sin(angulo);

                    Vector3 localPos = new Vector3(cosA * radioVar, sinA * radioVar, 0f);
                    vertices.Add(centroSpine + localPos);

                    // Normales apuntando hacia adentro (interior de la cueva)
                    normales.Add(-new Vector3(cosA, sinA, 0f).normalized);

                    // UVs limpios y proporcionales
                    uvs.Add(new Vector2(tR * escalaUV.x, tZ * escalaUV.y));
                }
            }

            // Triangulación de las paredes tubulares interiores
            for (int z = 0; z < segmentosLongitudinales; z++)
            {
                for (int r = 0; r < segmentosRadiales; r++)
                {
                    int currentIdx = z * (segmentosRadiales + 1) + r;
                    int nextIdx = currentIdx + (segmentosRadiales + 1);

                    triangulos.Add(currentIdx);
                    triangulos.Add(currentIdx + 1);
                    triangulos.Add(nextIdx);

                    triangulos.Add(nextIdx);
                    triangulos.Add(currentIdx + 1);
                    triangulos.Add(nextIdx + 1);
                }
            }

            // Cierre posterior de la cueva (Fondo abisal ciego)
            if (cerrarFondo)
            {
                int capCenterIdx = vertices.Count;
                Vector3 capCenterPos = centroSpineFin + new Vector3(0f, 0f, radioBase * profundidadCúpulaFondo);
                vertices.Add(capCenterPos);
                normales.Add(new Vector3(0f, 0f, -1f));
                uvs.Add(new Vector2(0.5f * escalaUV.x, escalaUV.y));

                int lastRingStart = segmentosLongitudinales * (segmentosRadiales + 1);

                for (int r = 0; r < segmentosRadiales; r++)
                {
                    int ringCurrent = lastRingStart + r;
                    int ringNext = lastRingStart + r + 1;

                    // Triángulos para la pared del fondo mirando hacia adentro de la cueva
                    triangulos.Add(ringCurrent);
                    triangulos.Add(capCenterIdx);
                    triangulos.Add(ringNext);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normales);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangulos, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            meshFilter.sharedMesh = mesh;

            if (materialTunel != null)
            {
                meshRenderer.sharedMaterial = materialTunel;
            }

            // MeshCollider para colisiones perfectas
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
