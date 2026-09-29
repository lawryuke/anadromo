using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;

namespace Anadromo.Locomotion
{
    /// <summary>Movimiento libre de manos en 3D, con filtrado y velocidad proporcional.</summary>
    public class FlapDetector : MonoBehaviour
    {
        public enum TrackingSource { ExternalCamera, QuestHands }
        [SerializeField] private TrackingSource trackingSource;
        [SerializeField] private Anadromo.Systems.ExternalCameraReceiver cameraReceiver;
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private FlapSettings settings;
        [Header("Movimiento libre de manos XR")]
        [Range(0f, 0.99f)] [SerializeField] private float xrSmoothingFactor = 0.45f;
        [Tooltip("Velocidad mínima en cualquier dirección (m/s).")]
        [Min(0.01f)] [SerializeField] private float xrFlapVelocityThreshold = 0.15f;
        [Tooltip("Desplazamiento mínimo desde el comienzo de cada impulso (m).")]
        [Min(0.01f)] [SerializeField] private float xrMinimumStrokeDistance = 0.06f;
        [Tooltip("Velocidad media que produce el impulso máximo (m/s).")]
        [Min(0.1f)] [SerializeField] private float xrFullIntensitySpeed = 1.2f;
        [Tooltip("Saltos de tracking superiores a esta velocidad se descartan (m/s).")]
        [Min(1f)] [SerializeField] private float xrMaximumSampleSpeed = 5f;
        public UnityEvent<float> OnLeftFlap = new UnityEvent<float>();
        public UnityEvent<float> OnRightFlap = new UnityEvent<float>();
        [SerializeField] private bool leftHandTracked;
        [SerializeField] private bool rightHandTracked;
        [SerializeField] private int totalLeftFlaps;
        [SerializeField] private int totalRightFlaps;
        private ArmState left, right;
        private XRHandSubsystem handSubsystem;
        private readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
        private bool suppressFlaps;
        public bool SuppressFlaps
        {
            get => suppressFlaps;
            set { if (suppressFlaps == value) return; suppressFlaps = value; ResetState(); }
        }

        private struct ArmState
        {
            public bool initialized;
            public Vector3 raw, filtered, anchor;
            public float path, movingTime, idleTime, cooldown;
        }

        public bool IsOperational => isActiveAndEnabled && settings != null &&
            (leftHandTracked || rightHandTracked);
        private void OnEnable() => ResetState();
        private void OnDisable() => ResetState();
        private void Update()
        {
            if (SuppressFlaps || settings == null || Time.deltaTime <= 0f) return;
            ProcessArm(true, ref left, ref leftHandTracked, OnLeftFlap, ref totalLeftFlaps);
            ProcessArm(false, ref right, ref rightHandTracked, OnRightFlap, ref totalRightFlaps);
        }

        private bool TryReadWrist(bool isLeft, out Pose pose)
        {
            pose = default;
            if (trackingSource != TrackingSource.QuestHands) return false;
            if (handSubsystem == null || !handSubsystem.running)
            {
                handSubsystem = null;
                SubsystemManager.GetSubsystems(subsystems);
                foreach (var subsystem in subsystems)
                    if (subsystem.running) { handSubsystem = subsystem; break; }
            }
            if (handSubsystem == null) return false;
            var hand = isLeft ? handSubsystem.leftHand : handSubsystem.rightHand;
            return hand.isTracked && hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out pose);
        }

        /// <summary>Pose mundial para las aletas, incluyendo Camera Offset del XR Origin.</summary>
        public bool TryGetTrackedWrist(bool isLeft, out Pose pose)
        {
            pose = default;
            if (!isActiveAndEnabled || !TryReadWrist(isLeft, out pose)) return false;
            var hand = isLeft ? handSubsystem.leftHand : handSubsystem.rightHand;
            if (hand.GetJoint(XRHandJointID.MiddleProximal).TryGetPose(out Pose middle) &&
                hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out Pose index) &&
                hand.GetJoint(XRHandJointID.LittleProximal).TryGetPose(out Pose little))
            {
                Vector3 forward = middle.position - pose.position;
                Vector3 normal = Vector3.Cross(forward, index.position - little.position) * (isLeft ? -1f : 1f);
                if (forward.sqrMagnitude > 0.00001f && normal.sqrMagnitude > 0.00000001f)
                    pose.rotation = Quaternion.LookRotation(forward, normal);
            }
            Transform trackingSpace = headTransform != null ? headTransform.parent : xrOrigin;
            if (trackingSpace == null) return false;
            pose = new Pose(trackingSpace.TransformPoint(pose.position), trackingSpace.rotation * pose.rotation);
            return true;
        }

        private void ProcessArm(bool isLeft, ref ArmState arm, ref bool tracked,
            UnityEvent<float> flapEvent, ref int count)
        {
            Vector3 point;
            bool xr = trackingSource == TrackingSource.QuestHands;
            if (xr)
            {
                tracked = TryReadWrist(isLeft, out Pose pose);
                // Espacio de tracking: el giro de cabeza y la locomoción virtual
                // no generan movimiento artificial de brazos.
                point = pose.position;
            }
            else
            {
                tracked = cameraReceiver != null && cameraReceiver.IsReceiving;
                point = tracked ? (isLeft ? cameraReceiver.LeftWrist : cameraReceiver.RightWrist) : Vector3.zero;
            }
            float dt = Time.deltaTime;
            if (!tracked || dt > 0.15f)
            {
                arm = default;
                return;
            }
            if (!arm.initialized || (xr && Vector3.Distance(point, arm.raw) / dt > xrMaximumSampleSpeed))
            {
                arm = new ArmState { initialized = true, raw = point, filtered = point, anchor = point };
                return;
            }
            arm.raw = point;
            Vector3 previous = arm.filtered;
            float retention = xr ? Mathf.Pow(xrSmoothingFactor, dt * 72f) : settings.smoothingFactor;
            arm.filtered = Vector3.Lerp(point, previous, retention);
            arm.cooldown = Mathf.Max(0f, arm.cooldown - dt);
            if (!xr)
            {
                float downwardSpeed = (previous.y - arm.filtered.y) / dt;
                if (downwardSpeed > settings.flapVelocityThreshold && arm.cooldown <= 0f)
                {
                    flapEvent.Invoke(Mathf.Clamp(downwardSpeed * settings.intensityMultiplier,
                        settings.minIntensity, settings.maxIntensity));
                    arm.cooldown = settings.flapCooldown;
                    count++;
                }
                return;
            }
            float distance = Vector3.Distance(previous, arm.filtered);
            if (distance / dt < xrFlapVelocityThreshold)
            {
                arm.idleTime += dt;
                if (arm.idleTime >= 0.12f)
                {
                    arm.anchor = arm.filtered;
                    arm.path = arm.movingTime = 0f;
                }
                return;
            }
            arm.idleTime = 0f;
            arm.path += distance;
            arm.movingTime += dt;
            if (arm.cooldown > 0f || arm.movingTime < 0.08f ||
                Vector3.Distance(arm.anchor, arm.filtered) < xrMinimumStrokeDistance) return;
            float speed = arm.path / arm.movingTime;
            float intensity = Mathf.Lerp(Mathf.Max(0.25f, settings.minIntensity), settings.maxIntensity,
                Mathf.InverseLerp(xrFlapVelocityThreshold, xrFullIntensitySpeed, speed));
            flapEvent.Invoke(intensity);
            count++;
            arm.anchor = arm.filtered;
            arm.path = arm.movingTime = 0f;
            arm.cooldown = settings.flapCooldown;
        }

        public void ResetState()
        {
            left = right = default;
            leftHandTracked = rightHandTracked = false;
            totalLeftFlaps = totalRightFlaps = 0;
        }
    }
}
