using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleFlyCamera : MonoBehaviour
{
    [Header("Settings")]
    public float movementSpeed = 10f;
    public float fastMovementSpeed = 25f;
    public float mouseSensitivity = 0.2f;

    private float pitch = 0f;
    private float yaw = 0f;

    private void Start()
    {
        // Set initial rotation based on current transform
        Vector3 angles = transform.eulerAngles;
        pitch = angles.x;
        yaw = angles.y;
        
        // Bloquear el cursor en el centro y ocultarlo
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        if (keyboard == null || mouse == null) return;

        // Liberar el cursor si pulsamos Escape
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Camera Rotation (Siempre activa si el cursor está bloqueado)
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            yaw += mouseDelta.x * mouseSensitivity;
            pitch -= mouseDelta.y * mouseSensitivity;
            
            // Clamp pitch to avoid flipping over
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }

        // Camera Movement
        float currentSpeed = keyboard.leftShiftKey.isPressed ? fastMovementSpeed : movementSpeed;
        Vector3 direction = Vector3.zero;

        if (keyboard.wKey.isPressed) direction += transform.forward;
        if (keyboard.sKey.isPressed) direction -= transform.forward;
        if (keyboard.dKey.isPressed) direction += transform.right;
        if (keyboard.aKey.isPressed) direction -= transform.right;
        if (keyboard.eKey.isPressed) direction += transform.up;
        if (keyboard.qKey.isPressed) direction -= transform.up;

        transform.position += direction * currentSpeed * Time.deltaTime;
    }
}
