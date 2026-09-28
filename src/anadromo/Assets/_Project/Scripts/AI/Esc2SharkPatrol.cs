using UnityEngine;
using Anadromo.Systems; // Para PlayerEnergyController

namespace Anadromo.AI
{
    [AddComponentMenu("Anadromo/AI/Shark Patrol")]
    public class Esc2SharkPatrol : MonoBehaviour
    {
        [Header("Recorrido Infinito")]
        [Tooltip("Puntos del camino. El tiburón nadará del 0 al 1 al 2... y volverá al 0.")]
        public Transform[] waypoints;
        public float speed = 12f;
        public float rotationSpeed = 3f;

        [Header("Caja de Colisión Matemática (Hitbox)")]
        [Tooltip("Radio de grosor del tiburón")]
        public float contactRadius = 0.8f;
        [Tooltip("Largo del tiburón desde el centro hacia la cabeza/cola")]
        public float bodyHalfLength = 1.5f;

        private int currentWaypoint = 0;
        private PlayerEnergyController player;

        void Start()
        {
            player = FindAnyObjectByType<PlayerEnergyController>();

            if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
            {
                transform.position = waypoints[0].position;
            }
        }

        void Update()
        {
            if (waypoints == null || waypoints.Length < 2 || player == null || !player.HasEnergy) return;

            Transform targetPoint = waypoints[currentWaypoint];
            if (targetPoint == null) return;

            Vector3 startPos = transform.position;
            Vector3 delta = targetPoint.position - startPos;
            
            // Si llegó al punto, cambiar al siguiente (haciendo un loop de regreso al 0)
            if (delta.magnitude < 0.1f)
            {
                currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
                return;
            }

            // Rotación suave hacia el siguiente punto
            Vector3 direction = delta.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * rotationSpeed);

            // Movimiento
            float step = speed * Time.deltaTime;
            Vector3 endPos = Vector3.MoveTowards(startPos, targetPoint.position, step);
            transform.position = endPos;

            // Comprobar colisión letal usando un segmento (de la cabeza a la cola) para no requerir BoxColliders
            Vector3 sharkForward = transform.forward;
            float distToPlayer = SegmentDistance(
                player.transform.position, 
                transform.position - sharkForward * bodyHalfLength, 
                transform.position + sharkForward * bodyHalfLength
            );

            // Si el jugador toca el cuerpo del tiburón, muere
            if (distToPlayer <= contactRadius)
            {
                player.TakeDamage(9999f);
            }
        }

        // Matemática ultra optimizada para calcular la distancia de un punto a un tubo
        public static float SegmentDistance(Vector3 point, Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float t = delta.sqrMagnitude < 0.000001f ? 0 : Mathf.Clamp01(Vector3.Dot(point - from, delta) / delta.sqrMagnitude);
            return Vector3.Distance(point, from + delta * t);
        }

        void OnDrawGizmosSelected()
        {
            // Dibujar línea de recorrido
            if (waypoints != null && waypoints.Length > 1)
            {
                Gizmos.color = Color.red;
                for (int i = 0; i < waypoints.Length; i++)
                {
                    if (waypoints[i] != null)
                    {
                        Transform next = waypoints[(i + 1) % waypoints.Length];
                        if (next != null) Gizmos.DrawLine(waypoints[i].position, next.position);
                    }
                }
            }

            // Dibujar la Hitbox Letal del Tiburón en el Editor
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Vector3 forward = transform.forward;
            Vector3 p1 = transform.position - forward * bodyHalfLength;
            Vector3 p2 = transform.position + forward * bodyHalfLength;
            Gizmos.DrawWireSphere(p1, contactRadius);
            Gizmos.DrawWireSphere(p2, contactRadius);
            Gizmos.DrawLine(p1 + transform.right * contactRadius, p2 + transform.right * contactRadius);
            Gizmos.DrawLine(p1 - transform.right * contactRadius, p2 - transform.right * contactRadius);
        }
    }
}
