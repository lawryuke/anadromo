using System.Collections.Generic;
using UnityEngine;

namespace Anadromo.Mechanics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxObjectSpawner))]
    [AddComponentMenu("Anadromo/Swim Group Controller")]
    public class SwimGroupController : MonoBehaviour
    {
        public enum SwimState { Normal, MoveToTarget, AtTarget }
        public enum SwimStyle { Fish, Shark }
        public enum ArrivalMode { StayNear, Circle, ReturnToNormal }

        [Header("Estado y destino (independiente del Target Object del spawner)")]
        public SwimState state = SwimState.Normal;
        public SwimStyle swimStyle = SwimStyle.Fish;
        public Transform movementTarget;
        [Tooltip("Usar el promedio de integrantes activos si el destino tiene Box Object Spawner; en otros grupos, promediar sus hijos directos activos.")]
        public bool useTargetGroupCenter = true;
        public ArrivalMode arrivalMode = ArrivalMode.StayNear;
        [Min(0.1f)] public float arrivalDistance = 1f;

        [Header("Nado normal")]
        [Tooltip("Vacío: permanecer alrededor de la zona inicial. Con puntos: patrullar en orden y repetir.")]
        public Transform[] normalWaypoints = new Transform[0];
        [Min(0f)] public float wanderRadius = 0.5f;
        [Min(0f)] public float wanderFrequency = 0.7f;

        [Header("Velocidad y giro")]
        [Min(0f)] public float swimSpeed = 2f;
        [Min(0.01f)] public float acceleration = 2f;
        [Min(1f)] public float fishTurnSpeed = 180f;
        [Min(1f)] public float sharkTurnSpeed = 45f;
        [Min(0.1f)] public float orbitRadius = 3f;

        [Header("Cohesión y separación")]
        [Min(0f)] public float separationDistance = 0.6f;
        [Min(0f)] public float separationStrength = 2f;

        private sealed class Member
        {
            public Transform transform;
            public Rigidbody body;
            public bool wasKinematic;
            public bool usedGravity;
            public Vector3 offset;
            public Vector3 position;
            public float speed;
            public float phase;
        }

        private readonly Dictionary<GameObject, Member> members = new Dictionary<GameObject, Member>();
        private readonly List<GameObject> removed = new List<GameObject>();
        private readonly List<Member> active = new List<Member>();
        private BoxObjectSpawner spawner;
        private Vector3 home;
        private Vector3 groupCenter;
        private Vector3 arrivalTargetPosition;
        private Transform previousTarget;
        private int waypointIndex;
        private float swimTime;
        private bool initialized;

        public int MemberCount => members.Count;
        public bool Controls(GameObject instance) => isActiveAndEnabled && members.ContainsKey(instance);

        private void OnEnable()
        {
            spawner = GetComponent<BoxObjectSpawner>();
            initialized = false;
            waypointIndex = 0;
            swimTime = 0f;
            previousTarget = movementTarget;
        }

        private void OnDisable()
        {
            foreach (Member member in members.Values) Release(member);
            members.Clear();
            active.Clear();
            initialized = false;
        }

        public void GoToTarget(Transform target)
        {
            movementTarget = target;
            state = target != null ? SwimState.MoveToTarget : SwimState.Normal;
        }

        public void ReturnToNormal() => state = SwimState.Normal;

        private void FixedUpdate()
        {
            SyncMembers();
            if (active.Count == 0) return;
            float dt = Time.fixedDeltaTime;
            swimTime += dt;

            Vector3 actualCenter = Vector3.zero;
            Vector3 offsetCenter = Vector3.zero;
            foreach (Member member in active)
            {
                member.position = member.body != null ? member.body.position : member.transform.position;
                actualCenter += member.position;
                offsetCenter += member.offset;
            }
            actualCenter /= active.Count;
            offsetCenter /= active.Count;

            if (movementTarget != previousTarget)
            {
                if (state == SwimState.AtTarget) state = SwimState.MoveToTarget;
                previousTarget = movementTarget;
            }
            Vector3 destination = home;
            if (state != SwimState.Normal)
            {
                if (movementTarget == null || movementTarget == transform || movementTarget.IsChildOf(transform))
                    state = SwimState.Normal;
                else
                {
                    destination = ResolveTargetPosition();
                    float threshold = Mathf.Max(0.1f, arrivalDistance);
                    if (state == SwimState.AtTarget && Vector3.Distance(destination, arrivalTargetPosition) > threshold * 2f)
                        state = SwimState.MoveToTarget;
                    if (state == SwimState.MoveToTarget && Vector3.Distance(actualCenter, destination) <= threshold)
                    {
                        state = arrivalMode == ArrivalMode.ReturnToNormal ? SwimState.Normal : SwimState.AtTarget;
                        arrivalTargetPosition = destination;
                    }
                }
            }
            if (state == SwimState.Normal) destination = NormalDestination();
            // The parent remains still; this virtual center guides the individual members.
            groupCenter = Vector3.MoveTowards(groupCenter, destination, Mathf.Max(0f, swimSpeed) * 0.65f * dt);

            bool orbit = state != SwimState.MoveToTarget &&
                (swimStyle == SwimStyle.Shark || (state == SwimState.AtTarget && arrivalMode == ArrivalMode.Circle));
            float turnSpeed = Mathf.Max(1f, swimStyle == SwimStyle.Shark ? sharkTurnSpeed : fishTurnSpeed);
            float radius = Mathf.Max(orbitRadius, swimSpeed / (turnSpeed * Mathf.Deg2Rad) * 1.5f, 0.1f);
            foreach (Member member in active)
            {
                Vector3 goal = groupCenter + member.offset - offsetCenter;
                if (orbit)
                {
                    float angle = swimTime * Mathf.Max(0f, swimSpeed) / radius + member.phase;
                    goal += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                }
                else
                {
                    float phase = swimTime * Mathf.Max(0f, wanderFrequency) + member.phase;
                    goal += new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 0.7f) * 0.3f,
                        Mathf.Cos(phase * 0.9f)) * Mathf.Max(0f, wanderRadius);
                }
                Vector3 direction = goal - member.position;
                Vector3 separation = Vector3.zero;
                float separationRange = Mathf.Max(0f, separationDistance);
                foreach (Member other in active)
                {
                    if (member == other) continue;
                    Vector3 away = member.position - other.position;
                    float distance = away.magnitude;
                    if (distance < separationRange)
                    {
                        // Stable, opposite directions also separate initially coincident members.
                        Vector3 axis = distance > 0.0001f ? away / distance :
                            (member.phase < other.phase ? Vector3.right : Vector3.left);
                        separation += axis * (1f - distance / separationRange);
                    }
                }
                direction += separation * Mathf.Max(0f, separationStrength);
                float remaining = direction.magnitude;
                if (remaining < 0.001f) { member.speed = 0f; continue; }
                Vector3 forward = direction / remaining;
                Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;
                Quaternion current = member.body != null ? member.body.rotation : member.transform.rotation;
                Quaternion rotation = Quaternion.RotateTowards(current, Quaternion.LookRotation(forward, up), turnSpeed * dt);
                float desiredSpeed = Mathf.Max(0f, swimSpeed);
                if (swimStyle == SwimStyle.Fish) desiredSpeed *= Mathf.Clamp01(remaining);
                member.speed = Mathf.MoveTowards(member.speed, desiredSpeed, Mathf.Max(0.01f, acceleration) * dt);
                Vector3 position = member.position + rotation * Vector3.forward * Mathf.Min(member.speed * dt, remaining);
                if (member.body != null)
                {
                    member.body.MovePosition(position);
                    member.body.MoveRotation(rotation);
                }
                else member.transform.SetPositionAndRotation(position, rotation);
            }
        }

        private Vector3 NormalDestination()
        {
            if (normalWaypoints == null || normalWaypoints.Length == 0) return home;
            waypointIndex %= normalWaypoints.Length;
            for (int i = 0; i < normalWaypoints.Length; i++)
            {
                Transform point = normalWaypoints[waypointIndex];
                if (point != null)
                {
                    Vector3 result = point.position;
                    if (Vector3.Distance(groupCenter, result) <= Mathf.Max(0.1f, arrivalDistance))
                        waypointIndex = (waypointIndex + 1) % normalWaypoints.Length;
                    return result;
                }
                waypointIndex = (waypointIndex + 1) % normalWaypoints.Length;
            }
            return home;
        }

        private Vector3 ResolveTargetPosition()
        {
            if (!useTargetGroupCenter) return movementTarget.position;
            BoxObjectSpawner targetSpawner = movementTarget.GetComponent<BoxObjectSpawner>();
            Vector3 sum = Vector3.zero;
            int count = 0;
            if (targetSpawner != null)
            {
                foreach (GameObject instance in targetSpawner.GeneratedObjects)
                    if (instance != null && instance.activeInHierarchy) { sum += instance.transform.position; count++; }
            }
            else
            {
                foreach (Transform child in movementTarget)
                    if (child.gameObject.activeInHierarchy) { sum += child.position; count++; }
            }
            return count > 0 ? sum / count : movementTarget.position;
        }

        private void SyncMembers()
        {
            active.Clear();
            removed.Clear();
            foreach (var entry in members)
            {
                bool exists = false;
                foreach (GameObject instance in spawner.GeneratedObjects)
                    if (instance == entry.Key) { exists = true; break; }
                if (!exists || entry.Key == null) removed.Add(entry.Key);
            }
            foreach (GameObject instance in removed) { Release(members[instance]); members.Remove(instance); }
            if (members.Count == 0) initialized = false;
            if (!initialized)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                foreach (GameObject instance in spawner.GeneratedObjects)
                    if (instance != null && instance.activeInHierarchy) { sum += instance.transform.position; count++; }
                if (count == 0) return;
                home = groupCenter = sum / count;
                initialized = true;
            }
            foreach (GameObject instance in spawner.GeneratedObjects)
            {
                if (instance == null || members.ContainsKey(instance)) continue;
                var member = new Member
                {
                    transform = instance.transform,
                    body = instance.GetComponent<Rigidbody>(),
                    offset = instance.transform.position - groupCenter,
                    phase = (instance.GetInstanceID() & 0xFFFF) * 2.399963f
                };
                if (member.body != null)
                {
                    member.wasKinematic = member.body.isKinematic;
                    member.usedGravity = member.body.useGravity;
                    member.body.isKinematic = true;
                    member.body.useGravity = false;
                }
                members.Add(instance, member);
            }
            active.Clear();
            foreach (var entry in members)
                if (entry.Key != null && entry.Key.activeInHierarchy) active.Add(entry.Value);
        }

        private static void Release(Member member)
        {
            if (member.body == null) return;
            member.body.isKinematic = member.wasKinematic;
            member.body.useGravity = member.usedGravity;
        }
    }
}
