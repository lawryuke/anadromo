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

        [Header("Prefabs de Corales y Algas")]
        public GameObject prefabCoralRojo;
        public GameObject prefabAbanicoMar;

        [Header("Materiales Abisales y de Arrecife")]
        public Material materialRocaPBR;
        public Material materialPisoArrecife; // Shader mate difuso de arena/alga (sin brillo espejo)
        public Material materialAlgasAnimadas; // Algas ondulantes

        [Header("Configuración de la Cueva / Túnel de Piedras")]
        [Range(10f, 150f)] public float longitudTunel = 60f;
        [Range(5f, 18f)] public float radioCueva = 8.5f;
        [Range(4, 24)] public int seccionesLongitudinales = 12;
        [Range(6, 16)] public int rocasPorAnillo = 10;

        [Header("Cierre de Cueva & Jardín de Arrecife")]
        public bool cerrarFondo = true;
        public bool generarPisoDeRoca = true;

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
            // Limpiar rocas e hijas previas
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

                    // Desplazamiento radial orgánico (cueva irregular)
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

            // Generar pared ciego de roca en el fondo para cerrar la cueva
            if (cerrarFondo)
            {
                float posZFin = longitudTunel;
                float offsetXFin = Mathf.Sin(Mathf.PI) * curvaturaX;
                float offsetYFin = Mathf.Sin(Mathf.PI) * curvaturaY;
                Vector3 centroFin = new Vector3(offsetXFin, offsetYFin, posZFin);

                int rocasFondoCount = Mathf.CeilToInt(rocasPorAnillo * 1.5f);
                for (int i = 0; i < rocasFondoCount; i++)
                {
                    float distRadial = Random.Range(0f, radioCueva * 0.95f);
                    float angulo = Random.Range(0f, Mathf.PI * 2f);

                    Vector3 posFondo = centroFin + new Vector3(
                        Mathf.Cos(angulo) * distRadial + Random.Range(-1f, 1f),
                        Mathf.Sin(angulo) * distRadial + Random.Range(-1f, 1f),
                        Random.Range(0f, 3f)
                    );

                    GameObject prefabFondo = listaPrefabs[Random.Range(0, listaPrefabs.Count)];
                    GameObject instanciaFondo = Instantiate(prefabFondo, transform);
                    instanciaFondo.name = $"Roca_Fondo_Cueva_{i}";
                    instanciaFondo.transform.localPosition = posFondo;
                    instanciaFondo.transform.localRotation = Quaternion.Euler(
                        Random.Range(-180f, 180f),
                        Random.Range(-180f, 180f),
                        Random.Range(-180f, 180f)
                    );
                    float escala = Random.Range(escalaMinRoca * 1.2f, escalaMaxRoca * 1.6f);
                    instanciaFondo.transform.localScale = new Vector3(escala, escala, escala);

                    if (materialRocaPBR != null)
                    {
                        foreach (var mr in instanciaFondo.GetComponentsInChildren<MeshRenderer>())
                        {
                            Material[] mats = new Material[mr.sharedMaterials.Length];
                            for (int k = 0; k < mats.Length; k++) mats[k] = materialRocaPBR;
                            mr.sharedMaterials = mats;
                        }
                    }
                }
            }

            // Generar piso lecho de arrecife rocoso con algas y corales (sin brillo de espejo)
            if (generarPisoDeRoca)
            {
                Material matPiso = materialPisoArrecife != null ? materialPisoArrecife : materialRocaPBR;
                int bloquesPiso = 18;

                for (int i = 0; i < bloquesPiso; i++)
                {
                    float t = (float)i / (bloquesPiso - 1);
                    float posZ = t * (longitudTunel + 20f) - 10f;
                    float offsetX = Mathf.Sin(t * Mathf.PI) * curvaturaX;

                    Vector3 posPiso = new Vector3(
                        offsetX + Random.Range(-radioCueva * 1.1f, radioCueva * 1.1f),
                        -radioCueva * 0.95f + Random.Range(-0.6f, 0.3f),
                        posZ
                    );

                    GameObject prefabPiso = listaPrefabs[Random.Range(0, listaPrefabs.Count)];
                    GameObject instanciaPiso = Instantiate(prefabPiso, transform);
                    instanciaPiso.name = $"Roca_Piso_Arrecife_{i}";
                    instanciaPiso.transform.localPosition = posPiso;
                    instanciaPiso.transform.localRotation = Quaternion.Euler(
                        Random.Range(-10f, 10f),
                        Random.Range(-180f, 180f),
                        Random.Range(-10f, 10f)
                    );
                    float escala = Random.Range(escalaMinRoca * 1.3f, escalaMaxRoca * 1.8f);
                    instanciaPiso.transform.localScale = new Vector3(escala * 1.4f, escala * 0.45f, escala * 1.4f);

                    // Aplicar material de arrecife sin reflejo de espejo
                    if (matPiso != null)
                    {
                        foreach (var mr in instanciaPiso.GetComponentsInChildren<MeshRenderer>())
                        {
                            Material[] mats = new Material[mr.sharedMaterials.Length];
                            for (int k = 0; k < mats.Length; k++) mats[k] = matPiso;
                            mr.sharedMaterials = mats;
                        }
                    }

                    // Esparcir corales en el piso
                    if (i % 2 == 0 && (prefabCoralRojo != null || prefabAbanicoMar != null))
                    {
                        GameObject prefabCoral = (i % 4 == 0 && prefabAbanicoMar != null) ? prefabAbanicoMar : prefabCoralRojo;
                        if (prefabCoral != null)
                        {
                            GameObject coral = Instantiate(prefabCoral, transform);
                            coral.name = $"Coral_Arrecife_{i}";
                            coral.transform.localPosition = posPiso + new Vector3(Random.Range(-1.2f, 1.2f), 0.4f, Random.Range(-1.2f, 1.2f));
                            coral.transform.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
                            float scaleCoral = Random.Range(0.6f, 1.2f);
                            coral.transform.localScale = new Vector3(scaleCoral, scaleCoral, scaleCoral);
                        }
                    }
                }
            }

            Debug.Log($"✅ Cueva natural y lecho de arrecife marino con algas y corales construida con {transform.childCount} módulos orgánicos.");
        }
    }
}
