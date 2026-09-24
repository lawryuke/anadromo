using UnityEngine;

namespace Anadromo.Locomotion 
{
    public class PlayerTestController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 5f;
        
        [Header("Mouse Look")]
        public float mouseSensitivity = 300f;
        public float maxLookAngle = 80f;

        public bool IsMoving { get; private set; }
        
        private Transform cameraTransform;
        private float rotationX = 0f;

        void Start()
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            
            // Bloquear el cursor para la experiencia en primera persona
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            // 1. Rotación con Ratón
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            // Rotación horizontal (Cuerpo del jugador)
            transform.Rotate(Vector3.up * mouseX);

            // Rotación vertical (Cámara)
            if (cameraTransform != null)
            {
                rotationX -= mouseY;
                rotationX = Mathf.Clamp(rotationX, -maxLookAngle, maxLookAngle);
                cameraTransform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
            }

            // 2. Movimiento WASD
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveZ = Input.GetAxisRaw("Vertical");

            // Mover en la dirección a la que estamos mirando
            Vector3 moveDirection = (transform.right * moveX + transform.forward * moveZ).normalized;

            if (moveDirection.magnitude >= 0.1f)
            {
                transform.position += moveDirection * moveSpeed * Time.deltaTime;
                IsMoving = true;
            }
            else
            {
                IsMoving = false;
            }
        }
    }
}
