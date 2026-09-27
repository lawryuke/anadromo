using UnityEngine;

namespace Anadromo.AI
{
    public sealed class PiranhaSchool : MonoBehaviour
    {
        public PiranhaPlayerTarget target;
        public Vector3 zoneSize = new Vector3(8,4,10);
        public LayerMask obstacleLayers = Physics.DefaultRaycastLayers;
        public float alertRadius = 20;
        public bool Contains(Vector3 point) => new Bounds(Vector3.zero,zoneSize).Contains(transform.InverseTransformPoint(point));
        public Vector3 Clamp(Vector3 point)
        {
            Vector3 p = transform.InverseTransformPoint(point), half = zoneSize*.5f-Vector3.one*.3f;
            p = new Vector3(Mathf.Clamp(p.x,-half.x,half.x),Mathf.Clamp(p.y,-half.y,half.y),Mathf.Clamp(p.z,-half.z,half.z));
            return transform.TransformPoint(p);
        }
        public bool IsObstacle(Collider c) => c && !c.transform.IsChildOf(transform) && (!target || !c.transform.IsChildOf(target.transform.root));
        public bool ClearPath(Vector3 from, Vector3 to)
        {
            Vector3 d = to-from;
            foreach (var hit in Physics.RaycastAll(from,d.normalized,d.magnitude,obstacleLayers,QueryTriggerInteraction.Ignore))
                if (IsObstacle(hit.collider)) return false;
            return true;
        }
        public Vector3 MoveWithoutObstacles(Vector3 from, Vector3 to, float radius)
        {
            Vector3 delta=to-from;
            float distance=delta.magnitude;
            if(distance<.00001f) return from;
            foreach(var hit in Physics.SphereCastAll(from,radius,delta/distance,distance,obstacleLayers,QueryTriggerInteraction.Ignore))
                if(IsObstacle(hit.collider)) distance=Mathf.Min(distance,Mathf.Max(0,hit.distance-.02f));
            return from+delta.normalized*distance;
        }
        public void Alert(Esc2Piranha source)
        {
            // Hierarchy membership makes Ctrl+D and prefab instances work without lists.
            foreach (var fish in GetComponentsInChildren<Esc2Piranha>())
                if (Vector3.Distance(source.transform.position,fish.transform.position) <= alertRadius && ClearPath(source.transform.position,fish.transform.position)) fish.BeginAttack();
        }
        public void ResetFish()
        {
            foreach(var fish in GetComponentsInChildren<Esc2Piranha>(true)) fish.ResetFish();
            foreach(var fish in GetComponentsInChildren<Esc2Anglerfish>(true)) fish.ResetFish();
            foreach(var fish in GetComponentsInChildren<Esc2Lamprey>(true)) fish.ResetLamprey();
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan; Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero,zoneSize);
        }
    }
}
