using UnityEngine;
using UnityEngine.InputSystem;

namespace Anadromo.Mechanics
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class Scenario4Swimmer : MonoBehaviour
    {
        public Camera viewer;
        public bool canMove;
        public float speed = 3.4f;
        public float sensitivity = .12f;
        CharacterController body;
        float pitch, yaw, stagger;
        Vector3 disorientationOffset;
        public bool IsStaggered => stagger > 0;
        public float SpeedMultiplier => Mathf.Lerp(1, .3f, Mathf.Clamp01(stagger / .8f));

        void Awake()
        {
            body = GetComponent<CharacterController>(); yaw = transform.eulerAngles.y;
            // Desktop mouse-look and HMD pose tracking must never write the same transform.
            if (viewer)
            {
                var tracking = viewer.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                if (tracking) tracking.enabled = UnityEngine.XR.XRSettings.isDeviceActive;
            }
        }
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (!body) body = GetComponent<CharacterController>();
            body.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            body.enabled = true;
            yaw = rotation.eulerAngles.y;
            pitch = 0;
            stagger = 0;
            disorientationOffset = Vector3.zero;
            if (viewer && !UnityEngine.XR.XRSettings.isDeviceActive) viewer.transform.localRotation = Quaternion.identity;
        }
        public void Stagger(float duration)
        {
            stagger = Mathf.Max(stagger, duration);
            // Apply impact disorientation recoil (pitch tilt & roll roll)
            disorientationOffset = new Vector3(
                Random.Range(-14f, 14f),
                Random.Range(-8f, 8f),
                Random.Range(-25f, 25f)
            );
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var rock = hit.collider.GetComponent<FallingRock>();
            if (rock) rock.HitSwimmer();
        }
        void Update()
        {
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            stagger = Mathf.Max(0, stagger - Time.deltaTime);
            disorientationOffset = Vector3.Lerp(disorientationOffset, Vector3.zero, Time.deltaTime * 3.5f);

            if (keys != null && keys.escapeKey.wasPressedThisFrame)
            { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (!canMove || !viewer) return;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !UnityEngine.XR.XRSettings.isDeviceActive)
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            Vector3 direction = Vector3.zero;
            if (UnityEngine.XR.XRSettings.isDeviceActive)
            {
                var hand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
                if (hand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis))
                    direction = viewer.transform.forward * axis.y + viewer.transform.right * axis.x;
            }
            else if (keys != null)
            {
                if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
                {
                    Vector2 delta = mouse.delta.ReadValue() * sensitivity;
                    yaw += delta.x;
                    pitch = Mathf.Clamp(pitch - delta.y, -85, 85);
                    transform.rotation = Quaternion.Euler(0, yaw, 0);
                }
                if (keys.wKey.isPressed) direction += viewer.transform.forward;
                if (keys.sKey.isPressed) direction -= viewer.transform.forward;
                if (keys.dKey.isPressed) direction += viewer.transform.right;
                if (keys.aKey.isPressed) direction -= viewer.transform.right;
                if (keys.eKey.isPressed) direction += Vector3.up;
                if (keys.qKey.isPressed) direction -= Vector3.up;
            }

            if (!UnityEngine.XR.XRSettings.isDeviceActive)
            {
                viewer.transform.localRotation = Quaternion.Euler(pitch + disorientationOffset.x, disorientationOffset.y, disorientationOffset.z);
            }

            body.Move(Vector3.ClampMagnitude(direction, 1) * (speed * SpeedMultiplier * Time.deltaTime));
        }
    }
}
