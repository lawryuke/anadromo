using UnityEngine;

namespace Anadromo.AI
{
    public class FishBoid : MonoBehaviour
    {
        private FlockManager manager;
        public float speed;
        private Vector3 targetDirection;
        private bool isFleeing = false;

        public void Initialize(FlockManager flockManager)
        {
            manager = flockManager;
            speed = Random.Range(manager.minSpeed, manager.maxSpeed);
            targetDirection = transform.forward;
        }

        private void Update()
        {
            if (manager == null) return;
            
            isFleeing = false;
            
            // 1. EVASIÓN URGENTE (Sobrescribe todas las demás reglas, incluso los límites)
            if (manager.player != null)
            {
                float distToPlayer = Vector3.Distance(transform.position, manager.player.position);
                if (distToPlayer < manager.fleeDistance)
                {
                    Vector3 fleeDir = transform.position - manager.player.position;
                    // Agregar un poco de aleatoriedad hacia arriba/abajo para que se dispersen bien
                    fleeDir.y += Random.Range(-1f, 1f); 
                    
                    targetDirection = fleeDir.normalized; 
                    speed = manager.maxSpeed * 3f; // ¡El triple de rápido al huir!
                    isFleeing = true;
                }
            }

            // 2. Si NO huye, verifica los límites de la caja imaginaria
            if (!isFleeing)
            {
                Bounds b = new Bounds(manager.transform.position, manager.swimLimits * 2);
                if (!b.Contains(transform.position))
                {
                    // Volver al centro de la caja
                    targetDirection = manager.transform.position - transform.position;
                }
                else
                {
                    // Si está dentro de la caja y a salvo, aplica reglas de cardumen
                    ApplyFlockingRules(); 
                }
            }

            // 3. Rotar (Gira 5 VECES MÁS RÁPIDO si está huyendo)
            float currentRotSpeed = isFleeing ? manager.rotationSpeed * 5f : manager.rotationSpeed;
            
            if (targetDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(targetDirection),
                    currentRotSpeed * Time.deltaTime);
            }

            // 4. Moverse
            transform.Translate(0, 0, Time.deltaTime * speed);
        }

        private void ApplyFlockingRules()
        {
            if (Random.Range(0, 100) > 15) return; // Solo el 15% del tiempo procesa esto

            GameObject[] gos = manager.allFish;
            Vector3 vCenter = Vector3.zero; 
            Vector3 vAvoid = Vector3.zero;  
            float gSpeed = 0.01f;           
            float nDistance;
            int groupSize = 0;

            foreach (GameObject go in gos)
            {
                if (go != this.gameObject && go != null)
                {
                    nDistance = Vector3.Distance(go.transform.position, transform.position);
                    if (nDistance <= manager.neighborDistance)
                    {
                        vCenter += go.transform.position;
                        groupSize++;
                        
                        if (nDistance < 1.0f)
                        {
                            vAvoid += (transform.position - go.transform.position);
                        }
                        
                        FishBoid anotherBoid = go.GetComponent<FishBoid>();
                        if (anotherBoid != null) gSpeed += anotherBoid.speed;
                    }
                }
            }

            if (groupSize > 0)
            {
                vCenter = vCenter / groupSize;
                speed = gSpeed / groupSize;
                if (speed > manager.maxSpeed) speed = manager.maxSpeed;
                if (speed < manager.minSpeed) speed = manager.minSpeed;

                targetDirection = (vCenter + vAvoid) - transform.position;
            }
        }
    }
}
