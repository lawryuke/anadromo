using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Anadromo.Systems;

namespace Anadromo.Mechanics
{
    [RequireComponent(typeof(SphereCollider))]
    public class PlayerFeeding : MonoBehaviour
    {
        [SerializeField] private float mouthRadius = 1f;
        public string[] edibleTags = { "Food_PlayerOnly", "Food_PlayerOnly_First" };
        public UnityEvent OnPreyConsumed = new UnityEvent();
        public bool consumptionEnabled = true;
        public int TotalConsumed { get; private set; }
        public string LastConsumedTag { get; private set; }
        public LayerMask biteBlockingLayers = ~0;
        readonly Dictionary<string, int> consumed = new Dictionary<string, int>();
        EnergySystem energy;
        SphereCollider mouth;

        void Awake()
        {
            energy = GetComponentInParent<EnergySystem>();
            mouth = GetComponent<SphereCollider>();
            mouth.isTrigger = true;
            mouth.radius = mouthRadius;
        }

        public int ConsumedWithTag(string tag) => consumed.TryGetValue(tag, out int count) ? count : 0;
        void OnTriggerEnter(Collider other) => TryEat(other);
        void OnTriggerStay(Collider other) => TryEat(other);

        public bool TryEat(Collider other)
        {
            if (!consumptionEnabled || !isActiveAndEnabled || other == null || !other.enabled ||
                other.transform.IsChildOf(transform.root)) return false;
            Prey prey = other.GetComponentInParent<Prey>();
            if (prey == null || !prey.isActiveAndEnabled || prey.IsConsumed) return false;
            string tag = prey.tag;
            bool edible = edibleTags == null || edibleTags.Length == 0;
            if (edibleTags != null) foreach (string allowed in edibleTags) edible |= tag == allowed;
            if (!edible) return false;
            Vector3 origin = transform.TransformPoint(mouth.center);
            float radius = mouth.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z));
            Vector3 delta = other.ClosestPoint(origin) - origin;
            if (delta.sqrMagnitude > radius * radius) return false;
            if (delta.sqrMagnitude > .000001f)
                foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
                    biteBlockingLayers, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(transform.root) && !hit.transform.IsChildOf(prey.transform)) return false;
            float value = prey.energyValue;
            if (!prey.TryConsume()) return false;
            TotalConsumed++;
            LastConsumedTag = tag;
            consumed[tag] = ConsumedWithTag(tag) + 1;
            if (energy != null) energy.RestoreEnergy(value);
            OnPreyConsumed.Invoke();
            return true;
        }
    }
}
