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
        public UnityEvent<float> OnEnergyChanged = new UnityEvent<float>();
        public UnityEvent OnEnergyDepleted = new UnityEvent();

        private float currentEnergy;
        private bool isSprinting;

        public float CurrentEnergy => currentEnergy;
        public float MaxEnergy => maxEnergy;

        private void Awake()
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
            bool wasAlive = currentEnergy > 0;
            currentEnergy = Mathf.Clamp(value, 0f, maxEnergy);
            OnEnergyChanged?.Invoke(GetEnergyPercentage());
            if(wasAlive && currentEnergy<=0) OnEnergyDepleted?.Invoke();
        }

        public void SetSprinting(bool sprinting)
        {
            isSprinting = sprinting;
        }

        public void ConsumeEnergy(float amount)
        {
            if (currentEnergy <= 0f || amount <= 0f) return;
            SetEnergy(currentEnergy-amount);
        }

        public void RestoreEnergy(float amount)
        {
            if(amount<=0) return;
            SetEnergy(currentEnergy+amount);
        }

        public float GetEnergyPercentage()
        {
            return currentEnergy / Mathf.Max(.001f,maxEnergy);
        }
    }
}
