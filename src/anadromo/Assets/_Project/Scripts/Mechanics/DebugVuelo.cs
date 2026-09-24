using UnityEngine;
using UnityEngine.InputSystem;
using Anadromo.Config;

namespace Anadromo.Mechanics
{
    public class DebugVuelo : MonoBehaviour
    {
        [Header("Configuración de Vuelo (se sobreescribe con GameSettings)")]
        public float speed = 10f;
        public Transform cameraTransform; // Arrastra aquí la Main Camera

        [Header("Vista con botón derecho")]
        public float mouseSensitivity = 0.12f;

        private float yaw;
        private float pitch;
        private Rigidbody body;
        private bool wasKinematic;
        private Vector3 movement;

        private void OnEnable()
        {
            if (cameraTransform != null)
            {
                yaw = cameraTransform.localEulerAngles.y;
                pitch = Mathf.DeltaAngle(0f, cameraTransform.localEulerAngles.x);
            }

            body = GetComponent<Rigidbody>();
            if (body != null)
            {
                wasKinematic = body.isKinematic;
                body.isKinematic = true;
            }
        }

        private void OnDisable()
        {
            movement = Vector3.zero;
            if (body != null) body.isKinematic = wasKinematic;
        }

        void Update()
        {
            movement = Vector3.zero;
            if (cameraTransform == null || Keyboard.current == null) return;

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                float sens = GameSettings.I ? GameSettings.I.mouseSensitivity : mouseSensitivity;
                yaw += delta.x * sens;
                pitch = Mathf.Clamp(pitch - delta.y * sens, -85f, 85f);
                cameraTransform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            Vector3 moveDir = Vector3.zero;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveDir += cameraTransform.forward;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveDir -= cameraTransform.forward;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveDir += cameraTransform.right;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveDir -= cameraTransform.right;
            if (Keyboard.current.eKey.isPressed) moveDir += Vector3.up;
            if (Keyboard.current.qKey.isPressed) moveDir -= Vector3.up;

            movement = moveDir.normalized;
            float spd = GameSettings.I ? GameSettings.I.playerSpeed : speed;
            if (body == null) transform.position += (movement * spd + Anadromo.Act1.OceanEnvironment.PlayerCurrentAt(transform.position)) * Time.deltaTime;
        }

        private void FixedUpdate()
        {
            if (body != null)
            {
                float spd = GameSettings.I ? GameSettings.I.playerSpeed : speed;
                body.MovePosition(body.position + (movement * spd + Anadromo.Act1.OceanEnvironment.PlayerCurrentAt(body.position)) * Time.fixedDeltaTime);
            }
        }
    }
}
