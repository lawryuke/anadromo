using UnityEngine;
using UnityEngine.Events;
using Anadromo.Systems;

namespace Anadromo.Locomotion
{
    /// <summary>Each camera sample is processed once; each arm has a stroke/recovery cycle.</summary>
    public class FlapDetector : MonoBehaviour
    {
        [SerializeField] private ExternalCameraReceiver cameraReceiver;
        [SerializeField] private FlapSettings settings;
        public UnityEvent<float> OnLeftFlap = new UnityEvent<float>();
        public UnityEvent<float> OnRightFlap = new UnityEvent<float>();

        [Header("Debug")]
        [SerializeField] private int totalLeftFlaps;
        [SerializeField] private int totalRightFlaps;
        private readonly ArmStrokeCycle left = new ArmStrokeCycle();
        private readonly ArmStrokeCycle right = new ArmStrokeCycle();
        private uint lastSampleId;

        public string LeftStatus => Status(left);
        public string RightStatus => Status(right);
        public int TotalLeftFlaps => totalLeftFlaps;
        public int TotalRightFlaps => totalRightFlaps;
        public bool IsOperational => isActiveAndEnabled && settings != null && cameraReceiver != null &&
            cameraReceiver.IsReceiving && cameraReceiver.PoseAge <= settings.trackingTimeout &&
            (left.Initialized || right.Initialized);

        private static string Status(ArmStrokeCycle arm) => !arm.Initialized ? "SIN TRACKING" :
            arm.Recovering ? "RECUPERANDO" : "LISTO";

        private void OnEnable() => ResetState();
        private void OnDisable() => ResetTracking();

        private void Update()
        {
            if (settings == null || cameraReceiver == null || !cameraReceiver.IsReceiving ||
                cameraReceiver.PoseAge > settings.trackingTimeout || Time.timeScale == 0f)
            {
                ResetTracking();
                return;
            }
            if (lastSampleId == cameraReceiver.SampleId) return;
            lastSampleId = cameraReceiver.SampleId;
            float dt = cameraReceiver.SampleDeltaTime;
            if (dt <= 0f || dt > settings.trackingTimeout) ResetTracking();

            ProcessArm(true, left, dt, OnLeftFlap, ref totalLeftFlaps);
            ProcessArm(false, right, dt, OnRightFlap, ref totalRightFlaps);
        }

        private void ProcessArm(bool isLeft, ArmStrokeCycle arm, float dt,
            UnityEvent<float> flapEvent, ref int count)
        {
            if (!cameraReceiver.TryGetArmPoint(isLeft, settings.minimumVisibility, out Vector2 point))
            {
                arm.Reset();
                return;
            }
            if (arm.Step(point, dt, settings.cycle, out float intensity))
            {
                count++;
                flapEvent.Invoke(intensity);
            }
        }

        private void ResetTracking()
        {
            left.Reset();
            right.Reset();
        }

        public void ResetState()
        {
            ResetTracking();
            totalLeftFlaps = totalRightFlaps = 0;
            lastSampleId = cameraReceiver != null ? cameraReceiver.SampleId : 0;
        }
    }
}
