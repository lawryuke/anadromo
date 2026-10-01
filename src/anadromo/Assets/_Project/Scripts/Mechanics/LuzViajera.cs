using UnityEngine;
using Anadromo.Logic;
using Anadromo.Systems;

namespace Anadromo.Mechanics
{
    [AddComponentMenu("Anadromo/Efectos/Luz Viajera")]
    public class LuzViajera : MonoBehaviour
    {
        [Header("Visual")]
        public GameObject visualPrefab;

        [Header("Recorrido (una sola vez)")]
        [Tooltip("Si se marca, el visual aparece inmediatamente en el primer waypoint y espera allí.")]
        public bool spawnOnStart = false;
        [Tooltip("Puntos en orden. Aparece en el primero y permanece visible en el ultimo.")]
        public Transform[] waypoints;
        [Min(0.01f)] public float speed = 2f;

        [Header("Entrada del jugador")]
        [Tooltip("Zona fija que debe entrar el jugador para iniciar el viaje.")]
        public ZoneLimit activationZone;
        [Tooltip("Opcional. Sin asignar se usa la cabeza del jugador activo del nivel.")]
        public Transform playerTarget;

        [Header("Debug")]
        public bool showDebug = true;
        public bool logRouteEvents = true;
        public Camera debugCamera;

        public bool IsMoving => state == RouteState.Travelling;
        public bool HasFinished => state == RouteState.Finished;
        enum RouteState { Waiting, Travelling, Finished, Invalid }
        RouteState state;
        GameObject spawnedVisual;
        Vector3[] routePositions;
        int nextPoint;
        bool prepared;
        bool warnedMissingPlayer;
        PlayerEnergyController fallbackPlayer;
        Renderer[] existingRenderers;
        bool[] rendererStates;
        Light[] existingLights;
        bool[] lightStates;

        void Awake()
        {
            if (visualPrefab != null)
            {
                spawnedVisual = Instantiate(visualPrefab, transform.position, transform.rotation, transform);
                spawnedVisual.SetActive(false);
                Log($"Visual creado: {spawnedVisual.name}");
            }
            else
            {
                existingRenderers = GetComponentsInChildren<Renderer>(true);
                rendererStates = new bool[existingRenderers.Length];
                for (int i = 0; i < existingRenderers.Length; i++) rendererStates[i] = existingRenderers[i].enabled;
                existingLights = GetComponentsInChildren<Light>(true);
                lightStates = new bool[existingLights.Length];
                for (int i = 0; i < existingLights.Length; i++) lightStates[i] = existingLights[i].enabled;
            }
            SetVisible(false);
        }

        void Start()
        {
            if (!PrepareRoute()) return;
            if (spawnOnStart) SetVisible(true);
            if (state != RouteState.Waiting) return;
            if (activationZone == null)
                Debug.LogWarning($"[MedusaDebug] {name}: asigna Activation Zone. Esperando activacion externa.", this);
            else
                Log($"ESPERANDO jugador en {activationZone.name}; inicio={routePositions[0]:F3}; puntos={routePositions.Length}");
        }

        bool PrepareRoute()
        {
            if (prepared) return state != RouteState.Invalid;
            prepared = true;
            if (waypoints == null || waypoints.Length == 0)
                return InvalidRoute("Faltan waypoints.");
            routePositions = new Vector3[waypoints.Length];
            // Snapshot BEFORE moving: child points must never travel with the medusa.
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) return InvalidRoute($"Waypoint {i} sin asignar.");
                routePositions[i] = waypoints[i].position;
            }
            transform.position = routePositions[0];
            return true;
        }

        bool InvalidRoute(string reason)
        {
            state = RouteState.Invalid;
            Debug.LogError($"[MedusaDebug] {name}: {reason}", this);
            return false;
        }

        bool TryPlayerPosition(out Vector3 position)
        {
            if (playerTarget != null && playerTarget.gameObject.activeInHierarchy)
            { position = playerTarget.position; return true; }
            var level = LevelManager.Instance;
            if (level != null && level.gameObject.scene == gameObject.scene && level.playerCamera != null && level.playerCamera.isActiveAndEnabled)
            { position = level.playerCamera.transform.position; return true; }
            if (fallbackPlayer == null || !fallbackPlayer.isActiveAndEnabled)
            {
                fallbackPlayer = null;
                foreach (var candidate in FindObjectsByType<PlayerEnergyController>(FindObjectsSortMode.None))
                    if (candidate.isActiveAndEnabled && candidate.gameObject.scene == gameObject.scene)
                    { fallbackPlayer = candidate; break; }
            }
            if (fallbackPlayer != null) { position = fallbackPlayer.Position; return true; }
            position = default;
            return false;
        }

        bool PlayerInside(Vector3 position)
        {
            return activationZone != null && activationZone.Contains(position);
        }
        }

        // Public for existing UnityEvents / ZoneLimitMedusaWakeup. Never restarts a finished route.
        public void BeginRoute()
        {
            if (state != RouteState.Waiting || !PrepareRoute()) return;
            enabled = true;
            state = RouteState.Travelling;
            nextPoint = 1;
            SetVisible(true);
            Log($"INICIO: punto 0 en {transform.position:F3}; velocidad={speed:F2} m/s");
            if (nextPoint >= routePositions.Length) FinishRoute();
        }

        void Update()
        {
            if (state == RouteState.Invalid || state == RouteState.Finished || !PrepareRoute()) return;
            if (state == RouteState.Waiting)
            {
                if (activationZone == null) return;
                if (!TryPlayerPosition(out Vector3 position))
                {
                    if (!warnedMissingPlayer)
                        Debug.LogWarning($"[MedusaDebug] {name}: no hay jugador activo; asigna Player Target.", this);
                    warnedMissingPlayer = true;
                    return;
                }
                if (PlayerInside(position))
                {
                    Log($"ENTRADA jugador en hitbox; posicion={position:F3}");
                    BeginRoute();
                }
                return;
            }

            // Consume the frame's distance across short/duplicate segments without overshooting.
            AdvanceRoute(Time.deltaTime);
        }

        void AdvanceRoute(float deltaTime)
        {
            if (state != RouteState.Travelling) return;
            float travel = Mathf.Max(0f, speed) * Mathf.Max(0f, deltaTime);
            while (nextPoint < routePositions.Length)
            {
                Vector3 target = routePositions[nextPoint];
                float distance = Vector3.Distance(transform.position, target);
                if (distance > travel)
                {
                    transform.position = Vector3.MoveTowards(transform.position, target, travel);
                    return;
                }
                transform.position = target;
                travel -= distance;
                Log($"LLEGADA punto {nextPoint}: {target:F3}");
                nextPoint++;
            }
            FinishRoute();
        }

        void FinishRoute()
        {
            state = RouteState.Finished;
            Log($"FIN: visible y quieta en ultimo waypoint {transform.position:F3}");
        }

        void SetVisible(bool visible)
        {
            if (spawnedVisual != null) { spawnedVisual.SetActive(visible); return; }
            if (existingRenderers != null)
                for (int i = 0; i < existingRenderers.Length; i++)
                    if (existingRenderers[i] != null) existingRenderers[i].enabled = visible && rendererStates[i];
            if (existingLights != null)
                for (int i = 0; i < existingLights.Length; i++)
                    if (existingLights[i] != null) existingLights[i].enabled = visible && lightStates[i];
        }

        void Log(string message)
        {
            if (logRouteEvents) Debug.Log($"[MedusaDebug] {name}: {message}", this);
        }

        void OnDrawGizmos()
        {
            if (!showDebug) return;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            Gizmos.color = Color.yellow;
            bool cached = Application.isPlaying && routePositions != null;
            int count = cached ? routePositions.Length : (waypoints != null ? waypoints.Length : 0);
            for (int i = 0; i < count; i++)
            {
                if (!cached && waypoints[i] == null) continue;
                Vector3 point = cached ? routePositions[i] : waypoints[i].position;
                Gizmos.DrawWireSphere(point, 0.2f);
                if (i > 0 && (cached || waypoints[i - 1] != null))
                    Gizmos.DrawLine(cached ? routePositions[i - 1] : waypoints[i - 1].position, point);
            }
        }
    }
}
