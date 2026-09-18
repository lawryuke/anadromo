using UnityEngine;
using Anadromo.Systems;

namespace Anadromo.Mechanics
{
    [RequireComponent(typeof(SphereCollider))]
    public class PlayerFeeding : MonoBehaviour
    {
        [Header("Feeding Settings")]
        [SerializeField] private float mouthRadius = 1.0f; // Tamaño de la boca

        public UnityEngine.Events.UnityEvent OnPreyConsumed = new UnityEngine.Events.UnityEvent();

        private EnergySystem energySystem;
        private SphereCollider mouthCollider;

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
            // Verificamos si lo que chocó con la boca es una Presa (Prey) en lugar de un compañero
            Prey prey = other.GetComponent<Prey>();
            if (prey != null && prey.isActiveAndEnabled && energySystem != null)
            {
                EatPrey(prey);
            }
        }

        private void EatPrey(Prey prey)
        {
            float energyGained = prey.energyValue;
            // Destroy se completa al final del frame: impedir comer dos veces la misma presa.
            prey.enabled = false;
            // Destruir la presa para que desaparezca
            Destroy(prey.gameObject);

            // Recuperar energía si tenemos el sistema conectado
            if (energySystem != null)
            {
                energySystem.RestoreEnergy(energyGained);
                OnPreyConsumed.Invoke();
                Debug.Log($"¡Presa comida! Recuperaste {energyGained} de energía.");
            }
        }
    }
}
