using UnityEngine;
using UnityEngine.InputSystem;

namespace Anadromo.Mechanics
{
    public class DebugVuelo : MonoBehaviour
    {
        [Header("Configuración de Vuelo")]
        public float speed = 10f;
        public Transform cameraTransform; // Arrastra aquí la Main Camera

        void Update()
        {
            if (cameraTransform == null || Keyboard.current == null) return;

            Vector3 moveDir = Vector3.zero;

            // Usamos las FLECHAS en lugar de WASD para que no sume la velocidad del simulador
            if (Keyboard.current.upArrowKey.isPressed) moveDir += cameraTransform.forward;
            if (Keyboard.current.downArrowKey.isPressed) moveDir -= cameraTransform.forward;
            if (Keyboard.current.rightArrowKey.isPressed) moveDir += cameraTransform.right;
            if (Keyboard.current.leftArrowKey.isPressed) moveDir -= cameraTransform.right;

            // Movemos al jugador en la dirección a la que mira la cámara
            transform.position += moveDir.normalized * speed * Time.deltaTime;
        }
    }
}
