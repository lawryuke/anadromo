using System.Collections.Generic;
using UnityEngine;

namespace Anadromo.Environment
{
    [ExecuteAlways]
    public class NaturalRockCaveAssembler : MonoBehaviour
    {
        [Header("Prefabs de Roca Real (Assets de Acantilados/Rocas)")]
        public GameObject prefabRocaCliffB; // Seabed Fractured Cliff B
        public GameObject prefabRocaCliffA; // Seabed Layered Cliff A
        public GameObject prefabRocaBoulder; // Seabed Boulder A

        [Header("Material PBR Submarino Abisal")]
        public Material materialRocaPBR;

        [Header("Configuración de la Cueva / Túnel de Piedras")]
        [Range(10f, 150f)] public float longitudTunel = 60f;
        [Range(5f, 18f)] public float radioCueva = 8.5f;
        [Range(4, 24)] public int seccionesLongitudinales = 12;
        [Range(6, 16)] public int rocasPorAnillo = 10;

        [Header("Variación Orgánica (Sin Patrones Repetitivos)")]
        [Range(0.6f, 2.2f)] public float escalaMinRoca = 0.9f;
        [Range(0.6f, 2.2f)] public float escalaMaxRoca = 1.45f;
        [Range(0f, 35f)] public float jitterPosicion = 1.2f;
        [Range(0f, 180f)] public float rotacionAleatoria = 45f;

        [Header("Curvatura de la Travesía")]
        [Range(-25f, 25f)] public float curvaturaX = 6f;
        [Range(-15f, 15f)] public float curvaturaY = -2f;

        [ContextMenu("🪨 CONSTRUIR CUEVA NATURAL DE ROCOSA")]
        public void ConstruirCuevaNatural()
        {
            // Limpiar rocas hijas previas
            List<GameObject> hijosABorrar = new List<GameObject>();
            foreach (Transform child in transform)
            {
                hijosABorrar.Add(child.gameObject);
            }
            foreach (var h in hijosABorrar)
            {
                if (Application.isEditor && !Application.isPlaying)
                    DestroyImmediate(h);
                else
                    Destroy(h);
            }

            if (prefabRocaCliffB == null && prefabRocaCliffA == null)
            {
                Debug.LogWarning("Por favor asigna al menos un Prefab de Roca (ej. Seabed Fractured Cliff B).");
                return;
            }

            List<GameObject> listaPrefabs = new List<GameObject>();
            if (prefabRocaCliffB != null) listaPrefabs.Add(prefabRocaCliffB);
            if (prefabRocaCliffA != null) listaPrefabs.Add(prefabRocaCliffA);
            if (prefabRocaBoulder != null) listaPrefabs.Add(prefabRocaBoulder);

            Random.InitState(1337); // Semilla fija para consistencia visual

            for (int z = 0; z < seccionesLongitudinales; z++)
            {
                float tZ = (float)z / (seccionesLongitudinales - 1);
                float posZ = tZ * longitudTunel;

                // Centro curvo de la cueva
                float offsetX = Mathf.Sin(tZ * Mathf.PI) * curvaturaX;
                float offsetY = Mathf.Sin(tZ * Mathf.PI) * curvaturaY;
                Vector3 centroAnillo = new Vector3(offsetX, offsetY, posZ);

                for (int r = 0; r < rocasPorAnillo; r++)
                {
                    float tR = (float)r / rocasPorAnillo;
                    float anguloBase = tR * Mathf.PI * 2f;

                    // Desplazamiento radial orgánico (cueva irregular, no círculo perfecto)
                    float radioNoise = radioCueva + (Mathf.PerlinNoise(tR * 4f, tZ * 3f) - 0.5f) * 2.8f;

                    Vector3 dirRad = new Vector3(Mathf.Cos(anguloBase), Mathf.Sin(anguloBase), 0f);
                    Vector3 posRoca = centroAnillo + dirRad * radioNoise;

                    // Añadir variación aleatoria (jitter)
                    posRoca += new Vector3(
                        Random.Range(-jitterPosicion, jitterPosicion),
                        Random.Range(-jitterPosicion, jitterPosicion),
                        Random.Range(-jitterPosicion, jitterPosicion)
                    );

                    // Seleccionar un prefab de roca al azar
                    GameObject prefabEleccion = listaPrefabs[Random.Range(0, listaPrefabs.Count)];
                    GameObject instancia = Instantiate(prefabEleccion, transform);
                    instancia.name = $"Roca_Cueva_Z{z}_R{r}";
                    instancia.transform.localPosition = posRoca;

                    // Apuntar la cara plana/frontal de la roca hacia el interior de la cueva
                    Quaternion rotBase = Quaternion.LookRotation(-dirRad, Vector3.up);
                    Quaternion rotOffset = Quaternion.Euler(
                        Random.Range(-rotacionAleatoria, rotacionAleatoria),
                        Random.Range(-rotacionAleatoria, rotacionAleatoria),
                        Random.Range(-rotacionAleatoria, rotacionAleatoria)
                    );
                    instancia.transform.localRotation = rotBase * rotOffset;

                    // Escala orgánica
                    float factorEscala = Random.Range(escalaMinRoca, escalaMaxRoca);
                    instancia.transform.localScale = new Vector3(factorEscala, factorEscala, factorEscala);

                    // Asignar material PBR si está especificado
                    if (materialRocaPBR != null)
                    {
                        foreach (var mr in instancia.GetComponentsInChildren<MeshRenderer>())
                        {
                            Material[] mats = new Material[mr.sharedMaterials.Length];
                            for (int i = 0; i < mats.Length; i++) mats[i] = materialRocaPBR;
                            mr.sharedMaterials = mats;
                        }
                    }
                }
            }

            Debug.Log($"✅ Cueva natural de roca construida con {transform.childCount} módulos rocosos orgánicos.");
        }
    }
}
