using UnityEngine;

namespace Anadromo.AI
{
    public class FishBoid : MonoBehaviour
    {
        private FlockManager manager;
        public float speed;
        private Vector3 targetDirection;
        private bool isFleeing = false;
        private bool hasArrived;
        
        // Animación de flotar en menú
        private float floatOffset;
        private Vector3 startPos; // Guardamos su posición inicial para que no se escapen al infinito

        public void Initialize(FlockManager flockManager)
        {
            manager = flockManager;
            hasArrived = false;
            speed = Random.Range(manager.minSpeed, manager.maxSpeed);
            targetDirection = transform.forward;
            floatOffset = Random.Range(0f, Mathf.PI * 2f); 
            startPos = transform.position; // Guardar donde nacieron
        }

        private void Update()
        {
            if (manager == null) return;
            
            // FASE 1: MENÚ (Pantalla de inicio, esperando la tecla J)
            if (!manager.isGameStarted)
            {
                // Flotan suavemente en Y y X sin alejarse de su StartPos
                float newY = startPos.y + Mathf.Sin(Time.time * manager.menuFloatSpeed + floatOffset) * manager.menuFloatAmplitude;
                float newX = startPos.x + Mathf.Cos(Time.time * (manager.menuFloatSpeed * 0.5f) + floatOffset) * (manager.menuFloatAmplitude * 0.3f);
                
                transform.position = new Vector3(newX, newY, startPos.z);

                // Rotan un poco de izquierda a derecha para verse vivos
                float rotY = Mathf.Sin(Time.time * manager.menuFloatSpeed + floatOffset) * manager.menuRotationSway;
                transform.rotation = Quaternion.Euler(0, rotY, 0); 
                
                return; // Cortamos el código aquí. No avanzan.
            }

            // FASE 2: MIGRACIÓN (Ya le diste a J, pero aún no llegan a la zona)
            if (!hasArrived && manager.krillZoneTarget != null)
            {
                hasArrived = Vector3.Distance(transform.position, manager.krillZoneTarget.position) <= manager.migrationStopDistance;
            }

            if (!hasArrived && manager.krillZoneTarget != null)
            {
                // Nadan directo hacia el objetivo
                targetDirection = (manager.krillZoneTarget.position - transform.position).normalized;
                
                // Rotar
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDirection), manager.rotationSpeed * Time.deltaTime);
                
                // Moverse
                transform.Translate(0, 0, Time.deltaTime * speed);
                return; // Cortamos aquí. Aún no se aplican las reglas de cardumen
            }

            // FASE 3: ZONA DE CORALES / FEEDING
            // A partir de aquí, aplican sus reglas normales de cardumen y huyen
            isFleeing = false;
            
            // A. Evasión del jugador
            if (manager.player != null)
            {
                float distToPlayer = Vector3.Distance(transform.position, manager.player.position);
                if (distToPlayer < manager.fleeDistance)
                {
                    Vector3 fleeDir = transform.position - manager.player.position;
                    fleeDir.y += Random.Range(-1f, 1f); 
                    targetDirection = fleeDir.normalized; 
                    speed = manager.maxSpeed * 3f; 
                    isFleeing = true;
                }
            }

            // B. Reglas de Banco
            if (!isFleeing)
            {
                // La velocidad de huida no debe persistir al perder al jugador.
                speed = Mathf.Clamp(speed, manager.minSpeed, manager.maxSpeed);
                // Centramos la "pecera invisible" en la zona de los krills
                Vector3 coralZoneCenter = manager.krillZoneTarget != null ? manager.krillZoneTarget.position : manager.transform.position;
                Bounds b = new Bounds(coralZoneCenter, manager.swimLimits * 2);
                
                if (!b.Contains(transform.position))
                {
                    targetDirection = coralZoneCenter - transform.position;
                }
                else
                {
                    ApplyFlockingRules(); 
                }
            }

            // Rotar y Moverse en Fase 3
            float currentRotSpeed = isFleeing ? manager.rotationSpeed * 5f : manager.rotationSpeed;
            if (targetDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDirection), currentRotSpeed * Time.deltaTime);
            }
            transform.Translate(0, 0, Time.deltaTime * speed);
        }

        private void ApplyFlockingRules()
        {
            if (Random.Range(0, 100) > 15) return; 

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
