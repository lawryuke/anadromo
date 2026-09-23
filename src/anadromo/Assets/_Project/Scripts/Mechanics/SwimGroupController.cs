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

        [Header("Individual Hunting (Caza Individual)")]
        [Tooltip("Persigue presas con el tag de PredatorEating dentro de Movement Target (un spawner, un grupo o una presa). Activo en Move To Target y At Target.")]
        public bool individualHunting = true;
        [Tooltip("Si dos peces persiguen el mismo objetivo y su distancia es menor a este valor ('q'), el más lejano cambiará de presa. 0 desactiva esta función.")]
        [Min(0f)] public float preyContentionDistance = 0f;

        [Header("Nado normal")]
        [Tooltip("Si se asigna, los peces siempre regresarán a este objeto invisible cuando terminen de cazar (estado Normal).")]
        public Transform customHome;

        [Tooltip("Vacío: permanecer alrededor de la zona inicial (o customHome). Con puntos: patrullar en orden y repetir.")]
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
        [Tooltip("Los nadadores del mismo perfil y dieta se separan por dirección, sin bloquearse como paredes con sus volúmenes de colisión. Incluye otros grupos.")]
        public bool avoidBlockingOtherSwimmers = true;

        [Header("Control de Teclado (Solo aplicable para perfil Shark)")]
        [Tooltip("Tecla para iniciar el nado si el perfil es Shark.")]
        public KeyCode startKeyForShark = KeyCode.Alpha4;
        private bool hasStartedMoving = false;

        [Header("Inicio de peces: espera y oscilación")]
        public bool useFishStartSequence;
        public KeyCode startKeyForFish = KeyCode.Alpha1;
        [Tooltip("Objetivo exclusivo de la oscilación inicial. Después se usa Movement Target. Si queda vacío, se usa Movement Target o Target Object como antes.")]
        public Transform fishOscillationTarget;
        [Min(0f)] public float fishOscillationDuration = 3f;
        [Tooltip("Desplazamiento máximo desde la posición inicial, en unidades de mundo, sobre el eje hacia el target.")]
        [Min(0f)] public float fishOscillationAmplitude = 0.3f;
        [Tooltip("Ciclos por segundo, con variación aleatoria individual.")]
        [Min(0f)] public float fishOscillationFrequency = 1f;
        private bool fishSequenceStarted;
        private bool fishSequenceFinished;
        private float fishSequenceTime;
        public string FishStartStatus => !useFishStartSequence || swimStyle != SwimStyle.Fish
            ? "Desactivado" : fishSequenceFinished ? "Nado habitual" : fishSequenceStarted ? "Oscilando" : "Esperando tecla";

        [Header("Colisión durante el nado")]
        public bool collideWithObstacles = true;
        public LayerMask collisionLayers = ~0;
        [Tooltip("Los triggers externos normalmente son zonas de detección, no paredes.")]
        public bool collideWithTriggers;
        [Tooltip("Calcula al registrar cada copia una esfera que envuelve sus colliders (o renderers si no tiene colliders). Es conservadora para peces largos.")]
        public bool automaticCollisionRadius = true;
        [Tooltip("Radio mínimo en unidades de mundo; radio exacto del barrido si Automatic Collision Radius está desactivado.")]
        [Min(0.01f)] public float collisionRadius = 0.15f;
        [Min(0.001f)] public float collisionSkin = 0.02f;
        public bool slideAlongObstacles = true;
        [Range(1, 16)] public int overlapRecoveryIterations = 8;
        public string LastCollisionObstacle { get; private set; }
        public int RecoveredOverlaps { get; private set; }
        private SphereCollider collisionProbe;

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
            public float initialRadius;
            public Vector3 initialScale;
            public Quaternion modelCorrection;
            public Transform currentPrey;
            public PredatorEating stomach;
            public int observedMeals;
            public bool oscillationInitialized;
            public Vector3 oscillationOrigin;
            public Vector3 oscillationAxis;
            public float waitTimer;
            public Vector3 currentTargetPos;
        }

        private readonly Dictionary<GameObject, Member> members = new Dictionary<GameObject, Member>();
        private readonly List<GameObject> removed = new List<GameObject>();
        private readonly List<Member> active = new List<Member>();
        private readonly List<Member> peerMembers = new List<Member>();
        private static readonly HashSet<SwimGroupController> enabledControllers = new HashSet<SwimGroupController>();
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

        public void UpdateHomePosition(Vector3 newHome)
        {
            if (customHome != null) return; // Si hay una caja final configurada, no la sobrescribimos
            home = newHome;
            // Al actualizar el home, acercamos el centro del grupo para que no vuelvan desde muy lejos
            groupCenter = Vector3.Lerp(groupCenter, home, 0.5f);
        }

        private void OnEnable()
        {
            enabledControllers.Add(this);
            spawner = GetComponent<BoxObjectSpawner>();
            initialized = false;
            waypointIndex = 0;
            swimTime = 0f;
            previousTarget = movementTarget;
            hasStartedMoving = false;
            fishSequenceStarted = false;
            fishSequenceFinished = false;
            fishSequenceTime = 0f;
        }

        private void OnDisable()
        {
            if (collisionProbe != null)
            {
                if (Application.isPlaying) Destroy(collisionProbe.gameObject);
                else DestroyImmediate(collisionProbe.gameObject);
                collisionProbe = null;
            }
            enabledControllers.Remove(this);
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

        public bool TryGetSharkInitialTarget(out Vector3 position)
        {
            position = default;
            if (swimStyle != SwimStyle.Shark || movementTarget == null ||
                movementTarget == transform || movementTarget.IsChildOf(transform)) return false;
            position = ResolveTargetPosition();
            return true;
        }

        public void StartSharkMovement()
        {
            if (swimStyle == SwimStyle.Shark && !hasStartedMoving)
            {
                hasStartedMoving = true;
                Debug.Log($"[{gameObject.name}] Estampida de tiburón iniciada automáticamente.");
            }
        }

        private void Update()
        {
            if (swimStyle == SwimStyle.Fish && useFishStartSequence && !fishSequenceStarted &&
                Input.GetKeyDown(startKeyForFish)) StartFishSequence();
            
            // La estampida del tiburón ahora se inicia externamente usando StartSharkMovement()
        }

        private void FixedUpdate()
        {
            SyncMembers();
            if (active.Count == 0) return;
            peerMembers.Clear();
            foreach (SwimGroupController controller in enabledControllers)
            {
                if (controller == null || !controller.isActiveAndEnabled || controller.swimStyle != swimStyle) continue;
                foreach (Member peer in controller.members.Values)
                    if (peer.transform != null && peer.transform.gameObject.activeInHierarchy) peerMembers.Add(peer);
            }

            // Si es perfil Shark y aún no se presionó la tecla, no hacemos nada de movimiento
            if (swimStyle == SwimStyle.Shark && !hasStartedMoving) return;

            if (collideWithObstacles) Physics.SyncTransforms();
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

            if (swimStyle == SwimStyle.Fish && useFishStartSequence && !fishSequenceFinished)
            {
                if (fishSequenceStarted) UpdateFishSequence(dt);
                return;
            }

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

            bool orbit = (state == SwimState.Normal && swimStyle == SwimStyle.Shark) ||
                (state == SwimState.AtTarget && arrivalMode == ArrivalMode.Circle);
            bool linearShark = swimStyle == SwimStyle.Shark && state != SwimState.Normal && !orbit;
            float turnSpeed = Mathf.Max(1f, swimStyle == SwimStyle.Shark ? sharkTurnSpeed : fishTurnSpeed);
            float radius = Mathf.Max(orbitRadius, swimSpeed / (turnSpeed * Mathf.Deg2Rad) * 1.5f, 0.1f);

            bool canHunt = individualHunting && movementTarget != null && state != SwimState.Normal;

            foreach (Member member in active)
            {
                Vector3 goal = (linearShark ? destination : groupCenter) + member.offset - offsetCenter;
                bool hunting = false;
                if (member.stomach != null && member.stomach.isActiveAndEnabled && !member.stomach.IsFull &&
                    (canHunt || (individualHunting && member.stomach.mealsEaten > 0)))
                {
                    member.stomach.TryEatNearby();
                    if (member.observedMeals != member.stomach.mealsEaten)
                    {
                        member.currentPrey = null;
                        member.observedMeals = member.stomach.mealsEaten;
                    }
                    bool firstMeal = member.stomach.mealsEaten == 0;
                    if (member.currentPrey == null ||
                        (firstMeal && (movementTarget == null || !member.currentPrey.IsChildOf(movementTarget))) ||
                        member.stomach.ResolvePrey(member.currentPrey) == null)
                        member.currentPrey = FindBestPrey(member, movementTarget);

                    // --- Lógica de contención de presas ---
                    if (member.currentPrey != null && preyContentionDistance > 0f)
                    {
                        float myDistToPrey = (member.position - member.currentPrey.position).sqrMagnitude;
                        float qSq = preyContentionDistance * preyContentionDistance;

                        foreach (Member other in active)
                        {
                            if (other != member && other.currentPrey == member.currentPrey)
                            {
                                float otherDistToPrey = (other.position - member.currentPrey.position).sqrMagnitude;
                                if (otherDistToPrey < myDistToPrey)
                                {
                                    float distToOther = (other.position - member.position).sqrMagnitude;
                                    if (distToOther < qSq)
                                    {
                                        // El 'other' está más cerca de la presa y nosotros muy cerca de él.
                                        // Buscamos otra presa, ignorando la actual.
                                        member.currentPrey = FindBestPrey(member, movementTarget, member.currentPrey);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    // --------------------------------------

                    hunting = !member.stomach.IsFull && member.currentPrey != null;
                    if (hunting)
                    {
                        // Bring the mouth to the prey, rather than stopping the root at the group center.
                        Vector3 mouthOffset = member.stomach.MouthPosition - member.position;
                        goal = member.currentPrey.position - mouthOffset;
                    }
                }
                if (!hunting)
                {
                    member.currentPrey = null;
                    if (orbit)
                    {
                        float angle = swimTime * Mathf.Max(0f, swimSpeed) / radius + member.phase;
                        goal += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    }
                    else if (!linearShark)
                    {
                        float phase = swimTime * Mathf.Max(0f, wanderFrequency) + member.phase;
                        goal += new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 0.7f) * 0.3f,
                            Mathf.Cos(phase * 0.9f)) * Mathf.Max(0f, wanderRadius);
                    }
                }
                Vector3 direction = goal - member.position;
                Vector3 separation = Vector3.zero;
                float separationRange = Mathf.Max(0f, separationDistance);
                foreach (Member other in peerMembers)
                {
                    if (member == other || !ArePeers(member, other)) continue;
                    Vector3 otherPosition = other.body != null ? other.body.position : other.transform.position;
                    Vector3 away = member.position - otherPosition;
                    float distance = away.magnitude;
                    if (distance < separationRange)
                    {
                        // Stable, opposite directions also separate initially coincident members.
                        Vector3 axis = distance > 0.0001f ? away / distance :
                            (member.phase < other.phase ? Vector3.right : Vector3.left);
                        separation += axis * (1f - distance / separationRange);
                    }
                }
                if (!linearShark)
                {
                    // Keep a distant prey from overpowering the local separation force.
                    if (hunting) direction = Vector3.ClampMagnitude(direction, 1f);
                    direction += separation * Mathf.Max(0f, separationStrength);
                }
                float remaining = direction.magnitude;
                if (remaining < 0.001f) { member.speed = 0f; continue; }
                Vector3 forward = direction / remaining;
                Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;
                Quaternion current = member.body != null ? member.body.rotation : member.transform.rotation;
                Quaternion heading = current * Quaternion.Inverse(member.modelCorrection);
                heading = Quaternion.RotateTowards(heading, Quaternion.LookRotation(forward, up), turnSpeed * dt);
                Quaternion rotation = heading * member.modelCorrection;
                float desiredSpeed = Mathf.Max(0f, swimSpeed);
                if (swimStyle == SwimStyle.Fish)
                {
                    // Slow down through tight turns and propel along the fish's heading,
                    // including while hunting, instead of sliding sideways toward prey.
                    float alignment = Mathf.Clamp01(Vector3.Dot(heading * Vector3.forward, forward));
                    float stroke = .85f + .15f * Mathf.Sin(swimTime * .7f + member.phase);
                    desiredSpeed *= Mathf.Clamp01(remaining) * alignment * stroke;
                }
                member.speed = Mathf.MoveTowards(member.speed, desiredSpeed, Mathf.Max(0.01f, acceleration) * dt);
                Vector3 travelDirection = linearShark || (hunting && swimStyle == SwimStyle.Shark)
                    ? forward : heading * Vector3.forward;
                Vector3 position = member.position + travelDirection * Mathf.Min(member.speed * dt, remaining);
                if (collideWithObstacles)
                    position = ResolveCollision(member, position - member.position, !linearShark);
                if (member.body != null)
                {
                    member.body.MovePosition(position);
                    member.body.MoveRotation(rotation);
                }
                else member.transform.SetPositionAndRotation(position, rotation);
                if (hunting) member.stomach.TryEatNearby();
            }
        }

        public void StartFishSequence()
        {
            if (!useFishStartSequence || swimStyle != SwimStyle.Fish || fishSequenceStarted) return;
            if (fishOscillationTarget == null && movementTarget == null && spawner.targetObject == null && fishOscillationDuration > 0f)
            {
                Debug.LogWarning("Asigna Fish Oscillation Target, Movement Target o Target Object para iniciar la oscilación de peces.", this);
                return;
            }
            fishSequenceStarted = true;
            fishSequenceTime = 0f;
        }

        private void UpdateFishSequence(float dt)
        {
            float duration = Mathf.Max(0f, fishOscillationDuration);
            fishSequenceTime = Mathf.Min(fishSequenceTime + dt, duration);
            float progress = duration > 0f ? fishSequenceTime / duration : 1f;
            
            foreach (Member member in active)
            {
                if (!member.oscillationInitialized)
                {
                    member.oscillationOrigin = member.position;
                    member.currentTargetPos = member.oscillationOrigin;
                    member.waitTimer = Random.Range(0.5f, 1.5f);
                    
                    Vector3 targetPosition = fishOscillationTarget != null ? fishOscillationTarget.position :
                        movementTarget != null ? ResolveTargetPosition() :
                        spawner.targetObject != null ? spawner.targetObject.position : member.position;
                    Vector3 direction = targetPosition - member.position;
                    member.oscillationAxis = direction.sqrMagnitude > 0.000001f ? direction.normalized :
                        member.transform.rotation * Quaternion.Inverse(member.modelCorrection) * Vector3.forward;
                        
                    member.oscillationInitialized = true;
                }

                member.waitTimer -= dt;
                if (member.waitTimer <= 0f)
                {
                    member.waitTimer = Random.Range(0.5f, 1.5f);

                    float amp = Mathf.Max(0f, fishOscillationAmplitude);
                    float[] choices = { -amp, 0f, amp };
                    float zOffset = choices[Random.Range(0, choices.Length)];
                    float yOffset = choices[Random.Range(0, choices.Length)];

                    if (zOffset == 0f && yOffset == 0f)
                    {
                        zOffset = amp;
                    }

                    Vector3 forward = member.oscillationAxis;
                    Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;
                    Vector3 right = Vector3.Cross(up, forward).normalized;
                    Vector3 realUp = Vector3.Cross(forward, right).normalized;

                    member.currentTargetPos = member.oscillationOrigin + forward * zOffset + realUp * yOffset;
                }

                // Smoothly lerp towards target position
                Vector3 position = Vector3.Lerp(member.position, member.currentTargetPos, dt * 2f);
                if (collideWithObstacles) position = ResolveCollision(member, position - member.position, false);

                Vector3 faceUp = Mathf.Abs(Vector3.Dot(member.oscillationAxis, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;
                Quaternion rotation = Quaternion.RotateTowards(member.transform.rotation,
                    Quaternion.LookRotation(member.oscillationAxis, faceUp) * member.modelCorrection,
                    Mathf.Max(1f, fishTurnSpeed) * dt);

                if (member.body != null)
                {
                    member.body.MovePosition(position);
                    member.body.MoveRotation(rotation);
                }
                else member.transform.SetPositionAndRotation(position, rotation);
                member.speed = 0f;
            }
            if (progress >= 1f) fishSequenceFinished = true;
        }

        private Vector3 ResolveCollision(Member member, Vector3 displacement, bool allowSliding = true)
        {
            float radius = Mathf.Max(0.01f, collisionRadius);
            if (automaticCollisionRadius)
            {
                Vector3 scale = member.transform.lossyScale;
                float ratio = Mathf.Max(ScaleRatio(scale.x, member.initialScale.x),
                    ScaleRatio(scale.y, member.initialScale.y), ScaleRatio(scale.z, member.initialScale.z));
                radius = Mathf.Max(radius, member.initialRadius * ratio);
            }
            float skin = Mathf.Max(0.001f, collisionSkin);
            Vector3 position = member.position;
            QueryTriggerInteraction triggers = collideWithTriggers
                ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

            // Recover initial penetration instead of permanently cancelling all motion.
            // Unity's custom-controller pattern: overlap query + minimum translation.
            if (collisionProbe == null)
            {
                var probeObject = new GameObject("Swim collision query");
                probeObject.hideFlags = HideFlags.HideAndDontSave;
                probeObject.SetActive(false);
                collisionProbe = probeObject.AddComponent<SphereCollider>();
                collisionProbe.enabled = false;
                collisionProbe.isTrigger = true;
                probeObject.SetActive(true);
            }
            collisionProbe.radius = radius;
            for (int recovery = 0; recovery < Mathf.Clamp(overlapRecoveryIterations, 1, 16); recovery++)
            {
                bool corrected = false;
                foreach (Collider obstacle in Physics.OverlapSphere(position, radius, collisionLayers, triggers))
                {
                    if (!Blocks(member, obstacle)) continue;
                    LastCollisionObstacle = obstacle.name;
                    if (!ComputeObstaclePenetration(position, obstacle, out Vector3 escape, out float depth) || depth <= 0f) continue;
                    position += escape * (depth + skin);
                    RecoveredOverlaps++;
                    corrected = true;
                }
                if (!corrected) break;
            }
            // A volume too large for a gap must not sweep through the surrounding walls.
            // Keep any recovery progress and retry next step if the constraints cannot be solved.
            foreach (Collider obstacle in Physics.OverlapSphere(position, radius, collisionLayers, triggers))
                if (Blocks(member, obstacle) && ComputeObstaclePenetration(position, obstacle, out _, out float depth) && depth > 0.00001f)
                    return position;

            for (int iteration = 0; iteration < 3; iteration++)
            {
                float distance = displacement.magnitude;
                if (distance < 0.00001f) break;
                Vector3 direction = displacement / distance;
                bool blocked = false;
                RaycastHit nearest = default;
                foreach (RaycastHit hit in Physics.SphereCastAll(position, radius, direction,
                    distance + skin, collisionLayers, triggers))
                {
                    if (!Blocks(member, hit.collider)) continue;
                    if (!blocked || hit.distance < nearest.distance)
                    {
                        blocked = true;
                        nearest = hit;
                    }
                }
                if (!blocked) { position += displacement; break; }
                LastCollisionObstacle = nearest.collider.name;
                float travel = Mathf.Clamp(nearest.distance - skin, 0f, distance);
                position += direction * travel;
                if (!slideAlongObstacles || !allowSliding) break;
                // Sweep again after projecting the remaining step, so corners cannot be cut.
                displacement = Vector3.ProjectOnPlane(direction * (distance - travel), nearest.normal);
            }
            return position;
        }

        private bool ComputeObstaclePenetration(Vector3 position, Collider obstacle, out Vector3 direction, out float depth)
        {
            // Unity 6 requires an enabled native shape for ComputePenetration.
            // Enable only inside this synchronous query, never during a physics step
            // or an overlap/cast query; the helper is not a physical scene obstacle.
            collisionProbe.enabled = true;
            try
            {
                return Physics.ComputePenetration(collisionProbe, position, Quaternion.identity,
                    obstacle, obstacle.transform.position, obstacle.transform.rotation, out direction, out depth);
            }
            finally { collisionProbe.enabled = false; }
        }

        private static float ScaleRatio(float current, float initial)
        {
            return Mathf.Abs(current) / Mathf.Max(0.00001f, Mathf.Abs(initial));
        }

        private static bool ArePeers(Member member, Member other)
        {
            if (member.stomach == null || other.stomach == null)
                return member.stomach == null && other.stomach == null;
            return member.stomach.preyTag == other.stomach.preyTag;
        }

        private bool Blocks(Member member, Collider obstacle)
        {
            if (obstacle != null && avoidBlockingOtherSwimmers)
                foreach (Member peer in peerMembers)
                    if (peer != member && peer.transform != null && ArePeers(member, peer) &&
                        obstacle.transform.IsChildOf(peer.transform)) return false;
            return obstacle != null && !obstacle.transform.IsChildOf(member.transform) &&
                !(member.currentPrey != null && member.stomach != null && !member.stomach.IsFull &&
                    obstacle.transform.IsChildOf(member.currentPrey)) &&
                !Physics.GetIgnoreLayerCollision(member.transform.gameObject.layer, obstacle.gameObject.layer);
        }

        private static float MeasureRadius(Transform root)
        {
            float radius = 0f;
            bool hasCollider = false;
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                hasCollider = true;
                Bounds bounds = collider.bounds;
                radius = Mathf.Max(radius, Vector3.Distance(root.position, bounds.center) + bounds.extents.magnitude);
            }
            if (!hasCollider)
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled) continue;
                    Bounds bounds = renderer.bounds;
                    radius = Mathf.Max(radius, Vector3.Distance(root.position, bounds.center) + bounds.extents.magnitude);
                }
            return radius;
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

        private Transform FindBestPrey(Member member, Transform target, Transform ignoredPrey = null)
        {
            if (member.stomach.IsFull) return null;
            if (member.stomach.mealsEaten > 0)
            {
                // Permitir búsqueda libre solo si no están ya enfocados en un grupo asustadizo
                if (target == null || target.GetComponent<Anadromo.AI.ScaredKrillBehavior>() == null)
                {
                    return member.stomach.FindNearestNearbyPrey(ignoredPrey);
                }
            }
            if (target == null) return null;
            var candidates = new List<Transform>();
            BoxObjectSpawner targetSpawner = target.GetComponent<BoxObjectSpawner>();
            if (targetSpawner != null)
            {
                foreach (GameObject instance in targetSpawner.GeneratedObjects)
                {
                    if (instance == null) continue;
                    Transform prey = member.stomach.ResolvePrey(instance.transform);
                    if (prey != null && prey != ignoredPrey && !candidates.Contains(prey)) candidates.Add(prey);
                }
            }
            else
            {
                foreach (Transform child in target.GetComponentsInChildren<Transform>())
                {
                    Transform prey = member.stomach.ResolvePrey(child);
                    if (prey != null && prey != ignoredPrey && !candidates.Contains(prey)) candidates.Add(prey);
                }
            }
            if (candidates.Count == 0) return null;
            if (member.stomach.mealsEaten == 0) return candidates[Random.Range(0, candidates.Count)];
            Transform closest = null;
            float distance = float.PositiveInfinity;
            foreach (Transform prey in candidates)
            {
                float candidateDistance = (member.stomach.MouthPosition - prey.position).sqrMagnitude;
                if (candidateDistance < distance) { closest = prey; distance = candidateDistance; }
            }
            return closest;
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
                    modelCorrection = spawner.ModelRotationCorrection,
                    stomach = instance.GetComponentInChildren<PredatorEating>(),
                    offset = instance.transform.position - groupCenter,
                    phase = (instance.GetInstanceID() & 0xFFFF) * 2.399963f
                };
                Physics.SyncTransforms();
                // Also orient copies made in edit mode before the movement target was assigned.
                // This is a one-time setup, before the keyboard gate, not continuous tracking.
                if (swimStyle == SwimStyle.Shark)
                {
                    Quaternion initialRotation = spawner.GetInitialRotation(instance.transform.position, instance.transform.rotation);
                    instance.transform.rotation = initialRotation;
                    if (member.body != null) member.body.rotation = initialRotation;
                    Physics.SyncTransforms();
                }
                if (member.stomach != null) member.stomach.BindOwner(instance.transform);
                member.initialRadius = MeasureRadius(instance.transform);
                member.initialScale = instance.transform.lossyScale;
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
