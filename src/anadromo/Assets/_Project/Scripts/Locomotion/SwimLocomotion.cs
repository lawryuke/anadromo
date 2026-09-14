using UnityEngine;
using UnityEngine.InputSystem;

namespace Anadromo.Locomotion
{
    [RequireComponent(typeof(Rigidbody))]
    public class SwimLocomotion : MonoBehaviour
    {
        [Header("Input Actions")]
        [SerializeField] private InputActionReference leftHandVelocity;
        [SerializeField] private InputActionReference rightHandVelocity;
        [SerializeField] private InputActionReference headRotation; // Triggers orientation

        [Header("Locomotion Settings")]
        [SerializeField] private float swimForceMultiplier = 50f;
        [SerializeField] private float dragInWater = 3f;
        [SerializeField] private float swimThreshold = 0.5f; // Min velocity to count as a stroke

        private Rigidbody rb;
        private Transform headTransform; // Reference to the VR Camera

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearDamping = dragInWater;
        }

        private void OnEnable()
        {
            leftHandVelocity.action.Enable();
            rightHandVelocity.action.Enable();
            headRotation.action.Enable();
        }

        private void OnDisable()
        {
            leftHandVelocity.action.Disable();
            rightHandVelocity.action.Disable();
            headRotation.action.Disable();
        }

        private void FixedUpdate()
        {
            // Get raw velocities from controllers
            Vector3 leftVel = leftHandVelocity.action.ReadValue<Vector3>();
            Vector3 rightVel = rightHandVelocity.action.ReadValue<Vector3>();

            // Calculate the stroke intensity (moving hands backwards propels you forwards)
            // Simplified calculation: we take the backward Z velocity relative to the player
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
            // Swim direction is dictated by the head/torso (VR Camera) forward vector
            if (headTransform != null)
            {
                Vector3 swimDirection = headTransform.forward;
                rb.AddForce(swimDirection * intensity * swimForceMultiplier, ForceMode.Force);
            }
            else
            {
                // Fallback to local forward if head is not set
                rb.AddForce(transform.forward * intensity * swimForceMultiplier, ForceMode.Force);
            }
        }

        public void SetHeadTransform(Transform head)
        {
            headTransform = head;
        }
    }
}
