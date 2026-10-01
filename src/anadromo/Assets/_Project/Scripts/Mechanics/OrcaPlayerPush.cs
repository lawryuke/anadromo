using Anadromo.Systems;
using UnityEngine;

namespace Anadromo.Mechanics
{
    [DisallowMultipleComponent]
    public sealed class OrcaPlayerPush : MonoBehaviour
    {
        [Min(0)] public float pushSpeed = 3f;
        [Min(.1f)] public float effectSeconds = 2f;
        public AudioClip pushClip;
        [Range(0, 1)] public float volume = 1f;
        CapsuleCollider hull;

        void Awake()
        {
            // The visual prefabs have no physical body. Fit a capsule in model space,
            // so imported rotations and instance scale are respected.
            Bounds bounds = default;
            bool found = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                var local = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (!found) { enabled = false; return; }
            int axis = bounds.size.x > bounds.size.y ? 0 : 1;
            if (bounds.size.z > bounds.size[axis]) axis = 2;
            hull = gameObject.AddComponent<CapsuleCollider>();
            hull.isTrigger = true;
            hull.direction = axis;
            hull.center = bounds.center;
            hull.radius = Mathf.Max(.01f, Mathf.Min(bounds.extents[(axis + 1) % 3], bounds.extents[(axis + 2) % 3]) * .8f);
            hull.height = Mathf.Max(hull.radius * 2, bounds.size[axis] * .9f);
        }

        void OnTriggerEnter(Collider other)
        {
            // Mouth/zone triggers must not turn proximity into a body collision.
            if (!isActiveAndEnabled || other.isTrigger || !other.attachedRigidbody) return;
            var player = other.attachedRigidbody.GetComponent<PlayerEnergyController>();
            if (!player || !player.HasEnergy || player.Energy.ConsumptionPaused) return;
            var impact = player.GetComponent<PlayerOrcaImpact>();
            if (!impact) impact = player.gameObject.AddComponent<PlayerOrcaImpact>();
            Vector3 direction;
            if (!Physics.ComputePenetration(hull, hull.transform.position, hull.transform.rotation,
                other, other.transform.position, other.transform.rotation, out Vector3 intoOrca, out _))
                direction = other.bounds.center - hull.bounds.center;
            else direction = -intoOrca;
            if (direction.sqrMagnitude < .0001f) direction = transform.right;
            impact.Push(direction.normalized * pushSpeed, effectSeconds, pushClip, volume);
        }

        void OnDisable() { if (hull) hull.enabled = false; }
        void OnEnable() { if (hull) hull.enabled = true; }
    }
}
