using UnityEngine;

namespace Anadromo.CavernMVP
{
    public sealed class CavernPiranha : CavernEnemy
    {
        public CavernPiranha[] school;
        public float normalSpeed = 1, dashSpeed = 7, noiseThreshold = 3, turnSpeedChill = 5, turnSpeedAttack = .5f;
        Vector3 direction = Vector3.forward;
        float lostTimer;
        protected override Color BaseColor => new Color(.25f, .8f, .45f);
        public override void CheckStatus(float dt)
        {
            if (SeesPlayer(radius))
            {
                lostTimer = 0;
                if (State != EnemyState.ATTACK)
                {
                    State = EnemyState.DISTURBED; disturbedTimer += dt;
                    if (target.Velocity.magnitude >= noiseThreshold || disturbedTimer >= disturbanceDelay) AlertSchool();
                }
            }
            else
            {
                disturbedTimer = 0; lostTimer += dt;
                if (State != EnemyState.ATTACK || lostTimer >= 3) State = EnemyState.CHILL;
            }
        }
        public void AlertSchool()
        {
            State = EnemyState.ATTACK; lostTimer = 0;
            if (school == null) return;
            foreach (var ally in school)
                if (ally && Vector3.Distance(transform.position, ally.transform.position) <= 20) { ally.State = EnemyState.ATTACK; ally.lostTimer = 0; }
        }
        public override void Move(float dt)
        {
            Vector3 destination = State == EnemyState.ATTACK ? target.transform.position : Home + new Vector3(Mathf.Sin(Time.time + Home.x), Mathf.Sin(Time.time * .6f) * .4f, Mathf.Cos(Time.time + Home.z)) * 2;
            Vector3 desired = (destination - transform.position).normalized;
            direction = Vector3.Slerp(direction, desired, 1 - Mathf.Exp(-(State == EnemyState.ATTACK ? turnSpeedAttack : turnSpeedChill) * dt)).normalized;
            MoveSafely(direction * (State == EnemyState.ATTACK ? dashSpeed : normalSpeed) * dt);
        }
        public override void ExecuteAttack(float dt)
        {
            if (State == EnemyState.ATTACK && contactCooldown <= 0 && TouchesPlayer(.95f)) { target.TakeDamage(8); contactCooldown = 1; }
        }
    }
}
