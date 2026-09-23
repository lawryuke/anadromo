using UnityEngine;

namespace Anadromo.Mechanics
{
    [AddComponentMenu("Anadromo/Efectos/Luz Viajera")]
    public class LuzViajera : MonoBehaviour
    {
        [Header("Recorrido de la Luz")]
        [Tooltip("Arrastra aquí los objetos vacíos que servirán como puntos del camino.")]
        public Transform[] waypoints;
        
        [Tooltip("Velocidad a la que se mueve la luz.")]
        public float speed = 2f;
        
        [Tooltip("¿Vuelve a empezar desde el principio cuando llega al final?")]
        public bool loop = true;

        private int currentWaypointIndex = 0;

        void Start()
        {
            // Si hay puntos, teletransportar la luz al primer punto al inicio
            if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
            {
                transform.position = waypoints[0].position;
            }
        }

        void Update()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            Transform target = waypoints[currentWaypointIndex];
            if (target == null) return;
            
            // Moverse lentamente hacia el punto objetivo
            transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

            // Si está muy cerca del punto, pasamos al siguiente
            if (Vector3.Distance(transform.position, target.position) < 0.1f)
            {
                currentWaypointIndex++;
                
                // Si llegamos al final del arreglo de puntos
                if (currentWaypointIndex >= waypoints.Length)
                {
                    if (loop) 
                    {
                        currentWaypointIndex = 0;
                        // Opcional: Descomenta la siguiente línea si quieres que se teletransporte al inicio en lugar de devolverse volando.
                        // transform.position = waypoints[0].position; 
                    }
                    else 
                    {
                        enabled = false; // Detener el script para que se quede quieta
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            // Dibuja una línea en el editor para que veas el camino de la luz
            if (waypoints == null || waypoints.Length < 2) return;
            
            Gizmos.color = Color.yellow;
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i+1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i+1].position);
                }
            }
            
            if (loop && waypoints[0] != null && waypoints[waypoints.Length - 1] != null)
            {
                Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
            }
        }
    }
}
