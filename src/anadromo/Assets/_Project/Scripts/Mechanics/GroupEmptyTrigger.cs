using UnityEngine;
using UnityEngine.Events;
using Anadromo.Mechanics;

namespace Anadromo.Logic
{
    [AddComponentMenu("Anadromo/Logic/Group Empty Trigger")]
    public class GroupEmptyTrigger : MonoBehaviour
    {
        [Tooltip("El spawner que genera los krills. Cuando todos sus objetos mueran o se desactiven, se lanzará el evento.")]
        public BoxObjectSpawner targetSpawner;
        
        public UnityEvent onGroupEmptyEvent;

        private bool hasTriggered = false;
        private bool isInitialized = false;

        private void Start()
        {
            // Esperamos un momento para asegurar que el spawner haya instanciado todo
            Invoke(nameof(Initialize), 1f);
        }

        private void Initialize()
        {
            isInitialized = true;
        }

        private void Update()
        {
            if (hasTriggered || !isInitialized) return;

            bool hasAlive = false;

            if (targetSpawner != null && targetSpawner.GeneratedObjects != null && targetSpawner.GeneratedObjects.Count > 0)
            {
                foreach (var obj in targetSpawner.GeneratedObjects)
                {
                    if (obj != null && obj.activeInHierarchy)
                    {
                        hasAlive = true;
                        break;
                    }
                }
            }
            else if (targetSpawner == null)
            {
                // Si no hay spawner, revisamos hijos
                foreach (Transform child in transform)
                {
                    if (child.gameObject.activeInHierarchy)
                    {
                        hasAlive = true;
                        break;
                    }
                }
            }
            else
            {
                // Spawner está asignado pero aún no tiene objetos (podría ser normal si no generó nada, pero no disparamos aún)
                hasAlive = true;
            }

            if (!hasAlive)
            {
                hasTriggered = true;
                onGroupEmptyEvent.Invoke();
            }
        }
    }
}
