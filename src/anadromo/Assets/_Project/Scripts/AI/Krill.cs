using UnityEngine;

namespace Anadromo.Mechanics
{
    [RequireComponent(typeof(Prey))]
    [RequireComponent(typeof(SphereCollider))]
    public class Krill : MonoBehaviour
    {
        [Header("Krill Movement")]
        [Tooltip("Si es verdadero, el krill se quedará quieto. Si es falso, flotará lentamente.")]
        public bool isStatic = false;
        
        [Tooltip("Qué tan lejos puede flotar desde su punto inicial.")]
        public float roamRadius = 1.5f;
        
        [Tooltip("Velocidad de movimiento lento.")]
        public float speed = 0.5f;

        private Vector3 startPos;
        private Vector3 targetPos;
        private SwimGroupController swimGroup;

        private void OnTransformParentChanged()
        {
            swimGroup = GetComponentInParent<SwimGroupController>();
        }

        private void Start()
        {
            swimGroup = GetComponentInParent<SwimGroupController>();
            startPos = transform.position;
            
            // Hacer que sea un trigger para que el jugador se lo pueda comer
            GetComponent<SphereCollider>().isTrigger = true;

            if (!isStatic)
            {
                PickNewTarget();
            }
        }

        private void Update()
        {
            if (swimGroup != null && swimGroup.Controls(gameObject)) return;
            if (isStatic) return;

            // Moverse lentamente hacia su objetivo
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

            // Si llegó a su destino, buscar otro punto cercano
            if (Vector3.Distance(transform.position, targetPos) < 0.1f)
            {
                PickNewTarget();
            }
        }

        private void PickNewTarget()
        {
            Vector3 randomOffset = Random.insideUnitSphere * roamRadius;
            randomOffset.y *= 0.1f; // Reducir la variación en el eje Y para que no floten mucho hacia arriba o abajo
            targetPos = startPos + randomOffset;
        }
    }
}
