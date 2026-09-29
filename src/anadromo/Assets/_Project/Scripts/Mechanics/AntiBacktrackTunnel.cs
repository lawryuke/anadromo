using UnityEngine;
using UnityEngine.Events;
using Anadromo.AI;
using Anadromo.Systems; // Para encontrar al PlayerEnergyController

namespace Anadromo.Mechanics
{
    [AddComponentMenu("Anadromo/Logic/Anti Backtrack Tunnel")]
    public class AntiBacktrackTunnel : MonoBehaviour
    {
        [Header("Zonas Matemáticas (ZoneLimits)")]
        [Tooltip("Zona al FINAL del túnel. Al tocarla, el jugador ya cruzó y el túnel se bloquea para no volver.")]
        public ZoneLimit checkoutZone; 
        
        [Tooltip("Zona de entrada al túnel (viniendo de reversa). Dispara la Medusa.")]
        public ZoneLimit warningZone;  
        
        [Tooltip("Zona más PROFUNDA (de reversa). Si la toca ignorando la medusa, ataca el tiburón.")]
        public ZoneLimit killZone;     

        [Header("Castigos")]
        [Tooltip("La Medusa que asustará al jugador.")]
        public GameObject medusaWarning;

        [Header("Movimiento de la medusa")]
        [Tooltip("Puntos del recorrido en orden. La medusa aparece en el primero. Si esta vacio, usa la ruta de Luz Viajera del modelo.")]
        public Transform[] medusaWaypoints;
        [Min(0f)] public float medusaSpeed = 2f;
        [Tooltip("Grados por segundo alrededor del eje Y mientras avanza.")]
        public float medusaSpinSpeed = 180f;

        private Vector3[] medusaRoute;
        private bool medusaMoving;
        private GameObject medusaModel;
        private bool ownsMedusa;
        private int medusaWaypointIndex;
        private Vector3 previousMedusaPosition;

        [Header("Ataque del tiburon")]
        [Tooltip("Tiburón de la escena (oculto hasta atacar) o prefab con Shark Instakill Cinematic (se crea al activar Kill Zone).")]
        public Esc2SharkInstakill sharkInstakill;

        [Tooltip("Opcional. Posición y rotación de aparición. Si está vacío, usa Spawn Point del tiburón o su posición inicial.")]
        public Transform sharkSpawnPoint;
        
        [Tooltip("Evento letal (ej. Encender el GameObject de un Tiburón o llamar a un script de muerte).")]
        public UnityEvent onSharkKill;

        private int state = 0; // 0 = Abierto, 1 = Bloqueado, 2 = Advertencia, 3 = Muerto
        private Transform player;
        private Esc2SharkInstakill spawnedShark;

        void Start()
        {
            // Busca automáticamente al jugador en la escena
            var p = FindAnyObjectByType<PlayerEnergyController>();
            if (p != null) player = p.transform;
        }

        void Update()
        {
            if (player == null || state == 3) return;

            // Estado 0: El túnel está limpio. Esperando que el jugador salga hacia la nueva zona (B).
            if (state == 0 && checkoutZone != null && checkoutZone.Contains(player.position))
            {
                state = 1; 
            }
            // La zona letal funciona también si se salta la advertencia o hay espacio entre zonas.
            else if (state >= 1 && killZone != null && killZone.Contains(player.position))
            {
                state = 3;
                if (sharkInstakill != null)
                {
                    if (sharkInstakill.gameObject.scene.IsValid())
                        spawnedShark = sharkInstakill;
                    else
                    {
                        Transform spawn = sharkSpawnPoint != null ? sharkSpawnPoint : sharkInstakill.spawnPoint;
                        Vector3 position = spawn != null ? spawn.position : sharkInstakill.transform.position;
                        Quaternion rotation = spawn != null ? spawn.rotation : sharkInstakill.transform.rotation;
                        spawnedShark = Instantiate(sharkInstakill, position, rotation);
                        // Its internal spawn marker was cloned too; the instance is already positioned.
                        spawnedShark.spawnPoint = null;
                    }
                    spawnedShark.TriggerInstakill(sharkSpawnPoint);
                }
                onSharkKill?.Invoke();
            }
            // Estado 1: El túnel está bloqueado. El jugador intentó devolverse y entró a la zona de advertencia.
            else if (state == 1 && warningZone != null && warningZone.Contains(player.position))
            {
                BeginMedusaWarning();
                state = 2; 
            }
            // Estado 2: Advertencia activa. El jugador está siendo perseguido por la medusa.
            else if (state == 2)
            {
                // Si el jugador hace caso y sale de la zona de advertencia hacia la zona segura (B)
                if (warningZone != null && !warningZone.Contains(player.position))
                {
                    state = 1; // Se reinicia la trampa
                }
            }
        }

        void BeginMedusaWarning()
        {
            if (medusaWarning == null) return;
            var guide = medusaWarning.GetComponentInChildren<LuzViajera>(true);
            if (guide == null) guide = medusaWarning.GetComponentInParent<LuzViajera>();
            Transform[] points = medusaWaypoints;
            if ((points == null || points.Length == 0) && guide != null) points = guide.waypoints;
            if (points == null || points.Length == 0 || System.Array.Exists(points, point => point == null))
            {
                Debug.LogWarning("Anti Backtrack: falta una ruta valida para la medusa. Asigna Medusa Waypoints o la ruta de su Luz Viajera.", this);
                return;
            }
            // Capture world positions before moving or spinning a model that may contain its route markers.
            if (medusaRoute == null)
            {
                medusaRoute = new Vector3[points.Length];
                for (int i = 0; i < points.Length; i++) medusaRoute[i] = points[i].position;
            }
            if (medusaModel == null)
            {
                ownsMedusa = !medusaWarning.scene.IsValid();
                medusaModel = ownsMedusa
                    ? Instantiate(medusaWarning, medusaRoute[0], medusaWarning.transform.rotation)
                    : medusaWarning;
                // Only this controller drives the warning, so Start/Abysm cannot overwrite its route.
                foreach (var route in medusaModel.GetComponentsInChildren<LuzViajera>(true))
                    route.enabled = false;
                if (!ownsMedusa && guide != null) guide.enabled = false;
            }
            medusaModel.transform.position = medusaRoute[0];
            medusaWaypointIndex = 1;
            medusaMoving = true;
            medusaModel.SetActive(true);
            previousMedusaPosition = medusaModel.transform.position;
        }

        void LateUpdate()
        {
            if (medusaModel == null || !medusaMoving) return;
            if (state == 3 || medusaWaypointIndex >= medusaRoute.Length)
            {
                medusaMoving = false;
                medusaModel.SetActive(false);
                return;
            }
            Vector3 destination = medusaRoute[medusaWaypointIndex];
            medusaModel.transform.position = Vector3.MoveTowards(medusaModel.transform.position,
                destination, Mathf.Max(0f, medusaSpeed) * Time.deltaTime);
            Vector3 currentPosition = medusaModel.transform.position;
            if ((currentPosition - previousMedusaPosition).sqrMagnitude > .00000001f)
                medusaModel.transform.Rotate(Vector3.up, medusaSpinSpeed * Time.deltaTime, Space.World);
            previousMedusaPosition = currentPosition;
            if (Vector3.Distance(currentPosition, destination) < .01f) medusaWaypointIndex++;
        }

        void OnDestroy()
        {
            if (ownsMedusa && medusaModel != null) Destroy(medusaModel);
        }
    }
}
