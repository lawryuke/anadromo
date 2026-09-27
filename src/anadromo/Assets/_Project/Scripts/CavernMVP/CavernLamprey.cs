using UnityEngine;

namespace Anadromo.CavernMVP
{
    public sealed class CavernLamprey : CavernEnemy
    {
        public enum LampreyState { CHILL, CHASING, ATTACHED, STUNNED }
        public LampreyState GripState { get; private set; }
        public float maxGrip = 100, drainInterval = 2, stunDuration = 3, chaseSpeed = 4;
        public float Grip { get; private set; } = 100;
        public Vector3 attachOffset = new Vector3(.4f, -.25f, .8f);
        float drainTimer, stunTimer;
        protected override Color BaseColor => new Color(.8f, .4f, .75f);
        public override void CheckStatus(float dt)
        {
            if (GripState == LampreyState.STUNNED)
            {
                stunTimer -= dt;
                if (stunTimer <= 0) { GripState = LampreyState.CHILL; Grip = maxGrip; State = EnemyState.CHILL; }
                return;
            }
            if (GripState == LampreyState.CHILL && SeesPlayer(radius)) { GripState = LampreyState.CHASING; State = EnemyState.DISTURBED; }
            if (GripState == LampreyState.CHASING && !SeesPlayer(radius * 2)) { GripState = LampreyState.CHILL; State = EnemyState.CHILL; }
        }
        public override void Move(float dt)
        {
            if (GripState == LampreyState.CHASING) MoveSafely((target.transform.position - transform.position).normalized * chaseSpeed * dt);
            if (GripState == LampreyState.ATTACHED) FollowCamera();
        }
        void LateUpdate() { if (target && GripState == LampreyState.ATTACHED) FollowCamera(); }
        void FollowCamera() { transform.position = target.view.transform.TransformPoint(attachOffset); transform.rotation = target.view.transform.rotation; }
        public override void ExecuteAttack(float dt)
        {
            if (GripState == LampreyState.CHASING && TouchesPlayer(1)) Attach();
            if (GripState != LampreyState.ATTACHED) return;
            drainTimer -= dt;
            while (drainTimer <= 0) { drainTimer += Mathf.Max(.1f, drainInterval); target.TakeDamage(5); }
        }
        public void Attach()
        {
            if (GripState == LampreyState.ATTACHED || GripState == LampreyState.STUNNED) return;
            GripState = LampreyState.ATTACHED; State = EnemyState.ATTACK; Grip = maxGrip; drainTimer = drainInterval; target.Add(this);
        }
        public void ReduceGrip(float amount)
        {
            if (GripState != LampreyState.ATTACHED) return;
            Grip = Mathf.Max(0, Grip - amount);
            if (Grip > 0) return;
            target.Remove(this); GripState = LampreyState.STUNNED; State = EnemyState.DISTURBED; stunTimer = stunDuration;
            transform.position = target.transform.position;
            MoveSafely(target.view.transform.forward * 2);
        }
        void OnDisable() { if (target) target.Remove(this); }
    }
}
