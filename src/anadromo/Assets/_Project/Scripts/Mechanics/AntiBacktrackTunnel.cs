using UnityEngine;
using UnityEngine.Events;
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
        public LuzViajera medusaWarning;
        
        [Tooltip("Evento letal (ej. Encender el GameObject de un Tiburón o llamar a un script de muerte).")]
        public UnityEvent onSharkKill;

        private int state = 0; // 0 = Abierto, 1 = Bloqueado, 2 = Advertencia, 3 = Muerto
        private Transform player;

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
            // Estado 1: El túnel está bloqueado. El jugador intentó devolverse y entró a la zona de advertencia.
            else if (state == 1 && warningZone != null && warningZone.Contains(player.position))
            {
                if (medusaWarning != null) medusaWarning.BeginRoute();
                state = 2; 
            }
            // Estado 2: Advertencia activa. El jugador está siendo perseguido por la medusa.
            else if (state == 2)
            {
                // Si el jugador es terco y sigue nadando hacia el peligro
                if (killZone != null && killZone.Contains(player.position))
                {
                    if (onSharkKill != null) onSharkKill.Invoke();
                    state = 3; 
                }
                // Si el jugador hace caso y sale de la zona de advertencia hacia la zona segura (B)
                else if (warningZone != null && !warningZone.Contains(player.position))
                {
                    state = 1; // Se reinicia la trampa
                }
            }
        }
    }
}
