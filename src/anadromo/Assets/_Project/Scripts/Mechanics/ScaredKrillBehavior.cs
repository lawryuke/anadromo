using UnityEngine;
using Anadromo.Mechanics;

namespace Anadromo.AI
{
    [RequireComponent(typeof(SwimGroupController))]
    public class ScaredKrillBehavior : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("Distancia a la que los krills detectan a los depredadores.")]
        public float scareDistance = 15f;
        
        [Tooltip("Tiempo entre comprobaciones (para rendimiento).")]
        public float checkInterval = 0.5f;

        [Header("Fleeing")]
        [Tooltip("Offset (posición relativa) a la que huirán. Z=15 significa 15 metros hacia el frente de su orientación actual.")]
        public Vector3 fleeOffset = new Vector3(0, 0, 15f);
        
        private SwimGroupController swimGroup;
        private bool isScared = false;
        private float timer = 0f;
        private Transform fleeTarget;

        void Start()
        {
            swimGroup = GetComponent<SwimGroupController>();
        }

        void Update()
        {
            if (isScared) return; // Si ya está huyendo, no hace falta comprobar más

            timer += Time.deltaTime;
            if (timer >= checkInterval)
            {
                timer = 0f;
                CheckForPredators();
            }
        }

        void CheckForPredators()
        {
            // Busca colliders en un radio alrededor del grupo
            Collider[] hits = Physics.OverlapSphere(transform.position, scareDistance);
            foreach (var hit in hits)
            {
                // Identificamos al depredador si tiene el componente PredatorEating,
                // PlayerFeeding, si tiene la etiqueta Player, o si su nombre incluye "Salmon"
                if (hit.GetComponentInParent<PredatorEating>() != null || 
                    hit.GetComponentInParent<Anadromo.Mechanics.PlayerFeeding>() != null ||
                    hit.CompareTag("Player") || 
                    hit.name.ToLower().Contains("salmon"))
                {
                    TriggerFlee();
                    break; // Solo necesitamos detectar uno para huir
                }
            }
        }

        void TriggerFlee()
        {
            isScared = true;
            
            // Creamos un objetivo temporal en la posición relativa (Z=15)
            GameObject targetObj = new GameObject(gameObject.name + "_FleeTarget");
            
            // TransformPoint convierte el offset local (Z=15) en coordenadas del mundo
            targetObj.transform.position = transform.TransformPoint(fleeOffset); 
            fleeTarget = targetObj.transform;

            // Configuramos el SwimGroupController para que huya a ese punto y se quede ahí
            swimGroup.movementTarget = fleeTarget;
            swimGroup.arrivalMode = SwimGroupController.ArrivalMode.StayNear;
            swimGroup.state = SwimGroupController.SwimState.MoveToTarget;
            
            Debug.Log($"[{gameObject.name}] ¡Depredador detectado! Huyendo hacia el abismo (Z=15): {fleeTarget.position}");
        }
    }
}
