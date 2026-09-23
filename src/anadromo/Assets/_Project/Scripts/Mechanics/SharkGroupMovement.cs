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
    [Header("Nado natural")]
    public Shader swimShader;
    [Min(.1f)] public float aceleracion = 1.8f;
    [Range(0, 1)] public float variacionVelocidad = .12f;
    [Range(0, 1)] public float amplitudTrayectoria = .28f;
    [Tooltip("Tiempo mínimo de espera antes de que salga el siguiente tiburón")]
    public float delayMinimo = 0f;
    [Tooltip("Tiempo máximo de espera antes de que salga el siguiente tiburón")]
    public float delayMaximo = 1.5f;

    private bool animacionIniciada = false;

    // Clase interna para guardar el estado individual de cada tiburón
    private class SharkData
    {
        public Vector3 posFin;
        public NaturalSwimPath movement;
        public float speed;
    }

    private List<SharkData> sharks = new List<SharkData>();

    void Start()
    {
        // Calculamos cuánto se debe mover en total basándonos en tus puntos
        Vector3 desplazamiento = puntoFin - puntoInicio;

        // Recopilamos todos los tiburones hijos manteniendo su formación original
        foreach (Transform child in transform)
        {
            var movement = child.GetComponent<NaturalSwimPath>();
            if (!movement) movement = child.gameObject.AddComponent<NaturalSwimPath>();
            movement.acceleration = aceleracion;
            movement.courseWidth = amplitudTrayectoria;
            movement.turnSpeed = 42f;
            movement.maxBankAngle = 20f;
            movement.orientToCourse = true;

            var animation = child.GetComponent<SharkSwimAnimation>();
            if (!animation) animation = child.gameObject.AddComponent<SharkSwimAnimation>();
            animation.Configure(swimShader ? swimShader : Shader.Find("Anadromo/White Shark Natural Swim"));
            
            sharks.Add(new SharkData { 
                posFin = child.position + desplazamiento, // Calcula hasta dónde debe llegar
                movement = movement,
                speed = velocidad * Random.Range(1 - variacionVelocidad, 1 + variacionVelocidad)
            });
        }
    }

    public void IniciarGrupo()
    {
        if (animacionIniciada) return;
        animacionIniciada = true;
        StartCoroutine(LanzarTiburonesAleatoriamente());
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
            if (shark.movement) shark.movement.Begin(shark.posFin, shark.speed);
            
            // Elegir un tiempo de espera aleatorio entre delayMinimo y delayMaximo
            float delay = Random.Range(delayMinimo, delayMaximo);
            yield return new WaitForSeconds(delay);
        }
    }
}
