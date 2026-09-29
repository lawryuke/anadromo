using UnityEngine;
using Anadromo.Systems; // Para acceder a la vida y control del jugador

namespace Anadromo.AI
{
    [AddComponentMenu("Anadromo/AI/Shark Instakill Cinematic")]
    public class Esc2SharkInstakill : MonoBehaviour
    {
        [Header("Configuración del Ataque")]
        [Tooltip("Opcional. Si está vacío, el tiburón aparece donde lo colocaste en la escena.")]
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
        private bool initialized;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        void Awake()
        {
            PrepareWaiting();
        }

        private void PrepareWaiting()
        {
            if (initialized) return;
            initialized = true;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            // Desactiva también colisiones y scripts, incluso sin Shark Mesh asignado.
            if (sharkMesh != null) sharkMesh.SetActive(false);
            gameObject.SetActive(false);
        }

        private void FindPlayer()
        {
            player = FindAnyObjectByType<PlayerEnergyController>();
            if (player != null)
            {
                playerCamera = player.GetComponentInChildren<Camera>();
            }
        }

        // Esta es la función que conectarás en tu AntiBacktrackTunnel
        public void TriggerInstakill()
        {
            TriggerInstakill(null);
        }

        public void TriggerInstakill(Transform spawnOverride)
        {
            if (isCharging || hasKilled) return;
            // También funciona con instancias que ya estaban inactivas antes de Awake.
            PrepareWaiting();
            if (player == null) FindPlayer();
            if (player == null || !player.HasEnergy) return;

            Transform spawn = spawnOverride != null ? spawnOverride : spawnPoint;
            Vector3 position = spawn != null ? spawn.position : initialPosition;
            Quaternion rotation = spawn != null ? spawn.rotation : initialRotation;
            transform.SetPositionAndRotation(position, rotation);

            // Hacer visible al tiburón
            isCharging = true;
            enabled = true;
            gameObject.SetActive(true);
            if (sharkMesh != null) sharkMesh.SetActive(true);
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
            
            if (directionToShark != Vector3.zero && !player.GetComponent<Unity.XR.CoreUtils.XROrigin>())
            {
                player.transform.rotation = Quaternion.LookRotation(directionToShark);
            }

            // 2. Matar al jugador instantáneamente
            player.TakeDamage(player.Maximum);
        }
    }
}
