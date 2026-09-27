using UnityEngine;

namespace Anadromo.CavernMVP
{
    public sealed class CavernAngler : CavernEnemy
    {
        public Light lure;
        public GameObject lureBulb;
        public float attackSpeed = 9;
        Vector3 attackDirection;
        float attackTime, recovery;
        bool dazzled;
        protected override Color BaseColor => new Color(.35f, .3f, .65f);
        public override void CheckStatus(float dt)
        {
            recovery = Mathf.Max(0, recovery - dt);
            dazzled = target.IsShiningAt(transform.position);
            if (dazzled) { State = EnemyState.CHILL; disturbedTimer = 0; recovery = .8f; return; }
            if (recovery > 0) return;
            if (State == EnemyState.CHILL && SeesPlayer(radius)) { State = EnemyState.DISTURBED; disturbedTimer = 0; }
            if (State == EnemyState.DISTURBED)
            {
                if (!SeesPlayer(radius * 1.5f)) { State = EnemyState.CHILL; disturbedTimer = 0; return; }
                disturbedTimer += dt;
                if (disturbedTimer >= disturbanceDelay) { State = EnemyState.ATTACK; attackDirection = (target.transform.position - transform.position).normalized; attackTime = 0; }
            }
        }
        public override void Move(float dt)
        {
            bool lit = State == EnemyState.CHILL && !dazzled && recovery <= 0;
            if (lure) lure.enabled = lit;
            if (lureBulb) { lureBulb.SetActive(lit); lureBulb.transform.localPosition = new Vector3(Mathf.Sin(Time.time * 3) * .12f, 1, .6f); }
            if (State != EnemyState.ATTACK) return;
            MoveSafely(attackDirection * attackSpeed * dt); attackTime += dt;
        }
        void ResetAmbush() { State = EnemyState.CHILL; disturbedTimer = 0; recovery = 2; }
        public override void ExecuteAttack(float dt)
        {
            if (State == EnemyState.ATTACK && contactCooldown <= 0 && TouchesPlayer(1.2f)) { target.TakeDamage(25); contactCooldown = 2; ResetAmbush(); }
            else if (State == EnemyState.ATTACK && attackTime > 1.3f) ResetAmbush();
        }
    }
}
