using UnityEngine;

namespace Anadromo.CavernMVP
{
    public enum EnemyState { CHILL, DISTURBED, ATTACK }

    public abstract class CavernEnemy : MonoBehaviour
    {
        public CavernPlayer target;
        public Renderer body;
        public float radius = 8, disturbanceDelay = 2;
        public EnemyState State { get; protected set; }
        public Vector3 Home { get; private set; }
        protected float disturbedTimer, contactCooldown;
        protected Vector3 previousPosition;
        MaterialPropertyBlock tint;
        protected virtual void Awake() { Home = transform.position; previousPosition = Home; tint = new MaterialPropertyBlock(); }
        protected virtual void Update() { if (target && target.Active) Tick(Time.deltaTime); }
        public virtual void Tick(float dt)
        {
            previousPosition = transform.position;
            contactCooldown -= dt;
            CheckStatus(dt);
            Move(dt);
            ExecuteAttack(dt);
            if (body)
            {
                Color color = State == EnemyState.ATTACK ? new Color(1, .16f, .12f) : State == EnemyState.DISTURBED ? new Color(1, .65f, .08f) : BaseColor;
                tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color); body.SetPropertyBlock(tint);
            }
        }
        protected virtual Color BaseColor => new Color(.3f, .8f, .8f);
        protected bool SeesPlayer(float distance)
        {
            Vector3 delta = target.transform.position - transform.position;
            return delta.magnitude <= distance && !Physics.Raycast(transform.position, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
        protected bool TouchesPlayer(float distance)
        {
            Vector3 segment = transform.position - previousPosition;
            float t = segment.sqrMagnitude < .00001f ? 0 : Mathf.Clamp01(Vector3.Dot(target.transform.position - previousPosition, segment) / segment.sqrMagnitude);
            return Vector3.Distance(previousPosition + segment * t, target.transform.position) <= distance;
        }
        protected void MoveSafely(Vector3 displacement)
        {
            float distance = displacement.magnitude;
            if (distance < .00001f) return;
            if (Physics.SphereCast(transform.position, .35f, displacement / distance, out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                displacement = displacement.normalized * Mathf.Max(0, hit.distance - .02f);
            transform.position += displacement;
            if (displacement.sqrMagnitude > .00001f) transform.rotation = Quaternion.LookRotation(displacement);
        }
        public abstract void CheckStatus(float dt);
        public abstract void Move(float dt);
        public abstract void ExecuteAttack(float dt);
    }
}
