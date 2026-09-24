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

        [Header("Detección del jugador (opcional)")]
        public bool scareFromPlayer;
        [Tooltip("Cámara o posición del jugador que debe asustar a los krils.")]
        public Transform player;
        [Min(0.01f)] public float playerScareDistance = 1f;

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
            if (scareFromPlayer && player != null && player.gameObject.activeInHierarchy)
            {
                float radius = Mathf.Max(0.01f, playerScareDistance);
                foreach (var krill in swimGroup.active)
                {
                    if (krill.transform == null || !krill.transform.gameObject.activeInHierarchy) continue;
                    if ((player.position - krill.transform.position).sqrMagnitude > radius * radius) continue;
                    TriggerFlee("Jugador");
                    return;
                }
            }

            float sqrScareDistance = scareDistance * scareDistance;

            // En lugar de usar Physics.OverlapSphere que puede fallar por capas o triggers,
            // comprobamos la distancia contra todos los miembros activos de los grupos de nado (salmones).
            foreach (SwimGroupController group in SwimGroupController.enabledControllers)
            {
                // Solo nos asustamos de los grupos que sean Salmones o depredadores
                if (group == swimGroup) continue;
                
                bool isPredator = group.CompareTag("Depredator_Salmon") || 
                                  group.GetComponent<BoxObjectSpawner>().spawnedTag == "Depredator_Salmon";
                                  
                if (!isPredator) continue;

                foreach (SwimGroupController.Member member in group.active)
                {
                    if (member.transform == null || !member.transform.gameObject.activeInHierarchy) continue;
                    
                    // Detect proximity to living krill, not the fixed spawn-box pivot.
                    foreach (var krill in swimGroup.active)
                    {
                        if (krill.transform == null || !krill.transform.gameObject.activeInHierarchy) continue;
                        float sqrDist = (member.transform.position - krill.transform.position).sqrMagnitude;
                        if (sqrDist > sqrScareDistance) continue;
                        TriggerFlee();
                        return;
                    }
                }
            }
            
        }

        void OnDestroy()
        {
            if (fleeTarget != null) Destroy(fleeTarget.gameObject);
        }

        void TriggerFlee(string threat = "Salmón")
        {
            isScared = true;
            
            // Creamos un objetivo temporal en la posición relativa (Z=15)
            GameObject targetObj = new GameObject(gameObject.name + "_FleeTarget");
            
            // TransformPoint convierte el offset local (Z=15) en coordenadas del mundo
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (var krill in swimGroup.active)
                if (krill.transform != null && krill.transform.gameObject.activeInHierarchy)
                { center += krill.transform.position; count++; }
            // Distances in metres; spawn-box scale must not multiply the flee offset.
            targetObj.transform.position = (count > 0 ? center / count : transform.position)
                + transform.TransformDirection(fleeOffset);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(targetObj, gameObject.scene); 
            fleeTarget = targetObj.transform;

            // Configuramos el SwimGroupController para que huya a ese punto y se quede ahí
            swimGroup.FleeTo(fleeTarget);
            
            Debug.Log($"[{gameObject.name}] ¡{threat} detectado junto a un kril! Destino de huida: {fleeTarget.position}");
        }
    }
}
