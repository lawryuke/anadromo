using UnityEngine;

namespace Anadromo.AI
{
    public class FlockManager : MonoBehaviour
    {
        [Header("Flock Settings")]
        public GameObject fishPrefab;
        public int numFish = 50;
        public Vector3 swimLimits = new Vector3(15, 5, 15);

        [Header("Game Flow")]
        public bool isGameStarted = false;
        public float migrationZTarget = 120f;
        public MonoBehaviour playerMovementScript;

        [Header("Menu Phase (Inicio)")]
        [Tooltip("Velocidad a la que flotan en el menú")]
        public float menuFloatSpeed = 1.5f;
        [Tooltip("Qué tanto suben y bajan (amplitud)")]
        public float menuFloatAmplitude = 0.5f;
        [Tooltip("Qué tanto giran hacia los lados mientras flotan")]
        public float menuRotationSway = 15f;

        [Header("Boid Rules")]
        [Range(0.0f, 5.0f)] public float minSpeed = 1f;
        [Range(0.0f, 10.0f)] public float maxSpeed = 3f;
        [Range(1.0f, 10.0f)] public float neighborDistance = 3f;
        [Range(1.0f, 5.0f)] public float rotationSpeed = 2f;

        [Header("Player Avoidance")]
        public Transform player; 
        public float fleeDistance = 5f;

        public GameObject[] allFish { get; private set; }

        private void Start()
        {
            if (playerMovementScript != null) playerMovementScript.enabled = false; // Bloquear jugador al inicio

            allFish = new GameObject[numFish];
            for (int i = 0; i < numFish; i++)
            {
                // Posición aleatoria dentro de los límites
                Vector3 pos = transform.position + new Vector3(
                    Random.Range(-swimLimits.x, swimLimits.x),
                    Random.Range(-swimLimits.y, swimLimits.y),
                    Random.Range(-swimLimits.z, swimLimits.z));
                
                allFish[i] = Instantiate(fishPrefab, pos, Quaternion.identity);
                
                // Asignar este manager al pececito
                FishBoid boid = allFish[i].GetComponent<FishBoid>();
                if (boid != null)
                {
                    boid.Initialize(this);
                }
            }
        }

        private void Update()
        {
            if (!isGameStarted && UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.jKey.wasPressedThisFrame)
                {
                    isGameStarted = true;
                    if (playerMovementScript != null) playerMovementScript.enabled = true; // Desbloqueamos al jugador
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Dibuja una caja azul en el editor para ver los límites donde nadarán
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.DrawCube(transform.position, swimLimits * 2);
        }
    }
}
