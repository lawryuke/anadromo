using UnityEngine;

namespace Anadromo.Mechanics
{
    public class TriggerHuntOnGroupEmpty : MonoBehaviour
    {
        [Header("Grupo a vigilar")]
        [Tooltip("El spawner que genera los krills que el jugador debe comer. Si está vacío, revisará los hijos directos de este objeto.")]
        public BoxObjectSpawner targetSpawner;

        [Header("Cardúmenes a activar")]
        [Tooltip("Los grupos de salmones que empezarán a cazar cuando el grupo vigilado se quede sin krills.")]
        public SwimGroupController[] swimGroups;

        [Header("Nuevos objetivos")]
        [Tooltip("Los nuevos objetivos para cada grupo (debe tener el mismo orden que Swim Groups).")]
        public Transform[] newMovementTargets;

        [Tooltip("La nueva etiqueta de presa que buscarán los salmones (ej. Food_Krill_Scary). Dejar en blanco para no cambiarla.")]
        public string newPreyTag = "Food_Krill_Scary";

        private bool hasTriggered = false;
        private bool isInitialized = false;

        private void Start()
        {
            // Esperamos un segundo para asegurarnos de que el spawner ya generó todos los krills iniciales
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
            else
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
                ActivateHunt();
            }
        }

        private void ActivateHunt()
        {
            if (swimGroups != null)
            {
                for (int i = 0; i < swimGroups.Length; i++)
                {
                    var group = swimGroups[i];
                    if (group != null)
                    {
                        // Asignamos el nuevo objetivo si existe uno para este grupo
                        if (newMovementTargets != null && i < newMovementTargets.Length && newMovementTargets[i] != null)
                        {
                            group.movementTarget = newMovementTargets[i];
                        }

                        // Actualizar la etiqueta que buscan para comer
                        if (!string.IsNullOrEmpty(newPreyTag))
                        {
                            var eaters = group.GetComponentsInChildren<PredatorEating>();
                            foreach (var eater in eaters)
                            {
                                eater.preyTag = newPreyTag;
                            }
                        }

                        // Iniciar la secuencia de nado (por si la tenían activa)
                        group.StartFishSequence();
                        // Activar la cacería
                        group.individualHunting = true;
                        // Enviar a los salmones a su objetivo
                        group.state = SwimGroupController.SwimState.MoveToTarget;
                    }
                }
            }
            Debug.Log($"[{gameObject.name}] El jugador se ha comido todos los krills. ¡Los salmones van a por los Scary!");
        }
    }
}
