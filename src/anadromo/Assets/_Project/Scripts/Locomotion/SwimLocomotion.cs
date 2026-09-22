using UnityEngine;
using UnityEngine.InputSystem;

namespace Anadromo.Locomotion
{
    [RequireComponent(typeof(Rigidbody))]
    public class SwimLocomotion : MonoBehaviour
    {
        [Header("Input Actions (Use Position)")]
        [SerializeField] private InputActionReference leftHandPosition;
        [SerializeField] private InputActionReference rightHandPosition;

        [Header("Locomotion Settings")]
        [SerializeField] private float swimForceMultiplier = 50f;
        [SerializeField] private float dragInWater = 3f;
        [SerializeField] private float swimThreshold = 0.5f;

        private Rigidbody rb;
        [SerializeField] private Transform headTransform;
        [SerializeField] private Anadromo.Environment.FishWaterExperience waterExperience;

        private Vector3 lastLeftPos;
        private Vector3 lastRightPos;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearDamping = dragInWater;
        }

        private void OnEnable()
        {
            if (leftHandPosition != null && leftHandPosition.action != null) leftHandPosition.action.Enable();
            if (rightHandPosition != null && rightHandPosition.action != null) rightHandPosition.action.Enable();
        }

        private void OnDisable()
        {
            if (leftHandPosition != null && leftHandPosition.action != null) leftHandPosition.action.Disable();
            if (rightHandPosition != null && rightHandPosition.action != null) rightHandPosition.action.Disable();
        }

        private void Start()
        {
            if (leftHandPosition != null && leftHandPosition.action != null)
                lastLeftPos = leftHandPosition.action.ReadValue<Vector3>();
            if (rightHandPosition != null && rightHandPosition.action != null)
                lastRightPos = rightHandPosition.action.ReadValue<Vector3>();
        }

        private void FixedUpdate()
        {
            if (!rb.isKinematic)
                rb.AddForce(Anadromo.Act1.OceanEnvironment.PlayerCurrentAt(rb.position) * dragInWater, ForceMode.Acceleration);
            if (!rb.isKinematic && waterExperience)
                rb.AddForce(waterExperience.Drift * dragInWater, ForceMode.Acceleration);

            if (leftHandPosition == null || rightHandPosition == null || leftHandPosition.action == null || rightHandPosition.action == null)
                return;

            Vector3 currentLeftPos = leftHandPosition.action.ReadValue<Vector3>();
            Vector3 currentRightPos = rightHandPosition.action.ReadValue<Vector3>();

            Vector3 leftVel = (currentLeftPos - lastLeftPos) / Time.fixedDeltaTime;
            Vector3 rightVel = (currentRightPos - lastRightPos) / Time.fixedDeltaTime;

            lastLeftPos = currentLeftPos;
            lastRightPos = currentRightPos;

            float strokeIntensity = 0f;
            
            if (leftVel.z < -swimThreshold) strokeIntensity += Mathf.Abs(leftVel.z);
            if (rightVel.z < -swimThreshold) strokeIntensity += Mathf.Abs(rightVel.z);

            if (strokeIntensity > 0f)
            {
                ApplySwimForce(strokeIntensity);
            }
        }

        private void ApplySwimForce(float intensity)
        {
            if (headTransform != null)
            {
                Vector3 swimDirection = headTransform.forward;
                rb.AddForce(swimDirection * intensity * swimForceMultiplier, ForceMode.Force);
            }
            else
            {
                rb.AddForce(transform.forward * intensity * swimForceMultiplier, ForceMode.Force);
            }
        }
    }
}
