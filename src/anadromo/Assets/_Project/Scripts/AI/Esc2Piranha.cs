using UnityEngine;

namespace Anadromo.AI
{
    public sealed class Esc2Piranha : MonoBehaviour
    {
        public enum BehaviourState { Chill, Disturbed, Attack }
        public Renderer body;
        public float detectionRadius = 5, disturbanceDelay = 2.5f, noiseThreshold = 6;
        public float normalSpeed = 1, attackSpeed = 9, calmTurn = 5, attackTurn = .5f;
        public float damage = 8, biteInterval = 1, loseInterestDelay = 3, contactRadius = .45f;
        [Header("Audio por estado")]
        public AudioClip disturbedSound;
        public AudioClip attackSound;
        [Range(0f,1f)] public float soundVolume = .65f;
        public BehaviourState State { get; private set; }
        public PiranhaSchool School { get; private set; }
        Vector3 home, direction, previous;
        float disturbance, lost, biteCooldown, clock;
        AudioSource stateAudio;
        BehaviourState audioState;
        void Awake() { home = transform.position; direction = transform.forward; }
        void OnEnable() { School = GetComponentInParent<PiranhaSchool>(); }
        void OnTransformParentChanged() { School = GetComponentInParent<PiranhaSchool>(); }
        void Update()
        {
            if (School && School.target && School.target.Alive) Tick(Time.deltaTime);
            else StopStateAudio();
        }
        public void BeginAttack() { State = BehaviourState.Attack; lost = 0; UpdateStateAudio(); }
        public void ResetFish() { StopStateAudio(); transform.position = home; State = BehaviourState.Chill; disturbance = lost = biteCooldown = 0; }
        void UpdateStateAudio()
        {
            if (!isActiveAndEnabled || !School || !School.target || !School.target.Alive)
            { StopStateAudio(); return; }
            if (stateAudio) stateAudio.volume = soundVolume;
            if (audioState == State) return;
            StopStateAudio();
            audioState = State;
            AudioClip clip = State == BehaviourState.Disturbed ? disturbedSound : State == BehaviourState.Attack ? attackSound : null;
            if (!clip) return;
            if (!stateAudio)
            {
                stateAudio = gameObject.AddComponent<AudioSource>();
                stateAudio.playOnAwake = false;
                stateAudio.spatialBlend = 1f;
                stateAudio.rolloffMode = AudioRolloffMode.Linear;
                stateAudio.minDistance = 1f;
                stateAudio.maxDistance = 15f;
                stateAudio.dopplerLevel = 0f;
            }
            stateAudio.volume = soundVolume;
            stateAudio.clip = clip;
            stateAudio.loop = State == BehaviourState.Attack;
            stateAudio.Play();
        }
        void StopStateAudio() { if (stateAudio) stateAudio.Stop(); audioState = BehaviourState.Chill; }
        void OnDisable() { StopStateAudio(); }
        public void Tick(float dt)
        {
            var target = School.target; clock += dt; biteCooldown -= dt; previous = transform.position;
            bool detected = School.Contains(target.Position) && Vector3.Distance(transform.position,target.Position) <= detectionRadius && School.ClearPath(transform.position,target.Position);
            if (detected)
            {
                lost = 0;
                if (State != BehaviourState.Attack)
                {
                    State = BehaviourState.Disturbed; disturbance += dt;
                    if (target.Velocity.magnitude >= noiseThreshold || disturbance >= disturbanceDelay) School.Alert(this);
                }
            }
            else
            {
                disturbance = 0; lost += dt;
                if (!School.Contains(target.Position) || State != BehaviourState.Attack || lost >= loseInterestDelay) State = BehaviourState.Chill;
            }
            Vector3 destination = State == BehaviourState.Attack ? target.Position : School.Clamp(home + new Vector3(Mathf.Sin(clock+home.x),Mathf.Sin(clock*.7f)*.3f,Mathf.Cos(clock+home.z))*1.2f);
            direction = Vector3.Slerp(direction,(destination-transform.position).normalized,1-Mathf.Exp(-(State == BehaviourState.Attack ? attackTurn : calmTurn)*dt)).normalized;
            Vector3 delta = School.Clamp(transform.position+direction*(State == BehaviourState.Attack ? attackSpeed : normalSpeed)*dt)-transform.position;
            float allowed = delta.magnitude; Vector3 normal = Vector3.zero;
            foreach (var hit in Physics.SphereCastAll(transform.position,.18f,delta.normalized,allowed,School.obstacleLayers,QueryTriggerInteraction.Ignore))
                if (School.IsObstacle(hit.collider) && hit.distance < allowed) { allowed = Mathf.Max(0,hit.distance-.02f); normal = hit.normal; }
            transform.position += delta.normalized*allowed;
            if (normal != Vector3.zero) direction = Vector3.Reflect(direction,normal).normalized;
            if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(direction);
            Vector3 segment = transform.position-previous;
            float t = segment.sqrMagnitude < .00001f ? 0 : Mathf.Clamp01(Vector3.Dot(target.Position-previous,segment)/segment.sqrMagnitude);
            if (State == BehaviourState.Attack && biteCooldown <= 0 && Vector3.Distance(previous+segment*t,target.Position) <= contactRadius && School.ClearPath(transform.position,target.Position))
            { target.TakeDamage(damage); biteCooldown = biteInterval; GetComponentInChildren<PredatorNaturalMotion>()?.Bite(); }
            UpdateStateAudio();
        }
    }
}
