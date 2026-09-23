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
        private float menuTimer = 0f;
        private Vector3 menuTargetPos;

        public void Initialize(FlockManager flockManager)
        {
            manager = flockManager;
            hasArrived = false;
            speed = Random.Range(manager.minSpeed, manager.maxSpeed);
            targetDirection = transform.forward;
            floatOffset = Random.Range(0f, Mathf.PI * 2f); 
            startPos = transform.position; // Guardar donde nacieron
            menuTargetPos = startPos;
            menuTimer = Random.Range(0.5f, 1.5f);
        }

        private void Update()
        {
            if (manager == null) return;
            
            // FASE 1: MENÚ (Pantalla de inicio, esperando la tecla J)
            if (!manager.isGameStarted)
            {
                menuTimer -= Time.deltaTime;
                if (menuTimer <= 0f)
                {
                    // Reiniciar tiempo de espera entre 0.5 y 1.5 segundos
                    menuTimer = Random.Range(0.5f, 1.5f);

                    // Elegir moverse 0, 30cm adelante (0.3f), o 30cm atrás (-0.3f)
                    float[] choices = { -0.3f, 0f, 0.3f };
                    float zOffset = choices[Random.Range(0, choices.Length)];
                    float yOffset = choices[Random.Range(0, choices.Length)];

                    // Si por azar ambos son 0, forzar que se mueva en algún eje para que no se quede quieto
                    if (zOffset == 0f && yOffset == 0f)
                    {
                        zOffset = 0.3f;
                    }

                    // Calcular la nueva posición objetivo relativa a la posición original (startPos)
                    // tomando en cuenta su rotación inicial para que Z sea adelante/atrás y Y arriba/abajo.
                    menuTargetPos = startPos + transform.forward * zOffset + transform.up * yOffset;
                }

                // Interpolar suavemente hacia la nueva posición elegida
                transform.position = Vector3.Lerp(transform.position, menuTargetPos, Time.deltaTime * 1.5f);

                // Rotan un poco de izquierda a derecha para verse vivos (mantenemos esto opcional)
                float rotY = Mathf.Sin(Time.time * manager.menuFloatSpeed + floatOffset) * manager.menuRotationSway;
                transform.rotation = Quaternion.Euler(0, rotY, 0); 
                
                return; // Cortamos el código aquí. No avanzan.
            }

            transform.position += Anadromo.Act1.OceanEnvironment.CurrentAt(transform.position) * Time.deltaTime;

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
                ApplyAntiStuckBounce();
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
            ApplyAntiStuckBounce();
        }

        private void ApplyAntiStuckBounce()
        {
            if (manager == null || manager.allFish == null) return;
            
            foreach (GameObject go in manager.allFish)
            {
                if (go != this.gameObject && go != null)
                {
                    float dist = Vector3.Distance(transform.position, go.transform.position);
                    // Si están muy cerca (menos de 0.6 unidades), aplicamos el rebote
                    if (dist > 0.001f && dist < 0.6f) 
                    {
                        Vector3 push = (transform.position - go.transform.position).normalized;
                        // Empujamos suavemente hacia afuera para separarlos y evitar que se atasquen
                        transform.position += push * Time.deltaTime * (manager.maxSpeed * 0.5f);
                    }
                }
            }
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
