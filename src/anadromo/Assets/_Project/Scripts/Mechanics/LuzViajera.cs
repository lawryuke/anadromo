using UnityEngine;
using Anadromo.Config;

namespace Anadromo.Mechanics
{
    [AddComponentMenu("Anadromo/Efectos/Luz Viajera")]
    public class LuzViajera : MonoBehaviour
    {
        [Header("Recorrido de la Luz")]
        [Tooltip("Arrastra aquí los objetos vacíos que servirán como puntos del camino.")]
        public Transform[] waypoints;
        
        [Tooltip("Velocidad a la que se mueve la luz (se sobreescribe con GameSettings).")]
        public float speed = 2f;
        
        [Tooltip("¿Vuelve a empezar desde el principio cuando llega al final?")]
        public bool loop = true;

        [Header("Activación")]
        [Tooltip("Si es true, la medusa estará oculta y no iniciará su recorrido hasta que el jugador entre en Abysm_Mid (se sobreescribe con GameSettings).")]
        public bool waitAbysmPhase = true;
        private bool isMoving = false;

        /// <summary>Velocidad efectiva: prioriza GameSettings.I si existe.</summary>
        float EffectiveSpeed => GameSettings.I ? GameSettings.I.jellyfishSpeed : speed;
        /// <summary>Esperar al abismo: prioriza GameSettings.I si existe.</summary>
        bool EffectiveWaitAbysm => GameSettings.I ? GameSettings.I.jellyfishWaitsForAbysm : waitAbysmPhase;

        [Header("Debug del recorrido")]
        public bool showDebug = true;
        public bool logRouteEvents = true;
        public Camera debugCamera;
        private float routeStartTime;
        private string routeStatus = "Sin iniciar";

        private int currentWaypointIndex = 0;

        void Start()
        {
            routeStartTime = Time.time;
            
            if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
            {
                transform.position = waypoints[0].position;
            }

            if (EffectiveWaitAbysm)
            {
                routeStatus = "Esperando entrada a Abysm_Mid";
                SetVisualsActive(false);
            }
            else
            {
                BeginRoute();
            }
        }

        private void SetVisualsActive(bool active)
        {
            var glow = GetComponent<MedusaGlow>();
            if (glow != null) glow.enabled = active;
            var light = GetComponent<Light>();
            if (light != null) light.enabled = active;
        }

        public void BeginRoute()
        {
            if (isMoving) return;
            isMoving = true;
            routeStartTime = Time.time;
            SetVisualsActive(true);

            if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
            {
                transform.position = waypoints[0].position;
                routeStatus = "Recorriendo";
                if (logRouteEvents) Debug.Log($"[{name}] Inicio en {waypoints[0].name}: {transform.position:F3}. Velocidad {speed:F2} m/s.", this);
            }
            else
            {
                routeStatus = "ERROR: falta el punto inicial";
                Debug.LogWarning($"[{name}] {routeStatus}", this);
            }
        }

        void Update()
        {
            if (EffectiveWaitAbysm && !isMoving)
            {
                if (Anadromo.Logic.LevelManager.Instance != null && Anadromo.Logic.LevelManager.Instance.isPlayerInAbysm)
                {
                    BeginRoute();
                }
                return;
            }

            if (!isMoving || waypoints == null || waypoints.Length == 0) return;

            Transform target = waypoints[currentWaypointIndex];
            if (target == null) { routeStatus = "ERROR: destino vacío"; return; }
            
            // Moverse lentamente hacia el punto objetivo
            transform.position = Vector3.MoveTowards(transform.position, target.position, EffectiveSpeed * Time.deltaTime);

            // Si está muy cerca del punto, pasamos al siguiente
            if (Vector3.Distance(transform.position, target.position) < 0.1f)
            {
                if (logRouteEvents) Debug.Log($"[{name}] Llegó a {target.name}: {transform.position:F3}", this);
                currentWaypointIndex++;
                
                // Si llegamos al final del arreglo de puntos
                if (currentWaypointIndex >= waypoints.Length)
                {
                    if (loop) 
                    {
                        currentWaypointIndex = 0;
                        // Opcional: Descomenta la siguiente línea si quieres que se teletransporte al inicio en lugar de devolverse volando.
                        // transform.position = waypoints[0].position; 
                    }
                    else 
                    {
                        routeStatus = "Recorrido terminado";
                        enabled = false; // Detener el script para que se quede quieta
                    }
                }
            }
        }

        void OnGUI()
        {
            if (!showDebug) return;
            Camera camera = debugCamera ? debugCamera : Camera.main;
            Transform first = waypoints != null && waypoints.Length > 0 ? waypoints[0] : null;
            Transform target = waypoints != null && currentWaypointIndex < waypoints.Length ? waypoints[currentWaypointIndex] : null;
            string details = $"MEDUSA — {routeStatus} | {Time.time - routeStartTime:F1} s | timeScale {Time.timeScale:F1}\n" +
                $"Posición mundial: {transform.position:F2}\n" +
                $"Inicio: {(first ? first.name + " " + first.position.ToString("F2") : "SIN ASIGNAR")}\n" +
                $"Destino [{currentWaypointIndex}]: {(target ? target.name + " " + target.position.ToString("F2") : "ninguno")} | velocidad {speed:F2}\n";
            if (camera)
            {
                Vector3 viewport = camera.WorldToViewportPoint(transform.position);
                bool inside = viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1;
                details += $"Cámara: {camera.name} | distancia {Vector3.Distance(camera.transform.position, transform.position):F2} m | {(inside ? "EN PANTALLA" : "FUERA DE PANTALLA")}\n";
                var glow = GetComponent<MedusaGlow>();
                if (glow) details += glow.VisibilityDebug(camera);
                else details += "ERROR: falta Medusa Glow";
            }
            else details += "ERROR: no hay cámara de debug ni MainCamera activa";
            GUI.Box(new Rect(12, Screen.height - 190, 720, 178), GUIContent.none);
            GUI.Label(new Rect(24, Screen.height - 182, 696, 164), details);
        }

        private void OnDrawGizmos()
        {
            if (showDebug)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position, 0.6f);
                if (waypoints != null && waypoints.Length > 0 && waypoints[0])
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(waypoints[0].position, 0.7f);
                }
            }
            // Dibuja una línea en el editor para que veas el camino de la luz
            if (waypoints == null || waypoints.Length < 2) return;
            
            Gizmos.color = Color.yellow;
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i+1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i+1].position);
                }
            }
            
            if (loop && waypoints[0] != null && waypoints[waypoints.Length - 1] != null)
            {
                Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
            }
        }
    }
}
