using UnityEngine;

namespace Anadromo.Mechanics
{
    [AddComponentMenu("Anadromo/Trigger Normal On Scary Empty")]
    public class TriggerNormalOnScaryEmpty : MonoBehaviour
    {
        [Header("Grupo a vigilar")]
        [Tooltip("El spawner que genera los krills scary.")]
        public BoxObjectSpawner targetSpawner;

        [Header("Cardúmenes a normalizar")]
        [Tooltip("Los grupos de salmones que volverán a la normalidad.")]
        public SwimGroupController[] swimGroups;

        [Header("Destino Final")]
        [Tooltip("La caja invisible (o Transform) a la que nadarán cuando terminen de comer.")]
        public Transform finalDestination;

        private bool hasTriggered = false;
        private bool isInitialized = false;

        private void Start()
        {
            // Esperamos un segundo para asegurarnos de que el spawner haya generado los objetos
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
                // Revisamos si queda algún krill vivo generado por el spawner
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
                // Si no hay spawner, revisamos los hijos directos del objeto
                foreach (Transform child in transform)
                {
                    if (child.gameObject.activeInHierarchy)
                    {
                        hasAlive = true;
                        break;
                    }
                }
            }

            // Si ya no queda ninguno vivo, disparamos el evento
            if (!hasAlive)
            {
                hasTriggered = true;
                ActivateNormal();
            }
        }

        private void ActivateNormal()
        {
            if (swimGroups != null)
            {
                foreach (var group in swimGroups)
                {
                    if (group != null)
                    {
                        group.NormalAtWaypoint(finalDestination);
                    }
                }
            }
            Debug.Log($"[{gameObject.name}] Se comieron todos los krills Scary. ¡Los salmones nadan hacia {(finalDestination != null ? finalDestination.name : "home")}!");
        }
    }
}
