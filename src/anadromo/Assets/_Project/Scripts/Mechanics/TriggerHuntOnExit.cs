using UnityEngine;

namespace Anadromo.Mechanics
{
    [RequireComponent(typeof(Collider))]
    public class TriggerHuntOnExit : MonoBehaviour
    {
        [Header("Configuración del Trigger")]
        [Tooltip("El tag del objeto (ej. Player) que activará el evento al salir de la caja.")]
        public string targetTag = "Player";

        [Tooltip("Si es verdadero, el evento solo se disparará la primera vez que el jugador salga.")]
        public bool triggerOnlyOnce = true;

        [Header("Cardúmenes a activar")]
        [Tooltip("Los grupos de salmones que empezarán a cazar cuando el jugador salga.")]
        public SwimGroupController[] swimGroups;

        private bool hasTriggered = false;

        private void OnTriggerExit(Collider other)
        {
            if (triggerOnlyOnce && hasTriggered) return;

            // Verificamos si el objeto que sale tiene el tag que buscamos
            if (other.CompareTag(targetTag))
            {
                hasTriggered = true;
                
                // Recorremos todos los grupos asignados
                if (swimGroups != null)
                {
                    foreach (var group in swimGroups)
                    {
                        if (group != null)
                        {
                            // Iniciar la secuencia de nado inicial si estaba pausada
                            group.StartFishSequence();
                            
                            // Activamos la bandera que hace que comiencen a cazar
                            group.individualHunting = true;
                            // Cambiamos su estado para que empiecen a nadar hacia el krill
                            group.state = SwimGroupController.SwimState.MoveToTarget;
                        }
                    }
                }
                
                Debug.Log($"[{gameObject.name}] El {targetTag} ha salido de la zona inicial. ¡Los salmones empiezan a cazar!");
            }
        }
    }
}
