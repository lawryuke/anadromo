using UnityEngine;
using Anadromo.Systems; // Para acceder a la vida y control del jugador

namespace Anadromo.AI
{
    [AddComponentMenu("Anadromo/AI/Shark Instakill Cinematic")]
    public class Esc2SharkInstakill : MonoBehaviour
    {
        [Header("Configuración del Ataque")]
        [Tooltip("El punto donde aparecerá el tiburón (ej. oculto en la oscuridad del túnel)")]
        public Transform spawnPoint;
        
        [Tooltip("Qué tan rápido nada hacia el jugador")]
        public float chargeSpeed = 18f;
        
        [Tooltip("Distancia a la que agarra al jugador, fuerza la cámara y lo mata")]
        public float eatDistance = 3.5f;

        [Header("Visuales (Opcional)")]
        [Tooltip("El modelo del tiburón para mantenerlo invisible hasta que ataque")]
        public GameObject sharkMesh;

        private bool isCharging = false;
        private bool hasKilled = false;
        private PlayerEnergyController player;
        private Camera playerCamera;

        void Start()
        {
            // Ocultar al tiburón al iniciar el nivel
            if (sharkMesh != null) sharkMesh.SetActive(false);
            
            // Buscar al jugador automáticamente
            player = FindAnyObjectByType<PlayerEnergyController>();
            if (player != null)
            {
                playerCamera = player.GetComponentInChildren<Camera>();
            }
        }

        // Esta es la función que conectarás en tu AntiBacktrackTunnel
        public void TriggerInstakill()
        {
            if (isCharging || hasKilled || player == null) return;

            // Teletransportar al tiburón al punto de spawn
            if (spawnPoint != null)
            {
                transform.position = spawnPoint.position;
                transform.rotation = spawnPoint.rotation;
            }

            // Hacer visible al tiburón
            if (sharkMesh != null) sharkMesh.SetActive(true);
            
            isCharging = true;
        }

        void Update()
        {
            if (!isCharging || player == null || hasKilled) return;

            // Determinar a qué altura atacar (preferiblemente apuntar a la cámara/cabeza)
            Vector3 targetPos = playerCamera != null ? playerCamera.transform.position : player.transform.position;
            
            // Mirar hacia el jugador
            Vector3 direction = (targetPos - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                // Giro rápido y agresivo hacia el jugador
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 8f);
            }

            // Nadar hacia el jugador
            transform.position = Vector3.MoveTowards(transform.position, targetPos, chargeSpeed * Time.deltaTime);

            // Comprobar si ya está lo suficientemente cerca para comérselo
            if (Vector3.Distance(transform.position, targetPos) <= eatDistance)
            {
                ExecuteCinematicDeath();
            }
        }

        private void ExecuteCinematicDeath()
        {
            hasKilled = true;
            isCharging = false; // Se detiene frente a la cámara

            // 1. Forzar al jugador a mirar al tiburón
            Vector3 directionToShark = (transform.position - player.transform.position).normalized;
            // Bloqueamos el eje Y para que el jugador no rote la cabeza de forma extraña en VR
            directionToShark.y = 0; 
            
            if (directionToShark != Vector3.zero)
            {
                player.transform.rotation = Quaternion.LookRotation(directionToShark);
            }

            // 2. Matar al jugador instantáneamente
            player.TakeDamage(9999f);
        }
    }
}
