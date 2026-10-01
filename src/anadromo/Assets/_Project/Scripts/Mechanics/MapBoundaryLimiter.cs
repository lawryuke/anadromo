using System.Collections.Generic;
using Anadromo.Locomotion;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Anadromo.Mechanics
{
    /// <summary>Warns on leaving the playable box, then returns the entire player rig inside.</summary>
    [DefaultExecutionOrder(1000)]
    public class MapBoundaryLimiter : MonoBehaviour
    {
        [Header("Zona y destino")]
        public ZoneLimit mapLimit;
        [Tooltip("Destino del giro. Sin referencia se usa la posicion inicial de la cabeza.")]
        public Transform referencePoint;
        public Transform playerCamera;
        [Tooltip("Raiz del jugador (XR Origin), nunca Camera Offset ni la cabeza con tracking.")]
        public Transform rigToRotate;

        [Header("Regreso automatico")]
        [Min(0.1f)] public float returnDistance = 3f;
        [Min(0.1f)] public float returnSpeed = 3f;
        [Min(0.05f)] public float interiorMargin = 0.75f;
        [Min(0f)] public float warningDuration = 0.35f;
        [Min(0f)] public float recoveryDuration = 0.4f;

        [Header("Efectos")]
        public AudioClip limitSound;
        [Range(0f, 1f)] public float soundVolume = 1f;
        public float soundDistance = 10f;

        enum Phase { Ready, Warning, Returning, Recovery }
        Phase phase;
        bool hasEntered;
        Vector3 startingPoint, returnTarget;
        float phaseTime;
        Rigidbody body;
        bool wasKinematic;
        readonly List<Behaviour> suspended = new List<Behaviour>();
        Volume feedback;
        VolumeProfile profile;
        public bool IsReturning => phase != Phase.Ready;

        void Start()
        {
            if (!mapLimit || !playerCamera || !rigToRotate ||
                (playerCamera != rigToRotate && !playerCamera.IsChildOf(rigToRotate)))
            {
                Debug.LogError("MapBoundaryLimiter requiere zona, camara y raiz del jugador compatibles.", this);
                enabled = false;
                return;
            }
            startingPoint = playerCamera.position;
            body = rigToRotate.GetComponent<Rigidbody>();
            hasEntered = mapLimit.Contains(startingPoint);
            var camera = playerCamera.GetComponent<Camera>();
            if (!camera) return;
            var data = camera.GetUniversalAdditionalCameraData();
            var go = new GameObject("Boundary Feedback (Runtime)");
            go.transform.SetParent(transform, false);
            for (int layer = 0; layer < 32; layer++)
                if ((data.volumeLayerMask.value & (1 << layer)) != 0) { go.layer = layer; break; }
            feedback = go.AddComponent<Volume>();
            feedback.isGlobal = true;
            feedback.priority = 200;
            feedback.weight = 0;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            feedback.sharedProfile = profile;
            profile.Add<ColorAdjustments>().saturation.Override(-100);
        }

        void LateUpdate()
        {
            if (!mapLimit || !mapLimit.isActiveAndEnabled || !playerCamera || !rigToRotate)
            {
                RestoreControl();
                return;
            }
            if (phase == Phase.Ready)
            {
                bool inside = mapLimit.Contains(playerCamera.position);
                // This scene starts outside the box: arm only after first entering it.
                if (inside) hasEntered = true;
                else if (hasEntered) BeginReturn();
                return;
            }
            phaseTime += Time.deltaTime;
            if (phase == Phase.Warning && phaseTime >= warningDuration)
            {
                FaceHome();
                phase = Phase.Returning;
                phaseTime = 0;
            }
            if (phase == Phase.Returning)
            {
                // Move the rig by the head delta, preserving room-scale offsets and tracking.
                Vector3 next = Vector3.MoveTowards(playerCamera.position, returnTarget,
                    Mathf.Max(0.1f, returnSpeed) * Time.deltaTime);
                rigToRotate.position += next - playerCamera.position;
                SyncBody();
                if ((playerCamera.position - returnTarget).sqrMagnitude < 0.0001f)
                {
                    phase = Phase.Recovery;
                    phaseTime = 0;
                }
            }
            else if (phase == Phase.Recovery)
            {
                // Keep the head inside even if the headset moves during the fade-out.
                rigToRotate.position += ClampInside(playerCamera.position) - playerCamera.position;
                SyncBody();
                if (feedback) feedback.weight = 1 - Mathf.Clamp01(phaseTime / Mathf.Max(0.001f, recoveryDuration));
                if (phaseTime >= recoveryDuration) RestoreControl();
            }
        }

        Vector3 Home => referencePoint ? referencePoint.position : startingPoint;

        Vector3 ClampInside(Vector3 point)
        {
            Vector3 local = mapLimit.transform.InverseTransformPoint(point) - mapLimit.center;
            Vector3 half = mapLimit.size * 0.5f;
            Vector3 scale = mapLimit.transform.lossyScale;
            for (int axis = 0; axis < 3; axis++)
            {
                float margin = Mathf.Max(0.05f, interiorMargin) / Mathf.Max(0.0001f, Mathf.Abs(scale[axis]));
                float extent = Mathf.Max(0, half[axis] - margin);
                local[axis] = Mathf.Clamp(local[axis], -extent, extent);
            }
            return mapLimit.transform.TransformPoint(local + mapLimit.center);
        }

        void BeginReturn()
        {
            phase = Phase.Warning;
            phaseTime = 0;
            Vector3 safeHome = ClampInside(Home);
            // Start from an interior point so even overshooting at sprint speed is recovered.
            returnTarget = ClampInside(Vector3.MoveTowards(ClampInside(playerCamera.position),
                safeHome, Mathf.Max(0.1f, returnDistance)));
            foreach (var behaviour in rigToRotate.GetComponentsInChildren<Behaviour>())
            {
                if (behaviour.enabled && (behaviour is SimpleFlyCamera || behaviour is FlapSwimController ||
                    behaviour is SwimLocomotion || behaviour is LocomotionModeManager))
                {
                    suspended.Add(behaviour);
                    behaviour.enabled = false;
                }
            }
            if (body)
            {
                wasKinematic = body.isKinematic;
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.isKinematic = true;
            }
            if (feedback) feedback.weight = 1;
            if (limitSound)
            {
                var go = new GameObject("BoundarySound");
                go.transform.position = playerCamera.position + playerCamera.forward * soundDistance;
                var source = go.AddComponent<AudioSource>();
                source.clip = limitSound;
                source.volume = soundVolume;
                source.spatialBlend = 1;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 5;
                source.maxDistance = Mathf.Max(50, soundDistance + 10);
                source.Play();
                Destroy(go, limitSound.length + 0.1f);
            }
        }

        void FaceHome()
        {
            Vector3 direction = Vector3.ProjectOnPlane(Home - playerCamera.position, Vector3.up);
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector3.ProjectOnPlane(returnTarget - playerCamera.position, Vector3.up);
            Vector3 forward = Vector3.ProjectOnPlane(playerCamera.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(rigToRotate.forward, Vector3.up);
            if (direction.sqrMagnitude > 0.001f && forward.sqrMagnitude > 0.001f)
                rigToRotate.RotateAround(playerCamera.position, Vector3.up,
                    Vector3.SignedAngle(forward, direction, Vector3.up));
            // A single yaw turn preserves headset pitch/roll; never write the tracked camera pose.
            SyncBody();
        }

        void SyncBody()
        {
            if (!body) return;
            body.position = rigToRotate.position;
            body.rotation = rigToRotate.rotation;
        }

        void RestoreControl()
        {
            if (phase != Phase.Ready && body) body.isKinematic = wasKinematic;
            foreach (var behaviour in suspended)
            {
                if (!behaviour) continue;
                if (behaviour is SimpleFlyCamera fly) fly.SyncLookRotation();
                behaviour.enabled = true;
            }
            suspended.Clear();
            if (feedback) feedback.weight = 0;
            phase = Phase.Ready;
        }

        void OnDisable() => RestoreControl();

        void OnDestroy()
        {
            if (feedback) Destroy(feedback.gameObject);
            if (!profile) return;
            foreach (var component in profile.components) if (component) Destroy(component);
            Destroy(profile);
        }
    }
}
