using UnityEngine;

namespace Anadromo.Mechanics
{
    public class PredatorEating : MonoBehaviour
    {
        [Header("Hunting Settings")]
        [Tooltip("Tag de la raíz de la presa, por ejemplo Food_Krill.")]
        public string preyTag = "Food";
        public LayerMask preyLayers = ~0;
        [Tooltip("Después de la primera comida busca la presa más cercana alrededor de su boca, aunque pertenezca a otro grupo.")]
        [Min(0f)] public float nearbyPreySearchRadius = 10f;

        [Header("Boca")]
        [Tooltip("SphereCollider de la boca, incluso si está en un hijo. Si no se asigna, busca una esfera trigger.")]
        public SphereCollider mouthCollider;
        [Tooltip("Punto de boca alternativo cuando no hay esfera. Sin asignar, usa este Transform.")]
        public Transform mouthPoint;
        [Min(0.001f)] public float biteRadius = 0.15f;
        public LayerMask biteBlockingLayers = ~0;

        [Header("Hunger System")]
        [Min(1)] public int minMeals = 1;
        [Min(1)] public int maxMeals = 3;
        private int mealsLimit;
        private bool hungerInitialized;
        private Transform owner;
        private bool controlled;
        private bool huntingEnabled;
        private Transform huntingScope;
        public bool unlimitedMeals;

        public void SetHunt(Transform target, string tag, bool active)
        {
            controlled = true;
            huntingScope = target;
            huntingEnabled = active;
            preyTag = tag;
            unlimitedMeals = true;
        }
        public int mealsEaten { get; private set; }
        public bool IsFull { get { InitializeHunger(); return !unlimitedMeals && mealsEaten >= mealsLimit; } }
        public int MealsLimit { get { InitializeHunger(); return mealsLimit; } }
        public Transform Owner => owner != null ? owner : transform;
        public Vector3 MouthPosition => mouthCollider != null
            ? mouthCollider.transform.TransformPoint(mouthCollider.center)
            : mouthPoint != null ? mouthPoint.position : transform.position;
        public float MouthRadius
        {
            get
            {
                if (mouthCollider == null) return Mathf.Max(0.001f, biteRadius);
                Vector3 scale = mouthCollider.transform.lossyScale;
                return Mathf.Max(0.001f, mouthCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            }
        }

        public void BindOwner(Transform root)
        {
            owner = root;
            if (mouthCollider == null)
                foreach (SphereCollider sphere in root.GetComponentsInChildren<SphereCollider>())
                    if (sphere.enabled && sphere.isTrigger) { mouthCollider = sphere; break; }
        }

        private void Start()
        {
            InitializeHunger();
            BindOwner(Owner);
        }

        private void InitializeHunger()
        {
            if (hungerInitialized) return;
            int minimum = Mathf.Max(1, minMeals);
            mealsLimit = Random.Range(minimum, Mathf.Max(minimum, maxMeals) + 1);
            hungerInitialized = true;
        }

        public void ResetHunger()
        {
            mealsEaten = 0;
            hungerInitialized = false;
        }

        public Transform ResolvePrey(Transform candidate)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy || string.IsNullOrEmpty(preyTag)) return null;
            if (controlled && (!huntingEnabled || huntingScope == null)) return null;
            Prey marker = candidate.GetComponentInParent<Prey>();
            if (marker == null || !marker.isActiveAndEnabled || marker.IsConsumed || marker.tag != preyTag) return null;
            Transform result = marker.transform;
            if (result == Owner || result.IsChildOf(Owner) || Owner.IsChildOf(result)) return null;
            if (controlled && !result.IsChildOf(huntingScope)) return null;
            return result;
        }

        private void FixedUpdate() => TryEatNearby();

        public Transform FindNearestNearbyPrey(Transform ignoredPrey = null)
        {
            if (IsFull || nearbyPreySearchRadius <= 0f) return null;
            Physics.SyncTransforms();
            Transform nearest = null;
            float nearestDistance = float.PositiveInfinity;
            Vector3 origin = MouthPosition;
            foreach (Collider collider in Physics.OverlapSphere(origin, nearbyPreySearchRadius,
                preyLayers, QueryTriggerInteraction.Collide))
            {
                Transform prey = ResolvePrey(collider.transform);
                if (prey == null || prey == ignoredPrey) continue;
                float distance = (collider.ClosestPoint(origin) - origin).sqrMagnitude;
                if (distance < nearestDistance) { nearestDistance = distance; nearest = prey; }
            }
            return nearest;
        }
        private void OnTriggerEnter(Collider other) => TryEatCollider(other);
        private void OnTriggerStay(Collider other) => TryEatCollider(other);

        public void TryEatNearby()
        {
            if (!isActiveAndEnabled || IsFull) return;
            Physics.SyncTransforms();
            foreach (Collider other in Physics.OverlapSphere(MouthPosition, MouthRadius, preyLayers, QueryTriggerInteraction.Collide))
            {
                if (TryEatCollider(other) && IsFull) break;
            }
        }

        private bool TryEatCollider(Collider other)
        {
            if (!isActiveAndEnabled || IsFull || other == null || !other.enabled ||
                (preyLayers.value & (1 << other.gameObject.layer)) == 0) return false;
            Transform prey = ResolvePrey(other.transform);
            if (prey == null) return false;
            Vector3 mouth = MouthPosition;
            Vector3 point = other.ClosestPoint(mouth);
            if ((point - mouth).sqrMagnitude > MouthRadius * MouthRadius) return false;
            Vector3 ray = point - mouth;
            if (ray.sqrMagnitude > 0.000001f)
                foreach (RaycastHit hit in Physics.RaycastAll(mouth, ray.normalized, ray.magnitude,
                    biteBlockingLayers, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(Owner) && !hit.transform.IsChildOf(prey)) return false;
            Prey marker = prey.GetComponent<Prey>();
            if (marker == null || !marker.TryConsume()) return false;
            mealsEaten++;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(MouthPosition, MouthRadius);
        }
    }
}
