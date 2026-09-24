using UnityEngine;
using UnityEngine.UI;
using Anadromo.Locomotion;

namespace Anadromo.AI
{
    public class PredatorController : MonoBehaviour
    {
        public enum PredatorState { Idle, Chase, Inspect, Return }
        
        [Header("Settings")]
        public float detectionRadius = 15f;
        public float minChaseSpeed = 1.5f; // Velocidad cuando está lejos (acecho)
        public float maxChaseSpeed = 8f;   // Velocidad cuando está cerca (ataque)
        public float returnSpeed = 3f;
        public float inspectDuration = 2f;
        
        [Header("References")]
        public Transform player;
        public GameObject deathScreenUI; // Canvas con texto "MORISTE"
        public AudioClip predatorSound;
        
        private AudioSource audioSource;
        private PredatorState currentState = PredatorState.Idle;
        private Vector3 startPosition;
        private float inspectTimer = 0f;
        private PlayerTestController playerController;

        void Start()
        {
            startPosition = transform.position;
            
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f; // Sonido 3D
                audioSource.maxDistance = detectionRadius;
            }

            if (player != null)
            {
                playerController = player.GetComponent<PlayerTestController>();
            }
            if (deathScreenUI != null) deathScreenUI.SetActive(false);
        }

        void Update()
        {
            if (player == null || playerController == null) return;

            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            bool playerInRadius = distanceToPlayer <= detectionRadius;
            bool isPlayerMoving = playerController.IsMoving;

            switch (currentState)
            {
                case PredatorState.Idle:
                    if (playerInRadius && isPlayerMoving)
                    {
                        ChangeState(PredatorState.Chase);
                    }
                    break;

                case PredatorState.Chase:
                    if (!isPlayerMoving)
                    {
                        ChangeState(PredatorState.Inspect);
                    }
                    else if (!playerInRadius)
                    {
                        ChangeState(PredatorState.Inspect); 
                    }
                    else
                    {
                        // Mirar al jugador
                        transform.LookAt(new Vector3(player.position.x, transform.position.y, player.position.z));
                        
                        // Calcular velocidad: más cerca = más rápido
                        // Inverse lerp: 0 cuando está en el límite del radio, 1 cuando la distancia es 0
                        float speedT = 1f - Mathf.Clamp01(distanceToPlayer / detectionRadius);
                        float currentSpeed = Mathf.Lerp(minChaseSpeed, maxChaseSpeed, speedT);
                        
                        transform.position = Vector3.MoveTowards(transform.position, player.position, currentSpeed * Time.deltaTime);
                    }
                    break;

                case PredatorState.Inspect:
                    if (playerInRadius && isPlayerMoving)
                    {
                        ChangeState(PredatorState.Chase);
                        break;
                    }
                    
                    inspectTimer -= Time.deltaTime;
                    
                    if (inspectTimer <= 0)
                    {
                        ChangeState(PredatorState.Return);
                    }
                    break;

                case PredatorState.Return:
                    if (playerInRadius && isPlayerMoving)
                    {
                        ChangeState(PredatorState.Chase);
                        break;
                    }
                    
                    transform.position = Vector3.MoveTowards(transform.position, startPosition, returnSpeed * Time.deltaTime);
                    transform.LookAt(new Vector3(startPosition.x, transform.position.y, startPosition.z));

                    if (Vector3.Distance(transform.position, startPosition) < 0.1f)
                    {
                        ChangeState(PredatorState.Idle);
                    }
                    break;
            }
        }

        private void ChangeState(PredatorState newState)
        {
            // Reproducir sonido de alerta si el depredador empieza a perseguirnos
            if (newState == PredatorState.Chase && currentState != PredatorState.Chase)
            {
                if (predatorSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(predatorSound);
                }
            }

            currentState = newState;
            
            if (newState == PredatorState.Inspect)
            {
                inspectTimer = inspectDuration;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.transform == player)
            {
                KillPlayer();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.transform == player)
            {
                KillPlayer();
            }
        }

        private void KillPlayer()
        {
            if (deathScreenUI != null)
            {
                deathScreenUI.SetActive(true);
            }
            Time.timeScale = 0f; // Detener el tiempo
        }

        // Dibuja el radio en el editor para visualización
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
