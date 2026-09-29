using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Anadromo.Systems;
using Anadromo.Mechanics;

namespace Anadromo.AI
{
    // Enemy adapter backed by the same energy resource used by feeding and fatigue.
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerEnergyController))]
    public sealed class PiranhaPlayerTarget : MonoBehaviour
    {
        public float maxHealth => Vital.Maximum; // Legacy enemy API, no separate health pool.
        public bool showDebugStatus;
        PlayerEnergyController vital;
        public PlayerEnergyController Vital => vital ? vital : (vital=GetComponent<PlayerEnergyController>());
        public SimpleFlyCamera desktopMovement;
        public UnityEvent<float> onHealthChanged = new UnityEvent<float>();
        public UnityEvent onDeath = new UnityEvent();
        public float Health => Vital.Current;
        public Vector3 Velocity { get; private set; }
        public bool Alive => Health > 0;
        Vector3 previous, spawn;
        Quaternion spawnRotation;
        Rigidbody body;

        LampreyShakeController shakeController;
        public Vector3 Position => Vital.Position;
        public static PiranhaPlayerTarget Resolve(Component owner, PiranhaPlayerTarget assigned)
        {
            if (assigned && assigned.enabled && assigned.gameObject.activeInHierarchy && assigned.gameObject.scene == owner.gameObject.scene) return assigned;
            foreach (var candidate in FindObjectsByType<PiranhaPlayerTarget>(FindObjectsSortMode.None))
                if (candidate.enabled && candidate.gameObject.activeInHierarchy && candidate.gameObject.scene == owner.gameObject.scene) return candidate;
            return null;
        }
        readonly HashSet<Object> attachedLampreys = new HashSet<Object>();
        public int AttachedLampreyCount => attachedLampreys.Count;
        public float ShakeProgress
        {
            get
            {
                float grip = 0;
                foreach (var item in attachedLampreys)
                    if (item is Esc2Lamprey lamprey) grip = Mathf.Max(grip, lamprey.Grip / Mathf.Max(1, lamprey.maxGrip));
                return 1 - Mathf.Clamp01(grip);
            }
        }
        void Awake()
        {
            body = GetComponent<Rigidbody>(); vital=GetComponent<PlayerEnergyController>();
            if(!vital) vital=gameObject.AddComponent<PlayerEnergyController>();
            Vital.Energy.OnEnergyDepleted.AddListener(Depleted);
            Vital.Energy.OnEnergyChanged.AddListener(EnergyChanged);
            previous = Position; spawn = transform.position; spawnRotation = transform.rotation;
            if (!desktopMovement) desktopMovement = GetComponent<SimpleFlyCamera>();
            shakeController = GetComponent<LampreyShakeController>();
            if (!shakeController) shakeController = gameObject.AddComponent<LampreyShakeController>();
        }
        void LateUpdate()
        {
            if (!body) SampleMotion(Time.deltaTime);
        }
        public void ShakeLampreys(float strength)
        {
            if (!Alive || strength <= 0) return;
            var attached=new List<Object>(attachedLampreys);
            foreach(var parasite in attached) if(parasite is Esc2Lamprey lamprey) lamprey.ReduceGrip(strength);
        }
        public int GetLampreySlot()
        {
            int slot=0;
            while(true)
            {
                bool used=false;
                foreach(var item in attachedLampreys) if(item is Esc2Lamprey l && l.AttachmentSlot==slot) used=true;
                if(!used) return slot;
                slot++;
            }
        }
        void FixedUpdate() { if (body) SampleMotion(Time.fixedDeltaTime); }
        public void SampleMotion(float dt)
        {
            Velocity = (Position-previous)/Mathf.Max(dt,.0001f);
            previous = Position;
        }
        public void TakeDamage(float amount)
        {
            Vital.TakeDamage(amount);
        }
        void EnergyChanged(float fraction) { onHealthChanged.Invoke(Health); }
        void OnDestroy()
        {
            if(!vital || !vital.Energy) return;
            vital.Energy.OnEnergyDepleted.RemoveListener(Depleted);
            vital.Energy.OnEnergyChanged.RemoveListener(EnergyChanged);
        }
        void Depleted()
        {
            if (shakeController) shakeController.HandleDeath();
            onDeath.Invoke();
        }
        public void ResetEncounter()
        {
            transform.SetPositionAndRotation(spawn,spawnRotation);
            if (body) { body.position = spawn; body.rotation = spawnRotation; if (!body.isKinematic) body.linearVelocity = Vector3.zero; }
            previous = Position; Velocity = Vector3.zero; Vital.ResetEnergy();
            foreach(var lamprey in FindObjectsByType<Esc2Lamprey>(FindObjectsSortMode.None)) if(lamprey.Target==this) lamprey.ResetLamprey();
            foreach (var school in FindObjectsByType<PiranhaSchool>(FindObjectsSortMode.None))
                if (school.target == this) school.ResetFish();
            attachedLampreys.Clear();
            if (shakeController) shakeController.ResetEncounter();
            foreach(var passage in FindObjectsByType<Esc2SharkPassage>(FindObjectsSortMode.None))
                if(passage.target==this) passage.ResetPassage();
            foreach(var fish in FindObjectsByType<Esc2BlindFish>(FindObjectsSortMode.None))
                if(fish.target==this) fish.ResetFish();
            var blindSensor=GetComponent<BlindFishMotionSensor>();
            if(blindSensor) blindSensor.ResetMotion();
            if(desktopMovement) { desktopMovement.externalSpeedMultiplier=1; desktopMovement.SyncLookRotation(); }
            onHealthChanged.Invoke(Health);
        }
        public void AddLamprey(Object source)
        {
            if (!source || !Alive || !attachedLampreys.Add(source)) return;
            if (shakeController) shakeController.AttachChanged();
        }
        public void RemoveLamprey(Object source)
        {
            attachedLampreys.Remove(source);
            if (shakeController) shakeController.AttachChanged();
        }
        public float SpeedMultiplier => Alive && attachedLampreys.Count == 0 ? 1f : 0f;
    }
}
