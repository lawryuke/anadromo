using UnityEngine;

namespace Anadromo.Systems
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnergySystem))]
    public sealed class PlayerEnergyController : MonoBehaviour
    {
        [Range(.01f,1)] public float criticalThreshold=.3f;
        [Range(.1f,1)] public float exhaustedSwimMultiplier=.55f;
        [Range(.1f,1)] public float exhaustedTurnMultiplier=.7f;
        public float exhaustedCurrentMultiplier=1.5f;
        public float impactDuration=.35f, recoveryDuration=.45f;
        public float collisionDamage=12, collisionSpeedThreshold=1.5f;
        EnergySystem energy;
        Transform trackedHead;
        public Vector3 Position => trackedHead ? trackedHead.position : transform.position;
        float impactTimer,recoveryTimer,collisionReady;
        public EnergySystem Energy => energy ? energy : (energy=GetComponent<EnergySystem>());
        public float Current => Energy.CurrentEnergy;
        public float Maximum => Energy.MaxEnergy;
        public bool HasEnergy => Current>0;
        public float Fraction => Energy.GetEnergyPercentage();
        public float Fatigue => Mathf.Clamp01(1-Fraction/Mathf.Max(.01f,criticalThreshold));
        public float SwimMultiplier => Mathf.Lerp(1,exhaustedSwimMultiplier,Fatigue);
        public float TurnMultiplier => Mathf.Lerp(1,exhaustedTurnMultiplier,Fatigue);
        public float CurrentMultiplier => Mathf.Lerp(1,exhaustedCurrentMultiplier,Fatigue);
        public float ImpactPulse => Mathf.Clamp01(impactTimer/Mathf.Max(.01f,impactDuration));
        public float RecoveryPulse => Mathf.Clamp01(recoveryTimer/Mathf.Max(.01f,recoveryDuration));
        void Awake()
        {
            energy=GetComponent<EnergySystem>();
            var origin=GetComponent<Unity.XR.CoreUtils.XROrigin>();
            if(origin && origin.Camera) trackedHead=origin.Camera.transform;
        }
        void Start() { if(!GetComponent<EnergyVisualFeedback>()) gameObject.AddComponent<EnergyVisualFeedback>(); }
        void Update()
        {
            impactTimer=Mathf.Max(0,impactTimer-Time.deltaTime);
            recoveryTimer=Mathf.Max(0,recoveryTimer-Time.deltaTime);
            collisionReady=Mathf.Max(0,collisionReady-Time.deltaTime);
        }
        public void TakeDamage(float amount)
        {
            if(Energy.ConsumptionPaused || !HasEnergy || amount<=0) return;
            impactTimer=impactDuration; recoveryTimer=0;
            Energy.ConsumeEnergy(amount);
        }
        public void ConsumeKrill(float amount)
        {
            if(!HasEnergy || amount<=0) return;
            float before=Current;
            Energy.RestoreEnergy(amount);
            if(Current>before) { recoveryTimer=recoveryDuration; impactTimer=0; }
        }
        public void ResetEnergy()
        { impactTimer=recoveryTimer=collisionReady=0; Energy.SetSprinting(false); Energy.SetEnergy(Energy.StartingEnergy); }
        void OnCollisionEnter(Collision collision)
        {
            if(collisionReady>0 || collision.relativeVelocity.magnitude<collisionSpeedThreshold) return;
            if(collision.transform.IsChildOf(transform.root) || collision.collider.isTrigger) return;
            collisionReady=.8f; TakeDamage(collisionDamage);
        }
    }
}
