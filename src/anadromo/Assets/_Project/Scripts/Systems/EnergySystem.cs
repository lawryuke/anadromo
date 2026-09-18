using UnityEngine;
using UnityEngine.Events;

namespace Anadromo.Systems
{
    public class EnergySystem : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField, Range(0f, 100f)] private float maxEnergy = 100f;
        [SerializeField] private float passiveDecayRate = 1f; // Energy lost per second
        [SerializeField] private float sprintDecayMultiplier = 3f;

        [Header("Events")]
        public UnityEvent<float> OnEnergyChanged;
        public UnityEvent OnEnergyDepleted;

        private float currentEnergy;
        private bool isSprinting;

        private void Start()
        {
            currentEnergy = maxEnergy;
            OnEnergyChanged?.Invoke(GetEnergyPercentage());
        }

        private void Update()
        {
            float decay = passiveDecayRate * (isSprinting ? sprintDecayMultiplier : 1f) * Time.deltaTime;
            ConsumeEnergy(decay);
        }

        public void SetEnergy(float value)
        {
            currentEnergy = Mathf.Clamp(value, 0f, maxEnergy);
            OnEnergyChanged?.Invoke(GetEnergyPercentage());
        }

        public void SetSprinting(bool sprinting)
        {
            isSprinting = sprinting;
        }

        public void ConsumeEnergy(float amount)
        {
            if (currentEnergy <= 0f) return;

            currentEnergy = Mathf.Clamp(currentEnergy - amount, 0f, maxEnergy);
            OnEnergyChanged?.Invoke(GetEnergyPercentage());

            if (currentEnergy <= 0f)
            {
                OnEnergyDepleted?.Invoke();
            }
        }

        public void RestoreEnergy(float amount)
        {
            currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, maxEnergy);
            OnEnergyChanged?.Invoke(GetEnergyPercentage());
        }

        public float GetEnergyPercentage()
        {
            return currentEnergy / maxEnergy;
        }
    }
}
