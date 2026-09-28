using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Anadromo.Systems;

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

        bool movementWasEnabled;
        readonly HashSet<Object> attachedLampreys = new HashSet<Object>();
        int lastSide;
        float lastSideAt=-10,shakeReady;
        void Awake()
        {
            body = GetComponent<Rigidbody>(); vital=GetComponent<PlayerEnergyController>();
            if(!vital) vital=gameObject.AddComponent<PlayerEnergyController>();
            Vital.Energy.OnEnergyDepleted.AddListener(Depleted);
            Vital.Energy.OnEnergyChanged.AddListener(EnergyChanged);
            previous = spawn = transform.position; spawnRotation = transform.rotation;
            if (!desktopMovement) desktopMovement = GetComponent<SimpleFlyCamera>();
        }
        void LateUpdate()
        {
            if (!body) SampleMotion(Time.deltaTime);
            if (!Alive && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetEncounter();
            var keyboard=Keyboard.current; var mouse=Mouse.current;
            if (!Alive || attachedLampreys.Count==0 || keyboard==null) return;
            shakeReady-=Time.deltaTime;
            int side=keyboard.aKey.wasPressedThisFrame ? -1 : keyboard.dKey.wasPressedThisFrame ? 1 : 0;
            bool alternating=side!=0 && side!=lastSide && lastSide!=0 && Time.time-lastSideAt<=.6f;
            if(side!=0) { lastSide=side; lastSideAt=Time.time; }
            Vector2 mouseDelta=mouse!=null ? mouse.delta.ReadValue() : Vector2.zero;
            if(shakeReady>0 || (Mathf.Abs(mouseDelta.x)<=50 && !alternating)) return;
            float strength=alternating ? 35 : 25;
            ShakeLampreys(strength);
            shakeReady=.12f;
        }
        public void ShakeLampreys(float strength)
        {
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
            Velocity = (transform.position-previous)/Mathf.Max(dt,.0001f);
            previous = transform.position;
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
            movementWasEnabled = desktopMovement && desktopMovement.enabled;
            if (desktopMovement) desktopMovement.enabled = false;
            if (body && !body.isKinematic) body.linearVelocity = Vector3.zero;
            onDeath.Invoke();
        }
        public void ResetEncounter()
        {
            bool dead = !Alive;
            if (body) { body.position = spawn; body.rotation = spawnRotation; if (!body.isKinematic) body.linearVelocity = Vector3.zero; }
            else transform.SetPositionAndRotation(spawn,spawnRotation);
            previous = spawn; Velocity = Vector3.zero; Vital.ResetEnergy();
            foreach(var lamprey in FindObjectsByType<Esc2Lamprey>(FindObjectsSortMode.None)) if(lamprey.Target==this) lamprey.ResetLamprey();
            if (dead && desktopMovement) desktopMovement.enabled = movementWasEnabled;
            foreach (var school in FindObjectsByType<PiranhaSchool>(FindObjectsSortMode.None))
                if (school.target == this) school.ResetFish();
            attachedLampreys.Clear(); lastSide=0; lastSideAt=-10; shakeReady=0;
            foreach(var passage in FindObjectsByType<Esc2SharkPassage>(FindObjectsSortMode.None))
                if(passage.target==this) passage.ResetPassage();
            foreach(var fish in FindObjectsByType<Esc2BlindFish>(FindObjectsSortMode.None))
                if(fish.target==this) fish.ResetFish();
            var blindSensor=GetComponent<BlindFishMotionSensor>();
            if(blindSensor) blindSensor.ResetMotion();
            if(desktopMovement) { desktopMovement.externalSpeedMultiplier=1; desktopMovement.SyncLookRotation(); }
            onHealthChanged.Invoke(Health);
        }
        public void AddLamprey(Object source) { if(source) attachedLampreys.Add(source); if(desktopMovement) desktopMovement.externalSpeedMultiplier=SpeedMultiplier; }
        public void RemoveLamprey(Object source) { attachedLampreys.Remove(source); if(desktopMovement) desktopMovement.externalSpeedMultiplier=SpeedMultiplier; }
        public float SpeedMultiplier => Mathf.Max(.25f,1-.15f*attachedLampreys.Count);
        void OnGUI()
        {
            if(showDebugStatus) GUI.Box(new Rect(18,18,220,40),"ENERGIA VITAL  "+Mathf.CeilToInt(Health)+" / "+Mathf.CeilToInt(maxHealth));
            if(UnityEngine.XR.XRSettings.isDeviceActive) return;
            if(attachedLampreys.Count>0) GUI.Box(new Rect(18,76,330,48),"Lampreas adheridas: "+attachedLampreys.Count+"\nAlterna A / D o sacude el ratón para soltarlas");
            if(!Alive) GUI.Box(new Rect(Screen.width/2-200,Screen.height/2-30,400,60),"Energia agotada.\nR: volver al inicio del encuentro");
        }
    }
}
