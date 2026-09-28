using UnityEngine;
using Anadromo.Systems; // Para encontrar al jugador

namespace Anadromo.Mechanics
{
    [AddComponentMenu("Anadromo/Logic/Medusa Wakeup Trigger")]
    public class ZoneLimitMedusaWakeup : MonoBehaviour
    {
        [Tooltip("La caja matemática celeste que el jugador debe tocar para despertar a la medusa.")]
        public ZoneLimit activationZone;

        [Tooltip("La medusa que se encenderá y empezará a moverse.")]
        public LuzViajera medusaToWake;

        private Transform player;

        void Start()
        {
            // Buscamos al jugador automáticamente al iniciar
            var p = FindAnyObjectByType<PlayerEnergyController>();
            if (p != null) player = p.transform;
        }

        void Update()
        {
            if (player == null || activationZone == null || medusaToWake == null) return;

            // Revisamos si el jugador entró a la caja matemática
            if (activationZone.Contains(player.position))
            {
                // Despertamos a la medusa
                medusaToWake.BeginRoute();
                
                // Destruimos este script para que no se siga comprobando (y ahorrar rendimiento)
                Destroy(this); 
            }
        }
    }
}
