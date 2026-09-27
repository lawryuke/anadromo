using UnityEngine;

namespace Anadromo.CavernMVP
{
    public sealed class CavernShark : CavernEnemy
    {
        public Vector3[] waypoints;
        public float travelSpeed = 15;
        public int WaypointIndex { get; private set; }
        public override void CheckStatus(float dt) { }
        public override void Move(float dt)
        {
            if (waypoints == null || WaypointIndex >= waypoints.Length) { Destroy(gameObject); return; }
            float budget = travelSpeed * dt;
            while (budget > 0 && WaypointIndex < waypoints.Length)
            {
                Vector3 delta = waypoints[WaypointIndex] - transform.position;
                float step = Mathf.Min(budget, delta.magnitude);
                Vector3 start = transform.position;
                transform.position = Vector3.MoveTowards(start, waypoints[WaypointIndex], step);
                if (delta.sqrMagnitude > .001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta), 1 - Mathf.Exp(-2 * dt));
                // Sweep every leg, including several waypoints crossed during a slow frame.
                previousPosition = start;
                if (TouchesPlayer(1.45f)) target.TakeDamage(100);
                budget -= step;
                if (Vector3.Distance(transform.position, waypoints[WaypointIndex]) < .001f) WaypointIndex++; else break;
            }
        }
        public override void ExecuteAttack(float dt) { }
    }
}
