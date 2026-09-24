using UnityEngine;

namespace Anadromo.Mechanics
{
    public enum BloopAttackPhase { Stalking, Warning, Approaching, Recovering }

    /// <summary>Attack target is locked during the warning, so swimming away always works.</summary>
    public sealed class BloopAttackCycle
    {
        public BloopAttackPhase Phase { get; private set; }
        public Vector3 Target { get; private set; }
        float timer;
        public void Reset() { Phase = BloopAttackPhase.Stalking; timer = 0; Target = Vector3.zero; }
        public bool Tick(float delta, Vector3 player, Vector3 mouth, float warning, float approach, float recovery)
        {
            if (Phase == BloopAttackPhase.Stalking)
            {
                float gap = player.y - mouth.y;
                if (gap < 0 || gap > 8) return false;
                Target = player;
                Phase = BloopAttackPhase.Warning;
                timer = 0;
                return false;
            }
            timer += delta;
            if (Phase == BloopAttackPhase.Warning && timer >= warning)
            { Phase = BloopAttackPhase.Approaching; timer = 0; }
            else if (Phase == BloopAttackPhase.Approaching && timer >= approach)
            { Phase = BloopAttackPhase.Recovering; timer = 0; return true; }
            else if (Phase == BloopAttackPhase.Recovering && timer >= recovery)
            { Phase = BloopAttackPhase.Stalking; timer = 0; }
            return false;
        }
        public bool InStrikeZone(Vector3 player) => Vector3.Distance(player, Target) <= 2.6f;
    }

    public sealed class BloopMouthRisingController : MonoBehaviour
    {
        public Transform visual;
        public Transform lairOpening;
        public Transform emergencePoint;
        public float emergenceDuration = 8;
        public float visualWidth = 16;
        public bool visualPrepared;
        public float captureRadius = 12;
        public float baseRiseSpeed = .35f;
        public float maximumRiseSpeed = .65f;
        public float attackRiseSpeed = .8f;
        public float warningDuration = 3;
        public float approachDuration = 4;
        public float recoveryDuration = 6;
        public float mouthHeightOffset;
        public float Threat { get; private set; }
        Scenario4Manager encounter;
        Vector3 origin;
        float speed;
        readonly BloopAttackCycle attack = new BloopAttackCycle();
        Transform jawBone;
        Quaternion initialJawRot;
        float emergenceTime, jawAngle;
        Vector3 pursuitPosition;
        public BloopAttackPhase AttackPhase => attack.Phase;
        public bool IsWarning => AttackPhase == BloopAttackPhase.Warning || AttackPhase == BloopAttackPhase.Approaching;

        public void Initialize(Scenario4Manager manager)
        {
            encounter = manager;
            origin = transform.position;
            pursuitPosition = origin;
            
            // Fit the existing authored animal around the central shaft, head upwards.
            if (!visual) return;
            Transform head = null, tail = null;
            foreach (var bone in visual.GetComponentsInChildren<Transform>())
            {
                if (bone.name == "w_hoofd1") head = bone;
                if (bone.name == "w_bek" || bone.name == "w_kaak" || bone.name == "w_bek_onder") jawBone = bone;
                if (bone.name == "w_rug4") tail = bone;
            }
            if (jawBone) initialJawRot = jawBone.localRotation;
            // Remove the nonuniform scale inherited from the old scene instance.
            if (!visualPrepared)
            {
            visual.localScale = Vector3.one;
            if (head && tail)
                visual.rotation = Quaternion.FromToRotation((head.position - tail.position).normalized, Vector3.up) * visual.rotation;
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var item in renderers) bounds.Encapsulate(item.bounds);
                float width = Mathf.Max(bounds.size.x, bounds.size.z);
                visual.localScale *= visualWidth / Mathf.Max(.01f, width);
                bounds = renderers[0].bounds;
                foreach (var item in renderers) bounds.Encapsulate(item.bounds);
                visual.position += new Vector3(transform.position.x - bounds.center.x,
                    transform.position.y - bounds.max.y, transform.position.z - bounds.center.z);
            }
            visualPrepared = true;
            }
            if (!visual.GetComponent<BloopSwimAnimation>()) visual.gameObject.AddComponent<BloopSwimAnimation>();
        }

        public void ResetPursuit()
        {
            transform.position = origin; speed = 0; Threat = 0; attack.Reset();
            emergenceTime = jawAngle = 0;
            pursuitPosition = origin;
            if (jawBone) jawBone.localRotation = initialJawRot;
        }
        public bool Contains(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            return point.y <= transform.position.y + mouthHeightOffset &&
                   new Vector2(offset.x, offset.z).sqrMagnitude <= captureRadius * captureRadius;
        }
        void Update()
        {
            if (!encounter || !encounter.viewer) return;
            float gap = encounter.viewer.transform.position.y - transform.position.y - mouthHeightOffset;
            Threat = encounter.Running ? 1 - Mathf.InverseLerp(2, 18, gap) : 0;
            if (encounter.Phase == Scenario4Phase.Awakening && emergencePoint)
            {
                emergenceTime += Time.deltaTime;
                transform.position = EvaluateEmergence(origin, emergencePoint.position, emergenceTime, emergenceDuration);
                pursuitPosition = transform.position;
                return;
            }
            if (!encounter.Running) return;
            Vector3 player = encounter.viewer.transform.position;
            bool strike = attack.Tick(Time.deltaTime, player, transform.position, warningDuration, approachDuration, recoveryDuration);
            if (strike && attack.InStrikeZone(player) && HasClearAttack(player)) encounter.BloopStrike();

            float targetSpeed = Mathf.Lerp(baseRiseSpeed, maximumRiseSpeed, encounter.Progress);
            if (AttackPhase == BloopAttackPhase.Warning) targetSpeed = .15f;
            if (AttackPhase == BloopAttackPhase.Approaching) targetSpeed = attackRiseSpeed;
            if (AttackPhase == BloopAttackPhase.Recovering) targetSpeed = .12f;
            speed = Mathf.MoveTowards(speed, targetSpeed, Time.deltaTime * .2f);
            pursuitPosition.y += speed * Time.deltaTime;
            // Clear the aperture before leaning towards the swimmer. Never snap under the player.
            if (lairOpening && pursuitPosition.y > lairOpening.position.y + 5)
            {
                Vector3 lateral = Vector3.ProjectOnPlane(player - lairOpening.position, Vector3.up);
                lateral = Vector3.ClampMagnitude(lateral, 2.4f);
                Vector3 desired = new Vector3(lairOpening.position.x + lateral.x, pursuitPosition.y,
                    lairOpening.position.z + lateral.z);
                pursuitPosition = Vector3.MoveTowards(pursuitPosition, desired, Time.deltaTime * .14f);
            }
            transform.position = pursuitPosition;
        }
        public static Vector3 EvaluateEmergence(Vector3 start, Vector3 end, float elapsed, float duration)
        {
            return Vector3.Lerp(start, end, Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.1f, duration))));
        }
        void LateUpdate()
        {
            // Spine, fins and fluke have one owner: the original BloopSwimAnimation.
            if (!jawBone || !encounter) return;
            float target = !encounter.Running ? 4 : AttackPhase == BloopAttackPhase.Approaching ? 22 : IsWarning ? 14 : 7;
            jawAngle = Mathf.MoveTowards(jawAngle, target, Time.deltaTime * 5);
            jawBone.localRotation = initialJawRot * Quaternion.Euler(jawAngle, 0, 0);
        }

        bool HasClearAttack(Vector3 player)
        {
            Vector3 from = transform.position + Vector3.up * .3f;
            Vector3 offset = player - from;
            if (!Physics.Raycast(from, offset.normalized, out var hit, offset.magnitude, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.GetComponentInParent<Scenario4Swimmer>() != null;
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * mouthHeightOffset, new Vector3(captureRadius * 2, .2f, captureRadius * 2));
        }
    }
}
