using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace Anadromo.AI
{
    // Damage/velocity adapter: it never supplies locomotion or consumes food energy.
    [DisallowMultipleComponent]
    public sealed class PiranhaPlayerTarget : MonoBehaviour
    {
        public float maxHealth = 100;
        public SimpleFlyCamera desktopMovement;
        public UnityEvent<float> onHealthChanged = new UnityEvent<float>();
        public UnityEvent onDeath = new UnityEvent();
        public float Health { get; private set; }
        public Vector3 Velocity { get; private set; }
        public bool Alive => Health > 0;
        Vector3 previous, spawn;
        Quaternion spawnRotation;
        Rigidbody body;
        float pulse;
        bool movementWasEnabled;
        readonly HashSet<Object> attachedLampreys = new HashSet<Object>();
        int lastSide;
        float lastSideAt=-10,shakeReady;
        void Awake()
        {
            body = GetComponent<Rigidbody>(); Health = maxHealth;
            previous = spawn = transform.position; spawnRotation = transform.rotation;
            if (!desktopMovement) desktopMovement = GetComponent<SimpleFlyCamera>();
        }
        void LateUpdate()
        {
            if (!body) SampleMotion(Time.deltaTime);
            pulse = Mathf.MoveTowards(pulse,0,Time.deltaTime);
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
            if (!Alive || amount <= 0) return;
            Health = Mathf.Max(0,Health-amount); pulse = .65f; onHealthChanged.Invoke(Health);
            if (Alive) return;
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
            previous = spawn; Velocity = Vector3.zero; Health = maxHealth; pulse = 0;
            foreach(var lamprey in FindObjectsByType<Esc2Lamprey>(FindObjectsSortMode.None)) if(lamprey.Target==this) lamprey.ResetLamprey();
            if (dead && desktopMovement) desktopMovement.enabled = movementWasEnabled;
            foreach (var school in FindObjectsByType<PiranhaSchool>(FindObjectsSortMode.None))
                if (school.target == this) school.ResetFish();
            attachedLampreys.Clear(); lastSide=0; lastSideAt=-10; shakeReady=0;
            foreach(var passage in FindObjectsByType<Esc2SharkPassage>(FindObjectsSortMode.None))
                if(passage.target==this) passage.ResetPassage();
            if(desktopMovement) { desktopMovement.externalSpeedMultiplier=1; desktopMovement.SyncLookRotation(); }
            onHealthChanged.Invoke(Health);
        }
        public void AddLamprey(Object source) { if(source) attachedLampreys.Add(source); if(desktopMovement) desktopMovement.externalSpeedMultiplier=SpeedMultiplier; }
        public void RemoveLamprey(Object source) { attachedLampreys.Remove(source); if(desktopMovement) desktopMovement.externalSpeedMultiplier=SpeedMultiplier; }
        public float SpeedMultiplier => Mathf.Max(.25f,1-.15f*attachedLampreys.Count);
        void OnGUI()
        {
            GUI.Box(new Rect(18,18,190,48),"SALUD  "+Mathf.CeilToInt(Health)+" / "+Mathf.CeilToInt(maxHealth));
            GUI.color=new Color(.07f,.12f,.15f,.9f); GUI.DrawTexture(new Rect(28,50,170,8),Texture2D.whiteTexture);
            GUI.color=Health>35?new Color(.18f,.85f,.55f):new Color(1,.16f,.08f);
            GUI.DrawTexture(new Rect(28,50,170*Mathf.Clamp01(Health/Mathf.Max(1,maxHealth)),8),Texture2D.whiteTexture); GUI.color=Color.white;
            if (pulse > 0)
            {
                GUI.color = new Color(1,.05f,.02f,pulse);
                GUI.DrawTexture(new Rect(0,0,Screen.width,12),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0,Screen.height-12,Screen.width,12),Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            if(attachedLampreys.Count>0) GUI.Box(new Rect(18,76,330,48),"Lampreas adheridas: "+attachedLampreys.Count+"\nAlterna A / D o sacude el ratón para soltarlas");
            if(!Alive) GUI.Box(new Rect(Screen.width/2-200,Screen.height/2-30,400,60),"Los depredadores te alcanzaron.\nR: volver al inicio del encuentro");
        }
    }
}
