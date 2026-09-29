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
        public BehaviourState State { get; private set; }
        public PiranhaSchool School { get; private set; }
        Vector3 home, direction, previous;
        float disturbance, lost, biteCooldown, clock;
        MaterialPropertyBlock tint;
        void Awake() { home = transform.position; direction = transform.forward; tint = new MaterialPropertyBlock(); }
        void OnEnable() { School = GetComponentInParent<PiranhaSchool>(); }
        void OnTransformParentChanged() { School = GetComponentInParent<PiranhaSchool>(); }
        void Update() { if (School && School.target && School.target.Alive) Tick(Time.deltaTime); }
        public void BeginAttack() { State = BehaviourState.Attack; lost = 0; }
        public void ResetFish() { transform.position = home; State = BehaviourState.Chill; disturbance = lost = biteCooldown = 0; }
        public void Tick(float dt)
        {
            var target = School.target; clock += dt; biteCooldown -= dt; previous = transform.position;
            bool detected = School.Contains(target.transform.position) && Vector3.Distance(transform.position,target.transform.position) <= detectionRadius && School.ClearPath(transform.position,target.transform.position);
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
                if (!School.Contains(target.transform.position) || State != BehaviourState.Attack || lost >= loseInterestDelay) State = BehaviourState.Chill;
            }
            Vector3 destination = State == BehaviourState.Attack ? target.transform.position : School.Clamp(home + new Vector3(Mathf.Sin(clock+home.x),Mathf.Sin(clock*.7f)*.3f,Mathf.Cos(clock+home.z))*1.2f);
            direction = Vector3.Slerp(direction,(destination-transform.position).normalized,1-Mathf.Exp(-(State == BehaviourState.Attack ? attackTurn : calmTurn)*dt)).normalized;
            Vector3 delta = School.Clamp(transform.position+direction*(State == BehaviourState.Attack ? attackSpeed : normalSpeed)*dt)-transform.position;
            float allowed = delta.magnitude; Vector3 normal = Vector3.zero;
            foreach (var hit in Physics.SphereCastAll(transform.position,.18f,delta.normalized,allowed,School.obstacleLayers,QueryTriggerInteraction.Ignore))
                if (School.IsObstacle(hit.collider) && hit.distance < allowed) { allowed = Mathf.Max(0,hit.distance-.02f); normal = hit.normal; }
            transform.position += delta.normalized*allowed;
            if (normal != Vector3.zero) direction = Vector3.Reflect(direction,normal).normalized;
            if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(direction);
            Vector3 segment = transform.position-previous;
            float t = segment.sqrMagnitude < .00001f ? 0 : Mathf.Clamp01(Vector3.Dot(target.transform.position-previous,segment)/segment.sqrMagnitude);
            if (State == BehaviourState.Attack && biteCooldown <= 0 && Vector3.Distance(previous+segment*t,target.transform.position) <= contactRadius && School.ClearPath(transform.position,target.transform.position))
            { target.TakeDamage(damage); biteCooldown = biteInterval; GetComponentInChildren<PredatorNaturalMotion>()?.Bite(); }
            if (body)
            {
                if (tint == null) tint = new MaterialPropertyBlock();
                Color c = State == BehaviourState.Attack ? new Color(1,.15f,.1f) : State == BehaviourState.Disturbed ? new Color(1,.7f,.1f) : new Color(.2f,.75f,.4f);
                tint.SetColor("_BaseColor",c); tint.SetColor("_Color",c); body.SetPropertyBlock(tint);
            }
        }
    }
}
