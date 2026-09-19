using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SharkGroupMovement : MonoBehaviour
{
    [Header("Puntos de Referencia (Para calcular la distancia)")]
    public Vector3 puntoInicio = new Vector3(0f, 0f, 0f);
    public Vector3 puntoFin = new Vector3(0f, 42f, 0f);

    [Header("Control de Velocidad y Retraso")]
    public float velocidad = 6f;
    [Tooltip("Tiempo mínimo de espera antes de que salga el siguiente tiburón")]
    public float delayMinimo = 0f;
    [Tooltip("Tiempo máximo de espera antes de que salga el siguiente tiburón")]
    public float delayMaximo = 1.5f;

    [Header("Control de Teclado")]
    [Tooltip("La tecla que debes presionar para que este grupo empiece a salir")]
    public KeyCode teclaParaIniciar = KeyCode.Alpha4;

    private bool animacionIniciada = false;

    // Clase interna para guardar el estado individual de cada tiburón
    private class SharkData
    {
        public Transform transform;
        public Vector3 posInicio;
        public Vector3 posFin;
        public bool isMoving;
    }

    private List<SharkData> sharks = new List<SharkData>();

    void Start()
    {
        // Calculamos cuánto se debe mover en total basándonos en tus puntos
        Vector3 desplazamiento = puntoFin - puntoInicio;

        // Recopilamos todos los tiburones hijos manteniendo su formación original
        foreach (Transform child in transform)
        {
            sharks.Add(new SharkData { 
                transform = child, 
                posInicio = child.position, // Guarda su posición original en la formación
                posFin = child.position + desplazamiento, // Calcula hasta dónde debe llegar
                isMoving = false
            });
        }
    }

    void Update()
    {
        // Esperar a que se presione la tecla configurada
        if (!animacionIniciada && Input.GetKeyDown(teclaParaIniciar))
        {
            animacionIniciada = true;
            StartCoroutine(LanzarTiburonesAleatoriamente());
        }

        // Mover individualmente a los tiburones que ya recibieron la orden de salir
        foreach (var shark in sharks)
        {
            if (shark.isMoving)
            {
                // Mover el tiburón hacia el punto objetivo final (arriba)
                shark.transform.position = Vector3.MoveTowards(shark.transform.position, shark.posFin, velocidad * Time.deltaTime);

                // Comprobar si ya llegó al punto
                if (Vector3.Distance(shark.transform.position, shark.posFin) < 0.01f)
                {
                    // Se detiene al llegar arriba
                    shark.isMoving = false; 
                }
            }
        }
    }

    IEnumerator LanzarTiburonesAleatoriamente()
    {
        // Crear una copia de la lista para desordenarla (aleatoriedad) sin perder la original
        List<SharkData> tiburonesPorLanzar = new List<SharkData>(sharks);
        
        // Desordenar la lista (Algoritmo Fisher-Yates shuffle)
        for (int i = 0; i < tiburonesPorLanzar.Count; i++)
        {
            SharkData temp = tiburonesPorLanzar[i];
            int randomIndex = Random.Range(i, tiburonesPorLanzar.Count);
            tiburonesPorLanzar[i] = tiburonesPorLanzar[randomIndex];
            tiburonesPorLanzar[randomIndex] = temp;
        }

        // Lanzar uno por uno con un tiempo aleatorio de diferencia
        foreach (var shark in tiburonesPorLanzar)
        {
            shark.isMoving = true; // Este tiburón en específico arranca
            
            // Elegir un tiempo de espera aleatorio entre 0 y 1.5 segundos (configurable)
            float delay = Random.Range(delayMinimo, delayMaximo);
            
            // Esperar esos segundos antes de que el ciclo pase al siguiente tiburón
            yield return new WaitForSeconds(delay);
        }
    }
}
