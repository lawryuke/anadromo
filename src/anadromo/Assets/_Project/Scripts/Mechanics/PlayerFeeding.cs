using UnityEngine;
using Anadromo.Systems;

namespace Anadromo.Mechanics
{
    [RequireComponent(typeof(SphereCollider))]
    public class PlayerFeeding : MonoBehaviour
    {
        [Header("Feeding Settings")]
        [SerializeField] private float mouthRadius = 1.0f; // Tamaño de la boca

        [Tooltip("Etiquetas (Tags) que el jugador puede comer. Si está vacío, puede comer cualquier presa.")]
        public string[] edibleTags = { "Food_PlayerOnly", "Food_PlayerOnly_First" };

        public UnityEngine.Events.UnityEvent OnPreyConsumed = new UnityEngine.Events.UnityEvent();

        private EnergySystem energySystem;
        private SphereCollider mouthCollider;
        private bool hasTriggeredScaryHunt;

        // Tag del grupo cuya eliminación completa activa la fase 2.
        private const string TriggerGroupTag = "Food_PlayerOnly_First";
        // Tag de presa al que cambiarán los depredadores en la fase 2.
        private const string ScaryPreyTag = "Food_Krill_Scary";

        private void Start()
        {
            // Buscamos el EnergySystem en la raíz del jugador (XR Origin)
            energySystem = GetComponentInParent<EnergySystem>();
            
            // Configuramos la boca invisible automáticamente
            mouthCollider = GetComponent<SphereCollider>();
            mouthCollider.isTrigger = true;
            mouthCollider.radius = mouthRadius;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Verificamos si la presa tiene alguna de las etiquetas permitidas
            bool canEat = edibleTags == null || edibleTags.Length == 0;
            
            if (!canEat)
            {
                foreach (string t in edibleTags)
                {
                    // Buscar la etiqueta en el objeto o en sus padres (como el Spawner)
                    Transform current = other.transform;
                    while (current != null)
                    {
                        if (current.CompareTag(t))
                        {
                            canEat = true;
                            break;
                        }
                        current = current.parent;
                    }
                    if (canEat) break;
                }
            }

            if (canEat)
            {
                // Evitar que el jugador se coma a sí mismo o a sus propios hijos
                if (other.transform.IsChildOf(transform.root)) return;

                // Buscamos si tiene el componente Prey para obtener su valor de energía
                Prey prey = other.GetComponentInParent<Prey>();
                
                // Si encontramos un componente Prey pero está desactivado, lo ignoramos
                if (prey != null && !prey.isActiveAndEnabled) return;

                EatPrey(prey, other.gameObject);
            }
        }

        private void EatPrey(Prey prey, GameObject preyObject)
        {
            float energyGained = prey != null ? prey.energyValue : 15f;
            
            if (prey != null) prey.enabled = false;
            
            // Si hay script Prey, destruimos la raíz que marca el script. Si no, destruimos el objeto colisionado.
            GameObject objToDestroy = prey != null ? prey.gameObject : preyObject;
            
            objToDestroy.SetActive(false); // Desactivar para evitar que se vuelva a procesar este frame
            Destroy(objToDestroy);

            // Recuperar energía si tenemos el sistema conectado
            if (energySystem != null)
            {
                energySystem.RestoreEnergy(energyGained);
            }
            
            OnPreyConsumed.Invoke();
            Debug.Log($"¡Presa comida! Recuperaste {energyGained} de energía.");

            // Verificar si ya se consumieron todos los krils Food_PlayerOnly_First
            if (!hasTriggeredScaryHunt)
            {
                CheckAndTriggerScaryHunt();
            }
        }

        /// <summary>
        /// Busca TODOS los grupos con tag Food_PlayerOnly_First en la escena.
        /// Si ninguno tiene hijos activos, activa la fase 2 de depredación.
        /// </summary>
        private void CheckAndTriggerScaryHunt()
        {
            GameObject[] triggerGroups = GameObject.FindGameObjectsWithTag(TriggerGroupTag);
            if (triggerGroups.Length == 0) return;

            // Revisar si algún grupo todavía tiene krils vivos (hijos activos)
            foreach (var group in triggerGroups)
            {
                foreach (Transform child in group.transform)
                {
                    if (child.gameObject.activeSelf)
                    {
                        return; // Aún quedan krils vivos, no activar fase 2
                    }
                }
            }

            // Todos los krils Food_PlayerOnly_First han sido consumidos
            hasTriggeredScaryHunt = true;
            Debug.Log($"¡{TriggerGroupTag} consumido por completo! Los salmones empezarán a cazar {ScaryPreyTag}.");
            ActivateScaryHunt();
        }

        /// <summary>
        /// Cambia el objetivo de todos los depredadores (salmones) a Food_Krill_Scary
        /// y redirige cada cardumen al grupo scary más cercano.
        /// </summary>
        private void ActivateScaryHunt()
        {
            // 1. Cambiar el preyTag de todos los depredadores y resetear su hambre
            PredatorEating[] predators = Object.FindObjectsByType<PredatorEating>(FindObjectsSortMode.None);
            foreach (var predator in predators)
            {
                predator.preyTag = ScaryPreyTag;
                predator.ResetHunger();
            }

            // 2. Encontrar los grupos scary disponibles
            GameObject[] scaryGroups = GameObject.FindGameObjectsWithTag(ScaryPreyTag);
            if (scaryGroups.Length == 0) return;

            // 3. Redirigir cada cardumen de salmones al grupo scary más cercano
            SwimGroupController[] allSwimGroups = Object.FindObjectsByType<SwimGroupController>(FindObjectsSortMode.None);
            foreach (var swimGroup in allSwimGroups)
            {
                // Solo redirigir swim groups que tienen depredadores (salmones, no krils)
                if (swimGroup.GetComponentsInChildren<PredatorEating>().Length == 0) continue;

                // Buscar el grupo scary más cercano a este cardumen
                Transform closest = null;
                float closestDist = float.MaxValue;
                foreach (var sg in scaryGroups)
                {
                    float dist = (sg.transform.position - swimGroup.transform.position).sqrMagnitude;
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = sg.transform;
                    }
                }

                if (closest != null)
                {
                    swimGroup.movementTarget = closest;
                    swimGroup.state = SwimGroupController.SwimState.MoveToTarget;
                    swimGroup.individualHunting = true;
                }
            }
        }
    }
}
